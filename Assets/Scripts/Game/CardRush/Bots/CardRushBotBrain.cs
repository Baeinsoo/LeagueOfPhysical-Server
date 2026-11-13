namespace LOP
{
    /// <summary>
    /// 봇 정책 하나 — 이번 틱에 탭하나. <see cref="CardRushBotSystem"/>이 봇 몸마다 하나씩 묻는다.
    /// 출시 봇은 <see cref="CardRushBotBrain"/> 하나이고, 튜닝 하네스가 시험용 정책(읽는 고수·파랑만·막누름)을 끼운다(4차 spec §8).
    /// </summary>
    public interface ICardRushBotPolicy
    {
        bool ShouldTap(in CardRushRunnerState s, long tick, long gameplayStartTick, int lane, CardRushTrack track, in CardRushTuning tuning);
    }

    /// <summary>봇이 지금 상대하는 것 — 가까운 순으로 하나만 본다(4차 spec §7).</summary>
    public enum CardRushBotThreat
    {
        None = 0,
        Zone,
        Barricade,
        Roller,
        Pit,
        /// <summary>날고 있는 투사체가 지금 장애물을 맞힐 참 — 할 일이 없다.</summary>
        ShotInFlight,
    }

    /// <summary>봇의 지금 계획 — 시험이 들여다본다. Want·Alt가 null이면 "그 카드는 없음".</summary>
    public readonly struct CardRushBotPlan
    {
        public readonly bool Active;
        public readonly CardRushBotThreat Threat;
        /// <summary>어느 구역·장애물에 세운 계획인가 — 바뀌면 새로 세운다.</summary>
        public readonly long Key;
        public readonly CardColor? Want;
        public readonly CardColor? Alt;
        public readonly long ReactTick;
        /// <summary>빈 길 계획만 끝이 있다(지나면 다시 뽑는다). 위협 계획은 위협이 바뀔 때까지.</summary>
        public readonly long Until;

        public CardRushBotPlan(CardRushBotThreat threat, long key, CardColor? want, CardColor? alt, long reactTick, long until)
        {
            Active = true;
            Threat = threat;
            Key = key;
            Want = want;
            Alt = alt;
            ReactTick = reactTick;
            Until = until;
        }
    }

    /// <summary>
    /// 카드 러시 출시 봇(4차 spec §7) — 다음 위협에 맞는 색을 <b>잡고</b>, 쓸 때가 되면 <b>쓴다</b>. 안 맞는 카드를 쥐고 있으면 해 없는 때 써서 비운다.
    /// 위협(가까운 순): 켜진 구역 → 파랑(바로) / 8.3 m 안 장애물 — 바리케이드 → 빨강(사거리 안), 굴러오는 것 → 빨강 먼저·늦으면 노랑(닿기 직전),
    /// 구덩이 → 노랑(뛰는 창 가운데) / 없음 → 확률로 빈 길 대시.
    /// 사람과 같은 입력 길로 판다. 읽는 값은 서버 월드의 권위 상태라 결정론이 필요 없다 — 난수는 봇마다 나눈 <see cref="System.Random"/>.
    /// </summary>
    public sealed class CardRushBotBrain : ICardRushBotPolicy
    {
        /// <summary>장애물을 "상대할 것"으로 보는 거리 — 투사체 사거리 + 손에서 떠나는 자리(8.3 m).</summary>
        public const int NoticeMm = CardRushRules.ShotRangeMm + CardRushRules.ShotStartAheadMm;
        /// <summary>빨강을 쓰는 거리 여유 — 사거리 끝에 아슬아슬하게 걸리지 않게 사거리 − 0.5 m 안에서 던진다.</summary>
        public const int ShotAimSlackMm = 500;
        /// <summary>빈 길 판단: 바리케이드·굴러오는 것은 앞면이 대시 + 이만큼 밖, 구덩이는 대시 + <see cref="ClearPitMarginMm"/> 밖.</summary>
        public const int ClearObstacleMarginMm = 1400;
        public const int ClearPitMarginMm = 2400;
        /// <summary>빈 길 판단: 다음 구역 시작이 대시 + 이만큼 밖이어야 한다(구역 앞에서 파랑을 써 버리지 않게).</summary>
        public const int ClearZoneMarginMm = 1000;
        /// <summary>노랑을 버리는(뛰는) 것은 바리케이드·구덩이 앞이 이만큼 넘게 비었을 때만.</summary>
        public const int YellowDiscardClearMm = 1000;
        /// <summary>굴러오는 것: 닿기까지 이 틱 안이면 빨강을 기다리지 않고 노랑을 잡는다(두 장).</summary>
        public const int RollerAltGrabTicks = 2 * CardRushRules.CardDwellTicks;
        /// <summary>굴러오는 것을 노랑으로 넘는 때 — 닿기까지 점프 절반(20틱) 안. 공중에 있는 동안 닿아야 넘는다.</summary>
        public const int RollerJumpLeadTicks = CardRushRules.JumpTicks / 2;
        /// <summary>빈 길 계획의 수명과 반응 — 0.8초 / 0.4초(3차 그대로).</summary>
        public const int FreePlanTicks = 40;
        public const int FreeReactTicks = 20;

        private const int Closing = CardRushRules.RunMmPerTick + CardRushRules.RollerMmPerTick;

        private readonly System.Random rng;
        private readonly int reactTicks;
        private readonly int extraTicks;
        private readonly double hesitateChance;
        private readonly double freeDashChance;

        private CardRushBotPlan plan;
        //  망설여 넘긴 카드 한 장(쓴 횟수, 몇 번째 장) — 같은 장에서 다시 주사위를 굴리지 않는다.
        private int skippedUse = -1;
        private long skippedTurn = -1;

        public CardRushBotBrain(System.Random rng, CardRushConfig config)
            : this(rng, PickReact(rng, config),
                   //  Ticks는 지속이라 0초도 1틱으로 올린다 — 흔들림 0은 "없음"이어야 한다(시험·하네스가 반응을 고정한다).
                   config.BotReactExtraSeconds > 0f ? CardRushTiming.Ticks(config.BotReactExtraSeconds) : 0,
                   config.BotHesitateChance, config.BotFreeDashChance)
        {
        }

        /// <summary>값을 바로 넣는 생성자 — 하네스의 "읽는 고수"(반응 12틱, 흔들림·망설임 0, 빈 길이면 늘 대시)가 쓴다.</summary>
        public CardRushBotBrain(System.Random rng, int reactTicks, int extraTicks, double hesitateChance, double freeDashChance)
        {
            this.rng = rng;
            this.reactTicks = reactTicks;
            this.extraTicks = extraTicks;
            this.hesitateChance = hesitateChance;
            this.freeDashChance = freeDashChance;
        }

        private static int PickReact(System.Random rng, CardRushConfig config)
        {
            float react = config.BotReactMinSeconds + (float)rng.NextDouble() * (config.BotReactMaxSeconds - config.BotReactMinSeconds);
            return CardRushTiming.Ticks(react);
        }

        /// <summary>봇마다 한 번 뽑은 반응 틱.</summary>
        public int ReactTicks => reactTicks;

        public CardRushBotPlan Plan => plan;

        public bool ShouldTap(in CardRushRunnerState s, long tick, long gameplayStartTick, int lane, CardRushTrack track, in CardRushTuning tuning)
        {
            //  손을 쓸 수 없는 틱(출발 전·쿨다운·기절·완주·공중의 노랑·파랑)엔 생각하지 않는다 — 계획도 그대로 둔다.
            if (track == null) return false;
            //  돌리는 데는 판단이 필요 없다 — 사람도 쿨다운이 끝나면 바로 누른다. 시작할 수 있으면 바로 돌린다.
            if (CardRushCard.CanStart(s, tick, gameplayStartTick)) return true;
            bool canLock = CardRushCard.CanLock(s, tick, gameplayStartTick);
            bool canUse = CardRushCard.CanUse(s, tick, gameplayStartTick, lane, track);
            if (!canLock && !canUse) return false;

            var threat = ReadThreat(s, tick, track, out long key);
            if (!plan.Active || plan.Key != key || plan.Threat != threat || tick > plan.Until)
                plan = MakePlan(s, tick, track, threat, key);

            if (canUse)
            {
                var held = CardRushCard.SpinColor(s, s.LockTick, gameplayStartTick, lane, track);
                return Fits(held, threat) ? UseDue(held, threat, s, tick, track) : SafeToDiscard(held, s, tick, track);
            }

            //  잡기 — 반응이 지나 원하는 색이 보이면.
            if (plan.Want == null || tick < plan.ReactTick) return false;
            if (!CardRushCard.CardAt(s, tick, gameplayStartTick, lane, track, out var card)) return false;
            if (card == plan.Want) return !Hesitates(s, tick, gameplayStartTick);
            if (card == plan.Alt && AltDue(threat, s, tick, track)) return true;
            return false;
        }

        /// <summary>지금 상대할 위협과 그 열쇠(어느 구역·장애물인가).</summary>
        public static CardRushBotThreat ReadThreat(in CardRushRunnerState s, long tick, CardRushTrack track, out long key)
        {
            if (s.ZoneTick != 0 && s.ZoneIndex < track.Zones.Count)
            {
                key = 1_000_000L + s.ZoneIndex;
                return CardRushBotThreat.Zone;
            }
            if (s.ObstacleIndex < track.Obstacles.Count)
            {
                var o = track.Obstacles[s.ObstacleIndex];
                key = 2_000_000L + s.ObstacleIndex;
                if (o.Kind == CardRushObstacleKind.Pit)
                {
                    if (o.AtMm - s.PosMm <= NoticeMm) return CardRushBotThreat.Pit;
                }
                else if (CardRushRunnerSim.TryFront(s, tick, track, out int front) && front - s.PosMm <= NoticeMm)
                {
                    //  이미 던진 투사체가 닿을 곳이면 다시 빨강을 잡지 않는다(굴러오는 것은 다가오기만 하니 맞는다).
                    if (s.ShotTick != 0 && front <= s.ShotFromMm + CardRushRules.ShotRangeMm)
                    {
                        key = 3_000_000L + s.ObstacleIndex;
                        return CardRushBotThreat.ShotInFlight;
                    }
                    return o.Kind == CardRushObstacleKind.Barricade ? CardRushBotThreat.Barricade : CardRushBotThreat.Roller;
                }
            }
            key = 0;
            return CardRushBotThreat.None;
        }

        private CardRushBotPlan MakePlan(in CardRushRunnerState s, long tick, CardRushTrack track, CardRushBotThreat threat, long key)
        {
            const long Forever = long.MaxValue;
            switch (threat)
            {
                case CardRushBotThreat.Zone:
                    return new CardRushBotPlan(threat, key, CardColor.Blue, null, ReactAt(tick), Forever);
                case CardRushBotThreat.Barricade:
                    return new CardRushBotPlan(threat, key, CardColor.Red, null, ReactAt(tick), Forever);
                case CardRushBotThreat.Roller:
                    return new CardRushBotPlan(threat, key, CardColor.Red, CardColor.Yellow, ReactAt(tick), Forever);
                case CardRushBotThreat.Pit:
                    return new CardRushBotPlan(threat, key, CardColor.Yellow, null, ReactAt(tick), Forever);
                case CardRushBotThreat.ShotInFlight:
                    return new CardRushBotPlan(threat, key, null, null, tick, Forever);
            }
            //  빈 길 — 확률로 대시하려 한다. 반응은 고정(3차 시안 0.4초).
            CardColor? want = ClearForDash(s, tick, track) && rng.NextDouble() < freeDashChance ? CardColor.Blue : (CardColor?)null;
            return new CardRushBotPlan(threat, key, want, null, tick + FreeReactTicks, tick + FreePlanTicks);
        }

        private static bool Fits(CardColor held, CardRushBotThreat threat) => threat switch
        {
            CardRushBotThreat.Zone => held == CardColor.Blue,
            CardRushBotThreat.Barricade => held == CardColor.Red,
            CardRushBotThreat.Roller => held == CardColor.Red || held == CardColor.Yellow,
            CardRushBotThreat.Pit => held == CardColor.Yellow,
            _ => false,
        };

        //  맞는 카드를 쥐었다 — 쓸 때인가.
        private static bool UseDue(CardColor held, CardRushBotThreat threat, in CardRushRunnerState s, long tick, CardRushTrack track)
        {
            switch (threat)
            {
                case CardRushBotThreat.Zone:
                    return true;   // 빠를수록 멀리 빠져나간다
                case CardRushBotThreat.Barricade:
                    return InShotRange(s, tick, track);
                case CardRushBotThreat.Roller:
                    if (held == CardColor.Red) return InShotRange(s, tick, track);
                    return s.ObstacleTick != 0 && TicksToRoller(s, tick, track) <= RollerJumpLeadTicks;
                case CardRushBotThreat.Pit:
                    return s.PosMm >= PitJumpCenter(track.Obstacles[s.ObstacleIndex]);
            }
            return false;
        }

        /// <summary>
        /// 구덩이를 넘는 뛰는 자리의 가운데. 이번 틱에 쓰면 40틱 뒤 지금 자리 + 2.4 m에 내려앉으니
        /// 뛰는 자리는 (앞 가장자리 + 폭 − 2.4 m, 앞 가장자리) 안이어야 한다(spec §2).
        /// </summary>
        public static int PitJumpCenter(in CardRushObstacle pit)
        {
            const int air = CardRushRules.JumpMmPerTick * CardRushRules.JumpTicks;
            return pit.AtMm - (air - pit.WidthMm) / 2;
        }

        private static bool InShotRange(in CardRushRunnerState s, long tick, CardRushTrack track)
            => CardRushRunnerSim.TryFront(s, tick, track, out int front)
               && front - (s.PosMm + CardRushRules.ShotStartAheadMm) <= CardRushRules.ShotRangeMm - ShotAimSlackMm;

        //  굴러오는 것에 닿기까지 남은 틱 — 아직 안 풀렸으면 풀리는 자리까지 달리는 틱을 더한다.
        private static long TicksToRoller(in CardRushRunnerState s, long tick, CardRushTrack track)
        {
            var o = track.Obstacles[s.ObstacleIndex];
            if (s.ObstacleTick != 0)
                return System.Math.Max(0, (CardRushRunnerSim.RollerPos(o, s.ObstacleTick, tick) - s.PosMm - CardRushRules.RollerContactMm) / Closing);
            int toReveal = System.Math.Max(0, o.AtMm - CardRushRules.RollerRevealMm - s.PosMm);
            return toReveal / CardRushRules.RunMmPerTick + (CardRushRules.RollerRevealMm - CardRushRules.RollerContactMm) / Closing;
        }

        //  대안 카드를 잡을 때 — 굴러오는 것이 가까워 빨강을 기다릴 틈이 없을 때만.
        private static bool AltDue(CardRushBotThreat threat, in CardRushRunnerState s, long tick, CardRushTrack track)
            => threat == CardRushBotThreat.Roller && TicksToRoller(s, tick, track) <= RollerAltGrabTicks;

        /// <summary>
        /// 안 맞는 카드를 쥐었다 — 지금 써서 비워도 해가 없나(4차 spec §7 "버리기"). 아니면 사라지길 기다린다.
        /// 빨강은 아무 때나(헛방). 노랑은 바리케이드·구덩이 앞이 1 m 넘게 비었을 때. 파랑은 굴러오는 것·구덩이가 대시 거리 + 여유 밖이고
        /// 곧 켜질 구역이 없을 때(구역에서 쓰려고 쥐고 있는다). 바리케이드 앞 대시는 벽에 일찍 닿을 뿐이라 해가 없다.
        /// 빈 길에서 쥔 파랑은 이 규칙으로 바로 쓴다(빈 길 대시).
        /// </summary>
        private static bool SafeToDiscard(CardColor held, in CardRushRunnerState s, long tick, CardRushTrack track)
        {
            switch (held)
            {
                case CardColor.Red:
                    return true;
                case CardColor.Yellow:
                    if (s.ObstacleIndex >= track.Obstacles.Count) return true;
                    var o = track.Obstacles[s.ObstacleIndex];
                    if (o.Kind == CardRushObstacleKind.Roller) return true;
                    return o.AtMm - s.PosMm > YellowDiscardClearMm;
                default:
                    return ObstaclesClearForDash(s, tick, track, barricadeOk: true) && NextZoneIsFar(s, track);
            }
        }

        private static bool ClearForDash(in CardRushRunnerState s, long tick, CardRushTrack track)
            => ObstaclesClearForDash(s, tick, track, barricadeOk: false) && NextZoneIsFar(s, track);

        private static bool ObstaclesClearForDash(in CardRushRunnerState s, long tick, CardRushTrack track, bool barricadeOk)
        {
            if (s.ObstacleIndex >= track.Obstacles.Count) return true;
            var o = track.Obstacles[s.ObstacleIndex];
            switch (o.Kind)
            {
                case CardRushObstacleKind.Pit:
                    return o.AtMm - s.PosMm > CardRushRules.DashMm + ClearPitMarginMm;
                case CardRushObstacleKind.Barricade:
                    return barricadeOk || o.AtMm - s.PosMm > CardRushRules.DashMm + ClearObstacleMarginMm;
                default:
                    return CardRushRunnerSim.TryFront(s, tick, track, out int front)
                           && front - s.PosMm > CardRushRules.DashMm + ClearObstacleMarginMm;
            }
        }

        private static bool NextZoneIsFar(in CardRushRunnerState s, CardRushTrack track)
        {
            if (s.ZoneIndex >= track.Zones.Count) return true;
            if (s.ZoneTick != 0) return false;
            return track.Zones[s.ZoneIndex].FromMm - s.PosMm > CardRushRules.DashMm + ClearZoneMarginMm;
        }

        //  망설임은 카드 한 장에 한 번 굴린다 — 넘기면 그 장이 지나갈 때까지 안 잡는다.
        private bool Hesitates(in CardRushRunnerState s, long tick, long gameplayStartTick)
        {
            long turn = (tick - s.RollStartTick) / CardRushRules.CardDwellTicks;
            if (skippedUse == s.UseCount && skippedTurn == turn) return true;
            if (hesitateChance > 0 && rng.NextDouble() < hesitateChance)
            {
                skippedUse = s.UseCount;
                skippedTurn = turn;
                return true;
            }
            return false;
        }

        //  봇마다 한 번 뽑은 반응 + 계획마다 U(0, extra) 흔들림.
        private long ReactAt(long tick) => tick + reactTicks + rng.Next(0, extraTicks + 1);
    }
}

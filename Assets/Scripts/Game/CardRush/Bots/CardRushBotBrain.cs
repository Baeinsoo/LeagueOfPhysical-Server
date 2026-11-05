namespace LOP
{
    /// <summary>
    /// 봇 정책 하나 — 이번 틱에 탭하나. <see cref="CardRushBotSystem"/>이 봇 몸마다 하나씩 묻는다.
    /// 출시 봇은 <see cref="CardRushBotBrain"/> 하나이고, 튜닝 하네스가 시험용 정책(읽는 고수·파랑만·연타)을 끼운다(spec §9, 판정 R2).
    /// </summary>
    public interface ICardRushBotPolicy
    {
        bool ShouldTap(in CardRushRunnerState s, long tick, long gameplayStartTick, int lane, CardRushTrack track, in CardRushTuning tuning);
    }

    /// <summary>봇의 지금 계획(spec §8) — 시험이 들여다본다. Want·Alt가 null이면 "그 카드는 없음".</summary>
    public readonly struct CardRushBotPlan
    {
        public readonly bool Active;
        public readonly CardColor? Want;
        public readonly CardColor? Alt;
        public readonly long Deadline;
        public readonly long Until;
        public readonly long ReactTick;
        public readonly bool NeedNear;

        public CardRushBotPlan(CardColor? want, CardColor? alt, long deadline, long until, long reactTick, bool needNear)
        {
            Active = true;
            Want = want;
            Alt = alt;
            Deadline = deadline;
            Until = until;
            ReactTick = reactTick;
            NeedNear = needNear;
        }
    }

    /// <summary>
    /// 카드 러시 출시 봇 — 웹 시안의 봇(<c>botThink</c>)을 정수 틱·mm로 옮겼다(spec §8).
    /// 카드를 쓸 수 있는 틱에만 생각한다: 계획이 없거나 지났으면 지금 상황(켜진 구역 → 가까운 장애물 → 빈 길)으로 새로 세우고,
    /// 반응 틱이 지나 원하는 카드가 보이면 탭한다. 마감이 가까우면 대안 카드도 쓴다.
    /// 봇이 읽는 값은 서버 월드의 권위 상태라 결정론이 필요 없다 — 난수는 봇마다 나눈 <see cref="System.Random"/>.
    /// </summary>
    public sealed class CardRushBotBrain : ICardRushBotPolicy
    {
        /// <summary>앞 장애물을 "상대할 것"으로 보는 거리 — 시안 120 u.</summary>
        public const int NoticeMm = 4160;
        /// <summary>빈 길 판단: 장애물 앞면이 대시 + 이만큼 밖이어야 한다 — 시안 DASH + 40 u.</summary>
        public const int ClearObstacleMarginMm = 1400;
        /// <summary>빈 길 판단: 다음 구역 시작이 대시 + 이만큼 밖이어야 한다 — 시안 DASH + 30 u.</summary>
        public const int ClearZoneMarginMm = 1000;
        /// <summary>구역 안에서 끝까지 남은 거리가 대시의 이 비율(%)보다 짧으면 대시로 빠져나간다 — 시안 0.85.</summary>
        public const int EscapePercent = 85;
        /// <summary>바리케이드 계획의 수명 — 시안 2초.</summary>
        public const int BarricadePlanTicks = 100;
        /// <summary>빈 길 계획의 수명과 반응 — 시안 0.8초 / 0.4초.</summary>
        public const int FreePlanTicks = 40;
        public const int FreeReactTicks = 20;
        /// <summary>마감 여유 — 구역 3틱(시안 0.05초), 굴러오는 것 4틱(시안 0.08초).</summary>
        public const int ZoneDeadlineSlackTicks = 3;
        public const int RollerDeadlineSlackTicks = 4;

        private readonly System.Random rng;
        private readonly int reactTicks;
        private readonly int extraTicks;
        private readonly double hesitateChance;
        private readonly double freeDashChance;

        private CardRushBotPlan plan;

        public CardRushBotBrain(System.Random rng, CardRushConfig config)
        {
            this.rng = rng;
            float react = config.BotReactMinSeconds + (float)rng.NextDouble() * (config.BotReactMaxSeconds - config.BotReactMinSeconds);
            reactTicks = CardRushTiming.Ticks(react);
            //  Ticks는 지속이라 0초도 1틱으로 올린다 — 흔들림 0은 "없음"이어야 한다(시험·하네스가 반응을 고정한다).
            extraTicks = config.BotReactExtraSeconds > 0f ? CardRushTiming.Ticks(config.BotReactExtraSeconds) : 0;
            hesitateChance = config.BotHesitateChance;
            freeDashChance = config.BotFreeDashChance;
        }

        /// <summary>봇마다 한 번 뽑은 반응 틱.</summary>
        public int ReactTicks => reactTicks;

        public CardRushBotPlan Plan => plan;

        public bool ShouldTap(in CardRushRunnerState s, long tick, long gameplayStartTick, int lane, CardRushTrack track, in CardRushTuning tuning)
        {
            //  쓸 수 없는 틱(출발 전·쿨다운·기절·완주)엔 생각하지 않는다 — 시안 busy(r).
            if (track == null || !CardRushCard.CanUse(s, tick, gameplayStartTick)) return false;

            if (!plan.Active || tick > plan.Until) plan = MakePlan(s, tick, track);
            if (plan.Want == null || tick < plan.ReactTick) return false;
            if (!CardRushCard.TryCardAt(s, tick, gameplayStartTick, lane, track, out var card)) return false;

            if (card == plan.Want)
            {
                if (plan.NeedNear && card == CardColor.Red && !CanReach(s, tick, track)) return false;
                if (rng.NextDouble() < hesitateChance) return false;
                plan = default;
                return true;
            }
            //  대안은 마감까지 한 장 남짓(× 1.2)일 때만 — 정수로 (deadline − 지금) × 5 < Dwell × 6.
            if (card == plan.Alt && (plan.Deadline - tick) * 5 < CardRushRules.CardDwellTicks * 6)
            {
                if (card == CardColor.Red && !CanReach(s, tick, track)) return false;
                plan = default;
                return true;
            }
            return false;
        }

        private CardRushBotPlan MakePlan(in CardRushRunnerState s, long tick, CardRushTrack track)
        {
            //  1) 켜진 구역 — 끝이 대시 거리 안이면 빠져나가고(대안 방패), 아니면 버틴다.
            if (s.ZoneTick != 0 && s.ZoneIndex < track.Zones.Count)
            {
                var z = track.Zones[s.ZoneIndex];
                bool escape = (z.ToMm - s.PosMm) < CardRushRules.DashMm * EscapePercent / 100;
                long boom = s.ZoneTick + z.WarnTicks;
                return new CardRushBotPlan(
                    escape ? CardColor.Blue : CardColor.Yellow,
                    escape ? CardColor.Yellow : (CardColor?)null,
                    boom - ZoneDeadlineSlackTicks, boom, ReactAt(tick), needNear: false);
            }

            bool hasFront = CardRushRunnerSim.TryFront(s, tick, track, out int front);
            //  2) 가까운 장애물 — 빨강으로 부순다. 굴러오는 것은 닿기 직전이면 방패도 쓴다.
            if (hasFront && front - s.PosMm <= NoticeMm)
            {
                var o = track.Obstacles[s.ObstacleIndex];
                if (o.Kind == CardRushObstacleKind.Barricade)
                    return new CardRushBotPlan(CardColor.Red, null, 0, tick + BarricadePlanTicks, ReactAt(tick), needNear: true);

                long meet = (front - s.PosMm) / (CardRushRules.RunMmPerTick + CardRushRules.RollerMmPerTick);
                return new CardRushBotPlan(CardColor.Red, CardColor.Yellow,
                    tick + meet - RollerDeadlineSlackTicks, tick + meet, ReactAt(tick), needNear: true);
            }

            //  3) 앞이 비었으면 확률로 대시하려 한다. 반응은 고정(시안 0.4초).
            bool clear = (!hasFront || front - s.PosMm > CardRushRules.DashMm + ClearObstacleMarginMm)
                         && NextZoneIsFar(s, track);
            CardColor? want = clear && rng.NextDouble() < freeDashChance ? CardColor.Blue : (CardColor?)null;
            return new CardRushBotPlan(want, null, 0, tick + FreePlanTicks, tick + FreeReactTicks, needNear: false);
        }

        private static bool NextZoneIsFar(in CardRushRunnerState s, CardRushTrack track)
        {
            if (s.ZoneIndex >= track.Zones.Count) return true;
            if (s.ZoneTick != 0) return false;
            return track.Zones[s.ZoneIndex].FromMm - s.PosMm > CardRushRules.DashMm + ClearZoneMarginMm;
        }

        //  봇마다 한 번 뽑은 반응 + 계획마다 U(0, extra) 흔들림(시안 + random·0.2).
        private long ReactAt(long tick) => tick + reactTicks + rng.Next(0, extraTicks + 1);

        //  판정과 같은 식(spec §3.5 빨강) — 손이 닿을 때만 빨강을 쓴다.
        private static bool CanReach(in CardRushRunnerState s, long tick, CardRushTrack track)
            => CardRushRunnerSim.TryFront(s, tick, track, out int front)
               && front - s.PosMm > -CardRushRules.SmashBackMm
               && front - s.PosMm <= CardRushRules.SmashReachMm;
    }
}

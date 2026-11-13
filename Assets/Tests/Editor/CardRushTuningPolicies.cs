namespace LOP.Tests
{
    /// <summary>
    /// 튜닝 하네스의 시험용 두뇌(spec §9.2, 판정 R2) — 출시 봇이 아니다. 같은 판에 정책만 바꿔 끼워 "읽고 누르는 것"이
    /// "파랑만 누르기"·"막 누르기"보다 얼마나 빠른지 잰다.
    /// 반응은 셋이 같게: 카드가 바뀐 뒤 <see cref="ReactTicks"/>틱이 지나야 그 카드를 본다(사람이 새 카드를 알아보는 시간).
    /// 막 누르기는 카드를 안 보므로 반응이 없다 — 쓸 수 있는 틱마다 누른다(= 그 순간의 무작위 첫 장을 쓴다).
    /// </summary>
    public static class CardRushTuningPolicies
    {
        /// <summary>읽는 고수·파랑만의 반응 — 0.24초(spec §9.2 "모두 12틱").</summary>
        public const int ReactTicks = 12;

        //  3차 값 — 4차 Shared에서 지워졌다. 컴파일만 지키는 임시(Task 3이 4차 정책으로 다시 짠다).
        private const int LegacySmashReachMm = 1660, LegacySmashBackMm = 200, LegacyBlockTicks = 50;

        public const string Bot = "봇";
        public const string Judged = "읽는고수";
        public const string BlueOnly = "파랑만";
        public const string Masher = "막누름";

        /// <summary>지금 보이는 카드를 이미 알아봤나 — 카드가 바뀐 지 react틱 지났으면 그 색을 돌려준다.</summary>
        public static bool TrySeenCard(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, int react, out CardColor card)
        {
            if (!(CardRushCard.CanLock(s, tick, g) || CardRushCard.CanUse(s, tick, g, lane, track)) || !CardRushCard.CardAt(s, tick, g, lane, track, out card))
            {
                card = default;
                return false;
            }
            long shown = (tick - CardRushCard.SpinBase(s, g)) % CardRushRules.CardDwellTicks;
            return shown >= react;
        }

        /// <summary>파랑만 — 파랑을 알아보면 쓴다. 장애물·구역은 안 본다.</summary>
        public sealed class BlueOnlyPolicy : ICardRushBotPolicy
        {
            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
                => TrySeenCard(s, tick, g, lane, track, ReactTicks, out var card) && card == CardColor.Blue;
        }

        /// <summary>막 누름 — 쓸 수 있으면 매 틱 누른다. 카드는 안 본다(쿨다운이 끝난 틱의 무작위 첫 장을 쓴다).</summary>
        public sealed class MasherPolicy : ICardRushBotPolicy
        {
            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
                => track != null && (CardRushCard.CanLock(s, tick, g) || CardRushCard.CanUse(s, tick, g, lane, track));
        }

        /// <summary>
        /// 읽는 고수(spec §9.2) — 상황을 정확히 읽는 사람의 대리. 반응만 사람(12틱).
        /// 빨강은 손이 닿을 때. 노랑은 굴러오는 것이 반 장 안에 닿거나, 구역이 방패 시간 안에 터지는데 그때 안에 있고 대시로 못 빠져나갈 때.
        /// 파랑은 굴러오는 것을 들이받지 않고, 켜진 구역에선 빠져나가는 대시일 때, 바리케이드 앞에 서 있지 않을 때 — 나머지는 늘.
        /// </summary>
        public sealed class JudgedPolicy : ICardRushBotPolicy
        {
            //  굴러오는 것: 대시가 끝나도 이만큼은 떨어져 있어야 들이받지 않는다고 본다.
            private const int RollerDashSafeMm = 1500;

            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
            {
                if (!TrySeenCard(s, tick, g, lane, track, ReactTicks, out var card)) return false;

                bool hasObstacle = s.ObstacleIndex < track.Obstacles.Count;
                var o = hasObstacle ? track.Obstacles[s.ObstacleIndex] : default;
                bool hasFront = CardRushRunnerSim.TryFront(s, tick, track, out int front);
                int gap = front - s.PosMm;
                bool rolling = hasObstacle && o.Kind == CardRushObstacleKind.Roller && s.ObstacleTick != 0;
                bool zoneOn = s.ZoneIndex < track.Zones.Count && s.ZoneTick != 0;
                var z = zoneOn ? track.Zones[s.ZoneIndex] : default;
                long boom = zoneOn ? s.ZoneTick + z.WarnTicks : 0;

                switch (card)
                {
                    case CardColor.Red:
                        return hasFront && gap > -LegacySmashBackMm && gap <= LegacySmashReachMm;

                    case CardColor.Yellow:
                        //  굴러오는 것: 다음 빨강이 닿기 전에 손에 안 들어오면, 방패 시간 안에 닿을 때 막는다.
                        if (rolling && !RedInTime(s, tick, g, lane, track, gap)
                            && gap - CardRushRules.RollerContactMm < CardRushRules.RollerMmPerTick * (LegacyBlockTicks - 2))
                            return true;
                        if (zoneOn)
                        {
                            long left = boom - tick;
                            //  방패가 터지는 틱까지 켜져 있어야 한다(쓴 틱 + BlockTicks > 터짐).
                            if (left >= LegacyBlockTicks) return false;
                            //  그냥 달려도 터질 때 밖이면 방패가 필요 없다.
                            int runAt = s.PosMm + CardRushRules.RunMmPerTick * (int)left;
                            if (!Inside(z, runAt)) return false;
                            return !DashEscapes(s.PosMm, z, left);
                        }
                        return false;

                    case CardColor.Blue:
                        if (rolling && gap < CardRushRules.DashMm + CardRushRules.RollerContactMm + RollerDashSafeMm) return false;
                        //  바리케이드 바로 앞이면 대시로 벌 거리가 없다.
                        if (hasObstacle && o.Kind == CardRushObstacleKind.Barricade
                            && o.AtMm - CardRushRules.BarricadeStopMm - s.PosMm < CardRushRules.DashMm / 2)
                            return false;
                        if (zoneOn) return DashEscapes(s.PosMm, z, boom - tick);
                        return true;
                }
                return false;
            }

            //  읽는 사람은 카드 순서(빨 → 노 → 파)를 안다 — 다음 빨강을 알아볼 틱들 중에 굴러오는 것이 손 안(닿기 전)인 틱이 있나.
            private static bool RedInTime(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, int gap)
            {
                const int closing = CardRushRules.RunMmPerTick + CardRushRules.RollerMmPerTick;
                long reach = tick + System.Math.Max(0, (gap - LegacySmashReachMm + closing - 1) / closing);
                long contact = tick + (gap - CardRushRules.RollerContactMm - closing) / closing;
                if (contact < reach) return false;
                if (!TryNextSeen(s, tick, g, lane, track, CardColor.Red, out long from, out long to)) return false;
                return from <= contact && to >= reach;
            }

            //  지금 이후 그 색을 알아보고 누를 수 있는 첫 구간 [from, to].
            private static bool TryNextSeen(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, CardColor color,
                                            out long from, out long to)
            {
                from = to = 0;
                long spinBase = CardRushCard.SpinBase(s, g);
                if (tick < spinBase) return false;
                long turn = (tick - spinBase) / CardRushRules.CardDwellTicks;
                int first = (int)track.FirstCard(lane, s.UseCount);
                for (long k = turn; k < turn + CardColors.Count; k++)
                {
                    if ((first + k) % CardColors.Count != (int)color) continue;
                    long start = spinBase + k * CardRushRules.CardDwellTicks;
                    from = System.Math.Max(tick, start + ReactTicks);
                    to = start + CardRushRules.CardDwellTicks - 1;
                    if (from <= to) return true;
                }
                return false;
            }

            private static bool Inside(in CardRushZone z, int pos)
                => pos >= z.FromMm - CardRushRules.ZoneMarginMm && pos <= z.ToMm + CardRushRules.ZoneMarginMm;

            //  지금 대시하면 터지는 틱에 구역 밖(뒤로 빠져나감)인가 — 대시 14틱 뒤엔 달린다.
            private static bool DashEscapes(int pos, in CardRushZone z, long left)
            {
                long k = System.Math.Min(left + 1, CardRushRules.DashTicks);
                long after = pos + CardRushRules.DashOffset((int)k) + CardRushRules.RunMmPerTick * System.Math.Max(0, left + 1 - CardRushRules.DashTicks);
                return after > z.ToMm + CardRushRules.ZoneMarginMm;
            }
        }
    }
}

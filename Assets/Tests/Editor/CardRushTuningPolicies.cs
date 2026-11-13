namespace LOP.Tests
{
    /// <summary>
    /// 튜닝 하네스의 시험용 두뇌(4차 spec §8) — 출시 봇이 아니다. 같은 판에 정책만 바꿔 끼워 "읽고 잡고 쓰는 것"이
    /// "파랑만 누르기"·"막 누르기"보다 얼마나 빠른지 잰다.
    /// 반응은 같게: 카드가 바뀐 뒤(또는 위협이 바뀐 뒤) <see cref="ReactTicks"/>틱이 지나야 그 카드를 잡는다(사람이 알아보는 시간).
    /// 막 누르기는 카드를 안 보므로 반응이 없다 — 쓸 수 있으면 매 틱 누른다(= 잡고 다음 틱에 쓴다).
    /// </summary>
    public static class CardRushTuningPolicies
    {
        /// <summary>읽는 고수·파랑만의 반응 — 0.24초(spec §8 "반응 12틱 같게").</summary>
        public const int ReactTicks = 12;

        public const string Bot = "봇";
        public const string Judged = "읽는고수";
        public const string BlueOnly = "파랑만";
        public const string Masher = "막누름";
        public const string YellowOnly = "노랑만";

        /// <summary>
        /// 읽는 고수 — 출시 봇과 같은 판단(<see cref="CardRushBotBrain"/>)에 반응만 사람(12틱), 흔들림·망설임 없음, 빈 길이면 늘 대시.
        /// 다음 위협에 맞는 색만 잡으므로 버릴 일이 거의 없다.
        /// </summary>
        public static ICardRushBotPolicy MakeJudged() => new CardRushBotBrain(new System.Random(0), ReactTicks, 0, 0.0, 1.0);

        /// <summary>파랑만 — 파랑을 알아보면 잡고 다음 틱에 쓴다. 장애물·구역은 안 본다.</summary>
        public sealed class BlueOnlyPolicy : ICardRushBotPolicy
        {
            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
            {
                if (track == null) return false;
                if (CardRushCard.CanStart(s, tick, g)) return true;
                if (s.LockTick != 0) return CardRushCard.CanUse(s, tick, g, lane, track);
                if (!CardRushCard.CanLock(s, tick, g) || !CardRushCard.CardAt(s, tick, g, lane, track, out var card)) return false;
                long shown = (tick - s.RollStartTick) % CardRushRules.CardDwellTicks;
                return card == CardColor.Blue && shown >= ReactTicks;
            }
        }

        /// <summary>
        /// 노랑만 — 노랑을 알아보면 잡고 다음 틱에 쓴다(땅에서만 쓸 수 있으니 공중이면 내려앉을 때까지 쥔다).
        /// 점프(3 m/s)가 달리기(1.8 m/s)보다 빨라 "노랑 연타"가 이득인지 잰다.
        /// </summary>
        public sealed class YellowOnlyPolicy : ICardRushBotPolicy
        {
            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
            {
                if (track == null) return false;
                if (CardRushCard.CanStart(s, tick, g)) return true;
                if (s.LockTick != 0) return CardRushCard.CanUse(s, tick, g, lane, track);
                if (!CardRushCard.CanLock(s, tick, g) || !CardRushCard.CardAt(s, tick, g, lane, track, out var card)) return false;
                long shown = (tick - s.RollStartTick) % CardRushRules.CardDwellTicks;
                return card == CardColor.Yellow && shown >= ReactTicks;
            }
        }

        /// <summary>막 누름 — 돌리거나 잡거나 쓸 수 있으면 매 틱 누른다. 카드는 안 본다(돌린 다음 틱의 첫 장을 잡아 바로 쓴다).</summary>
        public sealed class MasherPolicy : ICardRushBotPolicy
        {
            public bool ShouldTap(in CardRushRunnerState s, long tick, long g, int lane, CardRushTrack track, in CardRushTuning tuning)
                => track != null && (CardRushCard.CanStart(s, tick, g) || CardRushCard.CanLock(s, tick, g) || CardRushCard.CanUse(s, tick, g, lane, track));
        }
    }
}

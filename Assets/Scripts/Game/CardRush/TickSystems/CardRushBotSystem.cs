using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 카드 러시 시험용 봇의 손발(서버 Begin 페이즈). 봇 몸마다 정책(출시 봇 = <see cref="CardRushBotBrain"/>)에게
    /// "이번 틱에 탭하나"를 묻고, 그 답을 사람 입력과 같은 길(<see cref="InputBufferSystem.Enqueue"/>)로
    /// 이번 틱 번호에 넣는다. 같은 틱 뒤이어 도는 <see cref="CardRushWorld"/>가 그것을 꺼내 쓰므로
    /// 봇은 사람과 똑같이 카드를 잡는다.
    /// </summary>
    public sealed class CardRushBotSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly CardRushWorld world;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly Func<string, bool> isBot;
        private readonly Func<int, System.Random, ICardRushBotPolicy> policyFor;
        private readonly ulong seed;

        private readonly Dictionary<string, ICardRushBotPolicy> brains = new Dictionary<string, ICardRushBotPolicy>();
        private readonly Dictionary<string, long> sequences = new Dictionary<string, long>();
        private readonly List<string> botIds = new List<string>();

        public CardRushBotSystem(GameFramework.World.EntityRegistry registry, CardRushWorld world,
                                 InputBufferSystem inputBufferSystem, IMatchSeed matchSeed,
                                 CardRushConfig config, Func<string, bool> isBot,
                                 Func<int, System.Random, ICardRushBotPolicy> policyFor = null)
        {
            this.registry = registry;
            this.world = world;
            this.inputBufferSystem = inputBufferSystem;
            seed = matchSeed.Value;
            this.isBot = isBot;
            //  (레인, 봇 난수) → 정책. 안 넘기면 출시 봇. 튜닝 하네스가 레인마다 시험용 정책을 끼운다(판정 R2).
            this.policyFor = policyFor ?? ((lane, rng) => new CardRushBotBrain(rng, config));
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectBots();
            if (botIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < botIds.Count; i++)
            {
                string id = botIds[i];
                var entity = registry.Get(id);
                var runner = entity?.Get<CardRushRunner>();
                if (runner == null)
                {
                    continue;
                }

                bool tap = BrainFor(id, i, runner.Lane).ShouldTap(runner.State, tick, world.GameplayStartTick, runner.Lane, world.Track, world.Tuning);
                var command = new InputCommand { Grab = tap };
                //  사람 입력처럼 차례 번호를 하나씩 올린다 — 입력 시스템이 번호 틈을 유실로 센다.
                sequences.TryGetValue(id, out long sequence);
                command.SequenceNumber = ++sequence;
                sequences[id] = sequence;
                inputBufferSystem.Enqueue(entity.Get<InputBuffer>(), tick, command);
            }
        }

        //  순서를 고정한다 — 해시 순서를 타면 같은 씨앗으로 같은 판이 재현되지 않는다.
        private void CollectBots()
        {
            botIds.Clear();
            foreach (var entity in registry.All)
            {
                if (entity.Get<CardRushRunner>() == null || entity.Has<InputBuffer>() == false)
                {
                    continue;
                }
                var ownership = entity.Get<GameFramework.World.Ownership>();
                if (ownership != null && isBot(ownership.OwnerId))
                {
                    botIds.Add(entity.Id);
                }
            }
            botIds.Sort(StringComparer.Ordinal);
        }

        private ICardRushBotPolicy BrainFor(string entityId, int botIndex, int lane)
        {
            if (brains.TryGetValue(entityId, out var brain))
            {
                return brain;
            }
            //  봇마다 판 씨앗에서 따로 나눈 난수 — 같은 씨앗이면 같은 판이 다시 나온다.
            ulong mixed = GameFramework.Rng.Hashing.Combine(seed, (ulong)botIndex);
            brain = policyFor(lane, new System.Random(unchecked((int)mixed)));
            brains[entityId] = brain;
            return brain;
        }
    }
}

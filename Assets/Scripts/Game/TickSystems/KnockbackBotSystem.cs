using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 넉백 시험용 봇의 손발(서버 Begin 페이즈). 봇 몸마다 보이는 것을 모아 두뇌에 묻고, 그 답을
    /// 사람 입력과 같은 길(<see cref="InputBufferSystem.Enqueue"/>)로 이번 틱 번호에 넣는다.
    /// 같은 틱 뒤이어 도는 <see cref="ServerInputSystem"/>이 그것을 꺼내 쓰므로 봇은 사람과 똑같이 움직이고 쏜다.
    /// </summary>
    public sealed class KnockbackBotSystem : GameFramework.Runner.ITickSystem
    {
        // 이 높이 아래로 내려간 몸은 떨어지는 중이라 표적도 위협도 아니다(맞음 판정과 같은 기준).
        private const float StandingMinY = -0.05f;

        private readonly GameFramework.World.EntityRegistry registry;
        private readonly KnockbackWorld world;
        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly ulong seed;
        private readonly float tickInterval;

        private readonly Dictionary<string, KnockbackBotBrain> brains = new Dictionary<string, KnockbackBotBrain>();
        private readonly Dictionary<string, long> sequences = new Dictionary<string, long>();
        private readonly List<string> botIds = new List<string>();
        private readonly List<KnockbackBotBody> bodies = new List<KnockbackBotBody>();
        private readonly List<KnockbackBotShot> shots = new List<KnockbackBotShot>();

        public KnockbackBotSystem(GameFramework.World.EntityRegistry registry, KnockbackWorld world,
                                  KnockbackMatchState state, KnockbackConfig config,
                                  InputBufferSystem inputBufferSystem, IMatchSeed matchSeed,
                                  float tickInterval = 0.02f)
        {
            this.registry = registry;
            this.world = world;
            this.state = state;
            this.config = config;
            this.inputBufferSystem = inputBufferSystem;
            seed = matchSeed.Value;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (state.Bots.Count == 0)
            {
                return;
            }
            CollectBodies();
            if (botIds.Count == 0)
            {
                return;
            }
            CollectShots();

            var zone = MountedArcheryZone.CircleAt(state.ZoneStages, tick, Vector2.zero, world.Floor.ZoneStartRadius);
            var next = zone;
            if (state.ZoneStages.Count > 0)
            {
                var latest = state.ZoneStages[state.ZoneStages.Count - 1];
                next = (latest.toCenter, latest.toRadius);
            }

            foreach (var id in botIds)
            {
                var entity = registry.Get(id);
                var transform = entity.Get<GameFramework.World.Transform>();
                var velocity = entity.Get<GameFramework.World.Velocity>()?.Linear.ToUnity() ?? Vector3.zero;
                var position = transform.Position.ToUnity();
                var view = new KnockbackBotView
                {
                    SelfId = id,
                    Tick = tick,
                    TickInterval = tickInterval,
                    Started = tick >= world.GameplayStartTick,
                    Position = new Vector2(position.x, position.z),
                    Velocity = new Vector2(velocity.x, velocity.z),
                    Enemies = bodies,
                    Shots = shots,
                    Zone = zone,
                    NextZone = next,
                    Config = config,
                };

                var command = BrainFor(id).Think(view);
                //  사람 입력처럼 차례 번호를 하나씩 올린다 — 입력 시스템이 번호 틈을 유실로 센다.
                sequences.TryGetValue(id, out long sequence);
                command.SequenceNumber = ++sequence;
                sequences[id] = sequence;
                inputBufferSystem.Enqueue(entity.Get<InputBuffer>(), tick, command);
            }
        }

        // 순서를 고정한다 — 해시 순서를 타면 같은 씨앗으로 같은 판이 재현되지 않는다.
        private void CollectBodies()
        {
            bodies.Clear();
            botIds.Clear();
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) || registry.TryGet(id, out var entity) == false)
                {
                    continue;
                }
                var transform = entity.Get<GameFramework.World.Transform>();
                if (transform == null || transform.Position.Y < StandingMinY)
                {
                    continue;
                }

                var velocity = entity.Get<GameFramework.World.Velocity>()?.Linear.ToUnity() ?? Vector3.zero;
                bodies.Add(new KnockbackBotBody(id, new Vector2(transform.Position.X, transform.Position.Z),
                    new Vector2(velocity.x, velocity.z)));
                if (state.Bots.Contains(id) && entity.Has<InputBuffer>())
                {
                    botIds.Add(id);
                }
            }
            bodies.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            botIds.Sort(string.CompareOrdinal);
        }

        private void CollectShots()
        {
            shots.Clear();
            foreach (var shot in world.Shots.Shots)
            {
                shots.Add(new KnockbackBotShot(shot.ShooterId, shot.FireTick, shot.Origin, shot.Direction));
            }
        }

        private KnockbackBotBrain BrainFor(string entityId)
        {
            if (brains.TryGetValue(entityId, out var brain))
            {
                return brain;
            }
            //  봇마다 판 씨앗에서 따로 나눈 난수 — 같은 씨앗이면 같은 판이 다시 나온다.
            ulong mixed = seed ^ ((ulong)BotNumber(entityId) * 0x9E3779B97F4A7C15UL);
            brain = new KnockbackBotBrain(new System.Random(unchecked((int)(mixed ^ (mixed >> 32)))));
            brains[entityId] = brain;
            return brain;
        }

        // 봇 번호는 Players 안 순서로 센다 — 엔티티 id 문자열은 번호가 아니다.
        private int BotNumber(string entityId) => state.Players.IndexOf(entityId) + 1;
    }
}

using System;
using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 시험용 봇의 손발(서버 Begin 페이즈). 봇 몸마다 보이는 것을 모아 두뇌에 묻고, 그 답을
    /// 사람 입력과 같은 길(<see cref="InputBufferSystem.Enqueue"/>)로 이번 틱 번호에 넣는다.
    /// 같은 틱 뒤이어 도는 <see cref="ServerInputSystem"/>이 그것을 꺼내 쓰므로 봇은 사람과 똑같이 움직이고 쏜다.
    /// </summary>
    public sealed class MountedArcheryBotSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly MountedArcheryWorld world;
        private readonly MountedArcheryMatchState state;
        private readonly MountedArcheryConfig config;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly ulong seed;
        private readonly Func<Vector3, Vector3, bool> visible;
        private readonly float tickInterval;

        private readonly Dictionary<string, MountedArcheryBotBrain> brains = new Dictionary<string, MountedArcheryBotBrain>();
        private readonly Dictionary<string, long> sequences = new Dictionary<string, long>();
        private readonly List<string> botIds = new List<string>();
        private readonly List<BotBody> bodies = new List<BotBody>();
        private readonly List<BotArrow> arrows = new List<BotArrow>();

        public MountedArcheryBotSystem(GameFramework.World.EntityRegistry registry, MountedArcheryWorld world,
                                       MountedArcheryMatchState state, MountedArcheryConfig config,
                                       InputBufferSystem inputBufferSystem, IMatchSeed matchSeed,
                                       Func<Vector3, Vector3, bool> visible, float tickInterval = 0.02f)
        {
            this.registry = registry;
            this.world = world;
            this.state = state;
            this.config = config;
            this.inputBufferSystem = inputBufferSystem;
            seed = matchSeed.Value;
            this.visible = visible;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectBodiesAndBots();
            if (botIds.Count == 0)
            {
                return;
            }
            CollectArrows(tick);

            var zone = MountedArcheryZone.CircleAt(state.ZoneStages, tick, Vector2.zero, config.ZoneInitialRadius);
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
                var aim = entity.Get<ArcheryAim>();
                var view = new BotView
                {
                    SelfId = id,
                    Tick = tick,
                    TickInterval = tickInterval,
                    Started = tick >= world.GameplayStartTick,
                    Position = transform.Position.ToUnity(),
                    HeadingDeg = Mathf.DeltaAngle(0f, transform.Rotation.ToUnity().eulerAngles.y),
                    Velocity = entity.Get<GameFramework.World.Velocity>()?.Linear.ToUnity() ?? Vector3.zero,
                    AimDrawing = aim != null && aim.Drawing,
                    AimDrawRatio = aim?.DrawRatio ?? 0f,
                    Zone = zone,
                    NextZone = next,
                    Bodies = bodies,
                    Arrows = arrows,
                    Visible = visible,
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

        // 순서를 고정한다 — 딕셔너리 순서를 타면 같은 씨앗으로 같은 판이 재현되지 않는다.
        private void CollectBodiesAndBots()
        {
            bodies.Clear();
            botIds.Clear();
            foreach (var pair in state.Players)
            {
                if (pair.Value.Alive == false || registry.TryGet(pair.Key, out var entity) == false)
                {
                    continue;
                }
                var transform = entity.Get<GameFramework.World.Transform>();
                if (transform == null)
                {
                    continue;
                }

                bodies.Add(new BotBody(pair.Key, transform.Position.ToUnity(),
                    Mathf.DeltaAngle(0f, transform.Rotation.ToUnity().eulerAngles.y),
                    entity.Get<GameFramework.World.Velocity>()?.Linear.ToUnity() ?? Vector3.zero));
                if (MountedArcheryBots.IsBot(pair.Value.UserId) && entity.Has<InputBuffer>())
                {
                    botIds.Add(pair.Key);
                }
            }
            bodies.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            botIds.Sort(string.CompareOrdinal);
        }

        private void CollectArrows(long tick)
        {
            arrows.Clear();
            var shots = world.Shots;
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                //  봇이 보는 몸은 지난 틱 끝 모습이라 화살도 같은 때로 맞춘다.
                float age = Mathf.Max(0, tick - 1 - shot.FireTick) * tickInterval;
                arrows.Add(new BotArrow(shot.ShooterId,
                    ArcheryTrajectory.PositionAt(shot, age), ArcheryTrajectory.VelocityAt(shot, age)));
            }
        }

        private MountedArcheryBotBrain BrainFor(string entityId)
        {
            if (brains.TryGetValue(entityId, out var brain))
            {
                return brain;
            }
            //  봇마다 판 씨앗에서 따로 나눈 난수 — 같은 씨앗이면 같은 판이 다시 나온다.
            int number = BotNumber(state.Players[entityId].UserId);
            ulong mixed = seed ^ ((ulong)number * 0x9E3779B97F4A7C15UL);
            brain = new MountedArcheryBotBrain(config, new System.Random(unchecked((int)(mixed ^ (mixed >> 32)))));
            brains[entityId] = brain;
            return brain;
        }

        private static int BotNumber(string userId)
            => int.TryParse(userId.Substring(MountedArcheryBots.IdPrefix.Length), out int n) ? n : 0;
    }
}

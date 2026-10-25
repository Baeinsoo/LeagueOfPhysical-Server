using System.Collections.Generic;
using System.IO;
using GameFramework;
using GameFramework.World;
using Luban;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>봇 손발 — 두뇌의 답을 사람 입력과 같은 길로 넣고, 그 길로만 움직인다.</summary>
    public class MountedArcheryBotSystemTests
    {
        const float Dt = 0.02f;
        private const string MasterDataFolder = "Packages/com.baegames.lop.masterdata.server/Runtime.Generated/StreamingAssets/MasterData/";

        private sealed class NoopMotionBridge : IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(Entity entity) { }
            public void PushMotion(Entity entity) { }
        }

        private sealed class NeverHit : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
                Vector3 direction, float distance, int layerMask) => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit Raycast(Vector3 origin, Vector3 direction,
                float distance, int layerMask) => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => System.Array.Empty<GameFramework.Physics.CollisionHit>();
        }

        private sealed class FixedSeed : IMatchSeed
        {
            public ulong Value { get; set; } = 12345;
        }

        static readonly MountedArcheryConfig SmallConfig = new MountedArcheryConfig(
            150f, 40f, 80f, 2.5f, new MountSettings(13f, 9f, 16f, 172f, 92f, 100f, 0.35f, 0.5f, 3f),
            100, 34, 0.5f, 100f, 30f, null, 4f, 90f, 0.6f, 0.5f, 15f, 2.5f, 0.2f, 0.4f, 60f);

        //  LoadAsync는 EditMode에서 못 기다린다 — 패키지의 .bytes를 직접 읽어 실제 표를 만든다.
        private static MountedArcheryConfig RealConfig()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(MasterDataFolder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return new MountedArcheryConfigProvider(md).Get();
        }

        sealed class Fx
        {
            public readonly EntityRegistry Registry = new EntityRegistry();
            public readonly WorldEventBuffer Events = new WorldEventBuffer();
            public readonly MountedArcheryMatchState State = new MountedArcheryMatchState();
            public readonly InputBufferSystem Inputs = new InputBufferSystem();
            public readonly MountedArcheryConfig Config;
            public readonly MountedArcheryWorld World;
            public readonly ServerInputSystem ServerInput;
            public readonly MountedArcheryBotSystem Bots;
            public readonly MotionHistorySystem History;
            public readonly MountedArcheryHitSystem Hit;
            public readonly MountedArcheryZoneSystem Zone;
            public readonly List<string> Despawned = new List<string>();

            public Fx(MountedArcheryConfig config, ulong seed = 12345)
            {
                Config = config;
                World = new MountedArcheryWorld(Registry, Events,
                    new ArcheryAimSystem(config.ToBowConfig(), config.RiderEyeHeight, false),
                    new MountSystem(config.Mount),
                    new KinematicMoveSystem(new NeverHit(), 0),
                    new NoopMotionBridge(), Dt)
                {
                    GameplayStartTick = 0,
                };
                ServerInput = new ServerInputSystem(Registry, Inputs);
                Bots = new MountedArcheryBotSystem(Registry, World, State, config, Inputs,
                    new FixedSeed { Value = seed }, (a, b) => true, Dt);
                History = new MotionHistorySystem(World);
                //  디스폰은 서버처럼 레지스트리에서 뺀다 — 쓰러진 몸이 남으면 원·화살이 계속 잰다.
                Hit = new MountedArcheryHitSystem(World, Registry, State, config, new HealthSystem(), Events,
                    Despawn, (a, b) => null, Dt);
                Zone = new MountedArcheryZoneSystem(State, config, Registry, new HealthSystem(), World,
                    new System.Random(1), Despawn, Dt);
            }

            private void Despawn(string id)
            {
                Despawned.Add(id);
                Registry.Remove(id);
            }

            //  몸 모양(CapsuleShape)을 안 붙인다 — 땅이 없는 시험이라 키네마틱 이동을 건너뛰고 평지 이동(MoveOnFlatGround)으로 대신한다.
            public Entity Body(string entityId, string userId, Vector3 position, float heading)
            {
                var e = new Entity(entityId);
                e.Add(new GameFramework.World.Transform
                {
                    Position = position.ToNumerics(),
                    Rotation = Quaternion.Euler(0f, heading, 0f).ToNumerics(),
                });
                e.Add(new Velocity());
                e.Add(new ArcheryAim());
                e.Add(new InputBuffer());
                e.Add(new Simulated());
                e.Add(new Mount());
                e.Add(new Health(Config.MaxHp));
                e.Add(new Ownership(userId));
                Registry.Add(e);
                State.Add(entityId, userId, userId);
                return e;
            }

            /// <summary>사람·봇이 링 위에 가운데를 보고 선다(서버 규칙과 같은 배치).</summary>
            public void Ring(int bots, float radius = 30f)
            {
                for (int i = 0; i < bots; i++)
                {
                    float angle = i / (float)bots * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                    Body("e" + (i + 1), MountedArcheryBots.UserIdFor(i + 1), p,
                        MountedArcheryRuleSystem.YawFacingCenter(p));
                }
            }

            // 서버 한 틱과 같은 순서: Begin(봇) → 입력 확정 → 월드 → End(기록·판정·원).
            public void Step(long tick)
            {
                Bots.Tick(tick, Dt);
                ServerInput.Tick(tick, Dt);
                WorldTick(tick);
                Events.Clear();
                History.Tick(tick, Dt);
                Hit.Tick(tick, Dt);
                Zone.Tick(tick, Dt);
            }

            /// <summary>월드 한 틱 + 땅 대신 평지 이동. 탈것이 정한 수평 속도만큼 몸을 옮긴다(키네마틱 이동이 평지에서 하는 일).</summary>
            public void WorldTick(long tick)
            {
                World.Tick(tick, Dt);
                foreach (var e in Registry.All)
                {
                    var v = e.Get<Velocity>().Linear;
                    var t = e.Get<GameFramework.World.Transform>();
                    t.Position += new System.Numerics.Vector3(v.X, 0f, v.Z) * Dt;
                }
            }
        }

        [Test]
        public void 봇_입력은_그_틱에_소비된다()
        {
            var fx = new Fx(SmallConfig);
            var bot = fx.Body("e1", MountedArcheryBots.UserIdFor(1), new Vector3(0f, 0f, -20f), 0f);
            fx.Body("e2", MountedArcheryBots.UserIdFor(2), new Vector3(0f, 0f, 20f), 180f);
            var buffer = bot.Get<InputBuffer>();

            for (long tick = 1; tick <= 60; tick++)
            {
                fx.Bots.Tick(tick, Dt);
                fx.ServerInput.Tick(tick, Dt);

                Assert.IsNotNull(buffer.Current, $"{tick}틱 입력이 확정되지 않았다");
                Assert.AreEqual(tick, buffer.Current.ViewTick, "그 틱에 넣은 커맨드가 그 틱에 쓰여야 한다");
                Assert.AreEqual(tick, buffer.Current.SequenceNumber, "차례 번호는 틈 없이 오른다");
                Assert.AreEqual(0, buffer.PredictedTicks, "빈 틱을 메운 예측이 아니라 받은 커맨드여야 한다");
                Assert.AreEqual(0, buffer.Commands.Count, "버퍼에 쌓여 밀리지 않는다");

                fx.WorldTick(tick);
            }

            //  실제로 움직였다 — 위 단정이 아무것도 안 넣어도 통과하는 길(빈 커맨드)이 아니다.
            Assert.AreNotEqual(new Vector3(0f, 0f, -20f), bot.Get<GameFramework.World.Transform>().Position.ToUnity());
        }

        [Test]
        public void 사람_몸에는_입력을_넣지_않는다()
        {
            var fx = new Fx(SmallConfig);
            fx.Body("e1", MountedArcheryBots.UserIdFor(1), new Vector3(0f, 0f, -20f), 0f);
            var human = fx.Body("e2", "user-1", new Vector3(0f, 0f, 20f), 180f);

            fx.Bots.Tick(1, Dt);

            Assert.AreEqual(0, human.Get<InputBuffer>().Commands.Count);
            Assert.AreEqual(1, fx.Registry.Get("e1").Get<InputBuffer>().Commands.Count, "대조: 봇 몸에는 넣었다");
        }

        [Test]
        public void 봇은_Transform을_직접_쓰지_않는다()
        {
            var fx = new Fx(SmallConfig);
            fx.Ring(4);
            //  움직이는 중인 몸 — 두뇌가 속도를 0으로 돌려놓는 식의 직접 쓰기도 잡는다.
            var snapshot = new Dictionary<string, (System.Numerics.Vector3, System.Numerics.Quaternion, System.Numerics.Vector3)>();
            foreach (var e in fx.Registry.All)
            {
                e.Get<Velocity>().Linear = new System.Numerics.Vector3(1f, 0f, 2f);
                snapshot[e.Id] = (e.Get<GameFramework.World.Transform>().Position,
                                  e.Get<GameFramework.World.Transform>().Rotation,
                                  e.Get<Velocity>().Linear);
            }

            int enqueued = 0;
            for (long tick = 1; tick <= 120; tick++)
            {
                fx.Bots.Tick(tick, Dt);
                foreach (var e in fx.Registry.All)
                {
                    enqueued += e.Get<InputBuffer>().Commands.Count;
                    e.Get<InputBuffer>().Commands.Clear();
                }
            }

            Assert.AreEqual(4 * 120, enqueued, "대조: 봇 시스템은 매 틱 돌았다");
            foreach (var e in fx.Registry.All)
            {
                Assert.AreEqual(snapshot[e.Id].Item1, e.Get<GameFramework.World.Transform>().Position);
                Assert.AreEqual(snapshot[e.Id].Item2, e.Get<GameFramework.World.Transform>().Rotation);
                Assert.AreEqual(snapshot[e.Id].Item3, e.Get<Velocity>().Linear);
            }
        }

        [Test]
        public void 같은_씨앗이면_같은_판이다()
        {
            string Run(ulong seed)
            {
                var fx = new Fx(SmallConfig, seed);
                fx.Ring(4);
                for (long tick = 1; tick <= 400; tick++) fx.Step(tick);
                var text = new System.Text.StringBuilder();
                foreach (var e in fx.Registry.All)
                {
                    text.Append(e.Id).Append(e.Get<GameFramework.World.Transform>().Position).Append(';');
                }
                return text.ToString();
            }

            Assert.AreEqual(Run(7), Run(7));
            Assert.AreNotEqual(Run(7), Run(8), "대조: 씨앗이 다르면 다르게 움직인다");
        }

        [Test]
        public void 봇_8명_판은_혼자_끝난다()
        {
            var fx = new Fx(RealConfig());
            fx.Ring(8);

            long endTick = -1;
            for (long tick = 1; tick <= 9000; tick++)
            {
                fx.Step(tick);
                if (fx.State.AliveCount <= 1)
                {
                    endTick = tick;
                    break;
                }
            }

            int byArrow = 0;
            foreach (var player in fx.State.Players.Values)
            {
                if (player.Alive == false && string.IsNullOrEmpty(player.KillerEntityId) == false) byArrow++;
            }
            Debug.Log($"[BotMatch] 끝난 틱={endTick} ({endTick * Dt:F1}초) 화살 탈락={byArrow} 원 탈락={fx.State.Eliminations.Count - byArrow}");

            Assert.Greater(endTick, 0, "9000틱(180초) 안에 한 명 이하만 남아야 한다");
            Assert.Greater(byArrow, 0, "봇끼리 쏴서 쓰러뜨린 적이 있어야 한다 — 원만으로 끝나면 봇이 과녁처럼 서 있는 것이다");
        }

        [Test]
        public void 서로_못_보면_원이_끝낸다()
        {
            var fx = new Fx(RealConfig());
            fx.Ring(8);
            //  지형이 늘 가린다 — 아무도 못 쏜다. 그래도 원이 판을 끝내야 한다.
            var blind = new MountedArcheryBotSystem(fx.Registry, fx.World, fx.State, fx.Config, fx.Inputs,
                new FixedSeed(), (a, b) => false, Dt);

            long endTick = -1;
            for (long tick = 1; tick <= 9000; tick++)
            {
                blind.Tick(tick, Dt);
                fx.ServerInput.Tick(tick, Dt);
                fx.WorldTick(tick);
                fx.Events.Clear();
                fx.History.Tick(tick, Dt);
                fx.Hit.Tick(tick, Dt);
                fx.Zone.Tick(tick, Dt);
                if (fx.State.AliveCount <= 1)
                {
                    endTick = tick;
                    break;
                }
            }

            Debug.Log($"[BotMatch] 못 보는 판 끝난 틱={endTick} ({endTick * Dt:F1}초)");
            Assert.Greater(endTick, 0, "9000틱(180초) 안에 원이 끝내야 한다");
            foreach (var player in fx.State.Players.Values)
            {
                Assert.AreEqual("", player.KillerEntityId, "대조: 화살이 아니라 원이 끝냈다");
            }
        }
    }
}

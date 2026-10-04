using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeRefereeSystemTests
    {
        static readonly DodgeConfig C = new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);
        static readonly DodgeStageTable T = new DodgeStageTable(new[]
        {
            new DodgeStage("슬리퍼", 10f, new[] { DodgePatternKind.Ring }, 1f, 0f, 1.8f),
            new DodgeStage("수박", 10f, new[] { DodgePatternKind.Bomb }, 1f, 0f, 1.6f),
        });

        // 심판 캐릭터는 매 틱 동선 식(DodgeReferee.PoseAt) 그대로 선다 — 진행기가 쏘는 자리, 시뮬이 막는 자리와 같다.
        [Test]
        public void 심판은_동선_식_자리에_선다()
        {
            var world = new DodgeSimWorld { GameplayStartTick = 0 };
            var e = new GameFramework.World.Entity("ref");
            e.Add(new GameFramework.World.Transform());
            e.Add(new GameFramework.World.Velocity());
            e.Add(new GameFramework.World.GroundState());
            world.EntityRegistry.Add(e);
            var state = new DodgeMatchState { RefereeId = "ref" };
            var sys = new DodgeRefereeSystem(state, world, T, C);

            foreach (long tick in new long[] { 0, 60, 125, 300, 560 })
            {
                sys.Tick(tick, 0.02f);
                var want = DodgeReferee.PoseAt(tick, 0, T, C);
                var p = e.Get<GameFramework.World.Transform>().Position;
                Assert.AreEqual(want.Position.x, p.X, 1e-4f, "tick " + tick);
                Assert.AreEqual(want.Position.y, p.Z, 1e-4f, "tick " + tick);
                Assert.AreEqual(want.Velocity.y, e.Get<GameFramework.World.Velocity>().Linear.Z, 1e-4f, "tick " + tick);
                // 시뮬을 안 받으니 아무도 땅에 섰다고 안 써 준다 — 클라 달리기 애니는 "땅 위 + 속도"를 본다.
                Assert.IsTrue(e.Get<GameFramework.World.GroundState>().IsGrounded, "tick " + tick);
            }
        }
    }
}

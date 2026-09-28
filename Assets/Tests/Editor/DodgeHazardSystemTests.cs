using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeHazardSystemTests
    {
        const float DeltaTime = 0.02f;
        static readonly DodgeConfig C = new DodgeConfig(2, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, 0,
                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);

        GameFramework.World.EntityRegistry registry;
        DodgeMatchState state;
        List<string> eliminated;
        DodgeHazardSystem system;

        [SetUp]
        public void SetUp()
        {
            registry = new GameFramework.World.EntityRegistry();
            state = new DodgeMatchState();
            eliminated = new List<string>();
            system = new DodgeHazardSystem(state, registry, C, eliminated.Add);
            Add("1", Vector3.zero);
        }

        void Add(string id, Vector3 position)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(position.x, position.y, position.z) });
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new GameFramework.World.Simulated());
            registry.Add(e);
            state.Players[id] = new DodgePlayerLife { Lives = C.Lives };
        }

        // 원점에 선 사람 발밑에서 explodeTick에 터지는 폭탄.
        void BombAt(long explodeTick) =>
            state.Patterns.Add(new DodgePattern(state.Patterns.Count + 1, DodgePatternKind.Bomb,
                                                explodeTick - C.WarnTicks, 0, 0f, 0f, 2f, 0f));

        [Test]
        public void 첫_틱은_위치만_기억하고_판정하지_않는다()
        {
            BombAt(10);
            system.Tick(10, DeltaTime);
            Assert.AreEqual(C.Lives, state.Players["1"].Lives);
        }

        [Test]
        public void 맞으면_목숨이_하나_줄고_무적이_된다()
        {
            BombAt(11);
            int before = state.Version;
            system.Tick(10, DeltaTime);
            system.Tick(11, DeltaTime);
            Assert.AreEqual(C.Lives - 1, state.Players["1"].Lives);
            Assert.AreEqual(11 + C.InvulnerableTicks, state.Players["1"].InvulnerableUntilTick);
            Assert.Greater(state.Version, before);
        }

        [Test]
        public void 무적_중에는_또_맞아도_안_깎인다()
        {
            BombAt(11);
            BombAt(12);
            for (long t = 10; t <= 13; t++) system.Tick(t, DeltaTime);
            Assert.AreEqual(C.Lives - 1, state.Players["1"].Lives);
        }

        [Test]
        public void 한_틱에_두_패턴에_닿아도_한_번만_깎인다()
        {
            BombAt(11);
            BombAt(11);
            system.Tick(10, DeltaTime);
            system.Tick(11, DeltaTime);
            Assert.AreEqual(C.Lives - 1, state.Players["1"].Lives);
        }

        [Test]
        public void 목숨이_다_떨어지면_탈락하고_몸을_치운다()
        {
            BombAt(11);
            BombAt(11 + C.InvulnerableTicks);
            for (long t = 10; t <= 11 + C.InvulnerableTicks; t++) system.Tick(t, DeltaTime);
            Assert.IsFalse(state.Players["1"].Alive);
            Assert.AreEqual(11 + C.InvulnerableTicks, state.Players["1"].EliminatedTick);
            CollectionAssert.AreEqual(new[] { "1" }, eliminated);
            Assert.AreEqual(1, state.Eliminations.Count);
        }
    }
}

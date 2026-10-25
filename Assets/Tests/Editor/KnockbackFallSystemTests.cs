using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackFallSystemTests
    {
        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f);

        GameFramework.World.EntityRegistry registry;
        KnockbackMatchState state;
        List<string> despawned;
        KnockbackFallSystem system;

        [SetUp]
        public void SetUp()
        {
            registry = new GameFramework.World.EntityRegistry();
            state = new KnockbackMatchState();
            despawned = new List<string>();
            system = new KnockbackFallSystem(state, registry, C, despawned.Add);
            var e = new GameFramework.World.Entity("1");
            e.Add(new GameFramework.World.Transform());
            registry.Add(e);
            state.Players.Add("1");
        }

        void SetY(float y) =>
            registry.Get("1").Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(0, y, 0);

        [Test]
        public void 깊이보다_내려가면_탈락하고_사라진다()
        {
            SetY(-5.1f);
            system.Tick(42, 0.02f);
            Assert.AreEqual(42L, state.EliminatedTick["1"]);
            Assert.AreEqual(1, despawned.Count);
            Assert.AreEqual(42L, state.LastTick);
        }

        [Test]
        public void 깊이_위면_그대로()
        {
            SetY(-4.9f);
            system.Tick(42, 0.02f);
            Assert.IsFalse(state.EliminatedTick.ContainsKey("1"));
            Assert.AreEqual(0, despawned.Count);
        }

        [Test]
        public void 같은_몸은_한_번만()
        {
            SetY(-6f);
            system.Tick(42, 0.02f);
            system.Tick(43, 0.02f);
            Assert.AreEqual(1, despawned.Count);
            Assert.AreEqual(1, state.Eliminations.Count);
        }
    }
}

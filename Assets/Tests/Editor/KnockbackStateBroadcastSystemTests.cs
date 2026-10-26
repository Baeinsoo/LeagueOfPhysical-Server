using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackStateBroadcastSystemTests
    {
        sealed class FakeSession : GameFramework.ISession
        {
            public string sessionId { get; set; }
            public string userId { get; set; }
            public bool isConnected { get; set; } = true;
            public readonly List<GameFramework.IMessage> Sent = new List<GameFramework.IMessage>();
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage => Sent.Add(message);
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        sealed class FakeSessions : GameFramework.ISessionManager
        {
            public readonly List<GameFramework.ISession> All = new List<GameFramework.ISession>();
            public IReadOnlyCollection<GameFramework.ISession> GetAllSessions() => All;
            public void AddSession(GameFramework.ISession session) => All.Add(session);
            public void RemoveSession(GameFramework.ISession session) => All.Remove(session);
            public GameFramework.ISession GetSessionById(string sessionId) => throw new System.NotImplementedException();
            public GameFramework.ISession GetSessionByUserId(string userId) => throw new System.NotImplementedException();
            public T GetSessionById<T>(string sessionId) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public T GetSessionByUserId<T>(string userId) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public bool TryGetSessionById(string sessionId, out GameFramework.ISession session) => throw new System.NotImplementedException();
            public bool TryGetSessionByUserId<T>(string userId, out T session) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public void RemoveSessionById(string sessionId) => throw new System.NotImplementedException();
            public void RemoveSessionByUserId(string userId) => throw new System.NotImplementedException();
        }

        [Test]
        public void 판본이_바뀌면_다시_보낸다()
        {
            var state = new KnockbackMatchState();
            var registry = new GameFramework.World.EntityRegistry();
            var s = new FakeSession { sessionId = "s1" };
            var sessions = new FakeSessions(); sessions.All.Add(s);
            var system = new KnockbackStateBroadcastSystem(state, registry, sessions);

            system.Tick(1, 0.02f);
            Assert.AreEqual(0, s.Sent.Count);   // 판본 0 = 아직 알릴 것 없음

            state.MarkChanged();
            system.Tick(2, 0.02f);
            system.Tick(3, 0.02f);
            Assert.AreEqual(1, s.Sent.Count);

            state.MarkChanged();
            system.Tick(4, 0.02f);
            Assert.AreEqual(2, s.Sent.Count);
        }

        [Test]
        public void 피해와_탈락이_실린다()
        {
            var state = new KnockbackMatchState();
            var registry = new GameFramework.World.EntityRegistry();
            var e = new GameFramework.World.Entity("a");
            e.Add(new KnockbackBody { DamagePercent = 36 });
            registry.Add(e);
            state.Players.Add("a");
            state.Players.Add("b");
            state.Eliminate("b", 77);

            var wire = KnockbackStateBroadcastSystem.ToWire(state, registry);
            Assert.AreEqual(36, wire.Players[0].DamagePercent);
            Assert.AreEqual(-1L, wire.Players[0].EliminatedTick);
            Assert.AreEqual(77L, wire.Players[1].EliminatedTick);
        }
    }
}

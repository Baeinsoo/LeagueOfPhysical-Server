using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgeStateBroadcastSystemTests
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

        // 이 시험은 GetAllSessions만 쓴다 — 나머지는 인터페이스가 요구해서 채운다.
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
        public void 판본이_바뀔_때만_한_번씩_보낸다()
        {
            var state = new DodgeMatchState();
            var s = new FakeSession { sessionId = "s1" };
            var sessions = new FakeSessions(); sessions.All.Add(s);
            var system = new DodgeStateBroadcastSystem(state, sessions);

            state.MarkChanged();
            system.Tick(1, 0.02f);
            system.Tick(2, 0.02f);
            Assert.AreEqual(1, s.Sent.Count);

            state.MarkChanged();
            system.Tick(3, 0.02f);
            Assert.AreEqual(2, s.Sent.Count);
        }

        [Test]
        public void 다시_연결된_세션은_같은_판본도_다시_받는다()
        {
            var state = new DodgeMatchState();
            var s = new FakeSession { sessionId = "s1" };
            var sessions = new FakeSessions(); sessions.All.Add(s);
            var system = new DodgeStateBroadcastSystem(state, sessions);
            state.MarkChanged();
            system.Tick(1, 0.02f);
            s.isConnected = false;
            system.Tick(2, 0.02f);
            s.isConnected = true;
            system.Tick(3, 0.02f);
            Assert.AreEqual(2, s.Sent.Count);
        }

        [Test]
        public void 와이어에는_패턴과_목숨이_그대로_실린다()
        {
            var state = new DodgeMatchState();
            state.Patterns.Add(new DodgePattern(5, DodgePatternKind.Laser, 100, 9UL, 1f, 2f, 3f, 4f));
            state.Players["7"] = new DodgePlayerLife { Lives = 2, InvulnerableUntilTick = 50 };
            state.MarkChanged();
            var wire = DodgeStateBroadcastSystem.ToWire(state);
            Assert.AreEqual(state.Version, wire.Version);
            Assert.AreEqual(5, wire.Patterns[0].Id);
            Assert.AreEqual((int)DodgePatternKind.Laser, wire.Patterns[0].Kind);
            Assert.AreEqual(100, wire.Patterns[0].StartTick);
            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 4f }, wire.Patterns[0].P);
            Assert.AreEqual("7", wire.Players[0].EntityId);
            Assert.AreEqual(2, wire.Players[0].Lives);
            Assert.AreEqual(-1, wire.Players[0].EliminatedTick);
        }

        [Test]
        public void 예고_길이도_실어_보낸다()
        {
            var state = new DodgeMatchState();
            state.Patterns.Add(new DodgePattern(3, DodgePatternKind.Bomb, 100, 0, 1f, 2f, 2f, 0f, warnTicks: 42));
            state.MarkChanged();
            var wire = DodgeStateBroadcastSystem.ToWire(state);
            Assert.AreEqual(42, wire.Patterns[0].WarnTicks);
        }
    }
}

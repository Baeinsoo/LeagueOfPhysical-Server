using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LOP.Tests
{
    /// <summary>
    /// 결과가 정해진 사람이 나가겠다고 하면 로비에 그 사람 위치를 비워 달라고 하고 답한다.
    /// 그 밖에는 로비를 부르지 않고 거절한다 — 달리는 중에 빠져나가는 길이 생기면 안 된다.
    /// </summary>
    public class LeaveMatchMessageHandlerTests
    {
        private sealed class FakeSession : GameFramework.ISession
        {
            public string sessionId { get; set; } = "s1";
            public string userId { get; set; }
            public bool isConnected { get; set; } = true;
            public readonly List<GameFramework.IMessage> Sent = new List<GameFramework.IMessage>();
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage => Sent.Add(message);
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public MatchOutcome outcome { get; set; }
            public IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
            public void Clear() { }
        }

        private class FakeRule : IGameRuleSystem
        {
            public void Initialize() { }
            public void Deinitialize() { }
            public bool IsMatchOver => false;
            public long MatchDurationTicks => 0;
            public MatchOutcome ResolveOutcome() => null;
        }

        private sealed class FakeSettledRule : FakeRule, ISettledResults
        {
            public readonly HashSet<string> Settled = new HashSet<string>();
            public bool IsResultSettled(string userId) => Settled.Contains(userId);
        }

        private sealed class FakeReleaser : IUserLocationReleaser
        {
            public readonly List<(string userId, string roomId)> Calls = new List<(string, string)>();
            public bool Throw;

            public UniTask ReleaseAsync(string userId, string roomId)
            {
                Calls.Add((userId, roomId));
                if (Throw) throw new System.Exception("lobby down");
                return UniTask.CompletedTask;
            }
        }

        private static FakeRoomDataStore Store() => new FakeRoomDataStore
        {
            room = new Room { id = "room-1" },
            match = new Match { playerList = new[] { "u1", "u2" } },
        };

        private static bool Reply(FakeSession session)
        {
            Assert.AreEqual(1, session.Sent.Count, "답은 한 번");
            Assert.IsInstanceOf<LeaveMatchToC>(session.Sent[0]);
            return ((LeaveMatchToC)session.Sent[0]).Accepted;
        }

        private static void Handle(LeaveMatchMessageHandler handler, FakeSession session) =>
            handler.HandleAsync(session).GetAwaiter().GetResult();

        [Test]
        public void 결과가_정해진_참가자는_로비에서_위치를_비우고_수락한다()
        {
            var rule = new FakeSettledRule();
            rule.Settled.Add("u1");
            var releaser = new FakeReleaser();
            var session = new FakeSession { userId = "u1" };

            Handle(new LeaveMatchMessageHandler(null, Store(), rule, releaser), session);

            CollectionAssert.AreEqual(new[] { ("u1", "room-1") }, releaser.Calls);
            Assert.IsTrue(Reply(session));
        }

        [Test]
        public void 결과가_안_정해졌으면_로비를_부르지_않고_거절한다()
        {
            var releaser = new FakeReleaser();
            var session = new FakeSession { userId = "u1" };

            Handle(new LeaveMatchMessageHandler(null, Store(), new FakeSettledRule(), releaser), session);

            Assert.IsEmpty(releaser.Calls);
            Assert.IsFalse(Reply(session));
        }

        [Test]
        public void 참가자가_아니면_거절한다()
        {
            var rule = new FakeSettledRule();
            rule.Settled.Add("stranger");
            var releaser = new FakeReleaser();
            var session = new FakeSession { userId = "stranger" };

            Handle(new LeaveMatchMessageHandler(null, Store(), rule, releaser), session);

            Assert.IsEmpty(releaser.Calls);
            Assert.IsFalse(Reply(session));
        }

        [Test]
        public void 결과가_끝까지_안_정해지는_종목이면_거절한다()
        {
            var releaser = new FakeReleaser();
            var session = new FakeSession { userId = "u1" };

            Handle(new LeaveMatchMessageHandler(null, Store(), new FakeRule(), releaser), session);

            Assert.IsEmpty(releaser.Calls);
            Assert.IsFalse(Reply(session));
        }

        [Test]
        public void 로비_호출이_실패하면_거절로_답한다()
        {
            var rule = new FakeSettledRule();
            rule.Settled.Add("u1");
            var releaser = new FakeReleaser { Throw = true };
            var session = new FakeSession { userId = "u1" };
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("로비 위치 비우기 실패"));

            Handle(new LeaveMatchMessageHandler(null, Store(), rule, releaser), session);

            Assert.AreEqual(1, releaser.Calls.Count);
            Assert.IsFalse(Reply(session));
        }
    }
}

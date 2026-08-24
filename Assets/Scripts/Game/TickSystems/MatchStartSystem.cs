using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 언제 출발할지를 정해 전원에게 알리고, 확정된 출발틱을 월드에 꽂는다.
    /// 판단은 전부 <see cref="MatchStartGate"/>(순수 C#)에 있고 여기는 배선만 한다.
    /// </summary>
    public class MatchStartSystem : MessageHandlerBase, ITickSystem
    {
        //  50Hz 기준. 카운트다운 3초는 카트라이더·로켓리그 관례.
        private const long CountdownTicks = 150;
        //  실서비스: 모바일 콜드 로딩 + 맵 로드를 덮는 30초.
        private const long WaitCapTicks = 1500;
        //  로컬(에디터): 사람이 손으로 에디터 셋을 켜는 시간. 이 한 줄이 2인 검증 리그를 세운다.
        private const long StandaloneWaitCapTicks = 30000;

        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.IWorld world;
        private readonly ISubscriber<ClientMessage<MatchReadyToS>> readySubscriber;

        private readonly List<ClientMessage<MatchReadyToS>> received = new List<ClientMessage<MatchReadyToS>>();

        private MatchStartGate gate;
        private long lastBroadcastStartTick = long.MinValue;
        private int lastBroadcastReadyCount = -1;

        public MatchStartSystem(
            IRoomDataStore roomDataStore,
            ISessionManager sessionManager,
            GameFramework.World.IWorld world,
            ISubscriber<ClientMessage<MatchReadyToS>> readySubscriber)
        {
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.world = world;
            this.readySubscriber = readySubscriber;
        }

        public MatchPhase Phase => gate?.Phase ?? MatchPhase.WaitingForPlayers;
        public long StartTick => gate?.StartTick ?? long.MaxValue;

        protected override void Subscribe()
        {
            int expected = roomDataStore.match?.playerList?.Length ?? 0;
            long cap = EnvironmentSettings.active.Standalone ? StandaloneWaitCapTicks : WaitCapTicks;
            gate = new MatchStartGate(expected, cap, CountdownTicks);

            Track(readySubscriber.Subscribe(OnMatchReadyToS));
        }

        private void OnMatchReadyToS(ClientMessage<MatchReadyToS> message) => received.Add(message);

        public void Tick(long tick, float deltaTime)
        {
            for (int i = 0; i < received.Count; i++)
            {
                gate.MarkReady(received[i].Session.userId);
            }
            received.Clear();

            gate.Tick(tick);

            //  출발틱은 확정되면 안 바뀌지만, 준비 인원은 대기 중에 늘어난다("2/4" 표시).
            if (gate.StartTick == lastBroadcastStartTick && gate.ReadyCount == lastBroadcastReadyCount)
            {
                return;
            }

            lastBroadcastStartTick = gate.StartTick;
            lastBroadcastReadyCount = gate.ReadyCount;

            world.GameplayStartTick = gate.StartTick;

            foreach (var session in sessionManager.GetAllSessions())
            {
                //  놓치면 출발을 영영 모른다 — reliable로 보낸다.
                session.Send(BuildMessage());
            }
        }

        /// <summary>현재 게이트 상태로 MatchStartToC 한 장을 만든다.</summary>
        public MatchStartToC BuildMessage() => new MatchStartToC
        {
            //  와이어에서는 "미정"을 -1로 쓴다. long.MaxValue를 그대로 실으면 클라가 그 값을
            //  틱과 빼면서 넘침이 난다.
            StartTick = gate.StartTick == long.MaxValue ? -1 : gate.StartTick,
            ReadyCount = gate.ReadyCount,
            TotalCount = gate.ExpectedPlayers,
        };

        /// <summary>매치가 끝났음을 알린다. Task 5가 재접속·중도이탈 판정 등에 사용한다.</summary>
        public void Finish() => gate.Finish();
    }
}

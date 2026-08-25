using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 진행(서버). End 페이즈에서 돈다 — 물리가 돈 뒤·스냅샷 송신 전이라
    /// "이번 틱 결과를 보고 턴을 정한 뒤 그 상태를 같이 보낸다"가 한 틱 안에 끝난다.
    /// </summary>
    public class PanchigiTurnSystem : ITickSystem
    {
        private readonly ITickUpdater tickUpdater;
        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly PanchigiBoard board;

        private PanchigiTurn turn;
        private IReadOnlyList<string> coinIds;
        private readonly Dictionary<string, string> userToEntity = new();

        private int restTicks;
        private long aimDeadlineTick;
        private long lastDeadlineTurnCount = -1;   // 마감을 이미 정한 턴인지 — TurnCount는 조준 진입마다 반드시 바뀐다
        private PanchigiPhase sentPhase = PanchigiPhase.Over;   // 첫 틱에 반드시 한 번 보내도록
        private string sentEntityId;

        //  이번 상태(phase+차례)를 이미 받은 세션 id들. 늦게 접속하거나 재접속한 세션은 여기 없으니
        //  다음 틱에 현재 상태를 받는다 — "바뀔 때만 보낸다"가 "0명한테 보내고 끝"이 되지 않게 한다.
        private readonly HashSet<string> receivedSessionIds = new();

        public bool IsOver => turn != null && turn.Phase == PanchigiPhase.Over;
        public string WinnerEntityId => turn?.WinnerEntityId;

        public PanchigiTurnSystem(ITickUpdater tickUpdater, IRoomDataStore roomDataStore, ISessionManager sessionManager,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData,
            PanchigiBoard board)
        {
            this.tickUpdater = tickUpdater;
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
            this.board = board;
        }

        public void Begin(IReadOnlyList<string> playerEntityIds, IReadOnlyList<string> coinEntityIds)
        {
            coinIds = coinEntityIds;

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            turn = new PanchigiTurn(playerEntityIds, config != null ? config.MatchTurnLimit : 60);

            //  차례는 엔티티로 돌지만 타격은 userId로 온다 — 한 번만 이어 둔다.
            string[] playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length && i < playerEntityIds.Count; i++)
            {
                userToEntity[playerList[i]] = playerEntityIds[i];
            }
        }

        public bool CanStrike(string userId)
        {
            return turn != null
                && turn.Phase == PanchigiPhase.Aiming
                && userToEntity.TryGetValue(userId, out string entityId)
                && entityId == turn.CurrentEntityId;
        }

        public void NotifyStruck(string userId)
        {
            if (userToEntity.TryGetValue(userId, out string entityId))
            {
                turn?.OnStruck(entityId);
            }
        }

        public void Tick(long tick, float deltaTime)
        {
            if (turn == null || turn.Phase == PanchigiPhase.Over)
            {
                return;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                return;
            }

            if (turn.Phase == PanchigiPhase.Settling)
            {
                TickSettling(config);
            }
            else if (tick >= aimDeadlineTick)
            {
                turn.OnAimTimeout();
            }

            if (turn.Phase == PanchigiPhase.Aiming)
            {
                RefreshAimDeadlineIfNewTurn(tick, config);
            }

            BroadcastIfChanged();
        }

        /// <summary>
        /// 조준 마감은 "이번 조준에 들어선 순간" 한 번만 정한다. 방송 여부(대역폭 최적화)와 묶으면
        /// 안 된다 — 같은 사람이 연달아 차례를 받는 경우(예: 남은 사람이 1명) CurrentEntityId가 안
        /// 바뀌어 방송이 스킵되고, 그러면 마감도 영영 안 갱신돼 매 틱 타임아웃이 도는 사고가 난다.
        /// TurnCount는 조준 진입마다(패스든 타격이든) 반드시 바뀌므로 이걸로 "새 턴인지"를 본다.
        /// </summary>
        private void RefreshAimDeadlineIfNewTurn(long tick, LOP.MasterData.PanchigiConfig config)
        {
            if (turn.TurnCount == lastDeadlineTurnCount)
            {
                return;
            }

            lastDeadlineTurnCount = turn.TurnCount;

            double interval = tickUpdater?.interval ?? 0;
            long window = interval > 0 ? (long)(config.AimTimeoutSec / interval) : 0;
            aimDeadlineTick = tick + window;
        }

        private void TickSettling(LOP.MasterData.PanchigiConfig config)
        {
            if (AllAtRest(config) == false)
            {
                restTicks = 0;
                return;
            }

            if (++restTicks < config.RestTicks)
            {
                return;
            }

            restTicks = 0;
            ReturnOutOfBoardCoins();
            turn.OnRested(AllFlipped());
        }

        private bool AllAtRest(LOP.MasterData.PanchigiConfig config)
        {
            Bounds bounds = board.Bounds;
            foreach (string id in coinIds)
            {
                var body = entityRegistry.Get(id)?.Get<GameFramework.World.PhysicsBody>();
                if (body == null) { continue; }

                //  판 밖으로 떨어져 자유낙하하는 동전은 속도가 계속 커져 영영 안 멎는다. 그걸
                //  기다리면 "안 멎어서 복귀 못 하고, 복귀 못 해서 안 멎는" 교착에 빠진다 — 어차피
                //  다음 단계(ReturnOutOfBoardCoins)가 자리로 되돌리니 멎은 것으로 쳐도 안전하다.
                if (PanchigiCoin.IsOutOfBoard(body.GetPosition(), bounds))
                {
                    continue;
                }

                if (PanchigiCoin.IsAtRest(body.GetVelocity(), body.GetAngularVelocity(),
                        config.RestSpeedEpsilon, config.RestAngularEpsilon) == false)
                {
                    return false;
                }
            }
            return true;
        }

        private bool AllFlipped()
        {
            foreach (string id in coinIds)
            {
                var body = entityRegistry.Get(id)?.Get<GameFramework.World.PhysicsBody>();
                if (body == null) { continue; }

                if (PanchigiCoin.IsFlipped(body.GetRotation()) == false)
                {
                    return false;
                }
            }
            return true;
        }

        private void ReturnOutOfBoardCoins()
        {
            var setup = masterData.Tables.TbPanchigiSetup.GetOrDefault(roomDataStore.match.playerList.Length);
            if (setup == null || board.TryGetSlots(setup.Formation, out IReadOnlyList<Transform> slots) == false)
            {
                return;
            }

            Bounds bounds = board.Bounds;
            for (int i = 0; i < coinIds.Count && i < slots.Count; i++)
            {
                var body = entityRegistry.Get(coinIds[i])?.Get<GameFramework.World.PhysicsBody>();
                if (body == null || PanchigiCoin.IsOutOfBoard(body.GetPosition(), bounds) == false)
                {
                    continue;
                }

                //  동전은 dynamic이라 PhysX가 진실원본이다 — World.Transform에 쓰면 다음 틱에 덮어써진다.
                //  자세는 자리의 회전이 아니라 시작 면(+up)으로 되돌린다 — 스폰과 같은 규칙이어야
                //  "초기 세팅으로 복귀"가 성립한다.
                body.SetPosition(slots[i].position.ToNumerics());
                body.SetRotation(System.Numerics.Quaternion.Identity);
                body.SetVelocity(System.Numerics.Vector3.Zero);
                body.SetAngularVelocity(System.Numerics.Vector3.Zero);
            }
        }

        /// <summary>
        /// 현재 턴 상태를, 아직 못 받은 연결 세션에게만 보낸다. 상태(국면·차례)가 바뀌면 "받은 세션"
        /// 집합을 비워 전원에게 다시 돌린다 — 룸이 뜨자마자 시작해 아무도 안 붙어 있을 때 보낸 상태는
        /// 그렇게 0명에게 갔다 사라지지 않고, 나중에 접속(또는 재접속)한 세션도 다음 틱에 반드시
        /// 현재 상태를 받는다.
        /// </summary>
        private void BroadcastIfChanged()
        {
            if (turn.Phase == PanchigiPhase.Over)
            {
                return;   // 종료는 기존 매치 종료 경로가 알린다
            }

            if (turn.Phase != sentPhase || turn.CurrentEntityId != sentEntityId)
            {
                sentPhase = turn.Phase;
                sentEntityId = turn.CurrentEntityId;
                receivedSessionIds.Clear();
            }

            var message = new PanchigiStateToC
            {
                Phase = turn.Phase == PanchigiPhase.Aiming ? 1 : 0,
                CurrentEntityId = turn.CurrentEntityId ?? string.Empty,
                AimDeadlineTick = aimDeadlineTick,
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    //  끊긴 세션은 "받은 적 없음"으로 되돌린다. 재접속은 같은 sessionId를 그대로
                    //  다시 쓰기 때문(LOPRoom.OnPlayerEnter가 세션 객체를 재사용한다), 지워 두지
                    //  않으면 돌아온 플레이어가 자기 차례를 통째로 놓친다. 방을 떠난 세션의 id가
                    //  쌓이는 것도 같이 막는다.
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }

                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }

                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }
    }
}

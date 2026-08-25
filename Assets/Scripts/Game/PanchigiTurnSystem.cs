using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 진행(서버). End 페이즈에서 돈다 — 물리가 돈 뒤·스냅샷 송신 전이라
    /// "이번 틱 결과를 보고 턴을 정한 뒤 그 상태를 같이 보낸다"가 한 틱 안에 끝난다.
    /// </summary>
    public class PanchigiTurnSystem : ITickSystem
    {
        private readonly IRunner runner;
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
        private PanchigiPhase sentPhase = PanchigiPhase.Over;   // 첫 틱에 반드시 한 번 보내도록
        private string sentEntityId;

        public bool IsOver => turn != null && turn.Phase == PanchigiPhase.Over;
        public string WinnerEntityId => turn?.WinnerEntityId;

        public PanchigiTurnSystem(IRunner runner, IRoomDataStore roomDataStore, ISessionManager sessionManager,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData,
            PanchigiBoard board)
        {
            this.runner = runner;
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
            this.board = board;

            runner.RegisterSystem<End>(this);
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

            BroadcastIfChanged(tick, config);
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
            foreach (string id in coinIds)
            {
                var body = entityRegistry.Get(id)?.Get<GameFramework.World.PhysicsBody>();
                if (body == null) { continue; }

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

        private void BroadcastIfChanged(long tick, LOP.MasterData.PanchigiConfig config)
        {
            if (turn.Phase == PanchigiPhase.Over)
            {
                return;   // 종료는 기존 매치 종료 경로가 알린다
            }

            if (turn.Phase == sentPhase && turn.CurrentEntityId == sentEntityId)
            {
                return;
            }

            if (turn.Phase == PanchigiPhase.Aiming)
            {
                double interval = runner.tickUpdater?.interval ?? 0;
                long window = interval > 0 ? (long)(config.AimTimeoutSec / interval) : 0;
                aimDeadlineTick = tick + window;
            }

            sentPhase = turn.Phase;
            sentEntityId = turn.CurrentEntityId;

            var message = new PanchigiStateToC
            {
                Phase = turn.Phase == PanchigiPhase.Aiming ? 1 : 0,
                CurrentEntityId = turn.CurrentEntityId ?? string.Empty,
                AimDeadlineTick = aimDeadlineTick,
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(message);
            }
        }
    }
}

using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 타격(서버). 클라가 판을 끌어 놓으면 한 통 온다 — 검증한 뒤 동전마다 "판에 닿은 정도"를
    /// 재서 임펄스를 준다. 굴리는 것은 우리 시뮬이 아니라 유니티 물리이고, 결과는
    /// PhysicsSimulationSystem이 World로 되읽어 스냅샷에 실린다.
    /// </summary>
    public class PanchigiStrikeMessageHandler : MessageHandlerBase
    {
        //  판·동전만 본다. 판 밖 지형이나 트리거에 걸리면 판정이 엉킨다.
        private static readonly int StrikeLayerMask = LayerMask.GetMask("Default", "Character");

        //  샘플은 동전 바로 위에서 아래로 쏜다 — 발자국 위에 무엇이 얹혀 있는지 보려는 것이라
        //  동전 두께보다 넉넉히 위에서 시작해 판까지 닿을 만큼만 간다.
        private const float SampleRayHeight = 1f;
        private const float SampleRayDistance = 2f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Physics.ICollisionQuery collisionQuery;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly IRoomDataStore roomDataStore;
        private readonly ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber;

        private Bounds boardBounds;
        private bool boardFound;

        public PanchigiStrikeMessageHandler(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Physics.ICollisionQuery collisionQuery,
            LOP.MasterData.LOPMasterData masterData,
            IRoomDataStore roomDataStore,
            ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber)
        {
            this.entityRegistry = entityRegistry;
            this.collisionQuery = collisionQuery;
            this.masterData = masterData;
            this.roomDataStore = roomDataStore;
            this.strikeSubscriber = strikeSubscriber;
        }

        protected override void Subscribe() => Track(strikeSubscriber.Subscribe(OnStrike));

        private void OnStrike(ClientMessage<PanchigiStrikeToS> received)
        {
            if (TryGetBoardBounds(out Bounds board) == false)
            {
                Debug.LogWarning("[Panchigi] 판을 찾지 못했다 — 타격을 버린다.");
                return;
            }

            var config = masterData.Tables.TbPanchigiConfig.GetOrDefault(1);
            if (config == null)
            {
                Debug.LogWarning("[Panchigi] TbPanchigiConfig(1)이 없다 — 타격을 버린다.");
                return;
            }

            string userId = received.Session.userId;
            if (IsParticipant(userId) == false)
            {
                Debug.LogWarning($"[Panchigi] 참가자가 아닌 타격 — {userId}");
                return;
            }

            PanchigiStrikeToS message = received.Message;
            Vector3 strikePoint = MapperConfig.mapper.Map<Vector3>(message.StrikePoint);
            Vector3 dragDelta = MapperConfig.mapper.Map<Vector3>(message.DragDelta);

            //  클라가 이미 상한을 걸어 보내지만 믿지 않는다. 클램프가 아니라 거절이다 —
            //  클램프하면 조작된 값이 조용히 게임에 들어오고 로그도 안 남는다.
            if (ContainsXZ(board, strikePoint) == false)
            {
                Debug.LogWarning($"[Panchigi] 판 밖 타격점 {strikePoint} — {userId}");
                return;
            }
            if (message.HoldTime < 0f || message.HoldTime > config.HoldTimeMax)
            {
                Debug.LogWarning($"[Panchigi] 누른 시간 범위 밖 {message.HoldTime} — {userId}");
                return;
            }
            if (dragDelta.magnitude > config.StrikePowerMax)
            {
                Debug.LogWarning($"[Panchigi] 세기 범위 밖 {dragDelta.magnitude} — {userId}");
                return;
            }

            ApplyStrike(strikePoint, dragDelta, message.HoldTime, board, config);
        }

        private void ApplyStrike(Vector3 strikePoint, Vector3 dragDelta, float holdTime,
            Bounds board, LOP.MasterData.PanchigiConfig config)
        {
            var input = new StrikeInput(strikePoint.ToNumerics(), dragDelta.ToNumerics(), holdTime);
            var tuning = new StrikeTuning(
                config.ForceMultiplier, config.HorizontalForceMultiplier, config.FalloffRate);

            int sampleCount = config.CoverageSamples;
            var samples = new System.Numerics.Vector3[sampleCount];
            var live = new System.Numerics.Vector3[sampleCount];

            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                var disc = entity.Get<GameFramework.World.DiscShape>();
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                var transform = entity.Get<GameFramework.World.Transform>();
                if (disc == null || body == null || transform == null)
                {
                    continue;   // 동전이 아니다
                }

                PanchigiStrikeKernel.BuildSamples(transform.Position, disc.Radius, samples);

                int liveCount = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    Vector3 sample = samples[i].ToUnity();
                    if (ContainsXZ(board, sample) == false)
                    {
                        continue;   // 판 끄트머리 밖으로 삐져나온 부분
                    }

                    //  이 자리 위에 실제로 놓인 것이 이 동전인지 본다. 다른 동전이 먼저 맞으면
                    //  이 동전은 그 위에 얹혀 있다는 뜻이라 판에서 힘을 받지 못한다.
                    Vector3 origin = new Vector3(sample.x, sample.y + SampleRayHeight, sample.z);
                    GameFramework.Physics.CollisionHit hit =
                        collisionQuery.Raycast(origin, Vector3.down, SampleRayDistance, StrikeLayerMask);
                    if (hit.GetEntityId() != entity.Id)
                    {
                        continue;
                    }

                    live[liveCount++] = samples[i];
                }

                System.Numerics.Vector3 impulse =
                    PanchigiStrikeKernel.ComputeImpulse(input, tuning, live, liveCount, sampleCount);
                if (impulse == System.Numerics.Vector3.Zero)
                {
                    continue;
                }

                body.AddImpulseAtPosition(impulse, strikePoint.ToNumerics());
            }
        }

        private bool IsParticipant(string userId)
        {
            foreach (string participant in roomDataStore.match.playerList)
            {
                if (participant == userId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetBoardBounds(out Bounds bounds)
        {
            if (boardFound)
            {
                bounds = boardBounds;
                return true;
            }

            GameObject board = GameObject.Find("Board");
            Collider collider = board != null ? board.GetComponent<Collider>() : null;
            if (collider == null)
            {
                bounds = default;
                return false;
            }

            boardBounds = collider.bounds;
            boardFound = true;
            bounds = boardBounds;
            return true;
        }

        //  판은 평면이라 높이는 보지 않는다 — 위아래로 얼마나 떨어져 있든 "판 위"다.
        private static bool ContainsXZ(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x && point.x <= bounds.max.x
            && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }
}

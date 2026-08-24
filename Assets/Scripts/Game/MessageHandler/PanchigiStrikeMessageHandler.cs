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
        //  static 필드 초기화자에서 LayerMask.GetMask를 부르는 건 Unity가 MonoBehaviour에서
        //  금지하는 패턴이다 — 이 클래스는 MonoBehaviour가 아니라 지금 당장 문제는 없지만,
        //  그대로 두면 다음에 누가 그대로 베껴 MonoBehaviour에 옮길 위험이 있어 생성자로 옮긴다.
        private readonly int StrikeLayerMask;

        //  샘플 자리에서 바로 아래로 짧게 쏜다 — "판이 내 바로 밑에 있나"를 묻는 것이지
        //  "저 아래 어딘가에 판이 있나"가 아니다. 길게 쏘면 얹혀 있는 동전도 통과한다.
        private const float SampleRayDistance = 0.1f;

        //  클라가 상한에 맞춰 자른 값이라도 성분에서 크기를 다시 재면 미세하게 커질 수 있다
        //  (ClampMagnitude는 성분을 다시 계산한다). 정직한 클라가 경계에서 거절당하지 않게 봐준다.
        private const float BoundEpsilon = 0.001f;

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
            StrikeLayerMask = LayerMask.GetMask("Default", "Character");
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
            if (message.HoldTime < -BoundEpsilon || message.HoldTime > config.HoldTimeMax + BoundEpsilon)
            {
                Debug.LogWarning($"[Panchigi] 누른 시간 범위 밖 {message.HoldTime} — {userId}");
                return;
            }
            if (dragDelta.magnitude > config.StrikePowerMax + BoundEpsilon)
            {
                Debug.LogWarning($"[Panchigi] 세기 범위 밖 {dragDelta.magnitude} — {userId}");
                return;
            }
            if (config.CoverageSamples <= 0)
            {
                Debug.LogWarning($"[Panchigi] TbPanchigiConfig의 CoverageSamples가 {config.CoverageSamples}다 — 타격을 버린다.");
                return;
            }

            ApplyStrike(strikePoint, dragDelta, message.HoldTime, board, config);
        }

        private void ApplyStrike(Vector3 strikePoint, Vector3 dragDelta, float holdTime,
            Bounds board, LOP.MasterData.PanchigiConfig config)
        {
            //  끌지도 누르지도 않은 빈 탭 — 어차피 힘이 0이다. 동전마다 K번 쏘는 스윕을 아낀다.
            if (dragDelta == Vector3.zero && holdTime == 0f)
            {
                return;
            }

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

                    //  이 자리에서 내가 판에 닿아 있나 — 다른 동전이 먼저 걸리면 그 위에 얹혀
                    //  있다는 뜻이고, 그러면 판에서 힘을 받지 못한다. 자기 자신은 레이가 콜라이더
                    //  안에서 출발하므로 PhysX가 알아서 건너뛴다.
                    GameFramework.Physics.CollisionHit hit =
                        collisionQuery.Raycast(sample, Vector3.down, SampleRayDistance, StrikeLayerMask);
                    if (hit.HasHit == false || hit.GetEntityId() != null)
                    {
                        continue;   // 아무것도 없거나(허공) 엔티티가 먼저 걸렸다(포개짐)
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
        //  가장자리를 정확히 친 값도 반올림으로 밖에 떨어질 수 있어 BoundEpsilon만큼 넉넉히 본다.
        private static bool ContainsXZ(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x - BoundEpsilon && point.x <= bounds.max.x + BoundEpsilon
            && point.z >= bounds.min.z - BoundEpsilon && point.z <= bounds.max.z + BoundEpsilon;
    }
}

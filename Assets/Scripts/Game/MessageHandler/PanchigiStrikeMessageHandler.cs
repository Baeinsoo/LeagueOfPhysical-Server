using System.Collections.Generic;
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

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Physics.ICollisionQuery collisionQuery;
        private readonly LOP.MasterData.LOPMasterData masterData;
        private readonly IRoomDataStore roomDataStore;
        private readonly ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber;
        private readonly PanchigiBoardLocator boardLocator;
        private readonly PanchigiTurnSystem turnSystem;

        public PanchigiStrikeMessageHandler(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Physics.ICollisionQuery collisionQuery,
            LOP.MasterData.LOPMasterData masterData,
            IRoomDataStore roomDataStore,
            ISubscriber<ClientMessage<PanchigiStrikeToS>> strikeSubscriber,
            PanchigiBoardLocator boardLocator,
            PanchigiTurnSystem turnSystem)
        {
            this.entityRegistry = entityRegistry;
            this.collisionQuery = collisionQuery;
            this.masterData = masterData;
            this.roomDataStore = roomDataStore;
            this.strikeSubscriber = strikeSubscriber;
            this.boardLocator = boardLocator;
            this.turnSystem = turnSystem;
            StrikeLayerMask = LayerMask.GetMask("Default", "Character");
        }

        protected override void Subscribe() => Track(strikeSubscriber.Subscribe(OnStrike));

        private void OnStrike(ClientMessage<PanchigiStrikeToS> received)
        {
            if (boardLocator.Board == null)
            {
                Debug.LogWarning("[Panchigi] 판을 찾지 못했다 — 타격을 버린다.");
                return;
            }

            Bounds boardBounds = boardLocator.Board.Bounds;

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
            if (turnSystem.CanStrike(userId) == false)
            {
                Debug.LogWarning($"[Panchigi] 차례가 아닌 타격 — {userId}");
                return;
            }

            PanchigiStrikeToS message = received.Message;

            //  개수부터 본다 — Validate가 상한을 검사하긴 하지만, 그건 전부 매핑한 *뒤*다.
            //  조작된 클라가 접촉점을 아주 많이 보내면 거절하기 전에 그 개수만큼 매핑이 돈다.
            if (message.Contacts.Count > config.ContactMax)
            {
                Debug.LogWarning($"[Panchigi] 타격 거절 — 접촉점이 상한을 넘었다 {message.Contacts.Count} > {config.ContactMax} — {userId}");
                return;
            }

            var contacts = new List<PanchigiStrikeValidation.Contact>(message.Contacts.Count);
            foreach (PanchigiStrikeContact wire in message.Contacts)
            {
                contacts.Add(new PanchigiStrikeValidation.Contact(
                    MapperConfig.mapper.Map<Vector3>(wire.StrikePoint),
                    MapperConfig.mapper.Map<Vector3>(wire.DragDelta),
                    wire.HoldTime));
            }

            //  클라가 이미 상한을 걸어 보내지만 믿지 않는다. 클램프가 아니라 거절이다 —
            //  클램프하면 조작된 값이 조용히 게임에 들어오고 로그도 안 남는다.
            if (PanchigiStrikeValidation.Validate(contacts, boardBounds,
                    config.HoldTimeMax, config.StrikePowerMax, config.ContactMax, out string reason) == false)
            {
                Debug.LogWarning($"[Panchigi] 타격 거절 — {reason} — {userId}");
                return;
            }
            if (config.CoverageSamples <= 0)
            {
                Debug.LogWarning($"[Panchigi] TbPanchigiConfig의 CoverageSamples가 {config.CoverageSamples}다 — 타격을 버린다.");
                return;
            }

            //  접촉점마다 같은 커널을 돌려 임펄스를 누적한다 — 손가락 수와 간격이 결과를 바꾸는 건
            //  힘 모델이 달라서가 아니라, 힘이 각자 자리에서 여러 번 들어가기 때문이다.
            for (int i = 0; i < contacts.Count; i++)
            {
                ApplyStrike(contacts[i].StrikePoint, contacts[i].DragDelta, contacts[i].HoldTime,
                    boardBounds, config);
            }
            turnSystem.NotifyStruck(userId);
        }

        private void ApplyStrike(Vector3 strikePoint, Vector3 dragDelta, float holdTime,
            Bounds boardBounds, LOP.MasterData.PanchigiConfig config)
        {
            //  끌지도 누르지도 않은 빈 탭 — 어차피 힘이 0이다. 동전마다 K번 쏘는 스윕을 아낀다.
            if (dragDelta == Vector3.zero && holdTime == 0f)
            {
                return;
            }

            var input = new PanchigiStrike.StrikeInput(strikePoint.ToNumerics(), dragDelta.ToNumerics(), holdTime);
            var tuning = new PanchigiStrike.StrikeTuning(
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

                PanchigiStrike.BuildSamples(transform.Position, disc.Radius, samples);

                //  판에 닿아 있다면 중심이 판 위로 몸의 대각 절반보다 높이 뜰 수는 없다 —
                //  납작하게 누웠든 모로 섰든 이 거리 안에 판이 있어야 "닿아 있다"가 성립한다.
                //  고정값을 쓰면 모로 선 동전이 영영 타격에 반응하지 않는다.
                float reach = new Vector3(disc.Radius, disc.Thickness * 0.5f, disc.Radius).magnitude + PanchigiStrikeValidation.BoundEpsilon;

                int liveCount = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    Vector3 sample = samples[i].ToUnity();
                    if (PanchigiStrikeValidation.ContainsXZ(boardBounds, sample) == false)
                    {
                        continue;   // 판 끄트머리 밖으로 삐져나온 부분
                    }

                    //  이 자리에서 내가 판에 닿아 있나 — 다른 동전이 먼저 걸리면 그 위에 얹혀
                    //  있다는 뜻이고, 그러면 판에서 힘을 받지 못한다. 자기 자신은 레이가 콜라이더
                    //  안에서 출발하므로 PhysX가 알아서 건너뛴다.
                    GameFramework.Physics.CollisionHit hit =
                        collisionQuery.Raycast(sample, Vector3.down, reach, StrikeLayerMask);
                    if (hit.HasHit == false || hit.GetEntityId() != null)
                    {
                        continue;   // 아무것도 없거나(허공) 엔티티가 먼저 걸렸다(포개짐)
                    }

                    live[liveCount++] = samples[i];
                }

                System.Numerics.Vector3 impulse =
                    PanchigiStrike.ComputeImpulse(input, tuning, live, liveCount, sampleCount);
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
    }
}

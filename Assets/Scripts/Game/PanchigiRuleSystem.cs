using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 룰(서버). 이 슬라이스에서는 판을 세우는 것까지만 하고, 턴 상태 기계와 승패 판정은
    /// 다음 슬라이스에서 붙인다 — 지금은 전원 동일 등수를 돌려준다.
    /// </summary>
    public class PanchigiRuleSystem : IGameRuleSystem
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        public PanchigiRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            //  아바타는 없지만 신원 엔티티는 필요하다 — 누구 차례인지·누가 쳤는지를 잇는다.
            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = "",
                    characterCode = "",
                    position = Vector3.zero,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize() { }

        public MatchOutcome ResolveOutcome()
        {
            //  판이 끝나는 조건은 다음 슬라이스에서 붙는다 — 그때까지는 결과 보고 경로가 끊기지
            //  않도록 전원 동일 등수로 둔다(아직 승자가 정해지지 않는다).
            var outcome = new MatchOutcome();
            foreach (var userId in roomDataStore.match.playerList)
            {
                outcome.placements.Add(new MatchPlacement { userId = userId, placement = 1 });
            }
            return outcome;
        }
    }
}

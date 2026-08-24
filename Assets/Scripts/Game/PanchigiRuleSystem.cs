using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 룰(서버). 이 슬라이스에서는 판을 세우는 것까지만 하고, 턴 상태 기계와 승패 판정은
    /// 다음 슬라이스에서 붙인다 — 지금은 전원 동일 등수를 돌려준다.
    /// </summary>
    public class PanchigiRuleSystem : IGameRuleSystem
    {
        //  진짜 동전 아트가 아직 없다 — 임시 실린더로 모양만 세운다.
        //  아트가 들어오면 이 상수만 갈아 끼우면 된다.
        private const string CoinVisualId = "Assets/Art_Placeholder/Panchigi/Coin.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly LOP.MasterData.LOPMasterData masterData;

        public PanchigiRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner, LOP.MasterData.LOPMasterData masterData)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.masterData = masterData;
        }

        public void Initialize()
        {
            //  아바타는 없지만 신원 엔티티는 필요하다 — 누구 차례인지·누가 쳤는지를 잇는다.
            //  아바타가 없어 물리적 존재가 없어야 한다 — 판 위에 두면 다음 슬라이스의 동전을
            //  막는다. 실제 자리 배치는 전용 맵이 생길 때.
            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = "",
                    characterCode = "",
                    position = new Vector3(0f, -10f, 0f),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }

            var setup = masterData.Tables.TbPanchigiSetup.GetOrDefault(playerList.Length);
            if (setup == null)
            {
                //  조용히 넘기면 판이 빈 채로 시작하고 왜인지 런타임에 추적해야 한다.
                throw new System.InvalidOperationException(
                    $"TbPanchigiSetup에 {playerList.Length}인 구성이 없다 — 테이블을 채워야 한다.");
            }

            //  대형(formation) 해석은 슬라이스 3에서 갈라진다. 지금은 일렬로 떨어뜨려
            //  "쌓이는 것"과 "두 클라가 같은 것을 보는지"만 확인한다.
            for (int i = 0; i < setup.CoinCount; i++)
            {
                entitySpawner.Spawn(new CoinCreationData
                {
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = CoinVisualId,
                    position = new Vector3(0f, 1.5f, i * 0.5f),
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

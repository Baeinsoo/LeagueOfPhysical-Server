using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race 룰(서버). 지금은 매치 시작 시 참가자마다 새를 하나씩 세우는 일만 한다 —
    /// 결승선·순위·종료 판정은 다음 슬라이스에서 여기에 들어온다.
    /// </summary>
    public class FlappyRaceRuleSystem : IGameRuleSystem
    {
        // 새를 세로로 벌려 놓는 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float SpawnSpacingY = 2f;
        private const string BirdVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = BirdVisualId,
                    characterCode = "",
                    position = new Vector3(0f, i * SpawnSpacingY, 0f),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize()
        {
        }
    }
}

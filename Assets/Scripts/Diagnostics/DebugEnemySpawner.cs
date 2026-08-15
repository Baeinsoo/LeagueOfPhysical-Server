using GameFramework;   // SceneInjectMonoBehaviour
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 부하 실험용 적 스폰 조절. 인스펙터 우클릭 메뉴로 부르므로 Game 뷰에 포커스가 없어도 된다
    /// (에디터 두 개를 띄워 놓고 실험하면 포커스가 한쪽에만 있다).
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class DebugEnemySpawner : MonoBehaviour
    {
        [SerializeField] private int spawnCount = 50;

        [Inject] private FlapWangRuleSystem gameRuleSystem;

        [ContextMenu("Spawn Enemies")]
        private void SpawnEnemies()
        {
            gameRuleSystem.SpawnEnemies(spawnCount);
        }

        [ContextMenu("Despawn All Enemies")]
        private void DespawnAllEnemies()
        {
            gameRuleSystem.DespawnAllEnemies();
        }
    }
}

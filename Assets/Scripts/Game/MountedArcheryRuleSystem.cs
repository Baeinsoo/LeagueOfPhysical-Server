using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// MountedArchery 룰(서버). 1차 조각은 맞춤 판정이 없어 판은 시간 상한(150초)으로만 끝나고,
    /// 등수는 무작위다. 참가자는 가운데를 보고 선다.
    /// </summary>
    public class MountedArcheryRuleSystem : IGameRuleSystem
    {
        // 맵에 SpawnPoint가 없을 때만 쓰는 폴백 — 반지름 30 m 원 위에 등간격.
        private const float FallbackRingRadius = 30f;

        public const string BodyVisualId = "Assets/Characters/Chibi/Chibi.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly MountedArcheryConfig config;
        private readonly MountedArcheryMatchState state;
        private readonly List<string> userIds = new List<string>();

        public MountedArcheryRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                        MountedArcheryConfig config, MountedArcheryMatchState state)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.config = config;
            this.state = state;
        }

        /// <summary>원점을 바라보는 yaw(도). 0도가 +Z, 시계 방향이 +.</summary>
        public static float YawFacingCenter(Vector3 position)
            => Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg;

        public void Initialize()
        {
            userIds.Clear();
            var slots = SpawnPlacement.Arrange(
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[MountedArchery] 맵에 SpawnPoint가 없다 — 반지름 30 m 원 위에 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                userIds.Add(playerList[i]);
                var position = slots.Count > 0 ? slots[i % slots.Count] : RingSlot(i, playerList.Length);

                var look = PlayerLookResolver.Resolve(roomDataStore.looks, playerList[i], i);
                string entityId = entitySpawner.GenerateEntityId();
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = new Vector3(0f, YawFacingCenter(position), 0f),
                    velocity = Vector3.zero,
                    maxHP = config.MaxHp,
                    currentHP = config.MaxHp,
                    look = look,
                });
                //  킬 피드는 몸이 디스폰된 뒤에도 이름을 보여야 해서 판 상태에 이름을 따로 적어 둔다.
                state.Add(entityId, playerList[i], look.DisplayName);
            }
        }

        private static Vector3 RingSlot(int index, int total)
        {
            float angle = (index / (float)Mathf.Max(total, 1)) * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * FallbackRingRadius, 0f, Mathf.Cos(angle) * FallbackRingRadius);
        }

        public void Deinitialize() => userIds.Clear();

        public bool IsMatchOver => false;

        public long MatchDurationTicks => config.MatchDurationTicks(0.02f);

        public MatchOutcome ResolveOutcome()
        {
            var shuffled = new List<string>(userIds);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var outcome = new MatchOutcome();
            for (int i = 0; i < shuffled.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = shuffled[i], placement = i + 1 });
            }
            return outcome;
        }
    }
}

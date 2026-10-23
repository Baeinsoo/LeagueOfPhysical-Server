using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// MountedArchery 룰(서버). 산 사람이 한 명 이하가 되고 여운이 지나면 끝나며(시간 상한 150초),
    /// 등수는 탈락 순서의 역순이다. 참가자는 가운데를 보고 선다.
    /// </summary>
    public class MountedArcheryRuleSystem : IGameRuleSystem, ISettledResults
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

        public bool IsMatchOver => DodgeRuleSystem.MatchOver(
            state.Players.Count, state.AliveCount, state.LastEliminationTick, state.LastTick);

        public long MatchDurationTicks => config.MatchDurationTicks(0.02f);

        /// <summary>탈락했으면 결과가 정해졌다 — 그 뒤 관전하다 나가도 탈락 순서 그대로.</summary>
        public bool IsResultSettled(string userId)
        {
            foreach (var player in state.Players.Values)
            {
                if (player.UserId == userId) return player.Alive == false;
            }
            return false;
        }

        public MatchOutcome ResolveOutcome()
        {
            var alive = new List<string>();
            foreach (var player in state.Players.Values)
            {
                if (player.Alive) alive.Add(player.UserId);
            }
            return DodgePlacements.Resolve(alive, state.Eliminations);
        }
    }
}

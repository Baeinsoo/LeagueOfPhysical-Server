using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 판 도중 끊겨 있는 사람 목록을 방송한다(모든 게임 공통). 판본이 바뀌면 모든 세션에 한 번씩 보내고,
    /// 끊긴 세션은 받은 목록에서 지워 다시 연결되면 지금 상태를 한 번 더 받게 한다 — 피하기 상태 방송과 같은 모양.
    /// 클라는 남의 userId를 몰라 엔티티 id로 바꿔 보낸다.
    /// </summary>
    public class PresenceBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly PlayerPresence presence;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly EntitySpawner entitySpawner;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion = -1;

        public PresenceBroadcastSystem(PlayerPresence presence, GameFramework.ISessionManager sessionManager, EntitySpawner entitySpawner)
        {
            this.presence = presence;
            this.sessionManager = sessionManager;
            this.entitySpawner = entitySpawner;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (presence.Version != sentVersion)
            {
                sentVersion = presence.Version;
                receivedSessionIds.Clear();
            }

            PlayerPresenceToC message = null;
            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }
                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }
                message ??= ToWire(presence, entitySpawner.GetEntityIdByUserId);
                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }

        /// <param name="entityOf">userId → 엔티티 id. 몸이 없으면(잡혔거나 탈락) null — 그 사람은 뺀다.</param>
        public static PlayerPresenceToC ToWire(PlayerPresence presence, System.Func<string, string> entityOf)
        {
            var message = new PlayerPresenceToC { Version = presence.Version };
            foreach (var (userId, slot) in presence.AwaySlots)
            {
                string entityId = entityOf(userId);
                if (string.IsNullOrEmpty(entityId)) continue;
                message.Away.Add(new PresenceAwayWire { EntityId = entityId, Slot = slot });
            }
            return message;
        }
    }
}

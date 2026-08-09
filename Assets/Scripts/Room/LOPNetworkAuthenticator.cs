using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using GameFramework;
using VContainer;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPNetworkAuthenticator : NetworkAuthenticator
    {
        [Inject]
        private IRoomDataStore roomDataStore;

        #region Messages
        public struct AuthRequestMessage : NetworkMessage
        {
            public CustomProperties customProperties;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public int code;
            public string message;
        }
        #endregion

        #region Server
        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartServer()
        {
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        /// <summary>
        /// Called on server from StopServer to reset the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStopServer()
        {
            // unregister the handler for the authentication request
            NetworkServer.UnregisterHandler<AuthRequestMessage>();

            handledConnectionIds.Clear();
        }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnectionToClient conn) { }

        //  로비가 죽었을 때 30초(HttpClient 기본 타임아웃)를 기다리지 않는다 — 접속은 사람이 기다리는 경로다.
        private const int IntrospectTimeoutSeconds = 3;

        //  같은 연결이 인증 요청을 반복해 보내면 그때마다 로비를 부르게 된다(소켓 1회 → HTTP N회 증폭).
        //  첫 요청만 처리한다. 방 수명이 짧고 연결 수는 참가자 수로 묶이므로 OnStopServer에서 통째로 비운다.
        private readonly HashSet<int> handledConnectionIds = new HashSet<int>();

        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (handledConnectionIds.Add(conn.connectionId) == false)
            {
                return;
            }

            AuthenticateAsync(conn, msg).Forget();
        }

        private async UniTaskVoid AuthenticateAsync(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            string claimedUserId = msg.customProperties?.userId;

            if (roomDataStore.match.playerList.Contains(claimedUserId) == false)
            {
                Reject(conn, $"명단에 없는 userId: {claimedUserId}");
                return;
            }

#if UNITY_EDITOR
            //  에디터의 게임서버는 가짜 방·가짜 명단으로 돈다(ConfigureRoomComponent). 조회 키를 git에
            //  커밋하지 않으려고 introspect도 같은 경계 안에 둔다. 실환경에서는 아래 경로를 반드시 탄다.
            Debug.LogWarning("[Auth] 에디터라 introspect를 건너뜁니다. 신원은 클라가 주장한 값을 씁니다.");
            Accept(conn, msg.customProperties, claimedUserId);
            return;
#else
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")))
            {
                Debug.LogError("[Auth] INTERNAL_API_KEY가 없습니다. 접속을 허용할 수 없습니다.");
                Reject(conn, "server misconfigured");
                return;
            }

            IntrospectResponse introspect;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(IntrospectTimeoutSeconds));
                introspect = await WebAPI.Introspect(msg.customProperties.accessToken, timeout.Token);
            }
            catch (Exception exception)
            {
                //  확인하지 못한 것은 통과시키지 않는다(fail closed).
                Reject(conn, $"introspect 실패: {exception.Message}");
                return;
            }

            if (introspect.active == false)
            {
                Reject(conn, "토큰이 유효하지 않음");
                return;
            }

            if (introspect.sub != claimedUserId)
            {
                //  명단은 A로 통과했는데 토큰 주인은 B인 경우 — 사칭이다.
                Reject(conn, $"토큰 주인과 주장한 userId가 다름: {introspect.sub} != {claimedUserId}");
                return;
            }

            Accept(conn, msg.customProperties, introspect.sub);
#endif
        }

        private void Accept(NetworkConnectionToClient conn, CustomProperties customProperties, string authenticatedUserId)
        {
            if (NetworkServer.connections.ContainsKey(conn.connectionId) == false)
            {
                //  로비에 물어보는 동안 끊긴 연결이다. 아무것도 하지 않는다.
                //  (isReady는 씬 준비 여부라 여기서 볼 값이 아니다 — 미인증 연결은 늘 false다.)
                return;
            }

            //  클라가 주장한 값이 아니라 확인된 신원을 저장한다 — 이후 모든 서버 로직이 이 값을 신원으로 쓴다.
            customProperties.userId = authenticatedUserId;
            conn.authenticationData = customProperties;

            conn.Send(new AuthResponseMessage { code = 200, message = "success" });
            ServerAccept(conn);
        }

        private void Reject(NetworkConnectionToClient conn, string reason)
        {
            //  클라에는 사유를 나누지 않는다 — 왜 거부됐는지 알려주면 밖에서 상태를 떠볼 수 있다.
            Debug.LogWarning($"[Auth] 접속 거부: {reason}");

            conn.Send(new AuthResponseMessage { code = 401, message = "Invalid Credentials" });
            conn.isAuthenticated = false;
            ServerReject(conn);
        }
        #endregion
    }
}

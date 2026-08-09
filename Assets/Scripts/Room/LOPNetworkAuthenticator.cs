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

            handledConnections.Clear();
        }

        //  접속만 하고 인증 요청을 보내지 않는 연결은 그대로 두면 영원히 남아 maxConnections를 갉아먹는다.
        //  Mirror의 TimeoutAuthenticator와 같은 동작이되, 그 컴포넌트는 다른 인증기를 감싸는 데코레이터라
        //  씬에서 NetworkManager의 authenticator를 갈아끼워야 한다 — 여기서 직접 처리한다.
        private const float AuthenticationTimeoutSeconds = 60f;

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            StartCoroutine(DisconnectIfNotAuthenticated(conn));
        }

        private IEnumerator DisconnectIfNotAuthenticated(NetworkConnectionToClient conn)
        {
            yield return new WaitForSecondsRealtime(AuthenticationTimeoutSeconds);

            if (conn.isAuthenticated == false)
            {
                Debug.LogWarning($"[Auth] 제한 시간 안에 인증하지 않아 연결을 끊습니다. connectionId: {conn.connectionId}");
                conn.Disconnect();
            }
        }

        //  로비가 죽었을 때 30초(HttpClient 기본 타임아웃)를 기다리지 않는다 — 접속은 사람이 기다리는 경로다.
        private const int IntrospectTimeoutSeconds = 3;

        //  같은 연결이 인증 요청을 반복해 보내면 그때마다 로비를 부르게 된다(소켓 1회 → HTTP N회 증폭).
        //  첫 요청만 처리한다. 키는 connectionId(int)가 아니라 연결 객체 자체다 — kcp2k는 connectionId를
        //  클라 주소 해시로 만들어 재접속 시 같은 id가 재사용될 수 있는데, 그때도 Mirror는 매번 새
        //  NetworkConnectionToClient 인스턴스를 만들므로(참조 동일성) 객체 키는 재접속과 단순 재전송을
        //  오탐 없이 구분한다. 방 수명이 짧고 연결 수는 참가자 수로 묶이므로 OnStopServer에서 통째로 비운다.
        private readonly HashSet<NetworkConnectionToClient> handledConnections = new HashSet<NetworkConnectionToClient>();

        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (handledConnections.Add(conn) == false)
            {
                return;
            }

            AuthenticateAsync(conn, msg).Forget();
        }

        private async UniTaskVoid AuthenticateAsync(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            try
            {
                await DecideAsync(conn, msg);
            }
            catch (Exception exception)
            {
                //  UniTaskVoid 밖으로 예외가 새면 UniTaskScheduler가 로그만 남기고 삼킨다 — 그러면
                //  이 연결은 accept도 reject도 못 받은 채 영원히 멈춘다. 판정 도중 무엇이 터지든
                //  (널참조 포함) 반드시 여기서 거부 응답까지 보낸다.
                Reject(conn, $"인증 처리 실패: {exception.Message}");
            }
        }

        private async UniTask DecideAsync(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            string accessToken = msg.customProperties?.accessToken;

#if UNITY_EDITOR
            //  에디터의 게임서버는 가짜 방·가짜 명단으로 돈다(ConfigureRoomComponent). 조회 키를 git에
            //  커밋하지 않으려고 introspect도 같은 경계 안에 둔다. 실환경에서는 아래 #else를 반드시 탄다.
            Debug.LogWarning("[Auth] 에디터라 introspect를 건너뜁니다. 서명을 검증하지 않고 sub만 읽습니다.");

            if (EditorAccessTokenClaims.TryReadSubject(accessToken, out string editorUserId) == false)
            {
                Reject(conn, "토큰에서 sub를 읽지 못했습니다");
                return;
            }

            AcceptIfOnRoster(conn, editorUserId);
#else
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")))
            {
                Debug.LogError("[Auth] INTERNAL_API_KEY가 없습니다. 접속을 허용할 수 없습니다.");
                Reject(conn, "server misconfigured");
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(IntrospectTimeoutSeconds));
            IntrospectResponse introspect = await WebAPI.Introspect(accessToken, timeout.Token);

            if (introspect == null || introspect.active == false)
            {
                //  응답 본문이 비어 있으면(게이트웨이 이상 등) 역직렬화 결과가 null이다 — 토큰이
                //  유효하지 않은 경우와 동일하게 거부한다(fail closed).
                Reject(conn, "토큰이 유효하지 않음");
                return;
            }

            AcceptIfOnRoster(conn, introspect.sub);
#endif
        }

        //  명단 대조가 토큰 확인 *뒤에* 온다. 클라가 이름을 주장하지 않으므로 확인 전에는 대조할
        //  값 자체가 없다 — 신원은 토큰에서만 나온다.
        private void AcceptIfOnRoster(NetworkConnectionToClient conn, string userId)
        {
            if (roomDataStore.match.playerList.Contains(userId) == false)
            {
                Reject(conn, $"명단에 없는 참가자: {userId}");
                return;
            }

            Accept(conn, userId);
        }

        private void Accept(NetworkConnectionToClient conn, string authenticatedUserId)
        {
            //  connectionId만 보고 "살아있다"고 판단하면 안 된다 — kcp2k는 connectionId를 클라 주소로
            //  만들어 재접속 시 재사용한다. 로비에 물어보는 ≤3초 사이 같은 클라가 끊겼다 재접속하면,
            //  같은 connectionId를 가진 *다른* 연결 객체가 딕셔너리에 들어와 있을 수 있다. 그러면 이
            //  presence-only 검사는 통과하지만 우리가 들고 있는 conn은 이미 죽은 객체라, 그 연결을
            //  accept해도 아무것도 받지 못하는 좀비 세션이 된다. 그래서 존재 여부가 아니라 "지금
            //  등록된 연결이 바로 이 conn 객체인가"(참조 동일성)까지 확인한다.
            if (NetworkServer.connections.TryGetValue(conn.connectionId, out var current) == false || ReferenceEquals(current, conn) == false)
            {
                //  로비에 물어보는 동안 끊겼거나(그리고 재접속으로 자리가 바뀌었거나) 한 연결이다. 아무것도 하지 않는다.
                //  (isReady는 씬 준비 여부라 여기서 볼 값이 아니다 — 미인증 연결은 늘 false다.)
                return;
            }

            //  클라가 보낸 객체를 그대로 얹지 않는다 — 서버가 확정한 신원만 담은 타입을 새로 만든다.
            //  덤으로 접근 토큰이 연결 수명 내내 남는 문제도 사라진다(이 객체엔 토큰이 없다).
            conn.authenticationData = new ConnectionIdentity(authenticatedUserId);

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

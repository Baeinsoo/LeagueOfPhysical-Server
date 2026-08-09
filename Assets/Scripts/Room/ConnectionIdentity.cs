namespace LOP
{
    /// <summary>인증이 끝난 연결의 서버 측 신원. 클라가 보낸 값이 아니라 서버가 확정한 것만 담는다.</summary>
    public class ConnectionIdentity
    {
        public string UserId { get; }

        //  세션이 만들어지는 시점(LOPRoom.OnPlayerConnect)에 채워진다. Mirror가 인증 완료와 접속
        //  콜백을 따로 부르기 때문에 한 번에 다 채울 수 없다.
        public string SessionId { get; set; }

        public ConnectionIdentity(string userId)
        {
            UserId = userId;
        }
    }
}

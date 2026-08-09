using GameFramework;

namespace LOP
{
    /// <summary>클라가 보낸 메시지와, 그 연결에서 서버가 찾아낸 세션을 함께 나른다.
    /// 메시지 안에는 신원이 없다 — 신원은 연결에서만 나온다.</summary>
    public readonly struct ClientMessage<T> where T : IMessage
    {
        public ISession Session { get; }
        public T Message { get; }

        public ClientMessage(ISession session, T message)
        {
            Session = session;
            Message = message;
        }
    }
}

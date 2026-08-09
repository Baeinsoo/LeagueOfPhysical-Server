using GameFramework;

namespace LOP
{
    /// <summary>클라가 보낸 메시지와, 그 연결에서 서버가 확인한 신원을 함께 나른다.
    /// 메시지 안에 적힌 신원은 클라가 쓴 것이라 믿을 수 없다.</summary>
    public readonly struct ClientMessage<T> where T : IMessage
    {
        public string UserId { get; }
        public T Message { get; }

        public ClientMessage(string userId, T message)
        {
            UserId = userId;
            Message = message;
        }
    }
}

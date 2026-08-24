using GameFramework;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 앱 전역 메시지 버스(MessagePipe). 메시지 타입별 브로커는 각 마이그레이션 슬라이스에서
            // RegisterOrderedMessageBroker<T>로 명시 등록한다(IL2CPP open-generic 미지원 대비).
            //
            // MessagePipe 기본 브로커(RegisterMessageBroker)를 쓰지 않는 이유: 그쪽은 핸들러를 부르는
            // 순서가 구독 순서와 어긋날 수 있다 — 구독 해제된 자리를 재사용하기 때문에, 구독·해제를
            // 반복하면 나중에 구독한 쪽이 먼저 불린다. 지금 서버는 메시지마다 구독자가 하나라 증상이
            // 없지만, 둘째 구독자를 붙이는 순간 조용히 깨진다(클라에서 실제로 그렇게 났다).
            // 발행·구독 인터페이스(IPublisher/ISubscriber)는 MessagePipe 것 그대로다.
            //
            // RegisterMessagePipe 자체는 남긴다 — GlobalMessagePipe가 쓰는 IServiceProvider 등록이 여기 있다.
            builder.RegisterMessagePipe();

            // WebResponse — WebAPI가 GlobalMessagePipe로 발행하므로 SetProvider 필요.
            builder.RegisterOrderedMessageBroker<GetMatchResponse>();
            builder.RegisterOrderedMessageBroker<GetRoomResponse>();
            builder.RegisterOrderedMessageBroker<UpdateRoomStatusResponse>();
            builder.RegisterOrderedMessageBroker<HttpResponse>();

            // 엔티티 라이프사이클 / 아이템 접촉
            builder.RegisterOrderedMessageBroker<Event.Entity.EntityCreated>();
            builder.RegisterOrderedMessageBroker<Event.Entity.EntityDestroyed>();
            builder.RegisterOrderedMessageBroker<Event.Entity.ItemTouch>();

            // 네트워크 수신(NetworkMessageDispatcher가 발행 → MessageHandler가 구독)
            builder.RegisterOrderedMessageBroker<ClientMessage<GameInfoToS>>();
            builder.RegisterOrderedMessageBroker<ClientMessage<InputCommandToS>>();
            builder.RegisterOrderedMessageBroker<ClientMessage<StatAllocationToS>>();
            builder.Register<NetworkMessageDispatcher>(Lifetime.Singleton);

            builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<EnemyBrain>(Lifetime.Transient);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                // 정적/비-DI 코드(웹 인터셉터)가 GlobalMessagePipe.GetPublisher<T>로 발행할 수 있도록 provider 설정.
                GlobalMessagePipe.SetProvider(container.AsServiceProvider());
            });
            #endregion
        }
    }
}

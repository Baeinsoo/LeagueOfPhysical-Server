using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>
    /// 각 새가 이번 틱에 실제로 쓴 입력을 모두에게 되뿌린다. 받는 클라는 자기 것은 버리고(이미
    /// 갖고 있다) 남의 것만 자기 버퍼에 넣어, <b>남의 새도 진짜 입력으로 굴린다.</b>
    ///
    /// <para>이게 없으면 클라는 남이 눌렀는지 모르니 "안 눌렀다"로 굴릴 수밖에 없고, 상대가
    /// 날갯짓할 때마다 궤적이 통째로 틀린다(실측 최대 4.8m).</para>
    ///
    /// <para>unreliable + 최근 몇 틱 묶음(sliding-window redundancy) — 클라→서버가 쓰는 방식과 같다.
    /// 한 패킷을 잃어도 다음 패킷이 그 틱을 다시 실어 오므로 스스로 메워진다.</para>
    ///
    /// <para><see cref="ServerInputSystem"/> 다음에 돌아야 한다 — 거기서 이번 틱 커맨드가
    /// 확정(<c>buffer.Current</c>)되기 때문이다. 그리고 <c>Consume</c>이 버퍼에서 커맨드를 빼내므로
    /// 지난 틱을 버퍼에서 되읽을 수 없다 — 그래서 여기서 따로 최근 것을 들고 있는다.</para>
    /// </summary>
    public class EntityInputBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        //  패킷당 실어 보낼 최근 틱 수(현재 포함). 클라→서버(PlayerInputManager)와 같은 값이다.
        private const int RedundancyWindow = 4;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ISessionManager sessionManager;

        //  엔티티별 최근 확정 입력. Consume이 버퍼를 비우므로 여기 따로 쌓는다.
        private readonly Dictionary<string, List<InputCommandEntry>> recent
            = new Dictionary<string, List<InputCommandEntry>>();

        public EntityInputBroadcastSystem(GameFramework.World.EntityRegistry entityRegistry,
                                          ISessionManager sessionManager)
        {
            this.entityRegistry = entityRegistry;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            var message = new EntityInputsToC { Tick = tick };

            foreach (var worldEntity in entityRegistry.All)
            {
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;   // 입력 비조종(AI·아이템 등)
                }

                if (recent.TryGetValue(worldEntity.Id, out var history) == false)
                {
                    history = new List<InputCommandEntry>();
                    recent[worldEntity.Id] = history;
                }

                //  Current가 null인 틱도 실어 보낸다(0 커맨드) — 빈칸을 남기면 받는 쪽에서
                //  "안 눌렀다"와 "아직 안 왔다"가 구별되지 않는다(클라→서버와 같은 이유).
                history.Add(new InputCommandEntry
                {
                    Tick = tick,
                    InputCommand = ToProto(buffer.Current),
                });
                if (history.Count > RedundancyWindow)
                {
                    history.RemoveAt(0);
                }

                var entry = new EntityInputHistory { EntityId = worldEntity.Id };
                entry.RecentInputs.AddRange(history);
                message.Entities.Add(entry);
            }

            if (message.Entities.Count == 0)
            {
                return;
            }

            //  스냅샷과 같은 배송 등급 — 유실돼도 다음 패킷이 같은 틱을 다시 싣는다.
            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(message, reliable: false);
            }
        }

        /// <summary>떠난 새의 기록은 들고 있을 이유가 없다.</summary>
        public void Forget(string entityId) => recent.Remove(entityId);

        private static global::InputCommand ToProto(InputCommand command)
        {
            if (command == null)
            {
                return new global::InputCommand();
            }
            return new global::InputCommand
            {
                SequenceNumber = command.SequenceNumber,
                Horizontal = command.Horizontal,
                Vertical = command.Vertical,
                Jump = command.Jump,
                AbilityId = command.AbilityId,
            };
        }
    }
}

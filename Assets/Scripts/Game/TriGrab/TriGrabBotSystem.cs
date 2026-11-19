using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 봇의 손발(서버 Begin 페이즈). 봇 몸마다 두뇌(<see cref="TriGrabBotBrain"/>)에 이번 틱 행동을 묻고,
    /// 사람 입력과 같은 길(<see cref="InputBufferSystem.Enqueue"/>)로 이번 틱 번호에 넣는다.
    /// 이동은 월드 평면 방향(Horizontal = x, Vertical = z), 쏘기는 Fire + AimYaw(도).
    /// </summary>
    public sealed class TriGrabBotSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly TriGrabMatch match;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly TriGrabBotBrain brain;
        private readonly System.Func<string, bool> isBot;
        private readonly Dictionary<string, long> sequences = new Dictionary<string, long>();
        private readonly List<string> botIds = new List<string>();

        public TriGrabBotSystem(GameFramework.World.EntityRegistry registry, TriGrabMatch match,
                                InputBufferSystem inputBufferSystem, IMatchSeed matchSeed, System.Func<string, bool> isBot)
        {
            this.registry = registry;
            this.match = match;
            this.inputBufferSystem = inputBufferSystem;
            this.isBot = isBot;
            //  봇 두뇌는 판 씨앗에서 따로 나눈 난수 — 같은 씨앗이면 같은 판이 다시 나온다(판 엔진 줄기와 겹치지 않게 상수를 섞는다).
            brain = new TriGrabBotBrain(unchecked((int)matchSeed.Value ^ 0x42304254));
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectBots();
            foreach (var id in botIds)
            {
                var entity = registry.Get(id);
                var position = entity.Get<GameFramework.World.Transform>().Position;
                brain.Think(id, new Vector2(position.X, position.Z), tick, match.State, out var move, out var fireYaw);

                var command = new InputCommand { Horizontal = move.x, Vertical = move.y };
                if (fireYaw.HasValue)
                {
                    command.Fire = true;
                    command.AimYaw = fireYaw.Value;
                }
                //  사람 입력처럼 차례 번호를 하나씩 올린다 — 입력 시스템이 번호 틈을 유실로 센다.
                sequences.TryGetValue(id, out long sequence);
                command.SequenceNumber = ++sequence;
                sequences[id] = sequence;
                inputBufferSystem.Enqueue(entity.Get<InputBuffer>(), tick, command);
            }
        }

        //  등록 순서로 고정한다 — 해시 순서를 타면 같은 씨앗으로 같은 판이 재현되지 않는다.
        private void CollectBots()
        {
            botIds.Clear();
            foreach (var id in match.State.PlayerOrder)
            {
                if (registry.TryGet(id, out var entity) == false || entity.Has<InputBuffer>() == false) continue;
                var owner = entity.Get<GameFramework.World.Ownership>();
                if (owner != null && isBot(owner.OwnerId)) botIds.Add(id);
            }
        }
    }
}

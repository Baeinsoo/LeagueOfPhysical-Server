using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 한 발 승부의 라운드를 닫는다. 과녁이 사라지는 틱이 지나면 그 라운드의 순위 점수를 주고
    /// 결과 사건을 낸다. <b>서버에서만 돈다.</b>
    /// </summary>
    public class ArcheryRoundSystem : GameFramework.Runner.ITickSystem
    {
        private readonly System.Func<long> gameplayStartTick;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer eventBuffer;
        private readonly ArcheryCourse course;
        private readonly ArcheryRoundLog log;
        private readonly List<ArcheryRoundShot> shots = new List<ArcheryRoundShot>();

        //  다음에 닫을 라운드. 틱을 건너뛰어도 밀린 라운드를 차례로 다 닫는다.
        private int nextRound;

        public ArcheryRoundSystem(System.Func<long> gameplayStartTick,
                                  GameFramework.World.EntityRegistry entityRegistry,
                                  GameFramework.World.WorldEventBuffer eventBuffer,
                                  ArcheryCourse course, ArcheryRoundLog log)
        {
            this.gameplayStartTick = gameplayStartTick;
            this.entityRegistry = entityRegistry;
            this.eventBuffer = eventBuffer;
            this.course = course;
            this.log = log;
        }

        public void Tick(long tick, float deltaTime)
        {
            long start = gameplayStartTick();
            if (course.IsShootOff == false || start == long.MaxValue)
            {
                return;
            }

            while (nextRound < course.StepCount && tick >= course.RoundCloseTick(nextRound, start))
            {
                Close(nextRound);
                nextRound++;
            }
        }

        private void Close(int round)
        {
            shots.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Has<ArcheryScore>() == false)
                {
                    continue;
                }
                bool hit = log.TryGet(round, entity.Id, out var face, out float distance);
                shots.Add(new ArcheryRoundShot(entity.Id, hit, face, distance));
            }

            int multiplier = course.MultiplierAt(round);
            var placements = ArcheryShootOffRanking.Rank(shots, multiplier);
            foreach (var p in placements)
            {
                var score = entityRegistry.Get(p.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Gained += p.Points;
                }
            }

            eventBuffer.Append(new ArcheryRoundResultEvent(round, multiplier, placements));
            log.Forget(round);
        }
    }
}

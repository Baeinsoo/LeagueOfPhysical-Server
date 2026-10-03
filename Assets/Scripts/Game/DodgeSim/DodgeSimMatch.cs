using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>이번 틱에 가고 싶은 자리. 시뮬이 속도·벽·충돌로 자른다.</summary>
    public interface IDodgeSimMover
    {
        Vector2 Want(DodgeSimMatch m, int player);
    }

    /// <summary>
    /// Dodge 한 판을 오프라인으로 돈다(슬라이스 4a 스펙 §2·§3). 진행기·판정은 서버 시스템 그대로이고,
    /// 이동만 단순화했다 — 점, 최고 속도 4 m/s, 가속 없음, 몸 반지름 0.37m, 겹치면 멈춤.
    /// 틱 순서는 서버와 같다: 이동 → 진행기 → 판정(End 페이즈).
    /// </summary>
    public sealed class DodgeSimMatch
    {
        public const float BodyRadius = 0.37f;
        public const float MoveSpeed = 4f;
        private const float Dt = 1f / DodgeConfig.TicksPerSecond;

        /// <summary>DodgeMap SpawnPoint1~8(2026-10-03 에디터에서 읽은 값).</summary>
        public static readonly Vector2[] MapSpawns =
        {
            new Vector2(4f, 0f), new Vector2(2.83f, 2.83f), new Vector2(0f, 4f), new Vector2(-2.83f, 2.83f),
            new Vector2(-4f, 0f), new Vector2(-2.83f, -2.83f), new Vector2(0f, -4f), new Vector2(2.83f, -2.83f),
        };

        public readonly DodgeConfig Config;
        public readonly DodgeStageTable Stages;
        public readonly DodgeMatchState State = new DodgeMatchState();
        public readonly Vector2[] Positions;
        public readonly List<(int player, long tick)> Hits = new List<(int, long)>();
        public readonly List<(int player, long tick)> Eliminations = new List<(int, long)>();
        public readonly bool Collide;
        public long Tick { get; private set; } = -1;
        public int Players => Positions.Length;
        public float Limit => Config.ArenaHalf - BodyRadius;

        private readonly DodgeSimWorld world = new DodgeSimWorld();
        private readonly DodgeDirectorSystem director;
        private readonly DodgeHazardSystem hazards;
        private readonly string[] ids;
        private readonly bool[] alive;
        private readonly int[] lives;

        public IReadOnlyList<bool> Alive => alive;

        public DodgeSimMatch(ulong seed, DodgeConfig config, DodgeStageTable stages, int players, bool collide = true)
        {
            Config = config;
            Stages = stages;
            Collide = collide;
            world.GameplayStartTick = 0;
            director = new DodgeDirectorSystem(State, new DodgeDirector(seed, config, stages), world, config);
            hazards = new DodgeHazardSystem(State, world.EntityRegistry, config, id => world.EntityRegistry.Remove(id));

            Positions = new Vector2[players];
            ids = new string[players];
            alive = new bool[players];
            lives = new int[players];
            for (int i = 0; i < players; i++)
            {
                ids[i] = "p" + i;
                Positions[i] = MapSpawns[i % MapSpawns.Length];
                alive[i] = true;
                lives[i] = config.Lives;
                var e = new GameFramework.World.Entity(ids[i]);
                e.Add(new GameFramework.World.Transform());
                world.EntityRegistry.Add(e);
                State.Players[ids[i]] = new DodgePlayerLife { Lives = config.Lives };
            }
            SyncTransforms();
        }

        /// <summary>서버 끝 조건(DodgeRuleSystem.MatchOver의 "갈림" 부분) — 혼자면 탈락해야, 둘 이상이면 한 명 남으면.</summary>
        public bool Over => Players >= 2 ? State.AliveCount <= 1 : State.AliveCount == 0;

        public DodgeStagePoint StageAt(long tick) => Stages.At(tick, 0, Config);

        public void Run(IDodgeSimMover mover, long maxTicks)
        {
            while (!Over && Tick < maxTicks) Step(mover);
        }

        public void Step(IDodgeSimMover mover)
        {
            Tick++;
            for (int i = 0; i < Players; i++)
            {
                if (alive[i]) MoveTo(i, mover.Want(this, i));
            }
            SyncTransforms();

            director.Tick(Tick, Dt);
            hazards.Tick(Tick, Dt);

            for (int i = 0; i < Players; i++)
            {
                if (!alive[i]) continue;
                var life = State.Players[ids[i]];
                if (life.Lives < lives[i]) Hits.Add((i, Tick));
                lives[i] = life.Lives;
                if (!life.Alive)
                {
                    alive[i] = false;
                    Eliminations.Add((i, Tick));
                }
            }
        }

        /// <summary>검사기용 — 이동·진행기만 돌고 판정은 건너뛴다(유령은 맞지 않는다). 유령은 순간이동한다(조준 위치만 필요).</summary>
        public void StepPatternsOnly(IDodgeSimMover ghost)
        {
            Tick++;
            for (int i = 0; i < Players; i++) Positions[i] = ghost.Want(this, i);
            SyncTransforms();
            director.Tick(Tick, Dt);
        }

        private void MoveTo(int i, Vector2 want)
        {
            Vector2 from = Positions[i];
            Vector2 step = Vector2.ClampMagnitude(want - from, MoveSpeed * Dt);
            Vector2 to = from + step;
            to.x = Mathf.Clamp(to.x, -Limit, Limit);
            to.y = Mathf.Clamp(to.y, -Limit, Limit);
            if (Collide)
            {
                for (int j = 0; j < Players; j++)
                {
                    if (j != i && alive[j] && (to - Positions[j]).sqrMagnitude < 4f * BodyRadius * BodyRadius)
                    {
                        return;   // 단단한 벽 — 이번 틱은 멈춘다
                    }
                }
            }
            Positions[i] = to;
        }

        private void SyncTransforms()
        {
            for (int i = 0; i < Players; i++)
            {
                if (alive[i] && world.EntityRegistry.TryGet(ids[i], out var e))
                {
                    e.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(Positions[i].x, 0f, Positions[i].y);
                }
            }
        }
    }
}

using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 탄막 투척 심판을 동선 식(<see cref="DodgeReferee.PoseAt"/>) 자리에 세운다(서버 End 페이즈, 진행기보다 앞).
    /// 심판은 월드 시뮬을 안 받는 캐릭터라(벽·중력 무시 — 관중석 너머에서 걸어 들어온다) 위치·속도·물리 몸을 여기서 직접 쓴다.
    /// 물리 몸이 따라오므로 다음 틱 선수 이동이 심판에 막힌다(캐릭터끼리 단단한 벽).
    /// </summary>
    public class DodgeRefereeSystem : GameFramework.Runner.ITickSystem
    {
        private readonly DodgeMatchState state;
        private readonly GameFramework.World.IWorld world;
        private readonly DodgeStageTable stages;
        private readonly DodgeConfig config;

        public DodgeRefereeSystem(DodgeMatchState state, GameFramework.World.IWorld world, DodgeStageTable stages, DodgeConfig config)
        {
            this.state = state;
            this.world = world;
            this.stages = stages;
            this.config = config;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (string.IsNullOrEmpty(state.RefereeId) || !world.EntityRegistry.TryGet(state.RefereeId, out var e))
            {
                return;
            }
            var pose = DodgeReferee.PoseAt(tick, world.GameplayStartTick, stages, config);
            var position = new System.Numerics.Vector3(pose.Position.x, 0f, pose.Position.y);
            var transform = e.Get<GameFramework.World.Transform>();
            transform.Position = position;
            // 걸을 땐 가는 쪽, 서 있을 땐 카메라(남쪽)를 본다.
            Vector3 face = pose.Velocity.sqrMagnitude > 1e-6f ? new Vector3(pose.Velocity.x, 0f, pose.Velocity.y) : Vector3.back;
            transform.Rotation = Quaternion.LookRotation(face, Vector3.up).ToNumerics();
            var velocity = e.Get<GameFramework.World.Velocity>();
            if (velocity != null) velocity.Linear = new System.Numerics.Vector3(pose.Velocity.x, 0f, pose.Velocity.y);
            var body = e.Get<GameFramework.World.PhysicsBody>();
            if (body != null)
            {
                body.SetPosition(position);
                body.SetRotation(transform.Rotation);
            }
        }
    }
}

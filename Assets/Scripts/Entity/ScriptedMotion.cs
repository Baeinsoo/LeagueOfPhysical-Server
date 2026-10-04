namespace LOP
{
    /// <summary>
    /// 정해진 식으로 움직이는 캐릭터(서버) — AI 두뇌를 붙이지 않고(EntityBinder), 월드 시뮬도 안 받는다(Simulated 없음).
    /// 위치·속도·물리 몸은 그 모드의 시스템이 직접 쓴다. Dodge 투척 심판이 처음 쓴다.
    /// </summary>
    public sealed class ScriptedMotion : GameFramework.World.Component { }
}

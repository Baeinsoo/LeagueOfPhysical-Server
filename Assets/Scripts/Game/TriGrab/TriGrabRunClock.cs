namespace LOP
{
    /// <summary>
    /// 서버 틱 시스템이 적어 두고 룰이 읽는 시계. 룰은 틱을 직접 받지 못하고, 끝 조건("Over가 된 지 150틱")에 둘 다 필요하다.
    /// </summary>
    public sealed class TriGrabRunClock
    {
        public long LastTick;
        /// <summary>판이 Over가 된 걸 처음 본 틱. 아직이면 -1.</summary>
        public long OverSinceTick = -1;
    }
}

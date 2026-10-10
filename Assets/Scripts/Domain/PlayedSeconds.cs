using System;

namespace LOP
{
    /// <summary>경과 틱을 초로 바꾼다. 로비가 이 값으로 보상을 계산하므로 음수·소수가 나오면 안 된다.</summary>
    public static class PlayedSeconds
    {
        public static int Compute(long endTick, long startTick, double interval)
        {
            //  매치가 시작하지 않았거나(출발틱 미정) 끝이 시작보다 앞인 경우(이례적 호출 순서) 0으로 둔다.
            if (startTick == long.MaxValue || endTick < startTick)
            {
                return 0;
            }

            return (int)Math.Floor((endTick - startTick) * interval);
        }
    }
}

using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판을 <b>만들 때가 아니라 쓸 때</b> 찾는다.
    ///
    /// 판은 맵 씬에 있고, 맵은 게임 씬보다 늦게 로드된다(<see cref="LOPRunner"/>가 게임 씬 안에서
    /// 맵을 불러온다). 그래서 DI가 시스템들을 만드는 시점엔 판이 아직 씬에 없다 — 인스턴스를 바로
    /// 주입받으면 null이 박힌다. 판을 쓰는 곳은 전부 맵 로드가 끝난 뒤라 이 시점 차이를 이걸로 흡수한다.
    /// </summary>
    public class PanchigiBoardLocator
    {
        private PanchigiBoard cached;

        /// <summary>맵이 아직 안 올라왔으면 null. 부르는 쪽이 그 경우를 다뤄야 한다.</summary>
        public PanchigiBoard Board
        {
            get
            {
                if (cached == null)
                {
                    cached = Object.FindFirstObjectByType<PanchigiBoard>();
                }
                return cached;
            }
        }
    }
}

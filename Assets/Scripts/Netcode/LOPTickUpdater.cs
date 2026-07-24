using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = Runner.NetworkTime.serverNow;
        }
    }
}

using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        public GameFramework.Netcode.INetworkTime networkTime;

        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = networkTime.ServerNow;
        }
    }
}

using GameFramework.Netcode;

namespace LOP
{
    /// <summary>
    /// 서버 권위 시간을 INetworkTime으로 노출. 서버는 예측이 없어 ServerNow=PredictedTime(둘 다 NetworkTime.time, 일치), Rtt=0.
    /// </summary>
    public class MirrorNetworkTime : GameFramework.Netcode.INetworkTime
    {
        public double ServerNow => Mirror.NetworkTime.time;

        public double PredictedTime => Mirror.NetworkTime.time;

        public double Rtt => 0;
    }
}

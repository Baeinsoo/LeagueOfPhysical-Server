using Cysharp.Threading.Tasks;

namespace LOP
{
    /// <summary>
    /// 로비에 "이 사람은 이 방을 떠났다"를 알린다. 로비는 그 사람이 아직 이 방에 있을 때만 위치를 비운다 —
    /// 그래서 두 번 불러도, 판이 끝나 전원을 다시 비워도 결과가 같다.
    /// </summary>
    public interface IUserLocationReleaser
    {
        UniTask ReleaseAsync(string userId, string roomId);
    }
}

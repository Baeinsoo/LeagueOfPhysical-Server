using Cysharp.Threading.Tasks;

namespace LOP
{
    public class LobbyUserLocationReleaser : IUserLocationReleaser
    {
        public UniTask ReleaseAsync(string userId, string roomId) => WebAPI.ReleaseGameRoom(userId, roomId);
    }
}

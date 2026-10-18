using System.Threading;
using Cysharp.Threading.Tasks;

namespace LOP
{
    public class LobbyUserLocationReleaser : IUserLocationReleaser
    {
        public UniTask ReleaseAsync(string userId, string roomId, CancellationToken cancellationToken) =>
            WebAPI.ReleaseGameRoom(userId, roomId, cancellationToken);
    }
}

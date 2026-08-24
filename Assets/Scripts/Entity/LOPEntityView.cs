using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace LOP
{
    public class LOPEntityView : MonoBehaviour, ICleanup
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor actor { get; private set; }

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
        }

        private GameObject _visualGameObject;
        public GameObject visualGameObject
        {
            get => _visualGameObject;
            private set
            {
                if (_visualGameObject != value)
                {
                    Destroy(_visualGameObject);
                }

                _visualGameObject = value;
            }
        }

        private string visualId;
        private AsyncOperationHandle<GameObject> asyncOperationHandle;

        protected virtual void Start()
        {
            var appearance = entityRegistry.Get(actor.entityId)?.Get<Appearance>();
            if (appearance != null)
            {
                UpdateVisual(appearance.VisualId);
            }
        }

        public void Cleanup()
        {
            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }

            actor = null;
        }

        private async void UpdateVisual(string visualId)
        {
            if (this.visualId == visualId)
            {
                return;
            }

            this.visualId = visualId;

            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            //  빈 id는 "보여줄 몸이 없다"는 정당한 상태다(예: 아바타 없는 판치기 플레이어) —
            //  에러가 아니므로 로드를 시도하지 않고 조용히 끝낸다. id가 있는데 못 찾는 것과는 다르다:
            //  그건 진짜 에셋 누락 버그라 아래처럼 그대로 실패시켜 드러나게 둔다.
            if (string.IsNullOrEmpty(visualId))
            {
                // 지금은 아무도 비id→빈id로 갈아타지 않지만, 나중에 그런 전환이 생겨도
                // 이전 오브젝트가 참조를 잃고 떠돌지 않도록 미리 비워둔다.
                visualGameObject = null;
                return;
            }

            asyncOperationHandle = Addressables.LoadAssetAsync<GameObject>(visualId);
            await asyncOperationHandle.Task;

            if (asyncOperationHandle.Status != AsyncOperationStatus.Succeeded || asyncOperationHandle.Result == null)
            {
                Addressables.Release(asyncOperationHandle);
                return;
            }

            // Addressables 로드는 여러 프레임 걸린다 — 그 사이 엔티티가 디스폰되면 registry에서 사라져 null.
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity == null)
            {
                return;
            }

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, transform);
            visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
            visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
        }

        private void LateUpdate()
        {
            if (visualGameObject != null)
            {
                var worldEntity = entityRegistry.Get(actor.entityId);
                visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
                visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
            }
        }
    }
}

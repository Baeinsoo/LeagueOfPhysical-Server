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

        public LOPActor entity { get; private set; }

        public void SetEntity(LOPActor entity)
        {
            this.entity = entity;
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
            var appearance = entityRegistry.Get(entity.entityId)?.Get<Appearance>();
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

            entity = null;
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

            asyncOperationHandle = Addressables.LoadAssetAsync<GameObject>(visualId);
            await asyncOperationHandle.Task;

            // Addressables 로드는 여러 프레임 걸린다 — 그 사이 엔티티가 디스폰되면 registry에서 사라져 null.
            var worldEntity = entityRegistry.Get(entity.entityId);
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
                var worldEntity = entityRegistry.Get(entity.entityId);
                visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
                visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
            }
        }
    }
}

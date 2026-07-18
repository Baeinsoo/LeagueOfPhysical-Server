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

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, transform);
            visualGameObject.transform.position = entity.position;
            visualGameObject.transform.rotation = Quaternion.Euler(entity.rotation);
        }
        
        private void LateUpdate()
        {
            if (visualGameObject != null)
            {
                visualGameObject.transform.position = entity.position;
                visualGameObject.transform.rotation = Quaternion.Euler(entity.rotation);
            }
        }
    }
}

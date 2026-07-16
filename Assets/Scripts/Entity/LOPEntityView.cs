using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LOP
{
    public class LOPEntityView : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
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
        private System.IDisposable propertyChangeSubscription;

        protected virtual void Start()
        {
            propertyChangeSubscription = GlobalMessagePipe.GetSubscriber<string, PropertyChange>().Subscribe(entity.entityId, OnPropertyChange);

            if (entity.TryGetEntityComponent<AppearanceComponent>(out var appearanceComponent))
            {
                UpdateVisual(appearanceComponent.visualId);
            }
        }

        public void Cleanup()
        {
            propertyChangeSubscription?.Dispose();

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

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(AppearanceComponent.visualId):
                    UpdateVisual(entity.GetEntityComponent<AppearanceComponent>().visualId);
                    break;
            }
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

            GameObject visual = transform.parent.Find("Visual").gameObject;

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, visual.transform);
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

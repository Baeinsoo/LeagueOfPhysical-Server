using GameFramework;
using LOP.Event.Entity;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;

namespace LOP
{
    public class PhysicsComponent : LOPComponent
    {
        private GameObject _physicsGameObject;
        public GameObject physicsGameObject
        {
            get => _physicsGameObject;
            set
            {
                this.SetProperty(ref _physicsGameObject, value, entity.RaisePropertyChanged);
            }
        }

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(bool isKinematic, bool isTrigger)
        {
            entity.eventBus.Receive<PropertyChange>().Subscribe(OnPropertyChange).AddTo(this);

            GameObject physics = entity.transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0.1f;
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = entity.position;
            entityRigidbody.rotation = Quaternion.Euler(entity.rotation);
            entityRigidbody.linearVelocity = entity.velocity;
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };

            TriggerDetector triggerDetector = physicsGameObject.GetOrAddComponent<TriggerDetector>();
            triggerDetector.onTriggerEnter += OnTriggerEnter;
            triggerDetector.onTriggerStay += OnTriggerStay;
            triggerDetector.onTriggerExit += OnTriggerExit;
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(entity.position):
                    entityRigidbody.position = entity.position;
                    break;

                case nameof(entity.rotation):
                    entityRigidbody.rotation = Quaternion.Euler(entity.rotation);
                    break;

                case nameof(entity.velocity):
                    entityRigidbody.linearVelocity = entity.velocity;
                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            LOPEntity otherEntity = other.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
            if (otherEntity == null)
            {
                Debug.LogWarning($"Trigger detected with non-entity object: {other.name}");
                return;
            }

            //  Temp.. must be modified later
            if (otherEntity.GetComponent<EntityInputComponent>())
            {
                RoomEventBus.Publish(new ItemTouch(entity.entityId, otherEntity.entityId));
            }
        }

        private void OnTriggerStay(Collider other)
        {
        }

        private void OnTriggerExit(Collider other)
        {
        }
    }
}

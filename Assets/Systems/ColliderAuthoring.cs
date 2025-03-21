using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSCollisionSystem
{
    
    public class ColliderAuthoring : MonoBehaviour
    {
        [Header("AABB Configuration")] [SerializeField]
        private Vector2 _size = new(1f, 1f);

        [SerializeField] private Vector2 _offset = Vector2.zero;
        [SerializeField] private bool _updateAABBFromTransform = true;

        [Header("Collision Configuration")] [SerializeField]
        private LayerMask _collisionLayer;

        [SerializeField] private LayerMask _collidesWith;
        [SerializeField] private bool _isTrigger = false;

        public event Action<Entity, CollisionInfo> OnCollisionEnter;
        public event Action<Entity, CollisionInfo> OnCollisionStay;
        public event Action<Entity, CollisionInfo> OnCollisionExit;

        private Entity _entity;
        private EntityManager _entityManager;
        private bool _initialized = false;
        
        public Entity Entity => _entity;
        
        public bool IsTrigger
        {
            get => _isTrigger;
            set
            {
                _isTrigger = value;
                UpdateCollider();
            }
        }

        private void Awake()
        {
            TryInitialize();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_initialized)
            {
                return;
            }

            if (GlobalCollisionManager.Instance == null)
            {
                var managerGO = new GameObject("GlobalCollisionManager");
                managerGO.AddComponent<GlobalCollisionManager>();
            }

            if (World.DefaultGameObjectInjectionWorld != null)
            {
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                CreateColliderEntity();
                _initialized = true;
                
                GlobalCollisionManager.Instance.RegisterAuthoring(_entity, this);
            }
        }

        private void CreateColliderEntity()
        {
            _entity = _entityManager.CreateEntity();

            Vector2 position = transform.position;
            Vector2 halfSize = _size * 0.5f;
            Vector2 centerPos = position + _offset;

            AABBComponent aabb = new()
            {
                Min = new float2(centerPos.x - halfSize.x, centerPos.y - halfSize.y),
                Max = new float2(centerPos.x + halfSize.x, centerPos.y + halfSize.y)
            };

            _entityManager.AddComponentData(_entity, aabb);

            _entityManager.AddComponentData(_entity, new ColliderComponent
            {
                AABB = aabb,
                Owner = _entity,
                CollisionLayer = (uint)_collisionLayer.value,
                CollisionMask = (uint)_collidesWith.value
            });
            
            _entityManager.AddBuffer<ActiveCollision>(_entity);
            
            _entityManager.AddComponentData(_entity, new CollisionProcessingTag());
            
            _entityManager.AddComponentData(_entity, new CollisionCallbacksComponent
            {
                OnCollisionEnter = OnCollisionEnterInternal,
                OnCollisionStay = OnCollisionStayInternal,
                OnCollisionExit = OnCollisionExitInternal
            });
        }

        private void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
                return;
            }
            
            if (_updateAABBFromTransform)
            {
                UpdateCollider();
            }
        }

        public void UpdateCollider()
        {
            if (!_initialized || _entity == Entity.Null || !_entityManager.Exists(_entity))
            {
                TryInitialize();
                return;
            }

            Vector2 position = transform.position;
            Vector2 halfSize = _size * 0.5f;
            Vector2 centerPos = position + _offset;
            
            AABBComponent aabb = new()
            {
                Min = new float2(centerPos.x - halfSize.x, centerPos.y - halfSize.y),
                Max = new float2(centerPos.x + halfSize.x, centerPos.y + halfSize.y)
            };
            
            _entityManager.SetComponentData(_entity, aabb);

            ColliderComponent collider = _entityManager.GetComponentData<ColliderComponent>(_entity);
            collider.AABB = aabb;
            collider.CollisionLayer = (uint)_collisionLayer.value;
            collider.CollisionMask = (uint)_collidesWith.value;
            _entityManager.SetComponentData(_entity, collider);
        }
        
        internal void OnCollisionEnterInternal(Entity other, CollisionInfo info)
        {
            OnCollisionEnter?.Invoke(other, info);
            HandleCollisionEnter(other, info);
        }

        internal void OnCollisionStayInternal(Entity other, CollisionInfo info)
        {
            OnCollisionStay?.Invoke(other, info);
            HandleCollisionStay(other, info);
        }

        internal void OnCollisionExitInternal(Entity other, CollisionInfo info)
        {
            OnCollisionExit?.Invoke(other, info);
            HandleCollisionExit(other, info);
        }
        
        protected virtual void HandleCollisionEnter(Entity other, CollisionInfo info) { }

        protected virtual void HandleCollisionStay(Entity other, CollisionInfo info) { }

        protected virtual void HandleCollisionExit(Entity other, CollisionInfo info) { }
        
        
        public void SetAABBSize(Vector2 newSize)
        {
            _size = newSize;
            UpdateCollider();
        }

        public void SetAABBOffset(Vector2 newOffset)
        {
            _offset = newOffset;
            UpdateCollider();
        }

        public void SetCollisionLayer(LayerMask layer)
        {
            _collisionLayer = layer;
            UpdateCollider();
        }

        public void SetCollidesWith(LayerMask mask)
        {
            _collidesWith = mask;
            UpdateCollider();
        }

        private void OnDrawGizmos()
        {
            Vector2 position = transform.position;
            Vector2 centerPos = position + _offset;

            Gizmos.color = _initialized ? (_isTrigger ? Color.cyan : Color.green) : Color.yellow;
            Gizmos.DrawWireCube(centerPos, _size);
        }

        private void OnDestroy()
        {
            if (_initialized && _entity != Entity.Null && _entityManager.Exists(_entity))
            {
                GlobalCollisionManager.Instance.UnregisterAuthoring(_entity);

                if (_entityManager.HasComponent<CollisionCallbacksComponent>(_entity))
                {
                    var callbacks = _entityManager.GetComponentData<CollisionCallbacksComponent>(_entity);
                    callbacks.Dispose();
                }

                _entityManager.DestroyEntity(_entity);
                _entity = Entity.Null;
            }
        }
    }
}
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSCollisionSystem
{
    public class QuadtreeEntityBehaviour : EntityBehaviour
    {
        [Header("AABB Configuration")]
        [SerializeField] private Vector2 _size = new Vector2(1f, 1f);
        [SerializeField] private Vector2 _offset = Vector2.zero;
        [SerializeField] private bool _updateAABBFromTransform = true;

        private void Start()
        {
            Entity entity = GetOrCreateEntity();
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            entityManager.AddComponentData(entity, new AABBComponent
            {
                Min = float2.zero,
                Max = float2.zero
            });
            
            UpdateAABB();
        }

        private void Update()
        {
            if (_updateAABBFromTransform)
            {
                UpdateAABB();
            }
        }
        
        public void UpdateAABB()
        {
            if (Entity == Entity.Null || World.DefaultGameObjectInjectionWorld == null)
            {
                return;
            }

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            if (!entityManager.HasComponent<AABBComponent>(Entity))
            {
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
            
            entityManager.SetComponentData(Entity, aabb);
        }
        
        public void SetAABBSize(Vector2 newSize)
        {
            _size = newSize;
            UpdateAABB();
        }
        
        public void SetAABBOffset(Vector2 newOffset)
        {
            _offset = newOffset;
            UpdateAABB();
        }

        /// <summary>
        /// Draw the AABB as a gizmo in the editor
        /// </summary>
        private void OnDrawGizmos()
        {
            Vector2 position = transform.position;
            Vector2 centerPos = position + _offset;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(centerPos, _size);
        }
    }
}
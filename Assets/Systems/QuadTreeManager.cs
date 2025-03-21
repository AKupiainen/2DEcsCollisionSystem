using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECSCollisionSystem
{
    public class QuadtreeManager : MonoBehaviour
    {
        [FormerlySerializedAs("worldSize")]
        [Header("Quadtree Settings")]
        [SerializeField] private float _worldSize = 10f;
        [SerializeField] private Vector2 _worldOrigin = new(-5f, -5f);
    
        private EntityManager _entityManager;
        private Entity _rootEntity;
    
        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            SetupQuadtree();
        }
    
        private void SetupQuadtree()
        {
            CreateRootNode();
        }
    
        private void CreateRootNode()
        {
            _rootEntity = _entityManager.CreateEntity(
                ComponentType.ReadOnly<QuadtreeNodeComponent>()
            );
        
            _entityManager.SetComponentData(_rootEntity, new QuadtreeNodeComponent
            {
                Position = new float2(_worldOrigin.x, _worldOrigin.y),
                Size = new float2(_worldSize, _worldSize)
            });
        
            _entityManager.AddBuffer<QuadtreeEntityBuffer>(_rootEntity);
        }
    }
}
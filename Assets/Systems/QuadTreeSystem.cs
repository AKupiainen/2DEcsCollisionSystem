using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace ECSCollisionSystem
{
    public struct NeedsSubdivisionTag : IComponentData { }

    public struct PendingInsertionComponent : IComponentData
    {
        public Entity TargetNode;
    }

    [BurstCompile]
    public partial class QuadtreeSystem : SystemBase
    {
        private EntityQuery _aabbQuery;
        private const int MaxEntitiesPerNode = 4;

        protected override void OnCreate() { }

        protected override void OnUpdate()
        {
            EntityCommandBuffer endSimulationECB = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);
            
            ComponentLookup<QuadtreeNodeComponent> quadtreeNodes = GetComponentLookup<QuadtreeNodeComponent>();
            BufferLookup<QuadtreeEntityBuffer> entityBuffers = GetBufferLookup<QuadtreeEntityBuffer>();
            
            NativeArray<Entity> allNodes = SystemAPI.QueryBuilder().WithAll<QuadtreeNodeComponent>().Build().ToEntityArray(Allocator.Temp);

            Entities.WithoutBurst().ForEach((Entity entity, ref AABBComponent aabb) =>
            {
                InsertEntityIntoQuadtree(entity, aabb, quadtreeNodes, entityBuffers, allNodes, endSimulationECB);
            }).Run();

            allNodes.Dispose();
        }

        private void InsertEntityIntoQuadtree(Entity entity, AABBComponent aabb, 
                                            ComponentLookup<QuadtreeNodeComponent> quadtreeNodes, 
                                            BufferLookup<QuadtreeEntityBuffer> entityBuffers, 
                                            NativeArray<Entity> nodeEntities,
                                            EntityCommandBuffer ecb)
        {
            foreach (Entity nodeEntity in nodeEntities)
            {
                QuadtreeNodeComponent node = quadtreeNodes[nodeEntity];
                
                if (IsAABBInsideNode(aabb, node) && entityBuffers.HasBuffer(nodeEntity))
                {
                    DynamicBuffer<QuadtreeEntityBuffer> buffer = entityBuffers[nodeEntity];
                    
                    if (buffer.Length < MaxEntitiesPerNode)
                    {
                        buffer.Add(new QuadtreeEntityBuffer { Entity = entity });
                    }
                    else
                    {
                        ecb.AddComponent<NeedsSubdivisionTag>(nodeEntity);
                        ecb.AddComponent(entity, new PendingInsertionComponent { TargetNode = nodeEntity });
                    }
                    break;
                }
            }
        }

        private bool IsAABBInsideNode(AABBComponent aabb, QuadtreeNodeComponent node)
        {
            return aabb.Min.x >= node.Position.x && aabb.Max.x <= node.Position.x + node.Size.x &&
                   aabb.Min.y >= node.Position.y && aabb.Max.y <= node.Position.y + node.Size.y;
        }
    }

    [UpdateAfter(typeof(QuadtreeSystem))]
    public partial class QuadtreeSubdivisionSystem : SystemBase
    {
        private const int MaxEntitiesPerNode = 4;
        private EntityQuery _nodeQuery;

        protected override void OnCreate()
        {
            _nodeQuery = SystemAPI.QueryBuilder().WithAll<QuadtreeNodeComponent>().Build();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);
                
            ComponentLookup<QuadtreeNodeComponent> quadtreeNodes = GetComponentLookup<QuadtreeNodeComponent>();
            BufferLookup<QuadtreeEntityBuffer> entityBuffers = GetBufferLookup<QuadtreeEntityBuffer>();
            
            Entities.WithoutBurst().ForEach((Entity nodeEntity, in NeedsSubdivisionTag _) =>
            {
                SubdivideNode(nodeEntity, quadtreeNodes, entityBuffers, ecb);
                ecb.RemoveComponent<NeedsSubdivisionTag>(nodeEntity);
            }).Run();
            
            NativeArray<Entity> allNodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            
            Entities.WithoutBurst().ForEach((Entity entity, in PendingInsertionComponent _, in AABBComponent aabb) =>
            {
                bool inserted = false;

                foreach (var nodeEntity in allNodes)
                {
                    var node = quadtreeNodes[nodeEntity];
                    
                    if (IsAABBInsideNode(aabb, node) && entityBuffers.HasBuffer(nodeEntity))
                    {
                        var buffer = entityBuffers[nodeEntity];
                        
                        if (buffer.Length < MaxEntitiesPerNode)
                        {
                            buffer.Add(new QuadtreeEntityBuffer { Entity = entity });
                            inserted = true;
                            break;
                        }
                    }
                }
                
                if (inserted)
                {
                    ecb.RemoveComponent<PendingInsertionComponent>(entity);
                }
                
            }).Run();
            
            allNodes.Dispose();
        }
        
        private bool IsAABBInsideNode(AABBComponent aabb, QuadtreeNodeComponent node)
        {
            return aabb.Min.x >= node.Position.x && aabb.Max.x <= node.Position.x + node.Size.x &&
                   aabb.Min.y >= node.Position.y && aabb.Max.y <= node.Position.y + node.Size.y;
        }
        
        private void SubdivideNode(Entity nodeEntity, ComponentLookup<QuadtreeNodeComponent> quadtreeNodes, 
                                  BufferLookup<QuadtreeEntityBuffer> entityBuffers, EntityCommandBuffer ecb)
        {
            QuadtreeNodeComponent node = quadtreeNodes[nodeEntity];
            float2 halfSize = node.Size / 2;
            
            for (int i = 0; i < 4; i++)
            {
                QuadtreeNodeComponent childNode = new()
                {
                    Position = CalculateChildPosition(node.Position, halfSize, i),
                    Size = halfSize
                };

                Entity childNodeEntity = ecb.CreateEntity();
                ecb.AddComponent(childNodeEntity, childNode);
                ecb.AddBuffer<QuadtreeEntityBuffer>(childNodeEntity);
            }
            
            if (entityBuffers.HasBuffer(nodeEntity))
            {
                DynamicBuffer<QuadtreeEntityBuffer> buffer = entityBuffers[nodeEntity];
                
                for (int i = 0; i < buffer.Length; i++)
                {
                    ecb.AddComponent(buffer[i].Entity, new PendingInsertionComponent { TargetNode = nodeEntity });
                }
                
                buffer.Clear();
            }
        }

        private float2 CalculateChildPosition(float2 parentPosition, float2 halfSize, int childIndex)
        {
            float xOffset = childIndex % 2 == 0 ? 0 : halfSize.x;
            float yOffset = childIndex < 2 ? 0 : halfSize.y;

            return parentPosition + new float2(xOffset, yOffset);
        }
    }
}
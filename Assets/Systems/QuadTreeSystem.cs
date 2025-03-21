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
    public static class QuadtreeUtils
    {
        [BurstCompile]
        public static bool IsAABBInsideNode(AABBComponent aabb, QuadtreeNodeComponent node)
        {
            return aabb.Min.x >= node.Position.x && aabb.Max.x <= node.Position.x + node.Size.x &&
                   aabb.Min.y >= node.Position.y && aabb.Max.y <= node.Position.y + node.Size.y;
        }

        [BurstCompile]
        public static float2 CalculateChildPosition(float2 parentPosition, float2 halfSize, int childIndex)
        {
            float xOffset = (childIndex % 2) * halfSize.x;
            float yOffset = (childIndex / 2) * halfSize.y;
            return parentPosition + new float2(xOffset, yOffset);
        }
    }

    [BurstCompile]
    public partial class QuadtreeSystem : SystemBase
    {
        private EntityQuery _aabbQuery;
        private const int MaxEntitiesPerNode = 4;

        protected override void OnUpdate()
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            ComponentLookup<QuadtreeNodeComponent> quadtreeNodes = GetComponentLookup<QuadtreeNodeComponent>();
            BufferLookup<QuadtreeEntityBuffer> entityBuffers = GetBufferLookup<QuadtreeEntityBuffer>();
            
            NativeList<Entity> allNodes = new(Allocator.Temp);
            allNodes.CopyFrom(SystemAPI.QueryBuilder().WithAll<QuadtreeNodeComponent>().Build().ToEntityArray(Allocator.Temp));

            Entities.WithoutBurst().ForEach((Entity entity, ref AABBComponent aabb) =>
            {
                foreach (Entity nodeEntity in allNodes)
                {
                    QuadtreeNodeComponent node = quadtreeNodes[nodeEntity];
                    if (QuadtreeUtils.IsAABBInsideNode(aabb, node) && entityBuffers.HasBuffer(nodeEntity))
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
            }).Run();

            allNodes.Dispose();
        }
    }

    [BurstCompile, UpdateAfter(typeof(QuadtreeSystem))]
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
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            ComponentLookup<QuadtreeNodeComponent> quadtreeNodes = GetComponentLookup<QuadtreeNodeComponent>();
            BufferLookup<QuadtreeEntityBuffer> entityBuffers = GetBufferLookup<QuadtreeEntityBuffer>();
            
            NativeList<Entity> allNodes = new(Allocator.Temp);
            allNodes.CopyFrom(_nodeQuery.ToEntityArray(Allocator.Temp));

            Entities.WithoutBurst().ForEach((Entity nodeEntity, in NeedsSubdivisionTag _) =>
            {
                QuadtreeNodeComponent node = quadtreeNodes[nodeEntity];
                float2 halfSize = node.Size / 2;
                
                for (int i = 0; i < 4; i++)
                {
                    Entity childNodeEntity = ecb.CreateEntity();
                    ecb.AddComponent(childNodeEntity, new QuadtreeNodeComponent
                    {
                        Position = QuadtreeUtils.CalculateChildPosition(node.Position, halfSize, i),
                        Size = halfSize
                    });
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
                ecb.RemoveComponent<NeedsSubdivisionTag>(nodeEntity);
            }).Run();

            Entities.WithoutBurst().ForEach((Entity entity, in PendingInsertionComponent pending, in AABBComponent aabb) =>
            {
                if (quadtreeNodes.HasComponent(pending.TargetNode) && entityBuffers.HasBuffer(pending.TargetNode))
                {
                    DynamicBuffer<QuadtreeEntityBuffer> buffer = entityBuffers[pending.TargetNode];
                    if (buffer.Length < MaxEntitiesPerNode)
                    {
                        buffer.Add(new QuadtreeEntityBuffer { Entity = entity });
                        ecb.RemoveComponent<PendingInsertionComponent>(entity);
                        return;
                    }
                }

                foreach (Entity nodeEntity in allNodes)
                {
                    QuadtreeNodeComponent node = quadtreeNodes[nodeEntity];
                    if (QuadtreeUtils.IsAABBInsideNode(aabb, node) && entityBuffers.HasBuffer(nodeEntity))
                    {
                        DynamicBuffer<QuadtreeEntityBuffer> buffer = entityBuffers[nodeEntity];
                        if (buffer.Length < MaxEntitiesPerNode)
                        {
                            buffer.Add(new QuadtreeEntityBuffer { Entity = entity });
                            ecb.RemoveComponent<PendingInsertionComponent>(entity);
                            break;
                        }
                    }
                }
            }).Run();

            allNodes.Dispose();
        }
    }
}
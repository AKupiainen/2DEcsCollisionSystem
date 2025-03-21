using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSCollisionSystem
{
    [BurstCompile, UpdateAfter(typeof(QuadtreeSubdivisionSystem))]
    public partial class GlobalCollisionDetectionSystem : SystemBase
    {
        private EntityQuery _colliderQuery;
        private EntityCommandBuffer _ecb;
        
        protected override void OnCreate()
        {
            _colliderQuery = SystemAPI.QueryBuilder().WithAll<ColliderComponent, CollisionProcessingTag>().Build();
            
            // Make sure entities that need collision events have the buffer
            RequireForUpdate<QuadtreeNodeComponent>();
        }
        
        protected override void OnUpdate()
        {
            _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);
            
            BufferLookup<QuadtreeEntityBuffer> quadtreeBuffers = GetBufferLookup<QuadtreeEntityBuffer>(true);
            BufferLookup<ActiveCollision> activeCollisions = GetBufferLookup<ActiveCollision>();
            ComponentLookup<ColliderComponent> colliders = GetComponentLookup<ColliderComponent>(true);
            ComponentLookup<QuadtreeNodeComponent> nodes = GetComponentLookup<QuadtreeNodeComponent>(true);
            
            // Get all quadtree nodes
            NativeList<Entity> allNodes = new(Allocator.Temp);
            allNodes.CopyFrom(SystemAPI.QueryBuilder().WithAll<QuadtreeNodeComponent>().Build()
                .ToEntityArray(Allocator.Temp));
            
            // Process entities that need collision detection
            Entities.WithoutBurst().ForEach((Entity entity, ref ColliderComponent collider) =>
            {
                // Create active collision buffer if it doesn't exist
                if (!activeCollisions.HasBuffer(entity))
                {
                    _ecb.AddBuffer<ActiveCollision>(entity);
                    return; // Skip this frame, process after buffer is created
                }
                
                // Find the quadtree node this entity belongs to
                Entity containingNode = Entity.Null;
                foreach (Entity nodeEntity in allNodes)
                {
                    if (nodes.HasComponent(nodeEntity) && 
                        QuadtreeUtils.IsAABBInsideNode(collider.AABB, nodes[nodeEntity]))
                    {
                        containingNode = nodeEntity;
                        break;
                    }
                }
                
                if (containingNode == Entity.Null || !quadtreeBuffers.HasBuffer(containingNode))
                {
                    return; 
                }
                
                DynamicBuffer<ActiveCollision> activeCollisionsBuffer = activeCollisions[entity];
                
                DynamicBuffer<QuadtreeEntityBuffer> nodeEntities = quadtreeBuffers[containingNode];
                
                for (int i = 0; i < nodeEntities.Length; i++)
                {
                    Entity otherEntity = nodeEntities[i].Entity;
                    
                    if (otherEntity == entity) continue;
                    
                    if (!colliders.HasComponent(otherEntity)) continue;
                    
                    ColliderComponent otherCollider = colliders[otherEntity];
                    
                    // Check collision layers
                    if ((collider.CollisionLayer & otherCollider.CollisionMask) == 0 &&
                        (otherCollider.CollisionLayer & collider.CollisionMask) == 0)
                    {
                        continue; 
                    }
                    
                    if (CheckAABBCollision(collider.AABB, otherCollider.AABB, out var collisionInfo))
                    {
                        if (GlobalCollisionManager.Instance != null)
                        {
                            CollisionInfo info = new CollisionInfo
                            {
                                ContactPoint = new Vector2(collisionInfo.x, collisionInfo.y),
                                Normal = new Vector2(collisionInfo.z, collisionInfo.w),
                                PenetrationDepth = math.length(new float2(collisionInfo.z, collisionInfo.w))
                            };
                            
                            GlobalCollisionManager.Instance.AddCollision(entity, otherEntity, info);
                        }
                        
                        // Still maintain the ActiveCollision buffer for ECS-only systems
                        bool isNew = true;
                        for (int j = 0; j < activeCollisionsBuffer.Length; j++)
                        {
                            if (activeCollisionsBuffer[j].CollidingEntity == otherEntity)
                            {
                                isNew = false;
                                break;
                            }
                        }
                        
                        if (isNew)
                        {
                            activeCollisionsBuffer.Add(new ActiveCollision
                            {
                                CollidingEntity = otherEntity
                            });
                        }
                    }
                    else
                    {
                        for (int j = 0; j < activeCollisionsBuffer.Length; j++)
                        {
                            if (activeCollisionsBuffer[j].CollidingEntity == otherEntity)
                            {
                                activeCollisionsBuffer.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }).Run();
            
            allNodes.Dispose();
        }
        
        // Enhanced collision check that returns contact information
        private bool CheckAABBCollision(AABBComponent a, AABBComponent b, out float4 collisionInfo)
        {
            collisionInfo = float4.zero;
            
            if (a.Min.x <= b.Max.x && a.Max.x >= b.Min.x &&
                a.Min.y <= b.Max.y && a.Max.y >= b.Min.y)
            {
                float overlapX = math.min(a.Max.x, b.Max.x) - math.max(a.Min.x, b.Min.x);
                float overlapY = math.min(a.Max.y, b.Max.y) - math.max(a.Min.y, b.Min.y);
                
                float2 centerA = (a.Min + a.Max) * 0.5f;
                float2 centerB = (b.Min + b.Max) * 0.5f;
                
                float contactX = math.max(a.Min.x, b.Min.x) + overlapX * 0.5f;
                float contactY = math.max(a.Min.y, b.Min.y) + overlapY * 0.5f;
                
                float2 direction = centerB - centerA;
                float2 normal;
                
                if (overlapX < overlapY)
                {
                    normal = new float2(math.sign(direction.x), 0);
                }
                else
                {
                    normal = new float2(0, math.sign(direction.y));
                }
                
                collisionInfo = new float4(contactX, contactY, normal.x, normal.y);
                return true;
            }
            
            return false;
        }
    }
    
    [UpdateAfter(typeof(GlobalCollisionDetectionSystem))]
    public partial class EndOfFrameCollisionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (GlobalCollisionManager.Instance != null)
            {
                GlobalCollisionManager.Instance.ProcessEndOfFrameCollisions();
            }
        }
    }
}
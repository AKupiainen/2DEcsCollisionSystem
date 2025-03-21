using Unity.Entities;

namespace ECSCollisionSystem
{
    public struct ColliderComponent : IComponentData
    {
        public AABBComponent AABB;  
        public Entity Owner;        
        public uint CollisionLayer;  
        public uint CollisionMask;  
    }
}

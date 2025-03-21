using Unity.Entities;

namespace ECSCollisionSystem
{
    public struct CollisionEnterEvent : IComponentData
    {
        public Entity Entity;      
        public Entity Collider;     
    }
}
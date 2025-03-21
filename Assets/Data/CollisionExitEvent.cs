using Unity.Entities;

namespace ECSCollisionSystem
{
    public struct CollisionExitEvent : IComponentData
    {
        public Entity Entity;        
        public Entity Collider;   
    }
}
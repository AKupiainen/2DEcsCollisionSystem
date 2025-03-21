using Unity.Entities;

namespace ECSCollisionSystem
{
    public struct CollisionComponent : IComponentData
    {
        public Entity CollidedWith;
        public bool HasCollision;
    }
}

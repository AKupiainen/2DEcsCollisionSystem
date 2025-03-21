using Unity.Entities;

namespace ECSCollisionSystem
{
    public struct ActiveCollision : IBufferElementData
    {
        public Entity CollidingEntity;
    }
}
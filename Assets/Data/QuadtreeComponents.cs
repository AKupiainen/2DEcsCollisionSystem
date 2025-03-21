using Unity.Entities;
using Unity.Mathematics;

namespace ECSCollisionSystem
{
    public struct QuadtreeNodeComponent : IComponentData
    {
        public float2 Position;
        public float2 Size;
    }

    public struct QuadtreeEntityBuffer : IBufferElementData
    {
        public Entity Entity;
    }
}
using Unity.Entities;
using Unity.Mathematics;

namespace ECSCollisionSystem
{
    public struct AABBComponent : IComponentData
    {
        public float2 Min;
        public float2 Max;
    }
}
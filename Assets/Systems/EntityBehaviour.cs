using Unity.Entities;
using UnityEngine;

namespace ECSCollisionSystem
{
    public class EntityBehaviour : MonoBehaviour
    {
        protected Entity Entity;

        public Entity GetOrCreateEntity()
        {
            if (Entity != Entity.Null)
            {
                return Entity;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager manager = world.EntityManager;

            Entity = manager.CreateEntity();
            manager.SetName(Entity, name);

            return Entity;
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            
            if (world == null)
            {
                return;
            }

            EntityManager manager = world.EntityManager;
            manager.DestroyEntity(Entity);
        }

        private void OnEnable()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            
            if (world == null)
            {
                return;
            }

            EntityManager manager = world.EntityManager;
            manager.SetEnabled(Entity, true);
        }

        private void OnDisable()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            
            if (world == null)
            {
                return;
            }

            EntityManager manager = world.EntityManager;
            manager.SetEnabled(Entity, false);
        }
    }
}
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ECSCollisionSystem
{
    public delegate void CollisionCallback(Entity other, CollisionInfo info);
    
    public struct CollisionInfo
    {
        public Vector2 ContactPoint;
        public Vector2 Normal;
        public float PenetrationDepth;
    }
    
    public struct CollisionProcessingTag : IComponentData { }
    
    public class CollisionCallbacksComponent : IComponentData, IDisposable
    {
        public Action<Entity, CollisionInfo> OnCollisionEnter;
        public Action<Entity, CollisionInfo> OnCollisionStay;
        public Action<Entity, CollisionInfo> OnCollisionExit;

        public void Dispose()
        {
            OnCollisionEnter = null;
            OnCollisionStay = null;
            OnCollisionExit = null;
        }
    }
    
    public struct CollisionState : IComponentData
    {
        public Entity EntityA;
        public Entity EntityB;
        public float TimeEntered;
        public float LastContact;
        public CollisionInfo Info;
    }
    
    public struct CollisionChangeTag : IComponentData { }
    
    public class GlobalCollisionManager : MonoBehaviour
    {
        private static GlobalCollisionManager _instance;
        public static GlobalCollisionManager Instance => _instance;

        private readonly Dictionary<int, ColliderAuthoring> _entityToAuthoring = new();

        private readonly List<CollisionState> _activeCollisions = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterAuthoring(Entity entity, ColliderAuthoring authoring)
        {
            _entityToAuthoring.TryAdd(entity.Index, authoring);
        }

        public void UnregisterAuthoring(Entity entity)
        {
            if (_entityToAuthoring.ContainsKey(entity.Index))
            {
                _entityToAuthoring.Remove(entity.Index);
            }
        }

        public ColliderAuthoring GetAuthoring(Entity entity)
        {
            return _entityToAuthoring.GetValueOrDefault(entity.Index);
        }

        public void AddCollision(Entity entityA, Entity entityB, CollisionInfo info)
        {
            for (int i = 0; i < _activeCollisions.Count; i++)
            {
                CollisionState collision = _activeCollisions[i];

                if ((collision.EntityA == entityA && collision.EntityB == entityB) ||
                    (collision.EntityA == entityB && collision.EntityB == entityA))
                {
                    collision.LastContact = Time.time;
                    collision.Info = info;
                    _activeCollisions[i] = collision;

                    NotifyStay(entityA, entityB, info);
                    return;
                }
            }
            
            _activeCollisions.Add(new CollisionState
            {
                EntityA = entityA,
                EntityB = entityB,
                TimeEntered = Time.time,
                LastContact = Time.time,
                Info = info
            });
            
            NotifyEnter(entityA, entityB, info);
        }

        public void ProcessEndOfFrameCollisions()
        {
            float currentTime = Time.time;
            float threshold = currentTime - Time.deltaTime;

            for (int i = _activeCollisions.Count - 1; i >= 0; i--)
            {
                CollisionState collision = _activeCollisions[i];

                if (collision.LastContact < threshold)
                {
                    NotifyExit(collision.EntityA, collision.EntityB, collision.Info);
                    _activeCollisions.RemoveAt(i);
                }
            }
        }

        private void NotifyEnter(Entity entityA, Entity entityB, CollisionInfo info)
        {
            ColliderAuthoring authoringA = GetAuthoring(entityA);
            ColliderAuthoring authoringB = GetAuthoring(entityB);

            authoringA.OnCollisionEnterInternal(entityB, info);
            authoringB.OnCollisionEnterInternal(entityA, info);
        }

        private void NotifyStay(Entity entityA, Entity entityB, CollisionInfo info)
        {
            ColliderAuthoring authoringA = GetAuthoring(entityA);
            ColliderAuthoring authoringB = GetAuthoring(entityB);

            authoringA.OnCollisionStayInternal(entityB, info);
            authoringB.OnCollisionStayInternal(entityA, info);
        }

        private void NotifyExit(Entity entityA, Entity entityB, CollisionInfo info)
        {
            ColliderAuthoring authoringA = GetAuthoring(entityA);
            ColliderAuthoring authoringB = GetAuthoring(entityB);

            authoringA.OnCollisionExitInternal(entityB, info);
            authoringB.OnCollisionExitInternal(entityA, info);
        }
    }
}
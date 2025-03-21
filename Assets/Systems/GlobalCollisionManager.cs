using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace ECSCollisionSystem
{
    #region Components and Events

    // Collision callback delegates
    public delegate void CollisionCallback(Entity other, CollisionInfo info);

    // Struct to store collision information
    public struct CollisionInfo
    {
        public Vector2 ContactPoint;
        public Vector2 Normal;
        public float PenetrationDepth;
    }

    // Component to mark entities that need collision processing
    public struct CollisionProcessingTag : IComponentData
    {
    }

    // Component to store collision callbacks (managed)
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

    // Store collision state for each pair
    public struct CollisionState : IComponentData
    {
        public Entity EntityA;
        public Entity EntityB;
        public float TimeEntered;
        public float LastContact;
        public CollisionInfo Info;
    }

    // Tag entities that had collision changes
    public struct CollisionChangeTag : IComponentData
    {
    }

    #endregion
    

    /// <summary>
    /// Singleton manager for tracking collisions globally
    /// </summary>
    public class GlobalCollisionManager : MonoBehaviour
    {
        private static GlobalCollisionManager _instance;
        public static GlobalCollisionManager Instance => _instance;

        // Maps Entity.Index to ColliderAuthoring
        private Dictionary<int, ColliderAuthoring> _entityToAuthoring = new();

        // Stores active collision states
        private List<CollisionState> _activeCollisions = new();

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
            if (!_entityToAuthoring.ContainsKey(entity.Index))
            {
                _entityToAuthoring.Add(entity.Index, authoring);
            }
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
            return _entityToAuthoring.TryGetValue(entity.Index, out var authoring) ? authoring : null;
        }

        public void AddCollision(Entity entityA, Entity entityB, CollisionInfo info)
        {
            // Check if collision already exists
            for (int i = 0; i < _activeCollisions.Count; i++)
            {
                var collision = _activeCollisions[i];

                // If this pair already exists, update the collision
                if ((collision.EntityA == entityA && collision.EntityB == entityB) ||
                    (collision.EntityA == entityB && collision.EntityB == entityA))
                {
                    collision.LastContact = Time.time;
                    collision.Info = info;
                    _activeCollisions[i] = collision;

                    // Notify stay callbacks
                    NotifyStay(entityA, entityB, info);
                    return;
                }
            }

            // New collision
            _activeCollisions.Add(new CollisionState
            {
                EntityA = entityA,
                EntityB = entityB,
                TimeEntered = Time.time,
                LastContact = Time.time,
                Info = info
            });

            // Notify enter callbacks
            NotifyEnter(entityA, entityB, info);
        }

        public void ProcessEndOfFrameCollisions()
        {
            float currentTime = Time.time;

            // Check for expired collisions
            for (int i = _activeCollisions.Count - 1; i >= 0; i--)
            {
                var collision = _activeCollisions[i];

                // If last contact was not this frame, remove the collision
                if (collision.LastContact < currentTime - Time.deltaTime)
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

            authoringA?.OnCollisionEnterInternal(entityB, info);
            authoringB?.OnCollisionEnterInternal(entityA, info);
        }

        private void NotifyStay(Entity entityA, Entity entityB, CollisionInfo info)
        {
            ColliderAuthoring authoringA = GetAuthoring(entityA);
            ColliderAuthoring authoringB = GetAuthoring(entityB);

            authoringA?.OnCollisionStayInternal(entityB, info);
            authoringB?.OnCollisionStayInternal(entityA, info);
        }

        private void NotifyExit(Entity entityA, Entity entityB, CollisionInfo info)
        {
            ColliderAuthoring authoringA = GetAuthoring(entityA);
            var authoringB = GetAuthoring(entityB);

            authoringA?.OnCollisionExitInternal(entityB, info);
            authoringB?.OnCollisionExitInternal(entityA, info);
        }
    }
}
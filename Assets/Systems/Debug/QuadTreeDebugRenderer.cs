using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;

namespace ECSCollisionSystem
{
    public class QuadtreeDebugRenderer : MonoBehaviour
    {
        [FormerlySerializedAs("showQuadtreeNodes")]
        [Header("Visualization Settings")]
        [SerializeField] private bool _showQuadtreeNodes = true;
        [SerializeField] private bool _showContainedEntities = true;
        [SerializeField] private Color _nodeColor = new Color(0f, 1f, 0f, 0.2f);
        [SerializeField] private Color _nodeOutlineColor = new Color(0f, 1f, 0f, 0.8f);
        [SerializeField] private Color _entityColor = new Color(1f, 0f, 0f, 0.7f);
        [SerializeField] private float _entityRadius = 0.2f;
        
        [Header("Labels")]
        [SerializeField] private bool _showNodeLabels = true;
        [SerializeField] private bool _showEntityCounts = true;
        [SerializeField] private Color _labelColor = Color.white;
        [SerializeField] private float _labelSize = 12f;

        private EntityManager _entityManager;
        private EntityQuery _nodeQuery;
        private GUIStyle _labelStyle;
        private Camera _mainCamera;

        private void OnEnable()
        {
            _mainCamera = Camera.main;
            
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _nodeQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<QuadtreeNodeComponent>());
            
            _labelStyle = new GUIStyle();
            _labelStyle.normal.textColor = _labelColor;
            _labelStyle.fontSize = (int)_labelSize;
            _labelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void OnGUI()
        {
            if (!_showNodeLabels && !_showEntityCounts)
            {
                return;
            }

            NativeArray<Entity> nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var nodeEntity in nodes)
            {
                QuadtreeNodeComponent node = _entityManager.GetComponentData<QuadtreeNodeComponent>(nodeEntity);
                float2 worldPos = node.Position;
                float2 size = node.Size;
                
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(new Vector3(
                    worldPos.x + size.x / 2, 
                    worldPos.y + size.y / 2,
                    0));
                
                screenPos.y = Screen.height - screenPos.y;
                
                if (_showNodeLabels)
                {
                    GUI.Label(new Rect(screenPos.x - 50, screenPos.y - 20, 100, 20), 
                        $"({worldPos.x:F1}, {worldPos.y:F1})", _labelStyle);
                }
                
                if (_showEntityCounts && _entityManager.HasComponent<QuadtreeEntityBuffer>(nodeEntity))
                {
                    DynamicBuffer<QuadtreeEntityBuffer> buffer = _entityManager.GetBuffer<QuadtreeEntityBuffer>(nodeEntity);
                    GUI.Label(new Rect(screenPos.x - 50, screenPos.y, 100, 20), 
                        $"Entities: {buffer.Length}", _labelStyle);
                }
            }
            
            nodes.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            
            NativeArray<Entity> nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            
            if (_showQuadtreeNodes)
            {
                DrawQuadtreeNodes(nodes);
            }
            
            if (_showContainedEntities)
            {
                DrawContainedEntities(nodes);
            }
            
            nodes.Dispose();
        }

        private void DrawQuadtreeNodes(NativeArray<Entity> nodes)
        {
            foreach (Entity nodeEntity in nodes)
            {
                QuadtreeNodeComponent node = _entityManager.GetComponentData<QuadtreeNodeComponent>(nodeEntity);
                float2 pos = node.Position;
                float2 size = node.Size;
                
                Gizmos.color = _nodeColor;
                Gizmos.DrawCube(new Vector3(pos.x + size.x / 2, pos.y + size.y / 2, 0), 
                               new Vector3(size.x, size.y, 0.01f));
                
                Gizmos.color = _nodeOutlineColor;
                DrawRectangle(new Vector3(pos.x, pos.y, 0), new Vector3(size.x, size.y, 0));
            }
        }

        private void DrawContainedEntities(NativeArray<Entity> nodes)
        {
            foreach (Entity nodeEntity in nodes)
            {
                if (!_entityManager.HasComponent<QuadtreeEntityBuffer>(nodeEntity))
                {
                    continue;
                }

                DynamicBuffer<QuadtreeEntityBuffer> buffer = _entityManager.GetBuffer<QuadtreeEntityBuffer>(nodeEntity);
                
                foreach (QuadtreeEntityBuffer entityRef in buffer)
                {
                    Entity entity = entityRef.Entity;
                    Gizmos.color = _entityColor;
                    
                    if (_entityManager.HasComponent<LocalTransform>(entity))
                    {
                        float3 position = _entityManager.GetComponentData<LocalTransform>(entity).Position;
                        Gizmos.DrawSphere(new Vector3(position.x, position.y, 0), _entityRadius);
                    }
                    else if (_entityManager.HasComponent<AABBComponent>(entity))
                    {
                        AABBComponent aabb = _entityManager.GetComponentData<AABBComponent>(entity);
                        float2 center = (aabb.Min + aabb.Max) / 2;
                        float2 size = aabb.Max - aabb.Min;
                        
                        Gizmos.DrawCube(new Vector3(center.x, center.y, 0), 
                                      new Vector3(size.x, size.y, 0.01f));
                    }
                }
            }
        }

        private void DrawRectangle(Vector3 position, Vector3 size)
        {
            Vector3 topLeft = position;
            Vector3 topRight = position + new Vector3(size.x, 0, 0);
            Vector3 bottomRight = position + new Vector3(size.x, size.y, 0);
            Vector3 bottomLeft = position + new Vector3(0, size.y, 0);
            
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}

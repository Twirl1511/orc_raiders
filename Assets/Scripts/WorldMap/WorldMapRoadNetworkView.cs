using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapRoadNetworkView : Graphic
{
    [SerializeField] private List<WorldMapNode> _nodes = new List<WorldMapNode>();
    [SerializeField, Min(1f)] private float _lineWidth = 5f;
    [SerializeField] private Color _roadColor = new Color(0.38f, 0.24f, 0.13f, 0.82f);

    public void SetNodes(IReadOnlyList<WorldMapNode> nodes)
    {
        _nodes.Clear();
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && !_nodes.Contains(nodes[i]))
                {
                    _nodes.Add(nodes[i]);
                }
            }
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        for (int i = 0; i < _nodes.Count; i++)
        {
            WorldMapNode node = _nodes[i];
            if (node == null)
            {
                continue;
            }

            IReadOnlyList<WorldMapNode> neighbors = node.Neighbors;
            for (int neighborIndex = 0; neighborIndex < neighbors.Count; neighborIndex++)
            {
                WorldMapNode neighbor = neighbors[neighborIndex];
                int otherIndex = _nodes.IndexOf(neighbor);
                if (neighbor == null || otherIndex < i)
                {
                    continue;
                }

                AddLine(vertexHelper, node.MapPosition, neighbor.MapPosition, _lineWidth, _roadColor);
            }
        }
    }

    private static void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float width, Color32 color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * Mathf.Max(0.5f, width * 0.5f);
        int startIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(start - normal, color, Vector2.zero);
        vertexHelper.AddVert(start + normal, color, Vector2.zero);
        vertexHelper.AddVert(end + normal, color, Vector2.zero);
        vertexHelper.AddVert(end - normal, color, Vector2.zero);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        _lineWidth = Mathf.Max(1f, _lineWidth);
        SetVerticesDirty();
    }
}
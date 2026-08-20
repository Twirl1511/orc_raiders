using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapRouteView : Graphic
{
    [SerializeField, Min(1f)] private float _lineWidth = 7f;
    [SerializeField] private Color _routeColor = new Color(0.98f, 0.83f, 0.28f, 0.95f);

    private readonly List<Vector2> _points = new List<Vector2>();

    public void SetRoute(IReadOnlyList<WorldMapNode> route)
    {
        _points.Clear();
        if (route != null)
        {
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i] != null)
                {
                    _points.Add(route[i].MapPosition);
                }
            }
        }

        SetVerticesDirty();
    }

    public void ClearRoute()
    {
        _points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        for (int i = 0; i < _points.Count - 1; i++)
        {
            AddLine(vertexHelper, _points[i], _points[i + 1], _lineWidth, _routeColor);
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
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMapNode : MonoBehaviour
{
    [SerializeField] private string _id = "node";
    [SerializeField] private string _displayName = "Node";
    [SerializeField] private List<WorldMapNode> _neighbors = new List<WorldMapNode>();

    public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    public IReadOnlyList<WorldMapNode> Neighbors => _neighbors;

    public Vector2 MapPosition
    {
        get
        {
            RectTransform rectTransform = transform as RectTransform;
            return rectTransform != null ? rectTransform.anchoredPosition : (Vector2)transform.localPosition;
        }
    }

    public bool HasNeighbor(WorldMapNode neighbor)
    {
        return neighbor != null && _neighbors.Contains(neighbor);
    }

    public float GetDistanceTo(WorldMapNode other)
    {
        return other == null ? 0f : Vector2.Distance(MapPosition, other.MapPosition);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            _id = name;
        }

        for (int i = _neighbors.Count - 1; i >= 0; i--)
        {
            if (_neighbors[i] == null || _neighbors[i] == this || _neighbors.IndexOf(_neighbors[i]) != i)
            {
                _neighbors.RemoveAt(i);
            }
        }
    }
}
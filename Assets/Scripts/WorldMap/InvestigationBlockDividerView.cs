using UnityEngine;
using UnityEngine.UI;

public sealed class InvestigationBlockDividerView : Graphic
{
    [SerializeField, Min(1)] private int _blockCount = 1;
    [SerializeField, Min(1f)] private float _dividerWidth = 2f;

    public void SetBlockCount(int blockCount)
    {
        int clampedBlockCount = Mathf.Max(1, blockCount);
        if (_blockCount == clampedBlockCount)
        {
            return;
        }

        _blockCount = clampedBlockCount;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (_blockCount <= 1)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float halfWidth = Mathf.Max(0.5f, _dividerWidth * 0.5f);

        for (int i = 1; i < _blockCount; i++)
        {
            float normalizedPosition = i / (float)_blockCount;
            float x = Mathf.Lerp(rect.xMin, rect.xMax, normalizedPosition);
            AddQuad(vertexHelper, x - halfWidth, rect.yMin, x + halfWidth, rect.yMax, color);
        }
    }

    private static void AddQuad(VertexHelper vertexHelper, float xMin, float yMin, float xMax, float yMax, Color32 quadColor)
    {
        int startIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector3(xMin, yMin), quadColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(xMin, yMax), quadColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(xMax, yMax), quadColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(xMax, yMin), quadColor, Vector2.zero);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        _blockCount = Mathf.Max(1, _blockCount);
        _dividerWidth = Mathf.Max(1f, _dividerWidth);
    }
}

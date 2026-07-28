using UnityEngine;

[CreateAssetMenu(fileName = "Tooltips", menuName = "GAME/Tooltips")]
public sealed class TooltipConfig : ScriptableObject
{
    [SerializeField, Min(0f)] private float _showHideSeconds = 0.12f;
    [SerializeField, Range(0.01f, 1f)] private float _collapsedScale = 0.85f;
    [SerializeField, Min(0f)] private float _scrollScaleStep = 0.1f;
    [SerializeField, Min(1f)] private float _maxScale = 1.6f;

    public float ShowHideSeconds => Mathf.Max(0f, _showHideSeconds);
    public float CollapsedScale => Mathf.Clamp(_collapsedScale, 0.01f, 1f);
    public float ScrollScaleStep => Mathf.Max(0f, _scrollScaleStep);
    public float MinScale => 1f;
    public float MaxScale => Mathf.Max(MinScale, _maxScale);

    public bool ValidateForRuntime()
    {
        return _showHideSeconds >= 0f &&
            _collapsedScale > 0f &&
            _collapsedScale <= 1f &&
            _scrollScaleStep >= 0f &&
            _maxScale >= MinScale;
    }

    private void OnValidate()
    {
        _showHideSeconds = Mathf.Max(0f, _showHideSeconds);
        _collapsedScale = Mathf.Clamp(_collapsedScale, 0.01f, 1f);
        _scrollScaleStep = Mathf.Max(0f, _scrollScaleStep);
        _maxScale = Mathf.Max(MinScale, _maxScale);
    }
}

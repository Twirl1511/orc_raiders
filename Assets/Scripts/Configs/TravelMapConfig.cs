using UnityEngine;

[CreateAssetMenu(fileName = "Travel Map", menuName = "GAME/Travel Map")]
public sealed class TravelMapConfig : ScriptableObject
{
    [Header("Travel")]
    [SerializeField, Min(0.01f)] private float _partyTravelUnitsPerSecond = 45f;
    [SerializeField, Min(0.01f)] private float _minimumSegmentDistance = 0.01f;

    [Header("Zoom")]
    [SerializeField, Min(0.1f)] private float _minimumZoom = 0.6f;
    [SerializeField, Min(0.1f)] private float _maximumZoom = 2.2f;
    [SerializeField, Min(0.01f)] private float _zoomButtonStep = 0.2f;
    [SerializeField, Min(0.01f)] private float _scrollZoomStep = 0.15f;

    [Header("Pan")]
    [SerializeField, Min(0f)] private float _panStartThresholdPixels = 4f;
    [SerializeField, Min(0f)] private float _panOverscrollPixels = 160f;

    [Header("Labels")]
    [SerializeField] private string _mapUnitName = "лига";

    public float PartyTravelUnitsPerSecond => Mathf.Max(0.01f, _partyTravelUnitsPerSecond);
    public float MinimumSegmentDistance => Mathf.Max(0.01f, _minimumSegmentDistance);
    public float MinimumZoom => Mathf.Max(0.1f, _minimumZoom);
    public float MaximumZoom => Mathf.Max(MinimumZoom, _maximumZoom);
    public float ZoomButtonStep => Mathf.Max(0.01f, _zoomButtonStep);
    public float ScrollZoomStep => Mathf.Max(0.01f, _scrollZoomStep);
    public float PanStartThresholdPixels => Mathf.Max(0f, _panStartThresholdPixels);
    public float PanOverscrollPixels => Mathf.Max(0f, _panOverscrollPixels);
    public string MapUnitName => string.IsNullOrWhiteSpace(_mapUnitName) ? "unit" : _mapUnitName;

    public bool ValidateForRuntime(Object context)
    {
        bool valid = true;

        if (_partyTravelUnitsPerSecond <= 0f)
        {
            Debug.LogError($"{nameof(TravelMapConfig)} party travel speed must be greater than 0.", context);
            valid = false;
        }

        if (_minimumSegmentDistance <= 0f)
        {
            Debug.LogError($"{nameof(TravelMapConfig)} minimum segment distance must be greater than 0.", context);
            valid = false;
        }

        if (_maximumZoom < _minimumZoom)
        {
            Debug.LogError($"{nameof(TravelMapConfig)} maximum zoom must be greater than or equal to minimum zoom.", context);
            valid = false;
        }

        if (_zoomButtonStep <= 0f || _scrollZoomStep <= 0f)
        {
            Debug.LogError($"{nameof(TravelMapConfig)} zoom steps must be greater than 0.", context);
            valid = false;
        }

        return valid;
    }

    private void OnValidate()
    {
        _partyTravelUnitsPerSecond = Mathf.Max(0.01f, _partyTravelUnitsPerSecond);
        _minimumSegmentDistance = Mathf.Max(0.01f, _minimumSegmentDistance);
        _minimumZoom = Mathf.Max(0.1f, _minimumZoom);
        _maximumZoom = Mathf.Max(_minimumZoom, _maximumZoom);
        _zoomButtonStep = Mathf.Max(0.01f, _zoomButtonStep);
        _scrollZoomStep = Mathf.Max(0.01f, _scrollZoomStep);
        _panStartThresholdPixels = Mathf.Max(0f, _panStartThresholdPixels);
        _panOverscrollPixels = Mathf.Max(0f, _panOverscrollPixels);
    }
}

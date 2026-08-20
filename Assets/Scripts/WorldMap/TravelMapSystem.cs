using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TravelMapSystem : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TravelMapConfig _config = null;

    [Header("Scene References")]
    [SerializeField] private GameObject _mapPanel = null;
    [SerializeField] private RectTransform _mapContent = null;
    [SerializeField] private Button _openMapButton = null;
    [SerializeField] private Button _closeMapButton = null;
    [SerializeField] private Button _zoomInButton = null;
    [SerializeField] private Button _zoomOutButton = null;
    [SerializeField] private Button _resetZoomButton = null;
    [SerializeField] private TextMeshProUGUI _statusText = null;
    [SerializeField] private TextMeshProUGUI _distanceText = null;
    [SerializeField] private TextMeshProUGUI _zoomText = null;
    [SerializeField] private WorldMapLocation _tavernLocation = null;
    [SerializeField] private WorldMapLocation[] _questLocations = new WorldMapLocation[0];
    [SerializeField] private WorldMapNode[] _mapNodes = new WorldMapNode[0];
    [SerializeField] private WorldMapRoadNetworkView _roadNetworkView = null;
    [SerializeField] private WorldMapRouteView _routeView = null;
    [SerializeField] private RectTransform _partyMarker = null;

    private readonly List<WorldMapNode> _activePath = new List<WorldMapNode>();
    private WorldMapLocation _activeDestination;
    private int _activeSegmentIndex;
    private float _activeSegmentDistance;
    private float _currentZoom = 1f;
    private RectTransform _mapViewport;
    private Camera _mapUiCamera;
    private Vector2 _mapContentBaseAnchoredPosition;
    private Vector2 _panStartScreenPosition;
    private Vector2 _lastPanLocalPosition;
    private bool _isPointerDownOnMap;
    private bool _isPanningMap;
    private bool _suppressLocationClick;
    private bool _isTraveling;
    private bool _initialized;

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        HandleMapPanInput();
        HandleZoomInput();

        if (_isTraveling)
        {
            AdvanceParty(Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        UnsubscribeButtons();
    }

    public void OpenMap()
    {
        Initialize();

        if (_mapPanel != null)
        {
            _mapPanel.SetActive(true);
        }

        RefreshStatusForIdleMap();
        RefreshRoads();
    }

    public void CloseMap()
    {
        if (_mapPanel != null)
        {
            _mapPanel.SetActive(false);
        }

        StopMapPan();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();
        InitializeMapViewport();
        _initialized = true;
        SubscribeButtons();
        InitializeZoom();
        RefreshRoads();
        PlacePartyAtTavern();
        RefreshStatusForIdleMap();

        if (_mapPanel != null)
        {
            _mapPanel.SetActive(false);
        }
    }

    private void ValidateReferences()
    {
        if (_config == null || !_config.ValidateForRuntime(this))
        {
            throw new System.InvalidOperationException($"{nameof(TravelMapSystem)} requires valid {nameof(TravelMapConfig)}.");
        }

        if (_mapPanel == null || _mapContent == null || _openMapButton == null || _closeMapButton == null ||
            _zoomInButton == null || _zoomOutButton == null || _resetZoomButton == null || _statusText == null ||
            _distanceText == null || _zoomText == null || _tavernLocation == null || _partyMarker == null ||
            _roadNetworkView == null || _routeView == null)
        {
            throw new System.InvalidOperationException($"{nameof(TravelMapSystem)} requires scene references.");
        }

        if (_tavernLocation.Node == null)
        {
            throw new System.InvalidOperationException($"{nameof(TravelMapSystem)} tavern location requires a map node.");
        }

        if (_mapNodes == null || _mapNodes.Length == 0)
        {
            throw new System.InvalidOperationException($"{nameof(TravelMapSystem)} requires map nodes.");
        }
    }

    private void InitializeMapViewport()
    {
        _mapViewport = _mapContent.parent as RectTransform;
        if (_mapViewport == null)
        {
            throw new System.InvalidOperationException($"{nameof(TravelMapSystem)} map content requires a RectTransform parent.");
        }

        Canvas canvas = _mapContent.GetComponentInParent<Canvas>();
        _mapUiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        _mapContentBaseAnchoredPosition = _mapContent.anchoredPosition;
    }

    private void SubscribeButtons()
    {
        _openMapButton.onClick.RemoveListener(OpenMap);
        _openMapButton.onClick.AddListener(OpenMap);
        _closeMapButton.onClick.RemoveListener(CloseMap);
        _closeMapButton.onClick.AddListener(CloseMap);
        _zoomInButton.onClick.RemoveListener(ZoomIn);
        _zoomInButton.onClick.AddListener(ZoomIn);
        _zoomOutButton.onClick.RemoveListener(ZoomOut);
        _zoomOutButton.onClick.AddListener(ZoomOut);
        _resetZoomButton.onClick.RemoveListener(ResetZoom);
        _resetZoomButton.onClick.AddListener(ResetZoom);

        for (int i = 0; i < _questLocations.Length; i++)
        {
            if (_questLocations[i] == null)
            {
                continue;
            }

            _questLocations[i].Clicked -= HandleLocationClicked;
            _questLocations[i].Clicked += HandleLocationClicked;
            _questLocations[i].RefreshLabel();
        }
    }

    private void UnsubscribeButtons()
    {
        if (_openMapButton != null)
        {
            _openMapButton.onClick.RemoveListener(OpenMap);
        }

        if (_closeMapButton != null)
        {
            _closeMapButton.onClick.RemoveListener(CloseMap);
        }

        if (_zoomInButton != null)
        {
            _zoomInButton.onClick.RemoveListener(ZoomIn);
        }

        if (_zoomOutButton != null)
        {
            _zoomOutButton.onClick.RemoveListener(ZoomOut);
        }

        if (_resetZoomButton != null)
        {
            _resetZoomButton.onClick.RemoveListener(ResetZoom);
        }

        if (_questLocations == null)
        {
            return;
        }

        for (int i = 0; i < _questLocations.Length; i++)
        {
            if (_questLocations[i] != null)
            {
                _questLocations[i].Clicked -= HandleLocationClicked;
            }
        }
    }

    private void HandleLocationClicked(WorldMapLocation location)
    {
        if (_suppressLocationClick)
        {
            return;
        }

        if (location == null || !location.CanStartQuest)
        {
            return;
        }

        StartPartyTravel(location);
    }

    public void ZoomIn()
    {
        SetZoom(_currentZoom + _config.ZoomButtonStep);
    }

    public void ZoomOut()
    {
        SetZoom(_currentZoom - _config.ZoomButtonStep);
    }

    public void ResetZoom()
    {
        SetZoom(1f);
    }

    private void HandleMapPanInput()
    {
        if (_mapPanel == null || !_mapPanel.activeInHierarchy)
        {
            StopMapPan();
            return;
        }

        if (!TryReadPointerState(out Vector2 pointerPosition, out bool pressedThisFrame, out bool isHeld, out bool releasedThisFrame))
        {
            StopMapPan();
            return;
        }

        if (pressedThisFrame)
        {
            _suppressLocationClick = false;
            _isPanningMap = false;

            if (!CanStartMapPan(pointerPosition) || !TryScreenToMapLocalPoint(pointerPosition, out _lastPanLocalPosition))
            {
                _isPointerDownOnMap = false;
                return;
            }

            _isPointerDownOnMap = true;
            _panStartScreenPosition = pointerPosition;
            return;
        }

        if (!_isPointerDownOnMap)
        {
            return;
        }

        if (releasedThisFrame || !isHeld)
        {
            StopMapPan();
            return;
        }

        if (!TryScreenToMapLocalPoint(pointerPosition, out Vector2 currentLocalPosition))
        {
            return;
        }

        if (!_isPanningMap)
        {
            float panThreshold = _config.PanStartThresholdPixels;
            if ((pointerPosition - _panStartScreenPosition).sqrMagnitude < panThreshold * panThreshold)
            {
                return;
            }

            _isPanningMap = true;
            _suppressLocationClick = true;
        }

        Vector2 delta = currentLocalPosition - _lastPanLocalPosition;
        _lastPanLocalPosition = currentLocalPosition;

        if (delta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _mapContent.anchoredPosition += delta;
        ClampMapPan();
    }

    private void HandleZoomInput()
    {
        if (_mapPanel == null || !_mapPanel.activeInHierarchy)
        {
            return;
        }

        float scroll = ReadScrollDelta();
        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        SetZoom(_currentZoom + scroll * _config.ScrollZoomStep);
    }

    private void StopMapPan()
    {
        _isPointerDownOnMap = false;
        _isPanningMap = false;
    }

    private bool CanStartMapPan(Vector2 pointerPosition)
    {
        if (_mapViewport == null || !RectTransformUtility.RectangleContainsScreenPoint(_mapViewport, pointerPosition, _mapUiCamera))
        {
            return false;
        }

        return !IsPointerInsideBlockingControl(pointerPosition);
    }

    private bool IsPointerInsideBlockingControl(Vector2 pointerPosition)
    {
        if (IsPointerInsideRect(_closeMapButton != null ? _closeMapButton.transform as RectTransform : null, pointerPosition) ||
            IsPointerInsideRect(_zoomInButton != null ? _zoomInButton.transform as RectTransform : null, pointerPosition) ||
            IsPointerInsideRect(_zoomOutButton != null ? _zoomOutButton.transform as RectTransform : null, pointerPosition) ||
            IsPointerInsideRect(_resetZoomButton != null ? _resetZoomButton.transform as RectTransform : null, pointerPosition) ||
            IsPointerInsideRect(_zoomText != null ? _zoomText.transform.parent as RectTransform : null, pointerPosition))
        {
            return true;
        }

        return false;
    }

    private bool IsPointerInsideRect(RectTransform rectTransform, Vector2 pointerPosition)
    {
        return rectTransform != null &&
            rectTransform.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(rectTransform, pointerPosition, _mapUiCamera);
    }

    private bool TryScreenToMapLocalPoint(Vector2 pointerPosition, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        return _mapViewport != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_mapViewport, pointerPosition, _mapUiCamera, out localPosition);
    }

    private void ClampMapPan()
    {
        if (_mapContent == null || _mapViewport == null)
        {
            return;
        }

        Vector2 contentSize = Vector2.Scale(_mapContent.rect.size, _mapContent.localScale);
        Vector2 viewportSize = _mapViewport.rect.size;
        float horizontalLimit = Mathf.Max(0f, (contentSize.x - viewportSize.x) * 0.5f) + _config.PanOverscrollPixels;
        float verticalLimit = Mathf.Max(0f, (contentSize.y - viewportSize.y) * 0.5f) + _config.PanOverscrollPixels;
        Vector2 offset = _mapContent.anchoredPosition - _mapContentBaseAnchoredPosition;
        offset.x = Mathf.Clamp(offset.x, -horizontalLimit, horizontalLimit);
        offset.y = Mathf.Clamp(offset.y, -verticalLimit, verticalLimit);
        _mapContent.anchoredPosition = _mapContentBaseAnchoredPosition + offset;
    }

    private static bool TryReadPointerState(
        out Vector2 position,
        out bool pressedThisFrame,
        out bool isHeld,
        out bool releasedThisFrame)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            position = Vector2.zero;
            pressedThisFrame = false;
            isHeld = false;
            releasedThisFrame = false;
            return false;
        }

        position = mouse.position.ReadValue();
        pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
        isHeld = mouse.leftButton.isPressed;
        releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        pressedThisFrame = Input.GetMouseButtonDown(0);
        isHeld = Input.GetMouseButton(0);
        releasedThisFrame = Input.GetMouseButtonUp(0);
        return true;
#else
        position = Vector2.zero;
        pressedThisFrame = false;
        isHeld = false;
        releasedThisFrame = false;
        return false;
#endif
    }

    private static float ReadScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return 0f;
        }

        float scroll = mouse.scroll.ReadValue().y;
        return Mathf.Abs(scroll) > 1f ? scroll / 120f : scroll;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta.y;
#else
        return 0f;
#endif
    }

    private void InitializeZoom()
    {
        float initialZoom = _mapContent != null ? _mapContent.localScale.x : 1f;
        SetZoom(Mathf.Approximately(initialZoom, 0f) ? 1f : initialZoom);
    }

    private void SetZoom(float zoom)
    {
        _currentZoom = Mathf.Clamp(zoom, _config.MinimumZoom, _config.MaximumZoom);

        if (_mapContent != null)
        {
            _mapContent.localScale = Vector3.one * _currentZoom;
            ClampMapPan();
        }

        RefreshZoomState();
    }

    private void RefreshZoomState()
    {
        if (_zoomText != null)
        {
            _zoomText.text = $"{_currentZoom * 100f:0}%";
        }

        if (_zoomInButton != null)
        {
            _zoomInButton.interactable = _currentZoom < _config.MaximumZoom - 0.001f;
        }

        if (_zoomOutButton != null)
        {
            _zoomOutButton.interactable = _currentZoom > _config.MinimumZoom + 0.001f;
        }
    }

    private void StartPartyTravel(WorldMapLocation destination)
    {
        if (destination == null || destination.Node == null)
        {
            return;
        }

        if (!TryFindPath(_tavernLocation.Node, destination.Node, _activePath))
        {
            _statusText.text = $"Путь к '{destination.DisplayName}' не найден. Проверь дороги на карте.";
            _distanceText.text = "Маршрут: -";
            _routeView.ClearRoute();
            return;
        }

        _activeDestination = destination;
        _activeSegmentIndex = 0;
        _activeSegmentDistance = 0f;
        _isTraveling = _activePath.Count > 1;
        _routeView.SetRoute(_activePath);
        SetPartyMarkerPosition(_activePath[0].MapPosition);

        float totalDistance = CalculatePathDistance(_activePath);
        _statusText.text = $"Партия вышла из таверны: {destination.DisplayName}.";
        _distanceText.text = $"Дистанция: {FormatDistance(totalDistance)}. В пути: {FormatSeconds(totalDistance / _config.PartyTravelUnitsPerSecond)}.";

        if (!_isTraveling)
        {
            CompleteTravel();
        }
    }

    private void AdvanceParty(float deltaTime)
    {
        if (deltaTime <= 0f || _activePath.Count < 2)
        {
            return;
        }

        float remainingDistance = _config.PartyTravelUnitsPerSecond * deltaTime;
        while (remainingDistance > 0f && _activeSegmentIndex < _activePath.Count - 1)
        {
            WorldMapNode from = _activePath[_activeSegmentIndex];
            WorldMapNode to = _activePath[_activeSegmentIndex + 1];
            float segmentLength = Mathf.Max(_config.MinimumSegmentDistance, from.GetDistanceTo(to));
            float distanceToSegmentEnd = segmentLength - _activeSegmentDistance;

            if (remainingDistance < distanceToSegmentEnd)
            {
                _activeSegmentDistance += remainingDistance;
                float progress = Mathf.Clamp01(_activeSegmentDistance / segmentLength);
                SetPartyMarkerPosition(Vector2.Lerp(from.MapPosition, to.MapPosition, progress));
                RefreshTravelStatus();
                return;
            }

            remainingDistance -= distanceToSegmentEnd;
            _activeSegmentIndex++;
            _activeSegmentDistance = 0f;
            SetPartyMarkerPosition(to.MapPosition);
        }

        CompleteTravel();
    }

    private void CompleteTravel()
    {
        _isTraveling = false;

        if (_activeDestination != null)
        {
            _statusText.text = $"Партия добралась: {_activeDestination.DisplayName}.";
            _distanceText.text = "Маршрут завершен.";
        }
    }

    private void RefreshTravelStatus()
    {
        if (_activeDestination == null || _activePath.Count < 2)
        {
            return;
        }

        float traveled = CalculateTraveledDistance();
        float total = CalculatePathDistance(_activePath);
        float remaining = Mathf.Max(0f, total - traveled);
        _statusText.text = $"Партия идет к точке: {_activeDestination.DisplayName}.";
        _distanceText.text = $"Осталось: {FormatDistance(remaining)} / {FormatDistance(total)}.";
    }

    private void RefreshStatusForIdleMap()
    {
        if (_isTraveling)
        {
            RefreshTravelStatus();
            return;
        }

        if (_activeDestination != null)
        {
            _statusText.text = $"Последняя цель: {_activeDestination.DisplayName}.";
            return;
        }

        _statusText.text = "Выбери точку квеста на карте.";
        _distanceText.text = "Маршрут: -";
    }

    private void RefreshRoads()
    {
        _roadNetworkView.SetNodes(_mapNodes);
    }

    private void PlacePartyAtTavern()
    {
        if (_tavernLocation != null && _tavernLocation.Node != null)
        {
            SetPartyMarkerPosition(_tavernLocation.Node.MapPosition);
        }
    }

    private void SetPartyMarkerPosition(Vector2 mapPosition)
    {
        if (_partyMarker != null)
        {
            _partyMarker.anchoredPosition = mapPosition;
        }
    }

    private float CalculateTraveledDistance()
    {
        float distance = 0f;
        for (int i = 0; i < _activeSegmentIndex && i < _activePath.Count - 1; i++)
        {
            distance += Mathf.Max(_config.MinimumSegmentDistance, _activePath[i].GetDistanceTo(_activePath[i + 1]));
        }

        return distance + _activeSegmentDistance;
    }

    private float CalculatePathDistance(IReadOnlyList<WorldMapNode> path)
    {
        float distance = 0f;
        if (path == null)
        {
            return distance;
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            distance += Mathf.Max(_config.MinimumSegmentDistance, path[i].GetDistanceTo(path[i + 1]));
        }

        return distance;
    }

    private string FormatDistance(float distance)
    {
        return $"{distance:0.#} {_config.MapUnitName}";
    }

    private static string FormatSeconds(float seconds)
    {
        return $"{Mathf.Max(0f, seconds):0.#} сек.";
    }

    private bool TryFindPath(WorldMapNode start, WorldMapNode target, List<WorldMapNode> result)
    {
        result.Clear();
        if (start == null || target == null)
        {
            return false;
        }

        List<WorldMapNode> open = new List<WorldMapNode>();
        HashSet<WorldMapNode> closed = new HashSet<WorldMapNode>();
        Dictionary<WorldMapNode, float> distances = new Dictionary<WorldMapNode, float>();
        Dictionary<WorldMapNode, WorldMapNode> previous = new Dictionary<WorldMapNode, WorldMapNode>();

        open.Add(start);
        distances[start] = 0f;

        while (open.Count > 0)
        {
            WorldMapNode current = GetOpenNodeWithLowestDistance(open, distances);
            if (current == target)
            {
                BuildPath(target, previous, result);
                return true;
            }

            open.Remove(current);
            closed.Add(current);

            IReadOnlyList<WorldMapNode> neighbors = current.Neighbors;
            for (int i = 0; i < neighbors.Count; i++)
            {
                WorldMapNode neighbor = neighbors[i];
                if (neighbor == null || closed.Contains(neighbor))
                {
                    continue;
                }

                float candidateDistance = distances[current] + Mathf.Max(_config.MinimumSegmentDistance, current.GetDistanceTo(neighbor));
                if (!distances.TryGetValue(neighbor, out float knownDistance) || candidateDistance < knownDistance)
                {
                    distances[neighbor] = candidateDistance;
                    previous[neighbor] = current;
                    if (!open.Contains(neighbor))
                    {
                        open.Add(neighbor);
                    }
                }
            }
        }

        return false;
    }

    private static WorldMapNode GetOpenNodeWithLowestDistance(List<WorldMapNode> open, Dictionary<WorldMapNode, float> distances)
    {
        WorldMapNode selected = open[0];
        float selectedDistance = distances.TryGetValue(selected, out float distance) ? distance : float.PositiveInfinity;

        for (int i = 1; i < open.Count; i++)
        {
            WorldMapNode candidate = open[i];
            float candidateDistance = distances.TryGetValue(candidate, out float currentDistance) ? currentDistance : float.PositiveInfinity;
            if (candidateDistance < selectedDistance)
            {
                selected = candidate;
                selectedDistance = candidateDistance;
            }
        }

        return selected;
    }

    private static void BuildPath(WorldMapNode target, Dictionary<WorldMapNode, WorldMapNode> previous, List<WorldMapNode> result)
    {
        WorldMapNode current = target;
        while (current != null)
        {
            result.Add(current);
            previous.TryGetValue(current, out current);
        }

        result.Reverse();
    }
}

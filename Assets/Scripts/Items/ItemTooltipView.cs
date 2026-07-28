using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemTooltipView : MonoBehaviour
{
    private const int _topLeftCornerIndex = 1;
    private const int _topRightCornerIndex = 2;

    [Header("Scene UI")]
    [SerializeField] private RectTransform _panel = null;
    [SerializeField] private CanvasGroup _canvasGroup = null;
    [SerializeField] private Image _icon = null;
    [SerializeField] private RectTransform _iconRect = null;
    [SerializeField] private TextMeshProUGUI _titleText = null;
    [SerializeField] private TextMeshProUGUI _bodyText = null;

    [Header("Layout")]
    [SerializeField, Min(220f)] private float _width = 320f;
    [SerializeField, Min(0f)] private float _padding = 12f;
    [SerializeField, Min(0f)] private float _gap = 8f;
    [SerializeField, Min(24f)] private float _iconSize = 64f;
    [SerializeField, Min(0f)] private float _anchorOffset = 8f;
    [SerializeField, Min(0f)] private float _edgePadding = 12f;

    private readonly Vector3[] _anchorWorldCorners = new Vector3[4];
    private Canvas _canvas;
    private StatsConfig _statsConfig;
    private TooltipConfig _tooltipConfig;
    private Coroutine _animation;
    private RectTransform _currentAnchor;
    private Vector2 _currentBaseSize;
    private ItemRuntimeData _shownItem;
    private float _preferredScale = 1f;
    private float _currentScale = 1f;

    private void Awake()
    {
        ForceHide();
    }

    private void OnDisable()
    {
        ForceHide();
    }

    public void Configure(Canvas canvas, StatsConfig statsConfig, TooltipConfig tooltipConfig)
    {
        _canvas = canvas;
        _statsConfig = statsConfig;
        _tooltipConfig = tooltipConfig;
        _preferredScale = ClampTooltipScale(_preferredScale);
        ForceHide();
    }

    public void ShowItem(ItemRuntimeData item, RectTransform anchor)
    {
        ItemDefinition definition = item != null ? item.Definition : null;

        if (definition == null || anchor == null || !HasRequiredReferences())
        {
            Hide();
            return;
        }

        StopAnimation();
        _shownItem = item;
        _currentAnchor = anchor;
        _currentScale = ClampTooltipScale(_preferredScale);
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();

        if (_icon != null)
        {
            _icon.sprite = definition.Icon;
            _icon.enabled = definition.Icon != null;
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
        }

        _titleText.text = definition.DisplayName;
        _bodyText.text = ItemDescriptionFormatter.BuildDetailsText(definition, _statsConfig);
        PrepareText(_titleText);
        PrepareText(_bodyText);

        _currentBaseSize = ResizeToContent();
        PrepareVisibleCanvasGroup(0f);
        _panel.localScale = Vector3.one * GetCollapsedScale();
        PositionNearAnchor(anchor, _currentBaseSize);
        StartVisibilityAnimation(0f, 1f, GetCollapsedScale(), _currentScale, false);
    }

    public void HideItem(ItemRuntimeData item)
    {
        if (_shownItem == item)
        {
            Hide();
        }
    }

    public void Hide()
    {
        _shownItem = null;
        _currentAnchor = null;

        if (!Application.isPlaying || !isActiveAndEnabled || _panel == null || _canvasGroup == null ||
            _canvasGroup.alpha <= 0f)
        {
            ForceHide();
            return;
        }

        StopAnimation();
        StartVisibilityAnimation(_canvasGroup.alpha, 0f, _panel.localScale.x, GetCollapsedScale(), true);
    }

    public void AdjustScale(float scrollDelta)
    {
        if (_shownItem == null || _currentAnchor == null || _panel == null || Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        float step = GetScrollScaleStep();

        if (step <= 0f)
        {
            return;
        }

        float direction = scrollDelta > 0f ? 1f : -1f;
        float nextScale = Mathf.Clamp(_currentScale + direction * step, GetMinScale(), GetMaxScale());

        if (Mathf.Approximately(nextScale, _currentScale))
        {
            return;
        }

        StopAnimation();
        _currentScale = nextScale;
        _preferredScale = nextScale;
        PrepareVisibleCanvasGroup(1f);
        _panel.localScale = Vector3.one * _currentScale;
        PositionNearAnchor(_currentAnchor, _currentBaseSize);
    }

    private void ForceHide()
    {
        StopAnimation();
        _shownItem = null;
        _currentAnchor = null;
        _currentScale = ClampTooltipScale(_preferredScale);

        ApplyHiddenVisuals(true);
    }

    private void ApplyHiddenVisuals(bool clearContent)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_panel != null)
        {
            _panel.localScale = Vector3.one;
        }

        if (!clearContent)
        {
            return;
        }

        if (_icon != null)
        {
            _icon.sprite = null;
            _icon.enabled = false;
            _icon.raycastTarget = false;
        }

        if (_titleText != null)
        {
            _titleText.text = "";
        }

        if (_bodyText != null)
        {
            _bodyText.text = "";
        }
    }

    private bool HasRequiredReferences()
    {
        if (_panel == null)
        {
            _panel = transform as RectTransform;
        }

        return _canvas != null && _tooltipConfig != null && _panel != null &&
            _canvasGroup != null && _icon != null && _titleText != null && _bodyText != null;
    }

    private void PrepareText(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private Vector2 ResizeToContent()
    {
        float width = Mathf.Max(_width, _iconSize + _padding * 2f);
        float innerWidth = Mathf.Max(1f, width - _padding * 2f);
        float titleWidth = Mathf.Max(1f, innerWidth - _iconSize - _gap);
        float titleHeight = Mathf.Max(_iconSize, _titleText.GetPreferredValues(_titleText.text, titleWidth, 0f).y);
        float bodyHeight = Mathf.Max(1f, _bodyText.GetPreferredValues(_bodyText.text, innerWidth, 0f).y);
        float height = _padding + titleHeight + _gap + bodyHeight + _padding;

        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(width, height);

        RectTransform iconRect = _iconRect != null ? _iconRect : _icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(_padding, -_padding);
        iconRect.sizeDelta = new Vector2(_iconSize, _iconSize);

        RectTransform titleRect = _titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(_padding + _iconSize + _gap, -_padding);
        titleRect.sizeDelta = new Vector2(titleWidth, titleHeight);

        RectTransform bodyRect = _bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(0f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = new Vector2(_padding, -_padding - titleHeight - _gap);
        bodyRect.sizeDelta = new Vector2(innerWidth, bodyHeight);

        return _panel.sizeDelta;
    }

    private void PrepareVisibleCanvasGroup(float alpha)
    {
        _canvasGroup.alpha = Mathf.Clamp01(alpha);
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void PositionNearAnchor(RectTransform anchor, Vector2 tooltipSize)
    {
        RectTransform canvasRect = _canvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        anchor.GetWorldCorners(_anchorWorldCorners);

        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(canvasCamera, _anchorWorldCorners[_topLeftCornerIndex]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(canvasCamera, _anchorWorldCorners[_topRightCornerIndex]);
        float canvasScale = Mathf.Max(0.01f, _canvas.scaleFactor);
        Vector2 pixelSize = tooltipSize * canvasScale * _currentScale;
        float offset = _anchorOffset * canvasScale;
        float edge = _edgePadding * canvasScale;
        float anchorCenterX = (topLeft.x + topRight.x) * 0.5f;
        bool showRight = anchorCenterX < Screen.width * 0.5f;

        _panel.pivot = new Vector2(showRight ? 0f : 1f, 1f);

        Vector2 pivotCandidate = showRight
            ? new Vector2(topRight.x + offset, topRight.y)
            : new Vector2(topLeft.x - offset, topLeft.y);
        Vector2 panelTopLeft = PivotToTopLeft(pivotCandidate, pixelSize, showRight);
        Vector2 clampedTopLeft = ClampToScreen(panelTopLeft, pixelSize, edge);
        Vector2 selectedPivot = TopLeftToPivot(clampedTopLeft, pixelSize, showRight);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect,
            selectedPivot,
            canvasCamera,
            out Vector3 worldPoint))
        {
            _panel.position = worldPoint;
        }
    }

    private static Vector2 PivotToTopLeft(Vector2 pivotPoint, Vector2 pixelSize, bool showRight)
    {
        return showRight ? pivotPoint : new Vector2(pivotPoint.x - pixelSize.x, pivotPoint.y);
    }

    private static Vector2 TopLeftToPivot(Vector2 topLeft, Vector2 pixelSize, bool showRight)
    {
        return showRight ? topLeft : new Vector2(topLeft.x + pixelSize.x, topLeft.y);
    }

    private static Vector2 ClampToScreen(Vector2 topLeft, Vector2 pixelSize, float edge)
    {
        float minX = edge;
        float maxX = Screen.width - edge - pixelSize.x;
        float minTopY = edge + pixelSize.y;
        float maxTopY = Screen.height - edge;

        if (maxX >= minX)
        {
            topLeft.x = Mathf.Clamp(topLeft.x, minX, maxX);
        }
        else
        {
            topLeft.x = edge;
        }

        if (maxTopY >= minTopY)
        {
            topLeft.y = Mathf.Clamp(topLeft.y, minTopY, maxTopY);
        }
        else
        {
            topLeft.y = maxTopY;
        }

        return topLeft;
    }

    private void StartVisibilityAnimation(
        float fromAlpha,
        float toAlpha,
        float fromScale,
        float toScale,
        bool clearContentOnComplete)
    {
        float duration = GetAnimationSeconds();

        if (duration <= 0f)
        {
            PrepareVisibleCanvasGroup(toAlpha);
            _panel.localScale = Vector3.one * toScale;

            if (clearContentOnComplete)
            {
                ApplyHiddenVisuals(true);
            }

            return;
        }

        _animation = StartCoroutine(AnimateVisibility(
            fromAlpha,
            toAlpha,
            fromScale,
            toScale,
            duration,
            clearContentOnComplete));
    }

    private IEnumerator AnimateVisibility(
        float fromAlpha,
        float toAlpha,
        float fromScale,
        float toScale,
        float duration,
        bool clearContentOnComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            PrepareVisibleCanvasGroup(Mathf.Lerp(fromAlpha, toAlpha, easedProgress));
            _panel.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, easedProgress);
            yield return null;
        }

        PrepareVisibleCanvasGroup(toAlpha);
        _panel.localScale = Vector3.one * toScale;

        if (clearContentOnComplete)
        {
            ApplyHiddenVisuals(true);
        }

        _animation = null;
    }

    private void StopAnimation()
    {
        if (_animation == null)
        {
            return;
        }

        StopCoroutine(_animation);
        _animation = null;
    }

    private float GetAnimationSeconds()
    {
        return _tooltipConfig != null ? _tooltipConfig.ShowHideSeconds : 0f;
    }

    private float GetCollapsedScale()
    {
        return _tooltipConfig != null ? _tooltipConfig.CollapsedScale : 0.85f;
    }

    private float GetScrollScaleStep()
    {
        return _tooltipConfig != null ? _tooltipConfig.ScrollScaleStep : 0.1f;
    }

    private float GetMinScale()
    {
        return _tooltipConfig != null ? _tooltipConfig.MinScale : 1f;
    }

    private float GetMaxScale()
    {
        return _tooltipConfig != null ? _tooltipConfig.MaxScale : 1f;
    }

    private float ClampTooltipScale(float scale)
    {
        return Mathf.Clamp(scale, GetMinScale(), GetMaxScale());
    }
}

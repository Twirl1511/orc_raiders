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
    private ItemRuntimeData _shownItem;

    private void Awake()
    {
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    public void Configure(Canvas canvas, StatsConfig statsConfig)
    {
        _canvas = canvas;
        _statsConfig = statsConfig;
        Hide();
    }

    public void ShowItem(ItemRuntimeData item, RectTransform anchor)
    {
        ItemDefinition definition = item != null ? item.Definition : null;

        if (definition == null || anchor == null || !HasRequiredReferences())
        {
            Hide();
            return;
        }

        _shownItem = item;
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

        Vector2 tooltipSize = ResizeToContent();
        ShowCanvasGroup();
        PositionNearAnchor(anchor, tooltipSize);
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

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
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

        return _canvas != null && _panel != null && _canvasGroup != null &&
            _icon != null && _titleText != null && _bodyText != null;
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
        _panel.pivot = new Vector2(0f, 1f);
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

    private void ShowCanvasGroup()
    {
        _canvasGroup.alpha = 1f;
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
        float scale = Mathf.Max(0.01f, _canvas.scaleFactor);
        Vector2 pixelSize = tooltipSize * scale;
        float offset = _anchorOffset * scale;
        float edge = _edgePadding * scale;

        Vector2 rightCandidate = new Vector2(topRight.x + offset, topRight.y);
        Vector2 leftCandidate = new Vector2(topLeft.x - offset - pixelSize.x, topLeft.y);
        Vector2 selected = SelectCandidate(rightCandidate, leftCandidate, pixelSize, edge);
        selected = ClampToScreen(selected, pixelSize, edge);

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect,
            selected,
            canvasCamera,
            out Vector3 worldPoint))
        {
            _panel.position = worldPoint;
        }
    }

    private Vector2 SelectCandidate(Vector2 rightCandidate, Vector2 leftCandidate, Vector2 pixelSize, float edge)
    {
        if (FitsScreen(rightCandidate, pixelSize, edge))
        {
            return rightCandidate;
        }

        if (FitsScreen(leftCandidate, pixelSize, edge))
        {
            return leftCandidate;
        }

        bool rightFitsHorizontally = rightCandidate.x + pixelSize.x <= Screen.width - edge;
        bool leftFitsHorizontally = leftCandidate.x >= edge;

        if (!rightFitsHorizontally && leftFitsHorizontally)
        {
            return leftCandidate;
        }

        return rightCandidate;
    }

    private static bool FitsScreen(Vector2 topLeft, Vector2 pixelSize, float edge)
    {
        return topLeft.x >= edge &&
            topLeft.x + pixelSize.x <= Screen.width - edge &&
            topLeft.y <= Screen.height - edge &&
            topLeft.y - pixelSize.y >= edge;
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
}

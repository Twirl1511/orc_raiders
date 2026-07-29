using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class HeroStatRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
{
    [Header("Scene UI")]
    [SerializeField] private Image _background = null;
    [SerializeField] private TextMeshProUGUI _labelText = null;
    [SerializeField] private TextMeshProUGUI _valueText = null;
    [SerializeField] private RectTransform _barFill = null;
    [SerializeField] private Button _upgradeButton = null;
    [SerializeField] private TextMeshProUGUI _upgradeLabel = null;

    [Header("Colors")]
    [SerializeField] private Color _normalColor = new Color(0.11f, 0.13f, 0.15f, 0.72f);
    [SerializeField] private Color _hoverColor = new Color(0.18f, 0.21f, 0.24f, 0.9f);
    [SerializeField] private Color _barFillColor = new Color(0.86f, 0.9f, 0.96f, 1f);
    [SerializeField] private Color _barEmptyColor = new Color(0.27f, 0.31f, 0.35f, 1f);

    private ItemTooltipView _tooltipView;
    private RectTransform _rectTransform;
    private string _tooltipTitle = "";
    private string _tooltipBody = "";
    private bool _hasTooltip;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        ApplyNormalVisuals();
        PrepareStaticVisuals();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    public void ConfigureTooltip(ItemTooltipView tooltipView)
    {
        _tooltipView = tooltipView;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }
    }

    public void SetText(string label, string value)
    {
        if (_labelText != null)
        {
            _labelText.text = label ?? "";
        }

        if (_valueText != null)
        {
            _valueText.text = value ?? "";
        }
    }

    public void SetTooltip(string title, string body)
    {
        _tooltipTitle = title ?? "";
        _tooltipBody = body ?? "";
        _hasTooltip = !string.IsNullOrWhiteSpace(_tooltipTitle) && !string.IsNullOrWhiteSpace(_tooltipBody);
    }

    public void SetBar(float normalizedValue)
    {
        if (_barFill == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp01(normalizedValue);
        _barFill.anchorMin = new Vector2(0f, 0f);
        _barFill.anchorMax = new Vector2(clampedValue, 1f);
        _barFill.offsetMin = Vector2.zero;
        _barFill.offsetMax = Vector2.zero;

        Image fillImage = _barFill.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.color = _barFillColor;
            fillImage.raycastTarget = false;
        }
    }

    public void SetUpgradeVisible(bool visible, bool interactable)
    {
        if (_upgradeButton == null)
        {
            return;
        }

        _upgradeButton.gameObject.SetActive(true);
        _upgradeButton.interactable = visible && interactable;

        Graphic targetGraphic = _upgradeButton.targetGraphic;
        if (targetGraphic != null)
        {
            Color color = targetGraphic.color;
            color.a = visible ? 1f : 0f;
            targetGraphic.color = color;
            targetGraphic.raycastTarget = visible;
        }

        if (_upgradeLabel != null)
        {
            _upgradeLabel.text = "+";
            _upgradeLabel.enabled = visible;
            _upgradeLabel.raycastTarget = false;
        }
    }

    public void SetUpgradeHandler(UnityAction action)
    {
        if (_upgradeButton == null)
        {
            return;
        }

        _upgradeButton.onClick.RemoveAllListeners();

        if (action != null)
        {
            _upgradeButton.onClick.AddListener(action);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHoverVisuals();

        if (_hasTooltip && _tooltipView != null)
        {
            _tooltipView.ShowText(_tooltipTitle, _tooltipBody, _rectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyNormalVisuals();
        HideTooltip();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_hasTooltip && _tooltipView != null && eventData != null)
        {
            _tooltipView.AdjustScale(eventData.scrollDelta.y);
        }
    }

    private void HideTooltip()
    {
        if (_tooltipView != null)
        {
            _tooltipView.HideText(_rectTransform);
        }
    }

    private void PrepareStaticVisuals()
    {
        if (_labelText != null)
        {
            _labelText.raycastTarget = false;
            _labelText.textWrappingMode = TextWrappingModes.NoWrap;
            _labelText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_valueText != null)
        {
            _valueText.raycastTarget = false;
            _valueText.textWrappingMode = TextWrappingModes.NoWrap;
            _valueText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_upgradeLabel != null)
        {
            _upgradeLabel.raycastTarget = false;
            _upgradeLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _upgradeLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_barFill != null && _barFill.parent != null)
        {
            Image emptyImage = _barFill.parent.GetComponent<Image>();
            if (emptyImage != null)
            {
                emptyImage.color = _barEmptyColor;
                emptyImage.raycastTarget = false;
            }
        }
    }

    private void ApplyNormalVisuals()
    {
        if (_background != null)
        {
            _background.color = _normalColor;
            _background.raycastTarget = true;
        }
    }

    private void ApplyHoverVisuals()
    {
        if (_background != null)
        {
            _background.color = _hoverColor;
        }
    }
}

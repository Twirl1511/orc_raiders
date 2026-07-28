using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ItemButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _icon = null;
    [SerializeField] private TextMeshProUGUI _label = null;

    private ItemStoragePanelView _owner;
    private ItemRuntimeData _item;
    private CanvasGroup _canvasGroup;
    private HeroItemSlotView _pendingDropSlot;
    private bool _dragging;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Setup(ItemRuntimeData item, ItemStoragePanelView owner)
    {
        _item = item;
        _owner = owner;
        _pendingDropSlot = null;
        _dragging = false;
        SetDraggingVisual(false);

        ItemDefinition definition = item != null ? item.Definition : null;

        if (_icon != null)
        {
            _icon.sprite = definition != null ? definition.Icon : null;
            _icon.enabled = definition != null && definition.Icon != null;
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
        }

        if (_label != null)
        {
            _label.text = definition != null ? definition.DisplayName : "";
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 7f;
            _label.fontSizeMax = 12f;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.raycastTarget = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowItemDetails(_item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideItemDetails(_item);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _pendingDropSlot = null;
        _dragging = _owner != null && _owner.BeginDragItem(this, _item, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
        {
            return;
        }

        _owner?.UpdateDraggedItem(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging)
        {
            return;
        }

        bool completed = _pendingDropSlot != null && _owner != null &&
            _owner.TryCompleteDragToSlot(_item, _pendingDropSlot);
        _owner?.EndDragItem(this, completed);

        _pendingDropSlot = null;
        _dragging = false;
    }

    public void AcceptDropToSlot(HeroItemSlotView slotView)
    {
        if (_dragging && slotView != null)
        {
            _pendingDropSlot = slotView;
        }
    }

    public void SetDraggingVisual(bool dragging)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = dragging ? 0f : 1f;
        _canvasGroup.blocksRaycasts = !dragging;
        _canvasGroup.interactable = !dragging;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HeroItemSlotView : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
{
    [SerializeField] private Image _background = null;
    [SerializeField] private Image _icon = null;
    [SerializeField] private TextMeshProUGUI _label = null;

    private NecropolisSystem _owner;
    private ItemTooltipView _itemTooltip;
    private HeroRuntimeData _hero;
    private int _slotIndex;
    private bool _allowInteraction = true;

    public void Configure(NecropolisSystem owner, int slotIndex, ItemTooltipView itemTooltip)
    {
        _owner = owner;
        _slotIndex = slotIndex;
        _itemTooltip = itemTooltip;
        Refresh();
    }

    public void SetHero(HeroRuntimeData hero, bool allowInteraction)
    {
        _itemTooltip?.Hide();
        _hero = hero;
        _allowInteraction = allowInteraction;
        Refresh();
    }

    public bool TryAcceptItem(ItemRuntimeData item, ItemStorageSystem itemStorage)
    {
        return _allowInteraction && _owner != null && _hero != null &&
            _owner.TryEquipItemFromStorage(_hero, _slotIndex, item, itemStorage);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!_allowInteraction)
        {
            return;
        }

        ItemButtonView itemButton = eventData != null && eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ItemButtonView>()
            : null;

        itemButton?.AcceptDropToSlot(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_allowInteraction || eventData == null || eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (_owner != null && _hero != null && _owner.TryUnequipItemToStorage(_hero, _slotIndex))
        {
            _itemTooltip?.Hide();
            Refresh();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemRuntimeData item = _hero != null ? _hero.GetEquippedItem(_slotIndex) : null;

        if (item != null && item.Definition != null)
        {
            _itemTooltip?.ShowItem(item, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemRuntimeData item = _hero != null ? _hero.GetEquippedItem(_slotIndex) : null;
        _itemTooltip?.HideItem(item);
    }

    public void OnScroll(PointerEventData eventData)
    {
        ItemRuntimeData item = _hero != null ? _hero.GetEquippedItem(_slotIndex) : null;

        if (item != null && item.Definition != null && eventData != null)
        {
            _itemTooltip?.AdjustScale(eventData.scrollDelta.y);
        }
    }

    private void Refresh()
    {
        ItemRuntimeData item = _hero != null ? _hero.GetEquippedItem(_slotIndex) : null;
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
            _label.text = definition != null ? "" : HeroRuntimeData.GetEquipmentSlotDisplayName(_slotIndex);
            _label.enabled = definition == null;
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 6f;
            _label.fontSizeMax = 11f;
            _label.textWrappingMode = TextWrappingModes.Normal;
            _label.overflowMode = TextOverflowModes.Ellipsis;
            _label.raycastTarget = false;
        }

        if (_background != null)
        {
            _background.raycastTarget = true;
            if (definition != null)
            {
                _background.color = _allowInteraction
                    ? new Color(0.88f, 0.9f, 0.86f, 1f)
                    : new Color(0.48f, 0.49f, 0.46f, 1f);
            }
            else
            {
                _background.color = _allowInteraction
                    ? new Color(0.16f, 0.18f, 0.2f, 1f)
                    : new Color(0.1f, 0.11f, 0.12f, 1f);
            }
        }
    }
}

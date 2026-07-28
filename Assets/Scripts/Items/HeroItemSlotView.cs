using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class HeroItemSlotView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image _background = null;
    [SerializeField] private Image _icon = null;
    [SerializeField] private TextMeshProUGUI _label = null;

    private NecropolisSystem _owner;
    private HeroRuntimeData _hero;
    private int _slotIndex;

    public void Configure(NecropolisSystem owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;
        Refresh();
    }

    public void SetHero(HeroRuntimeData hero)
    {
        _hero = hero;
        Refresh();
    }

    public bool TryAcceptItem(ItemRuntimeData item, ItemStorageSystem itemStorage)
    {
        return _owner != null && _hero != null &&
            _owner.TryEquipItemFromStorage(_hero, _slotIndex, item, itemStorage);
    }

    public void OnDrop(PointerEventData eventData)
    {
        ItemButtonView itemButton = eventData != null && eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ItemButtonView>()
            : null;

        itemButton?.AcceptDropToSlot(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (_owner != null && _hero != null && _owner.TryUnequipItemToStorage(_hero, _slotIndex))
        {
            Refresh();
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
            _label.text = definition != null ? definition.DisplayName : $"Слот {_slotIndex + 1}";
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
            _background.color = definition != null
                ? new Color(0.88f, 0.9f, 0.86f, 1f)
                : new Color(0.16f, 0.18f, 0.2f, 1f);
        }
    }
}

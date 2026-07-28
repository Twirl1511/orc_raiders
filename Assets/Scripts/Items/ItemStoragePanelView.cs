using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ItemStoragePanelView : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private ItemStorageSystem _itemStorage = null;
    [SerializeField] private StatsConfig _statsConfig = null;

    [Header("Scene UI")]
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _itemsRoot = null;
    [SerializeField] private ItemButtonView _itemButtonTemplate = null;
    [SerializeField] private Image _detailsIcon = null;
    [SerializeField] private TextMeshProUGUI _detailsTitle = null;
    [SerializeField] private TextMeshProUGUI _detailsText = null;

    private readonly List<GameObject> _runtimeItemButtons = new List<GameObject>();
    private ItemRuntimeData _activeItem;
    private ItemRuntimeData _draggedItem;
    private Image _dragIcon;
    private RectTransform _dragIconRect;
    private bool _initialized;
    private bool _subscribedToStorage;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_initialized)
        {
            SubscribeToStorage();
            RefreshItems();
        }
        else
        {
            Initialize();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    private void OnDisable()
    {
        UnsubscribeFromStorage();
        DestroyDragIcon();
    }

    private void OnDestroy()
    {
        ClearRuntimeItemButtons();
    }

    public void Configure(ItemStorageSystem itemStorage, StatsConfig statsConfig, Canvas canvas)
    {
        UnsubscribeFromStorage();
        _itemStorage = itemStorage;
        _statsConfig = statsConfig;
        _canvas = canvas;

        if (_initialized)
        {
            SubscribeToStorage();
            RefreshItems();
            ClearDetails();
        }
    }

    public void ShowItemDetails(ItemRuntimeData item)
    {
        _activeItem = item;

        ItemDefinition definition = item != null ? item.Definition : null;

        if (definition == null)
        {
            ClearDetails();
            return;
        }

        if (_detailsIcon != null)
        {
            _detailsIcon.sprite = definition.Icon;
            _detailsIcon.enabled = definition.Icon != null;
            _detailsIcon.preserveAspect = true;
        }

        if (_detailsTitle != null)
        {
            _detailsTitle.text = definition.DisplayName;
        }

        if (_detailsText != null)
        {
            _detailsText.text = ItemDescriptionFormatter.BuildDetailsText(definition, _statsConfig);
        }
    }

    public void HideItemDetails(ItemRuntimeData item)
    {
        if (_activeItem != item)
        {
            return;
        }

        ClearDetails();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();
        _initialized = true;
        _itemStorage.Initialize();
        SubscribeToStorage();
        _itemButtonTemplate.gameObject.SetActive(false);
        ClearDetails();
        RefreshItems();
    }

    private void ValidateReferences()
    {
        if (_itemStorage == null)
        {
            throw new System.InvalidOperationException($"{nameof(ItemStoragePanelView)} requires {nameof(ItemStorageSystem)}.");
        }

        if (_statsConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(ItemStoragePanelView)} requires {nameof(StatsConfig)}.");
        }

        if (_canvas == null || _itemsRoot == null || _itemButtonTemplate == null || _detailsIcon == null ||
            _detailsTitle == null || _detailsText == null)
        {
            throw new System.InvalidOperationException($"{nameof(ItemStoragePanelView)} requires scene UI references.");
        }
    }

    private void RefreshItems()
    {
        ClearRuntimeItemButtons();

        if (_itemStorage == null || _itemsRoot == null || _itemButtonTemplate == null)
        {
            return;
        }

        IReadOnlyList<ItemRuntimeData> items = _itemStorage.Items;

        for (int i = 0; i < items.Count; i++)
        {
            ItemRuntimeData item = items[i];

            if (item == null)
            {
                continue;
            }

            ItemButtonView itemButton = Instantiate(_itemButtonTemplate, _itemsRoot, false);
            itemButton.gameObject.name = $"Item Button - {item.Id}";
            itemButton.gameObject.SetActive(true);
            itemButton.Setup(item, this);
            _runtimeItemButtons.Add(itemButton.gameObject);
        }
    }

    public bool BeginDragItem(ItemButtonView source, ItemRuntimeData item, PointerEventData eventData)
    {
        if (source == null || item == null || _itemStorage == null || !_itemStorage.Contains(item))
        {
            return false;
        }

        _draggedItem = item;
        source.SetDraggingVisual(true);
        CreateDragIcon(item);
        UpdateDragIconPosition(eventData);
        return true;
    }

    public void UpdateDraggedItem(PointerEventData eventData)
    {
        UpdateDragIconPosition(eventData);
    }

    public bool TryCompleteDragToSlot(ItemRuntimeData item, HeroItemSlotView slotView)
    {
        if (item == null || slotView == null || _itemStorage == null || item != _draggedItem)
        {
            return false;
        }

        return slotView.TryAcceptItem(item, _itemStorage);
    }

    public void EndDragItem(ItemButtonView source, bool completed)
    {
        if (!completed && source != null)
        {
            source.SetDraggingVisual(false);
        }

        _draggedItem = null;
        DestroyDragIcon();
    }

    private void SubscribeToStorage()
    {
        if (_subscribedToStorage || _itemStorage == null)
        {
            return;
        }

        _itemStorage.Changed += HandleStorageChanged;
        _subscribedToStorage = true;
    }

    private void UnsubscribeFromStorage()
    {
        if (!_subscribedToStorage || _itemStorage == null)
        {
            return;
        }

        _itemStorage.Changed -= HandleStorageChanged;
        _subscribedToStorage = false;
    }

    private void HandleStorageChanged()
    {
        if (_activeItem != null && !_itemStorage.Contains(_activeItem))
        {
            ClearDetails();
        }

        RefreshItems();
    }

    private void CreateDragIcon(ItemRuntimeData item)
    {
        DestroyDragIcon();

        ItemDefinition definition = item != null ? item.Definition : null;

        if (_canvas == null || definition == null)
        {
            return;
        }

        GameObject iconObject = new GameObject("Dragged Item Icon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        iconObject.transform.SetParent(_canvas.transform, false);
        iconObject.transform.SetAsLastSibling();

        _dragIconRect = (RectTransform)iconObject.transform;
        _dragIconRect.sizeDelta = new Vector2(64f, 64f);

        CanvasGroup canvasGroup = iconObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        _dragIcon = iconObject.GetComponent<Image>();
        _dragIcon.sprite = definition.Icon;
        _dragIcon.enabled = definition.Icon != null;
        _dragIcon.preserveAspect = true;
        _dragIcon.raycastTarget = false;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRect == null || _canvas == null || eventData == null)
        {
            return;
        }

        RectTransform canvasTransform = _canvas.transform as RectTransform;

        if (canvasTransform == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            _dragIconRect.anchoredPosition = localPoint;
        }
    }

    private void DestroyDragIcon()
    {
        if (_dragIcon != null)
        {
            Destroy(_dragIcon.gameObject);
        }

        _dragIcon = null;
        _dragIconRect = null;
    }

    private void ClearDetails()
    {
        _activeItem = null;

        if (_detailsIcon != null)
        {
            _detailsIcon.sprite = null;
            _detailsIcon.enabled = false;
        }

        if (_detailsTitle != null)
        {
            _detailsTitle.text = "";
        }

        if (_detailsText != null)
        {
            _detailsText.text = "";
        }
    }

    private void ClearRuntimeItemButtons()
    {
        for (int i = _runtimeItemButtons.Count - 1; i >= 0; i--)
        {
            if (_runtimeItemButtons[i] != null)
            {
                Destroy(_runtimeItemButtons[i]);
            }
        }

        _runtimeItemButtons.Clear();
    }
}

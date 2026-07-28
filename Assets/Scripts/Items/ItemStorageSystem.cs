using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ItemStorageSystem : MonoBehaviour
{
    [SerializeField] private ItemsConfig _itemsConfig = null;
    [SerializeField, Min(0)] private int _startingRandomItemsCount = 3;

    private readonly List<ItemRuntimeData> _items = new List<ItemRuntimeData>();
    private int _nextItemInstanceId = 1;
    private bool _initialized;

    public event Action Changed;

    public IReadOnlyList<ItemRuntimeData> Items => _items;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();
        _initialized = true;
        CreateStartingInventory();
        Changed?.Invoke();
    }

    public bool Contains(ItemRuntimeData item)
    {
        return item != null && _items.Contains(item);
    }

    public bool AddItem(ItemRuntimeData item)
    {
        if (item == null || _items.Contains(item))
        {
            return false;
        }

        _items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemRuntimeData item)
    {
        if (item == null || !_items.Remove(item))
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public bool TryEquipItemToHero(ItemRuntimeData item, HeroRuntimeData heroData, int slotIndex)
    {
        if (item == null || heroData == null || !_items.Remove(item))
        {
            return false;
        }

        if (!heroData.TryEquipItem(slotIndex, item, out ItemRuntimeData replacedItem))
        {
            _items.Add(item);
            Changed?.Invoke();
            return false;
        }

        if (replacedItem != null)
        {
            _items.Add(replacedItem);
        }

        Changed?.Invoke();
        return true;
    }

    private void ValidateReferences()
    {
        if (_itemsConfig == null || !_itemsConfig.ValidateForRuntime(_itemsConfig))
        {
            throw new InvalidOperationException($"{nameof(ItemStorageSystem)} requires valid {nameof(ItemsConfig)}.");
        }
    }

    private void CreateStartingInventory()
    {
        _items.Clear();

        if (_startingRandomItemsCount <= 0)
        {
            return;
        }

        List<ItemDefinition> candidates = new List<ItemDefinition>();
        IReadOnlyList<ItemDefinition> configItems = _itemsConfig.Items;

        for (int i = 0; i < configItems.Count; i++)
        {
            ItemDefinition item = configItems[i];

            if (item != null)
            {
                candidates.Add(item);
            }
        }

        int itemCount = Mathf.Min(_startingRandomItemsCount, candidates.Count);

        for (int i = 0; i < itemCount; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, candidates.Count);
            ItemDefinition selectedItem = candidates[randomIndex];
            candidates[randomIndex] = candidates[i];
            candidates[i] = selectedItem;
            _items.Add(new ItemRuntimeData(_nextItemInstanceId, selectedItem));
            _nextItemInstanceId++;
        }
    }
}

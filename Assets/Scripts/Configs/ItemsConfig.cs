using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Items", menuName = "GAME/Items")]
public sealed class ItemsConfig : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> _items = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> Items => _items;

    public ItemDefinition GetItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string normalizedId = id.Trim();

        for (int i = 0; i < _items.Count; i++)
        {
            ItemDefinition item = _items[i];

            if (item != null && string.Equals(item.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    public bool ValidateForRuntime(UnityEngine.Object context)
    {
        bool valid = true;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_items.Count == 0)
        {
            Debug.LogError($"{nameof(ItemsConfig)} requires at least one item definition.", context);
            return false;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            ItemDefinition item = _items[i];

            if (item == null)
            {
                Debug.LogError($"{nameof(ItemsConfig)} has an empty item entry at index {i}.", context);
                valid = false;
                continue;
            }

            string id = item.Id;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"{nameof(ItemsConfig)} item at index {i} has empty id.", context);
                valid = false;
            }
            else if (!ids.Add(id.Trim()))
            {
                Debug.LogError($"{nameof(ItemsConfig)} item id '{id}' is duplicated. Item ids must be unique.", context);
                valid = false;
            }

            if (!item.ValidateForRuntime(context, i))
            {
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        ValidateForRuntime(this);
    }
}

public enum ItemGroup
{
    None = 0,
    Weapon = 1,
    Armor = 2
}

public enum ItemStatTarget
{
    None = 0,
    Primary = 1,
    Secondary = 2
}

[Serializable]
public sealed class ItemDefinition
{
    [SerializeField] private string _id = "item_id";
    [SerializeField] private string _displayName = "Item";
    [SerializeField, TextArea] private string _description = "";
    [SerializeField] private ItemGroup _group = ItemGroup.Weapon;
    [SerializeField] private Sprite _icon = null;
    [SerializeField] private Sprite _heroOverlaySprite = null;
    [SerializeField] private Vector2 _heroOverlayOffset = Vector2.zero;
    [SerializeField] private Vector2 _heroOverlayScale = Vector2.one;
    [SerializeField] private float _heroOverlayRotationDegrees = 0f;
    [SerializeField] private Sprite _heroFrontOverlaySprite = null;
    [SerializeField] private Vector2 _heroFrontOverlayOffset = Vector2.zero;
    [SerializeField] private Vector2 _heroFrontOverlayScale = Vector2.one;
    [SerializeField] private float _heroFrontOverlayRotationDegrees = 0f;
    [SerializeField] private List<ItemStatModifier> _statModifiers = new List<ItemStatModifier>();

    public string Id => string.IsNullOrWhiteSpace(_id) ? "" : _id.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? Id : _displayName;
    public string Description => _description;
    public ItemGroup Group => _group;
    public Sprite Icon => _icon;
    public Sprite HeroOverlaySprite => _heroOverlaySprite;
    public Vector2 HeroOverlayOffset => _heroOverlayOffset;
    public Vector2 HeroOverlayScale => new Vector2(Mathf.Max(0.01f, _heroOverlayScale.x), Mathf.Max(0.01f, _heroOverlayScale.y));
    public float HeroOverlayRotationDegrees => _heroOverlayRotationDegrees;
    public Sprite HeroFrontOverlaySprite => _heroFrontOverlaySprite;
    public Vector2 HeroFrontOverlayOffset => _heroFrontOverlayOffset;
    public Vector2 HeroFrontOverlayScale => new Vector2(Mathf.Max(0.01f, _heroFrontOverlayScale.x), Mathf.Max(0.01f, _heroFrontOverlayScale.y));
    public float HeroFrontOverlayRotationDegrees => _heroFrontOverlayRotationDegrees;
    public IReadOnlyList<ItemStatModifier> StatModifiers => _statModifiers;

    public bool IsEquippable => _group == ItemGroup.Weapon || _group == ItemGroup.Armor;

    public bool ValidateForRuntime(UnityEngine.Object context, int itemIndex)
    {
        bool valid = true;
        string itemLabel = string.IsNullOrWhiteSpace(Id) ? $"item at index {itemIndex}" : $"item '{Id}'";

        if (_group == ItemGroup.None)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} must have a group.", context);
            valid = false;
        }

        if (!IsEquippable)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} must be a weapon or armor item for the current prototype.", context);
            valid = false;
        }

        if (_icon == null)
        {
            Debug.LogWarning($"{nameof(ItemsConfig)} {itemLabel} has no icon sprite.", context);
        }

        if (_heroOverlaySprite == null)
        {
            Debug.LogWarning($"{nameof(ItemsConfig)} {itemLabel} has no hero overlay sprite.", context);
        }

        if (_statModifiers.Count == 0)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} requires at least one stat modifier.", context);
            valid = false;
        }

        for (int i = 0; i < _statModifiers.Count; i++)
        {
            ItemStatModifier modifier = _statModifiers[i];

            if (modifier == null)
            {
                Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} has an empty stat modifier at index {i}.", context);
                valid = false;
                continue;
            }

            if (!modifier.ValidateForRuntime(context, itemLabel, i))
            {
                valid = false;
            }
        }

        return valid;
    }
}

[Serializable]
public sealed class ItemStatModifier
{
    [SerializeField] private ItemStatTarget _target = ItemStatTarget.Primary;
    [SerializeField] private PrimaryStatType _primaryStat = PrimaryStatType.None;
    [SerializeField] private SecondaryStatType _secondaryStat = SecondaryStatType.None;
    [SerializeField] private float _value = 1f;

    public ItemStatTarget Target => _target;
    public PrimaryStatType PrimaryStat => _primaryStat;
    public SecondaryStatType SecondaryStat => _secondaryStat;
    public float Value => _value;

    public bool ValidateForRuntime(UnityEngine.Object context, string itemLabel, int modifierIndex)
    {
        bool valid = true;

        if (_target == ItemStatTarget.None)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} must have a target.", context);
            valid = false;
        }

        if (Mathf.Approximately(_value, 0f))
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} value cannot be 0.", context);
            valid = false;
        }

        switch (_target)
        {
            case ItemStatTarget.Primary:
                if (_primaryStat == PrimaryStatType.None)
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} must select a primary stat.", context);
                    valid = false;
                }

                if (_secondaryStat != SecondaryStatType.None)
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} primary stat modifier {modifierIndex} must not select a secondary stat.", context);
                    valid = false;
                }

                if (!Mathf.Approximately(_value, Mathf.Round(_value)))
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} primary stat modifier {modifierIndex} value must be a whole number.", context);
                    valid = false;
                }

                break;
            case ItemStatTarget.Secondary:
                if (_secondaryStat == SecondaryStatType.None)
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} must select a secondary stat.", context);
                    valid = false;
                }

                if (_primaryStat != PrimaryStatType.None)
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} secondary stat modifier {modifierIndex} must not select a primary stat.", context);
                    valid = false;
                }

                break;
        }

        return valid;
    }
}

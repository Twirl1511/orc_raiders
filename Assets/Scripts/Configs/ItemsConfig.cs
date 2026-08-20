using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Items", menuName = "GAME/Items")]
public sealed class ItemsConfig : ScriptableObject
{
    [SerializeField] private List<ItemRarityDefinition> _rarities = new List<ItemRarityDefinition>();
    [SerializeField] private List<ItemDefinition> _items = new List<ItemDefinition>();

    public IReadOnlyList<ItemRarityDefinition> Rarities => _rarities;
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
        HashSet<ItemRarity> rarities = new HashSet<ItemRarity>();

        if (_items.Count == 0)
        {
            Debug.LogError($"{nameof(ItemsConfig)} requires at least one item definition.", context);
            return false;
        }

        for (int i = 0; i < _rarities.Count; i++)
        {
            ItemRarityDefinition rarity = _rarities[i];

            if (rarity == null)
            {
                Debug.LogError($"{nameof(ItemsConfig)} has an empty rarity entry at index {i}.", context);
                valid = false;
                continue;
            }

            if (!rarities.Add(rarity.Rarity))
            {
                Debug.LogError($"{nameof(ItemsConfig)} rarity '{rarity.Rarity}' is duplicated.", context);
                valid = false;
            }

            if (!rarity.ValidateForRuntime(context, i))
            {
                valid = false;
            }
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

    public ItemRarity RollRarity()
    {
        float totalWeight = 0f;

        for (int i = 0; i < _rarities.Count; i++)
        {
            ItemRarityDefinition rarity = _rarities[i];

            if (rarity != null && rarity.RollWeight > 0f)
            {
                totalWeight += rarity.RollWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return ItemRarity.Common;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);

        for (int i = 0; i < _rarities.Count; i++)
        {
            ItemRarityDefinition rarity = _rarities[i];

            if (rarity == null || rarity.RollWeight <= 0f)
            {
                continue;
            }

            roll -= rarity.RollWeight;

            if (roll <= 0f)
            {
                return rarity.Rarity;
            }
        }

        return ItemRarity.Common;
    }

    public float RollQualityForRarity(ItemRarity rarity)
    {
        ItemRarityDefinition definition = GetRarityDefinition(rarity);
        return definition != null
            ? definition.RollQuality()
            : ItemRarityDefinition.RollFallbackQuality(rarity);
    }

    public string GetRarityDisplayName(ItemRarity rarity)
    {
        ItemRarityDefinition definition = GetRarityDefinition(rarity);
        return definition != null
            ? definition.DisplayName
            : ItemRarityDefinition.GetFallbackDisplayName(rarity);
    }

    public Color GetRarityBackgroundColor(ItemRarity rarity)
    {
        ItemRarityDefinition definition = GetRarityDefinition(rarity);
        return definition != null
            ? definition.BackgroundColor
            : ItemRarityDefinition.GetFallbackBackgroundColor(rarity);
    }

    private ItemRarityDefinition GetRarityDefinition(ItemRarity rarity)
    {
        for (int i = 0; i < _rarities.Count; i++)
        {
            ItemRarityDefinition definition = _rarities[i];

            if (definition != null && definition.Rarity == rarity)
            {
                return definition;
            }
        }

        return null;
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

public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

[Serializable]
public sealed class ItemRarityDefinition
{
    [SerializeField] private ItemRarity _rarity = ItemRarity.Common;
    [SerializeField] private string _displayName = "Common";
    [SerializeField] private Color _backgroundColor = Color.white;
    [SerializeField, Min(0f)] private float _rollWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float _minRollQuality = 0f;
    [SerializeField, Range(0f, 1f)] private float _maxRollQuality = 1f;

    public ItemRarity Rarity => _rarity;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? GetFallbackDisplayName(_rarity) : _displayName;
    public Color BackgroundColor => _backgroundColor;
    public float RollWeight => Mathf.Max(0f, _rollWeight);
    public float MinRollQuality => Mathf.Clamp01(Mathf.Min(_minRollQuality, _maxRollQuality));
    public float MaxRollQuality => Mathf.Clamp01(Mathf.Max(_minRollQuality, _maxRollQuality));

    public float RollQuality()
    {
        return UnityEngine.Random.Range(MinRollQuality, MaxRollQuality);
    }

    public bool ValidateForRuntime(UnityEngine.Object context, int rarityIndex)
    {
        bool valid = true;

        if (_rollWeight < 0f)
        {
            Debug.LogError($"{nameof(ItemsConfig)} rarity entry {rarityIndex} roll weight cannot be negative.", context);
            valid = false;
        }

        if (_maxRollQuality < _minRollQuality)
        {
            Debug.LogError($"{nameof(ItemsConfig)} rarity entry {rarityIndex} max roll quality cannot be lower than min roll quality.", context);
            valid = false;
        }

        return valid;
    }

    public static float RollFallbackQuality(ItemRarity rarity)
    {
        Vector2 range = GetFallbackQualityRange(rarity);
        return UnityEngine.Random.Range(range.x, range.y);
    }

    public static string GetFallbackDisplayName(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return "Необычный";
            case ItemRarity.Rare:
                return "Редкий";
            case ItemRarity.Epic:
                return "Эпический";
            case ItemRarity.Legendary:
                return "Легендарный";
            default:
                return "Обычный";
        }
    }

    public static Color GetFallbackBackgroundColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return new Color(0.26f, 0.68f, 0.32f, 1f);
            case ItemRarity.Rare:
                return new Color(0.22f, 0.42f, 0.9f, 1f);
            case ItemRarity.Epic:
                return new Color(0.55f, 0.24f, 0.82f, 1f);
            case ItemRarity.Legendary:
                return new Color(0.95f, 0.67f, 0.16f, 1f);
            default:
                return new Color(0.92f, 0.92f, 0.88f, 1f);
        }
    }

    private static Vector2 GetFallbackQualityRange(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon:
                return new Vector2(0.25f, 0.55f);
            case ItemRarity.Rare:
                return new Vector2(0.45f, 0.75f);
            case ItemRarity.Epic:
                return new Vector2(0.65f, 0.9f);
            case ItemRarity.Legendary:
                return new Vector2(0.8f, 1f);
            default:
                return new Vector2(0f, 0.45f);
        }
    }
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
    [SerializeField] private float _minValue = 1f;
    [SerializeField] private float _maxValue = 1f;
    [SerializeField, HideInInspector] private float _value = 1f;

    public ItemStatTarget Target => _target;
    public PrimaryStatType PrimaryStat => _primaryStat;
    public SecondaryStatType SecondaryStat => _secondaryStat;
    public float MinValue => GetRawMinValue();
    public float MaxValue => GetRawMaxValue();
    public float Value => RollPreviewValue();

    public float RollValue(float quality)
    {
        float minValue = MinValue;
        float maxValue = MaxValue;
        float normalizedQuality = Mathf.Clamp01(quality);
        float t = IsLowerValueBetter() ? 1f - normalizedQuality : normalizedQuality;
        float value = Mathf.Lerp(minValue, maxValue, t);

        return _target == ItemStatTarget.Primary ? Mathf.Round(value) : value;
    }

    public bool ValidateForRuntime(UnityEngine.Object context, string itemLabel, int modifierIndex)
    {
        bool valid = true;
        float minValue = MinValue;
        float maxValue = MaxValue;

        if (_target == ItemStatTarget.None)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} must have a target.", context);
            valid = false;
        }

        if (maxValue < minValue)
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} max value cannot be lower than min value.", context);
            valid = false;
        }

        if (Mathf.Approximately(minValue, 0f) && Mathf.Approximately(maxValue, 0f))
        {
            Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} stat modifier {modifierIndex} range cannot be 0..0.", context);
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

                if (!Mathf.Approximately(minValue, Mathf.Round(minValue)) ||
                    !Mathf.Approximately(maxValue, Mathf.Round(maxValue)))
                {
                    Debug.LogError($"{nameof(ItemsConfig)} {itemLabel} primary stat modifier {modifierIndex} range values must be whole numbers.", context);
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

    private float RollPreviewValue()
    {
        return RollValue(0.5f);
    }

    private bool IsLowerValueBetter()
    {
        return _target == ItemStatTarget.Secondary && _secondaryStat == SecondaryStatType.AttackInterval;
    }

    private float GetRawMinValue()
    {
        return IsUsingLegacyFixedValue() ? _value : _minValue;
    }

    private float GetRawMaxValue()
    {
        return IsUsingLegacyFixedValue() ? _value : _maxValue;
    }

    private bool IsUsingLegacyFixedValue()
    {
        return Mathf.Approximately(_minValue, 1f) && Mathf.Approximately(_maxValue, 1f) &&
            !Mathf.Approximately(_value, 1f);
    }
}

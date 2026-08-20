using System.Collections.Generic;
using UnityEngine;

public sealed class ItemRuntimeData
{
    private readonly List<ItemRuntimeStatModifier> _statModifiers;

    public ItemRuntimeData(int instanceId, ItemDefinition definition)
        : this(
            instanceId,
            definition,
            ItemRarity.Common,
            ItemRarityDefinition.GetFallbackDisplayName(ItemRarity.Common),
            ItemRarityDefinition.GetFallbackBackgroundColor(ItemRarity.Common),
            BuildFixedModifiers(definition))
    {
    }

    public ItemRuntimeData(
        int instanceId,
        ItemDefinition definition,
        ItemRarity rarity,
        string rarityDisplayName,
        Color rarityBackgroundColor,
        IReadOnlyList<ItemRuntimeStatModifier> statModifiers)
    {
        InstanceId = instanceId;
        Definition = definition;
        Rarity = rarity;
        RarityDisplayName = string.IsNullOrWhiteSpace(rarityDisplayName)
            ? ItemRarityDefinition.GetFallbackDisplayName(rarity)
            : rarityDisplayName;
        RarityBackgroundColor = rarityBackgroundColor;
        _statModifiers = statModifiers != null
            ? new List<ItemRuntimeStatModifier>(statModifiers)
            : new List<ItemRuntimeStatModifier>();
    }

    public int InstanceId { get; }
    public ItemDefinition Definition { get; }
    public ItemRarity Rarity { get; }
    public string RarityDisplayName { get; }
    public Color RarityBackgroundColor { get; }
    public IReadOnlyList<ItemRuntimeStatModifier> StatModifiers => _statModifiers;

    public string Id => Definition != null ? Definition.Id : "";
    public string DisplayName => Definition != null ? Definition.DisplayName : "";

    public static ItemRuntimeData CreateGenerated(int instanceId, ItemDefinition definition, ItemsConfig itemsConfig)
    {
        ItemRarity rarity = itemsConfig != null ? itemsConfig.RollRarity() : ItemRarity.Common;
        string rarityDisplayName = itemsConfig != null
            ? itemsConfig.GetRarityDisplayName(rarity)
            : ItemRarityDefinition.GetFallbackDisplayName(rarity);
        Color rarityBackgroundColor = itemsConfig != null
            ? itemsConfig.GetRarityBackgroundColor(rarity)
            : ItemRarityDefinition.GetFallbackBackgroundColor(rarity);
        List<ItemRuntimeStatModifier> statModifiers = BuildRolledModifiers(definition, itemsConfig, rarity);

        return new ItemRuntimeData(
            instanceId,
            definition,
            rarity,
            rarityDisplayName,
            rarityBackgroundColor,
            statModifiers);
    }

    private static List<ItemRuntimeStatModifier> BuildRolledModifiers(
        ItemDefinition definition,
        ItemsConfig itemsConfig,
        ItemRarity rarity)
    {
        List<ItemRuntimeStatModifier> modifiers = new List<ItemRuntimeStatModifier>();

        if (definition == null)
        {
            return modifiers;
        }

        IReadOnlyList<ItemStatModifier> configuredModifiers = definition.StatModifiers;

        for (int i = 0; i < configuredModifiers.Count; i++)
        {
            ItemStatModifier configuredModifier = configuredModifiers[i];

            if (configuredModifier == null)
            {
                continue;
            }

            float quality = itemsConfig != null
                ? itemsConfig.RollQualityForRarity(rarity)
                : ItemRarityDefinition.RollFallbackQuality(rarity);
            modifiers.Add(new ItemRuntimeStatModifier(configuredModifier, configuredModifier.RollValue(quality)));
        }

        return modifiers;
    }

    private static List<ItemRuntimeStatModifier> BuildFixedModifiers(ItemDefinition definition)
    {
        List<ItemRuntimeStatModifier> modifiers = new List<ItemRuntimeStatModifier>();

        if (definition == null)
        {
            return modifiers;
        }

        IReadOnlyList<ItemStatModifier> configuredModifiers = definition.StatModifiers;

        for (int i = 0; i < configuredModifiers.Count; i++)
        {
            ItemStatModifier configuredModifier = configuredModifiers[i];

            if (configuredModifier != null)
            {
                modifiers.Add(new ItemRuntimeStatModifier(configuredModifier, configuredModifier.Value));
            }
        }

        return modifiers;
    }
}

public sealed class ItemRuntimeStatModifier
{
    public ItemRuntimeStatModifier(ItemStatModifier source, float value)
    {
        if (source != null)
        {
            Target = source.Target;
            PrimaryStat = source.PrimaryStat;
            SecondaryStat = source.SecondaryStat;
        }

        Value = value;
    }

    public ItemStatTarget Target { get; }
    public PrimaryStatType PrimaryStat { get; }
    public SecondaryStatType SecondaryStat { get; }
    public float Value { get; }
}

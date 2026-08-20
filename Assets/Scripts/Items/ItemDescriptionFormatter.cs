using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class ItemDescriptionFormatter
{
    public static string BuildDetailsText(ItemRuntimeData item, StatsConfig statsConfig)
    {
        ItemDefinition definition = item != null ? item.Definition : null;

        if (definition == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        AppendHeader(builder, definition, item.RarityDisplayName);
        builder.AppendLine();
        builder.AppendLine("Модификаторы:");
        int modifiersCount = AppendModifierGroup(
            builder,
            item.StatModifiers,
            statsConfig,
            ItemStatTarget.Primary,
            "Основные статы:");
        modifiersCount += AppendModifierGroup(
            builder,
            item.StatModifiers,
            statsConfig,
            ItemStatTarget.Secondary,
            "Вторичные статы:");

        if (modifiersCount == 0)
        {
            builder.AppendLine("-");
        }

        AppendDescription(builder, definition);

        return builder.ToString().TrimEnd();
    }

    public static string BuildDetailsText(ItemDefinition item, StatsConfig statsConfig)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        AppendHeader(builder, item, "");
        builder.AppendLine();
        builder.AppendLine("Диапазоны модификаторов:");
        int modifiersCount = AppendModifierGroup(builder, item, statsConfig, ItemStatTarget.Primary, "Основные статы:");
        modifiersCount += AppendModifierGroup(builder, item, statsConfig, ItemStatTarget.Secondary, "Вторичные статы:");

        if (modifiersCount == 0)
        {
            builder.AppendLine("-");
        }

        AppendDescription(builder, item);

        return builder.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder builder, ItemDefinition item, string rarityDisplayName)
    {
        builder.AppendLine($"Тип: {GetGroupDisplayName(item.Group)}");

        if (!string.IsNullOrWhiteSpace(rarityDisplayName))
        {
            builder.AppendLine($"Редкость: {rarityDisplayName}");
        }
    }

    private static void AppendDescription(StringBuilder builder, ItemDefinition item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Description))
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Описание:");
        builder.AppendLine(item.Description.Trim());
    }

    private static int AppendModifierGroup(
        StringBuilder builder,
        IReadOnlyList<ItemRuntimeStatModifier> modifiers,
        StatsConfig statsConfig,
        ItemStatTarget target,
        string title)
    {
        int addedCount = 0;

        for (int i = 0; i < modifiers.Count; i++)
        {
            ItemRuntimeStatModifier modifier = modifiers[i];

            if (modifier != null && modifier.Target == target)
            {
                if (addedCount == 0)
                {
                    builder.AppendLine(title);
                }

                builder.AppendLine(FormatModifier(modifier, statsConfig));
                addedCount++;
            }
        }

        return addedCount;
    }

    private static int AppendModifierGroup(
        StringBuilder builder,
        ItemDefinition item,
        StatsConfig statsConfig,
        ItemStatTarget target,
        string title)
    {
        IReadOnlyList<ItemStatModifier> modifiers = item.StatModifiers;
        int addedCount = 0;

        for (int i = 0; i < modifiers.Count; i++)
        {
            ItemStatModifier modifier = modifiers[i];

            if (modifier != null && modifier.Target == target)
            {
                if (addedCount == 0)
                {
                    builder.AppendLine(title);
                }

                builder.AppendLine(FormatModifierRange(modifier, statsConfig));
                addedCount++;
            }
        }

        return addedCount;
    }

    private static string FormatModifier(ItemRuntimeStatModifier modifier, StatsConfig statsConfig)
    {
        string sign = modifier.Value > 0f ? "+" : "";
        string statName = GetModifierStatName(
            modifier.Target,
            modifier.PrimaryStat,
            modifier.SecondaryStat,
            statsConfig);
        string suffix = GetModifierValueSuffix(modifier.Target, modifier.SecondaryStat);
        return $"{statName}: {sign}{FormatValue(modifier.Value)}{suffix}";
    }

    private static string FormatModifierRange(ItemStatModifier modifier, StatsConfig statsConfig)
    {
        string statName = GetModifierStatName(
            modifier.Target,
            modifier.PrimaryStat,
            modifier.SecondaryStat,
            statsConfig);
        string suffix = GetModifierValueSuffix(modifier.Target, modifier.SecondaryStat);

        if (Mathf.Approximately(modifier.MinValue, modifier.MaxValue))
        {
            string sign = modifier.MinValue > 0f ? "+" : "";
            return $"{statName}: {sign}{FormatValue(modifier.MinValue)}{suffix}";
        }

        return $"{statName}: {FormatSignedValue(modifier.MinValue)}..{FormatSignedValue(modifier.MaxValue)}{suffix}";
    }

    private static string GetModifierStatName(
        ItemStatTarget target,
        PrimaryStatType primaryStat,
        SecondaryStatType secondaryStat,
        StatsConfig statsConfig)
    {
        switch (target)
        {
            case ItemStatTarget.Primary:
                return statsConfig != null
                    ? statsConfig.GetPrimaryStatDisplayName(primaryStat)
                    : primaryStat.ToString();
            case ItemStatTarget.Secondary:
                return statsConfig != null
                    ? statsConfig.GetSecondaryStatDisplayName(secondaryStat)
                    : secondaryStat.ToString();
            default:
                return "Стат";
        }
    }

    private static string GetModifierValueSuffix(ItemStatTarget target, SecondaryStatType secondaryStat)
    {
        if (target != ItemStatTarget.Secondary)
        {
            return "";
        }

        switch (secondaryStat)
        {
            case SecondaryStatType.AttackInterval:
                return " сек.";
            case SecondaryStatType.MaxHp:
                return " HP";
            case SecondaryStatType.ExtraLootChance:
            case SecondaryStatType.DodgeChance:
                return "%";
            default:
                return "";
        }
    }

    private static string GetGroupDisplayName(ItemGroup group)
    {
        switch (group)
        {
            case ItemGroup.Weapon:
                return "Оружие";
            case ItemGroup.Armor:
                return "Доспех";
            default:
                return group.ToString();
        }
    }

    private static string FormatValue(float value)
    {
        return Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedValue(float value)
    {
        string sign = value > 0f ? "+" : "";
        return $"{sign}{FormatValue(value)}";
    }
}

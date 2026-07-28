using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class ItemDescriptionFormatter
{
    public static string BuildDetailsText(ItemDefinition item, StatsConfig statsConfig)
    {
        if (item == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Тип: {GetGroupDisplayName(item.Group)}");
        builder.AppendLine();
        builder.AppendLine("Модификаторы:");
        int modifiersCount = AppendModifierGroup(builder, item, statsConfig, ItemStatTarget.Primary, "Основные статы:");
        modifiersCount += AppendModifierGroup(builder, item, statsConfig, ItemStatTarget.Secondary, "Вторичные статы:");

        if (modifiersCount == 0)
        {
            builder.AppendLine("-");
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            builder.AppendLine();
            builder.AppendLine("Описание:");
            builder.AppendLine(item.Description.Trim());
        }

        return builder.ToString().TrimEnd();
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

                builder.AppendLine(FormatModifier(modifier, statsConfig));
                addedCount++;
            }
        }

        return addedCount;
    }

    private static string FormatModifier(ItemStatModifier modifier, StatsConfig statsConfig)
    {
        string sign = modifier.Value > 0f ? "+" : "";
        string statName = GetModifierStatName(modifier, statsConfig);
        string suffix = GetModifierValueSuffix(modifier);
        return $"{statName}: {sign}{FormatValue(modifier.Value)}{suffix}";
    }

    private static string GetModifierStatName(ItemStatModifier modifier, StatsConfig statsConfig)
    {
        switch (modifier.Target)
        {
            case ItemStatTarget.Primary:
                return statsConfig != null
                    ? statsConfig.GetPrimaryStatDisplayName(modifier.PrimaryStat)
                    : modifier.PrimaryStat.ToString();
            case ItemStatTarget.Secondary:
                return statsConfig != null
                    ? statsConfig.GetSecondaryStatDisplayName(modifier.SecondaryStat)
                    : modifier.SecondaryStat.ToString();
            default:
                return "Стат";
        }
    }

    private static string GetModifierValueSuffix(ItemStatModifier modifier)
    {
        if (modifier.Target != ItemStatTarget.Secondary)
        {
            return "";
        }

        switch (modifier.SecondaryStat)
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
}

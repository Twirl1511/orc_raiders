using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[CreateAssetMenu(fileName = "Stats", menuName = "GAME/Stats")]
public sealed class StatsConfig : ScriptableObject
{
    private const string _primaryValuePosition = "42%";
    private const string _primaryBarPosition = "55%";
    private const string _primaryBarCellWidth = "0.55em";
    private const char _filledPrimaryStatCell = '■';
    private const char _emptyPrimaryStatCell = '□';
    private const string _filledPrimaryStatCellColor = "#FFFFFF";
    private const string _emptyPrimaryStatCellColor = "#5B6570";

    [SerializeField, Min(1)] private int _maxPrimaryStatValue = 20;
    [SerializeField, Min(0.01f)] private float _attackIntervalSeconds = 1f;
    [SerializeField, Min(0f)] private float _damageBlockPercentPerArmor = 3.75f;
    [SerializeField, Range(0f, 100f)] private float _maxDamageBlockPercent = 75f;
    [SerializeField] private List<PrimaryStatDefinition> _primaryStats = new List<PrimaryStatDefinition>();
    [SerializeField] private List<SecondaryStatDefinition> _secondaryStats = new List<SecondaryStatDefinition>();
    [SerializeField] private List<StatScalingRule> _scalingRules = new List<StatScalingRule>();

    public int MaxPrimaryStatValue => _maxPrimaryStatValue;
    public float AttackIntervalSeconds => _attackIntervalSeconds;
    public float DamageBlockPercentPerArmor => _damageBlockPercentPerArmor;
    public float MaxDamageBlockPercent => _maxDamageBlockPercent;
    public IReadOnlyList<PrimaryStatDefinition> PrimaryStats => _primaryStats;
    public IReadOnlyList<SecondaryStatDefinition> SecondaryStats => _secondaryStats;
    public IReadOnlyList<StatScalingRule> ScalingRules => _scalingRules;

    public int GetPrimaryStatMinimumValue(PrimaryStatType statType)
    {
        for (int i = 0; i < _primaryStats.Count; i++)
        {
            PrimaryStatDefinition definition = _primaryStats[i];

            if (definition != null && definition.StatType == statType)
            {
                return Mathf.Clamp(definition.MinimumValue, 0, _maxPrimaryStatValue);
            }
        }

        return 0;
    }

    public int ClampPrimaryStat(PrimaryStatType statType, int value)
    {
        return Mathf.Clamp(value, GetPrimaryStatMinimumValue(statType), _maxPrimaryStatValue);
    }

    public float GetSecondaryStatMinimumValue(SecondaryStatType statType)
    {
        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition != null && definition.StatType == statType)
            {
                return statType == SecondaryStatType.AttackInterval
                    ? _attackIntervalSeconds
                    : definition.MinimumValue;
            }
        }

        return 0f;
    }

    public SecondaryStatsSnapshot CalculateSecondaryStats(PrimaryStats primaryStats)
    {
        SecondaryStatsSnapshot snapshot = new SecondaryStatsSnapshot();

        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition != null && definition.StatType != SecondaryStatType.None)
            {
                float minimumValue = definition.StatType == SecondaryStatType.AttackInterval
                    ? _attackIntervalSeconds
                    : definition.MinimumValue;
                snapshot.Add(definition.StatType, minimumValue);
            }
        }

        for (int i = 0; i < _scalingRules.Count; i++)
        {
            StatScalingRule rule = _scalingRules[i];

            if (rule != null)
            {
                snapshot.Add(rule.TargetStat, rule.Calculate(primaryStats));
            }
        }

        return snapshot;
    }

    public string GetPrimaryStatsSummary(PrimaryStats primaryStats)
    {
        if (primaryStats == null)
        {
            return "-";
        }

        List<string> lines = new List<string>();

        for (int i = 0; i < _primaryStats.Count; i++)
        {
            PrimaryStatDefinition definition = _primaryStats[i];

            if (definition == null || definition.StatType == PrimaryStatType.None)
            {
                continue;
            }

            int value = primaryStats.GetValue(definition.StatType);
            lines.Add(FormatPrimaryStatLine(definition.DisplayName, value));
        }

        if (lines.Count == 0)
        {
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(PrimaryStatType.Endurance), primaryStats.Endurance));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(PrimaryStatType.Strength), primaryStats.Strength));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(PrimaryStatType.Agility), primaryStats.Agility));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(PrimaryStatType.Intelligence), primaryStats.Intelligence));
        }

        return string.Join("\n", lines);
    }

    public string GetPrimaryStatDisplayName(PrimaryStatType statType)
    {
        for (int i = 0; i < _primaryStats.Count; i++)
        {
            PrimaryStatDefinition definition = _primaryStats[i];

            if (definition != null && definition.StatType == statType)
            {
                return definition.DisplayName;
            }
        }

        return GetFallbackPrimaryStatDisplayName(statType);
    }

    public string GetSecondaryStatDisplayName(SecondaryStatType statType)
    {
        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition != null && definition.StatType == statType)
            {
                return definition.DisplayName;
            }
        }

        return GetFallbackSecondaryStatDisplayName(statType);
    }

    public string GetSecondaryStatsSummary(PrimaryStats primaryStats)
    {
        return GetSecondaryStatsSummary(CalculateSecondaryStats(primaryStats));
    }

    public string GetSecondaryStatsSummary(SecondaryStatsSnapshot snapshot)
    {
        List<string> lines = new List<string>();

        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition == null || definition.StatType == SecondaryStatType.None)
            {
                continue;
            }

            float value = snapshot.GetValue(definition.StatType);
            lines.Add(FormatSecondaryStatLine(definition, value));
        }

        return lines.Count == 0 ? "-" : string.Join("\n", lines);
    }

    public float CalculateArmorBlockedDamagePercent(float armor)
    {
        return Mathf.Clamp(armor * _damageBlockPercentPerArmor, 0f, _maxDamageBlockPercent);
    }

    public bool ValidateForRuntime(UnityEngine.Object context)
    {
        bool valid = true;

        if (_primaryStats.Count == 0)
        {
            Debug.LogError($"{nameof(StatsConfig)} requires primary stat definitions.", context);
            valid = false;
        }

        if (_secondaryStats.Count == 0)
        {
            Debug.LogError($"{nameof(StatsConfig)} requires secondary stat definitions.", context);
            valid = false;
        }

        if (_scalingRules.Count == 0)
        {
            Debug.LogError($"{nameof(StatsConfig)} requires stat scaling rules.", context);
            valid = false;
        }

        if (_attackIntervalSeconds <= 0f)
        {
            Debug.LogError($"{nameof(StatsConfig)} attack interval must be greater than 0.", context);
            valid = false;
        }

        if (_damageBlockPercentPerArmor < 0f)
        {
            Debug.LogError($"{nameof(StatsConfig)} damage block percent per armor cannot be negative.", context);
            valid = false;
        }

        return valid;
    }

    private void OnValidate()
    {
        ValidateForRuntime(this);
    }

    private static string GetFallbackPrimaryStatDisplayName(PrimaryStatType statType)
    {
        switch (statType)
        {
            case PrimaryStatType.Endurance:
                return "Выносливость";
            case PrimaryStatType.Strength:
                return "Сила";
            case PrimaryStatType.Agility:
                return "Ловкость";
            case PrimaryStatType.Intelligence:
                return "Интеллект";
            default:
                return statType.ToString();
        }
    }

    private static string GetFallbackSecondaryStatDisplayName(SecondaryStatType statType)
    {
        switch (statType)
        {
            case SecondaryStatType.AttackInterval:
                return "Интервал атаки";
            case SecondaryStatType.MeleeDamage:
                return "Урон в ближнем бою";
            case SecondaryStatType.MaxHp:
                return "Здоровье";
            case SecondaryStatType.RangedDamage:
                return "Урон в дальнем бою";
            case SecondaryStatType.ExtraLootChance:
                return "Шанс найти больше лута";
            case SecondaryStatType.DodgeChance:
                return "Уклонение";
            case SecondaryStatType.Armor:
                return "Броня";
            default:
                return statType.ToString();
        }
    }

    private static string FormatValue(float value, string suffix)
    {
        string number = Mathf.Approximately(value, Mathf.Round(value))
            ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

        return $"{number}{suffix}";
    }

    private string FormatSecondaryStatLine(SecondaryStatDefinition definition, float value)
    {
        switch (definition.StatType)
        {
            case SecondaryStatType.AttackInterval:
                return $"{definition.DisplayName}: {FormatValue(Mathf.Max(0.01f, value), " сек.")}";
            case SecondaryStatType.Armor:
                return FormatArmorStatLine(value, definition.DisplayName, definition.ValueSuffix);
            default:
                return $"{definition.DisplayName}: {FormatValue(value, definition.ValueSuffix)}";
        }
    }

    private string FormatArmorStatLine(float value, string displayName = null, string valueSuffix = "")
    {
        string armorDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? GetSecondaryStatDisplayName(SecondaryStatType.Armor)
            : displayName;
        float blockedDamagePercent = CalculateArmorBlockedDamagePercent(value);
        return $"{armorDisplayName}: {FormatValue(value, valueSuffix)}    Блокируется {FormatValue(blockedDamagePercent, "%")} урона.";
    }

    private string FormatPrimaryStatLine(string displayName, int value)
    {
        int maxValue = Mathf.Max(1, _maxPrimaryStatValue);
        int filledCells = Mathf.Clamp(value, 0, maxValue);
        int emptyCells = maxValue - filledCells;
        string filled = new string(_filledPrimaryStatCell, filledCells);
        string empty = new string(_emptyPrimaryStatCell, emptyCells);

        return $"<nobr>{displayName}: <pos={_primaryValuePosition}>{value}/{maxValue}<pos={_primaryBarPosition}>" +
            $"<mspace={_primaryBarCellWidth}><color={_filledPrimaryStatCellColor}>{filled}</color><color={_emptyPrimaryStatCellColor}>{empty}</color></mspace></nobr>";
    }
}

public enum SecondaryStatType
{
    None = 0,
    AttackInterval = 1,
    MeleeDamage = 2,
    MaxHp = 3,
    RangedDamage = 4,
    ExtraLootChance = 5,
    DodgeChance = 6,
    Armor = 7
}

public enum StatModifierMode
{
    Flat = 0,
    Percent = 1
}

[Serializable]
public sealed class PrimaryStatDefinition
{
    [SerializeField] private PrimaryStatType _statType = PrimaryStatType.None;
    [SerializeField] private string _displayName = "Стат";
    [SerializeField, Min(0)] private int _minimumValue = 1;
    [SerializeField, TextArea] private string _description = "";

    public PrimaryStatType StatType => _statType;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _statType.ToString() : _displayName;
    public int MinimumValue => _minimumValue;
    public string Description => _description;
}

[Serializable]
public sealed class SecondaryStatDefinition
{
    [SerializeField] private SecondaryStatType _statType = SecondaryStatType.None;
    [SerializeField] private string _displayName = "Вторичный стат";
    [SerializeField] private string _valueSuffix = "";
    [SerializeField, Min(0f)] private float _minimumValue = 0f;
    [SerializeField, TextArea] private string _description = "";

    public SecondaryStatType StatType => _statType;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _statType.ToString() : _displayName;
    public string ValueSuffix => _valueSuffix;
    public float MinimumValue => _minimumValue;
    public string Description => _description;
}

[Serializable]
public sealed class StatScalingRule
{
    [SerializeField] private PrimaryStatType _sourceStat = PrimaryStatType.None;
    [SerializeField] private SecondaryStatType _targetStat = SecondaryStatType.None;
    [SerializeField] private StatModifierMode _mode = StatModifierMode.Flat;
    [SerializeField] private float _valuePerPoint = 1f;
    [SerializeField, TextArea] private string _description = "";

    public PrimaryStatType SourceStat => _sourceStat;
    public SecondaryStatType TargetStat => _targetStat;
    public StatModifierMode Mode => _mode;
    public float ValuePerPoint => _valuePerPoint;
    public string Description => _description;

    public float Calculate(PrimaryStats primaryStats)
    {
        if (primaryStats == null)
        {
            return 0f;
        }

        return primaryStats.GetValue(_sourceStat) * _valuePerPoint;
    }
}

[Serializable]
public struct SecondaryStatsSnapshot
{
    [SerializeField] private float _attackInterval;
    [SerializeField] private float _meleeDamage;
    [SerializeField] private float _maxHp;
    [SerializeField] private float _rangedDamage;
    [SerializeField] private float _extraLootChance;
    [SerializeField] private float _dodgeChance;
    [SerializeField] private float _armor;

    public float AttackInterval => _attackInterval;
    public float MeleeDamage => _meleeDamage;
    public float MaxHp => _maxHp;
    public float RangedDamage => _rangedDamage;
    public float ExtraLootChance => _extraLootChance;
    public float DodgeChance => _dodgeChance;
    public float Armor => _armor;

    public void Add(SecondaryStatType statType, float value)
    {
        switch (statType)
        {
            case SecondaryStatType.AttackInterval:
                _attackInterval = Mathf.Max(0.01f, _attackInterval + value);
                break;
            case SecondaryStatType.MeleeDamage:
                _meleeDamage += value;
                break;
            case SecondaryStatType.MaxHp:
                _maxHp += value;
                break;
            case SecondaryStatType.RangedDamage:
                _rangedDamage += value;
                break;
            case SecondaryStatType.ExtraLootChance:
                _extraLootChance += value;
                break;
            case SecondaryStatType.DodgeChance:
                _dodgeChance += value;
                break;
            case SecondaryStatType.Armor:
                _armor += value;
                break;
        }
    }

    public float GetValue(SecondaryStatType statType)
    {
        switch (statType)
        {
            case SecondaryStatType.AttackInterval:
                return _attackInterval;
            case SecondaryStatType.MeleeDamage:
                return _meleeDamage;
            case SecondaryStatType.MaxHp:
                return _maxHp;
            case SecondaryStatType.RangedDamage:
                return _rangedDamage;
            case SecondaryStatType.ExtraLootChance:
                return _extraLootChance;
            case SecondaryStatType.DodgeChance:
                return _dodgeChance;
            case SecondaryStatType.Armor:
                return _armor;
            default:
                return 0f;
        }
    }
}

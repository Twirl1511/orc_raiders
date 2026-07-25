using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[CreateAssetMenu(fileName = "Stats", menuName = "GAME/Stats")]
public sealed class StatsConfig : ScriptableObject
{
    private const string _primaryValuePosition = "34%";
    private const string _primaryBarPosition = "48%";
    private const string _primaryBarCellWidth = "0.55em";
    private const char _filledPrimaryStatCell = '■';
    private const char _emptyPrimaryStatCell = '□';
    private const string _filledPrimaryStatCellColor = "#FFFFFF";
    private const string _emptyPrimaryStatCellColor = "#5B6570";

    [SerializeField, Min(1)] private int _maxPrimaryStatValue = 20;
    [SerializeField] private List<PrimaryStatDefinition> _primaryStats = new List<PrimaryStatDefinition>();
    [SerializeField] private List<SecondaryStatDefinition> _secondaryStats = new List<SecondaryStatDefinition>();
    [SerializeField] private List<StatScalingRule> _scalingRules = new List<StatScalingRule>();

    public int MaxPrimaryStatValue => _maxPrimaryStatValue;
    public IReadOnlyList<PrimaryStatDefinition> PrimaryStats => _primaryStats;
    public IReadOnlyList<SecondaryStatDefinition> SecondaryStats => _secondaryStats;
    public IReadOnlyList<StatScalingRule> ScalingRules => _scalingRules;

    public int ClampPrimaryStat(int value)
    {
        return Mathf.Clamp(value, 0, _maxPrimaryStatValue);
    }

    public SecondaryStatsSnapshot CalculateSecondaryStats(OrcStats primaryStats)
    {
        SecondaryStatsSnapshot snapshot = new SecondaryStatsSnapshot();

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

    public string GetPrimaryStatsSummary(OrcStats primaryStats)
    {
        if (primaryStats == null)
        {
            return "-";
        }

        List<string> lines = new List<string>();

        for (int i = 0; i < _primaryStats.Count; i++)
        {
            PrimaryStatDefinition definition = _primaryStats[i];

            if (definition == null || definition.StatType == OrcStatType.None)
            {
                continue;
            }

            int value = primaryStats.GetValue(definition.StatType);
            lines.Add(FormatPrimaryStatLine(definition.DisplayName, value));
        }

        if (lines.Count == 0)
        {
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(OrcStatType.Endurance), primaryStats.Endurance));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(OrcStatType.Strength), primaryStats.Strength));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(OrcStatType.Agility), primaryStats.Agility));
            lines.Add(FormatPrimaryStatLine(GetFallbackPrimaryStatDisplayName(OrcStatType.Intelligence), primaryStats.Intelligence));
        }

        return string.Join("\n", lines);
    }

    public string GetPrimaryStatDisplayName(OrcStatType statType)
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

    public string GetSecondaryStatDisplayName(OrcSecondaryStatType statType)
    {
        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition != null && definition.StatType == statType)
            {
                return definition.DisplayName;
            }
        }

        return statType.ToString();
    }

    public string GetSecondaryStatsSummary(OrcStats primaryStats)
    {
        SecondaryStatsSnapshot snapshot = CalculateSecondaryStats(primaryStats);
        List<string> lines = new List<string>();

        for (int i = 0; i < _secondaryStats.Count; i++)
        {
            SecondaryStatDefinition definition = _secondaryStats[i];

            if (definition == null || definition.StatType == OrcSecondaryStatType.None || !definition.ShowInOrcInfo)
            {
                continue;
            }

            float value = snapshot.GetValue(definition.StatType);
            lines.Add($"{definition.DisplayName}: {FormatValue(value, definition.ValueSuffix)}");
        }

        return lines.Count == 0 ? "-" : string.Join("\n", lines);
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

        return valid;
    }

    private void OnValidate()
    {
        ValidateForRuntime(this);
    }

    private static string GetFallbackPrimaryStatDisplayName(OrcStatType statType)
    {
        switch (statType)
        {
            case OrcStatType.Endurance:
                return "Выносливость";
            case OrcStatType.Strength:
                return "Сила";
            case OrcStatType.Agility:
                return "Ловкость";
            case OrcStatType.Intelligence:
                return "Интеллект";
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

public enum OrcSecondaryStatType
{
    None = 0,
    AttackSpeed = 1,
    MeleeDamage = 2,
    MaxHp = 3,
    RangedDamage = 4,
    ExtraLootChance = 5,
    DodgeChance = 6
}

public enum StatModifierMode
{
    Flat = 0,
    Percent = 1
}

[Serializable]
public sealed class PrimaryStatDefinition
{
    [SerializeField] private OrcStatType _statType = OrcStatType.None;
    [SerializeField] private string _displayName = "Стат";
    [SerializeField, TextArea] private string _description = "";

    public OrcStatType StatType => _statType;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _statType.ToString() : _displayName;
    public string Description => _description;
}

[Serializable]
public sealed class SecondaryStatDefinition
{
    [SerializeField] private OrcSecondaryStatType _statType = OrcSecondaryStatType.None;
    [SerializeField] private string _displayName = "Вторичный стат";
    [SerializeField] private string _valueSuffix = "";
    [SerializeField] private bool _showInOrcInfo = true;
    [SerializeField, TextArea] private string _description = "";

    public OrcSecondaryStatType StatType => _statType;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _statType.ToString() : _displayName;
    public string ValueSuffix => _valueSuffix;
    public bool ShowInOrcInfo => _showInOrcInfo;
    public string Description => _description;
}

[Serializable]
public sealed class StatScalingRule
{
    [SerializeField] private OrcStatType _sourceStat = OrcStatType.None;
    [SerializeField] private OrcSecondaryStatType _targetStat = OrcSecondaryStatType.None;
    [SerializeField] private StatModifierMode _mode = StatModifierMode.Flat;
    [SerializeField] private float _valuePerPoint = 1f;
    [SerializeField, TextArea] private string _description = "";

    public OrcStatType SourceStat => _sourceStat;
    public OrcSecondaryStatType TargetStat => _targetStat;
    public StatModifierMode Mode => _mode;
    public float ValuePerPoint => _valuePerPoint;
    public string Description => _description;

    public float Calculate(OrcStats primaryStats)
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
    [SerializeField] private float _attackSpeed;
    [SerializeField] private float _meleeDamage;
    [SerializeField] private float _maxHp;
    [SerializeField] private float _rangedDamage;
    [SerializeField] private float _extraLootChance;
    [SerializeField] private float _dodgeChance;

    public float AttackSpeed => _attackSpeed;
    public float MeleeDamage => _meleeDamage;
    public float MaxHp => _maxHp;
    public float RangedDamage => _rangedDamage;
    public float ExtraLootChance => _extraLootChance;
    public float DodgeChance => _dodgeChance;

    public void Add(OrcSecondaryStatType statType, float value)
    {
        switch (statType)
        {
            case OrcSecondaryStatType.AttackSpeed:
                _attackSpeed += value;
                break;
            case OrcSecondaryStatType.MeleeDamage:
                _meleeDamage += value;
                break;
            case OrcSecondaryStatType.MaxHp:
                _maxHp += value;
                break;
            case OrcSecondaryStatType.RangedDamage:
                _rangedDamage += value;
                break;
            case OrcSecondaryStatType.ExtraLootChance:
                _extraLootChance += value;
                break;
            case OrcSecondaryStatType.DodgeChance:
                _dodgeChance += value;
                break;
        }
    }

    public float GetValue(OrcSecondaryStatType statType)
    {
        switch (statType)
        {
            case OrcSecondaryStatType.AttackSpeed:
                return _attackSpeed;
            case OrcSecondaryStatType.MeleeDamage:
                return _meleeDamage;
            case OrcSecondaryStatType.MaxHp:
                return _maxHp;
            case OrcSecondaryStatType.RangedDamage:
                return _rangedDamage;
            case OrcSecondaryStatType.ExtraLootChance:
                return _extraLootChance;
            case OrcSecondaryStatType.DodgeChance:
                return _dodgeChance;
            default:
                return 0f;
        }
    }
}

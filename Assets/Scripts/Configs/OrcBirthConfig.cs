using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Orc Birth", menuName = "GAME/Orc Birth")]
public sealed class OrcBirthConfig : ScriptableObject
{
    [Header("Dice")]
    [SerializeField, Min(1)] private int _requiredDiceCount = 6;
    [SerializeField] private DiceConfig _diceConfig = null;

    [Header("Stats")]
    [SerializeField] private StatsConfig _statsConfig = null;
    [SerializeField] private RestConfig _restConfig = null;
    [SerializeField] private LevelUpConfig _levelUpConfig = null;

    [Header("Orc")]
    [SerializeField] private Vector2 _orcVisualSize = new Vector2(0.65f, 0.65f);
    [SerializeField] private Color _orcVisualColor = new Color(0.45f, 0.75f, 0.4f, 1f);
    [SerializeField] private int _orcSpriteSortingOrder = 20;
    [SerializeField] private int _orcLabelSortingOrder = 25;
    [SerializeField] private Vector2 _firstOrcSpawnPosition = new Vector2(-5.5f, -1.7f);
    [SerializeField] private Vector2 _orcSpawnSpacing = new Vector2(1.45f, 0f);
    [SerializeField, Min(1)] private int _maxOrcsPerRow = 6;

    [Header("Rest")]
    [SerializeField] private Vector2 _firstRestingOrcPosition = new Vector2(3.2f, -1.7f);
    [SerializeField] private Vector2 _restingOrcSpacing = new Vector2(1f, 0f);
    [SerializeField, Min(1)] private int _maxRestingOrcsPerRow = 4;

    public int RequiredDiceCount => _requiredDiceCount;
    public DiceConfig DiceConfig => _diceConfig;
    public StatsConfig StatsConfig => _statsConfig;
    public RestConfig RestConfig => _restConfig;
    public LevelUpConfig LevelUpConfig => _levelUpConfig;
    public Vector2 OrcVisualSize => new Vector2(Mathf.Max(0.01f, _orcVisualSize.x), Mathf.Max(0.01f, _orcVisualSize.y));
    public Color OrcVisualColor => _orcVisualColor;
    public int OrcSpriteSortingOrder => _orcSpriteSortingOrder;
    public int OrcLabelSortingOrder => _orcLabelSortingOrder;
    public Vector2 FirstOrcSpawnPosition => _firstOrcSpawnPosition;
    public Vector2 OrcSpawnSpacing => _orcSpawnSpacing;
    public int MaxOrcsPerRow => _maxOrcsPerRow;
    public Vector2 FirstRestingOrcPosition => _firstRestingOrcPosition;
    public Vector2 RestingOrcSpacing => _restingOrcSpacing;
    public int MaxRestingOrcsPerRow => _maxRestingOrcsPerRow;

    public Vector2 GetOrcVisualSizeForLevel(int level)
    {
        float multiplier = _levelUpConfig != null
            ? _levelUpConfig.GetOrcVisualSizeMultiplierForLevel(level)
            : 1f;
        return OrcVisualSize * multiplier;
    }
}

public enum OrcStatType
{
    None = 0,
    Endurance = 1,
    Strength = 2,
    Agility = 3,
    Intelligence = 4
}

[Serializable]
public sealed class OrcStats
{
    [SerializeField] private int _endurance;
    [SerializeField] private int _strength;
    [SerializeField] private int _agility;
    [SerializeField] private int _intelligence;

    public int Endurance => _endurance;
    public int Strength => _strength;
    public int Agility => _agility;
    public int Intelligence => _intelligence;

    public void SetToMinimums(StatsConfig statsConfig)
    {
        if (statsConfig == null)
        {
            return;
        }

        _endurance = statsConfig.GetPrimaryStatMinimumValue(OrcStatType.Endurance);
        _strength = statsConfig.GetPrimaryStatMinimumValue(OrcStatType.Strength);
        _agility = statsConfig.GetPrimaryStatMinimumValue(OrcStatType.Agility);
        _intelligence = statsConfig.GetPrimaryStatMinimumValue(OrcStatType.Intelligence);
    }

    public void Apply(DiceFaceDefinition face)
    {
        Apply(face.Add.StatType, face.Add.Value);
        Apply(face.Remove.StatType, -face.Remove.Value);
    }

    public void ApplyLevelBonuses(LevelUpConfig levelUpConfig, StatsConfig statsConfig)
    {
        if (levelUpConfig == null)
        {
            return;
        }

        IReadOnlyList<PrimaryStatLevelBonus> bonuses = levelUpConfig.PrimaryStatBonuses;

        for (int i = 0; i < bonuses.Count; i++)
        {
            PrimaryStatLevelBonus bonus = bonuses[i];

            if (bonus != null)
            {
                Apply(bonus.StatType, bonus.ValuePerLevel);
            }
        }

        Clamp(statsConfig);
    }

    public bool TryAddPrimaryStatPoint(OrcStatType statType, StatsConfig statsConfig)
    {
        int previousValue = GetValue(statType);

        if (statType == OrcStatType.None)
        {
            return false;
        }

        Apply(statType, 1);
        Clamp(statsConfig);
        return GetValue(statType) > previousValue;
    }

    private void Apply(OrcStatType statType, int value)
    {
        if (value == 0)
        {
            return;
        }

        switch (statType)
        {
            case OrcStatType.Endurance:
                _endurance += value;
                break;
            case OrcStatType.Strength:
                _strength += value;
                break;
            case OrcStatType.Agility:
                _agility += value;
                break;
            case OrcStatType.Intelligence:
                _intelligence += value;
                break;
        }
    }

    public void ClampAfterBirth(StatsConfig statsConfig)
    {
        Clamp(statsConfig);
    }

    private void Clamp(StatsConfig statsConfig)
    {
        if (statsConfig != null)
        {
            _endurance = statsConfig.ClampPrimaryStat(OrcStatType.Endurance, _endurance);
            _strength = statsConfig.ClampPrimaryStat(OrcStatType.Strength, _strength);
            _agility = statsConfig.ClampPrimaryStat(OrcStatType.Agility, _agility);
            _intelligence = statsConfig.ClampPrimaryStat(OrcStatType.Intelligence, _intelligence);
        }
    }

    public int GetValue(OrcStatType statType)
    {
        switch (statType)
        {
            case OrcStatType.Endurance:
                return _endurance;
            case OrcStatType.Strength:
                return _strength;
            case OrcStatType.Agility:
                return _agility;
            case OrcStatType.Intelligence:
                return _intelligence;
            default:
                return 0;
        }
    }

    public string GetSummary(StatsConfig statsConfig)
    {
        if (statsConfig != null)
        {
            return statsConfig.GetPrimaryStatsSummary(this);
        }

        return $"{GetDisplayName(statsConfig, OrcStatType.Endurance)}: {_endurance}\n" +
            $"{GetDisplayName(statsConfig, OrcStatType.Strength)}: {_strength}\n" +
            $"{GetDisplayName(statsConfig, OrcStatType.Agility)}: {_agility}\n" +
            $"{GetDisplayName(statsConfig, OrcStatType.Intelligence)}: {_intelligence}";
    }

    private static string GetDisplayName(StatsConfig statsConfig, OrcStatType statType)
    {
        if (statsConfig != null)
        {
            return statsConfig.GetPrimaryStatDisplayName(statType);
        }

        return DiceFaceDefinition.GetStatDisplayName(statType);
    }
}

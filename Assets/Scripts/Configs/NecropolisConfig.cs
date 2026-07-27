using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Necropolis", menuName = "GAME/Necropolis")]
public sealed class NecropolisConfig : ScriptableObject
{
    [Header("Dice")]
    [SerializeField, Min(1)] private int _requiredDiceCount = 6;
    [SerializeField] private DiceConfig _diceConfig = null;

    [Header("Stats")]
    [SerializeField] private StatsConfig _statsConfig = null;
    [SerializeField] private RestConfig _restConfig = null;
    [SerializeField] private LevelUpConfig _levelUpConfig = null;

    [Header("Hero")]
    [SerializeField] private Vector2 _heroVisualSize = new Vector2(0.65f, 0.65f);
    [SerializeField] private Color _heroVisualColor = new Color(0.45f, 0.75f, 0.4f, 1f);
    [SerializeField] private int _heroSpriteSortingOrder = 20;
    [SerializeField] private int _heroLabelSortingOrder = 25;
    [SerializeField] private Vector2 _firstHeroSpawnPosition = new Vector2(-5.5f, -1.7f);
    [SerializeField] private Vector2 _heroSpawnSpacing = new Vector2(1.45f, 0f);
    [SerializeField, Min(1)] private int _maxHeroesPerRow = 6;

    [Header("Rest")]
    [SerializeField] private Vector2 _firstRestingHeroPosition = new Vector2(3.2f, -1.7f);
    [SerializeField] private Vector2 _restingHeroSpacing = new Vector2(1f, 0f);
    [SerializeField, Min(1)] private int _maxRestingHeroesPerRow = 4;

    public int RequiredDiceCount => _requiredDiceCount;
    public DiceConfig DiceConfig => _diceConfig;
    public StatsConfig StatsConfig => _statsConfig;
    public RestConfig RestConfig => _restConfig;
    public LevelUpConfig LevelUpConfig => _levelUpConfig;
    public Vector2 HeroVisualSize => new Vector2(Mathf.Max(0.01f, _heroVisualSize.x), Mathf.Max(0.01f, _heroVisualSize.y));
    public Color HeroVisualColor => _heroVisualColor;
    public int HeroSpriteSortingOrder => _heroSpriteSortingOrder;
    public int HeroLabelSortingOrder => _heroLabelSortingOrder;
    public Vector2 FirstHeroSpawnPosition => _firstHeroSpawnPosition;
    public Vector2 HeroSpawnSpacing => _heroSpawnSpacing;
    public int MaxHeroesPerRow => _maxHeroesPerRow;
    public Vector2 FirstRestingHeroPosition => _firstRestingHeroPosition;
    public Vector2 RestingHeroSpacing => _restingHeroSpacing;
    public int MaxRestingHeroesPerRow => _maxRestingHeroesPerRow;

    public Vector2 GetHeroVisualSizeForLevel(int level)
    {
        float multiplier = _levelUpConfig != null
            ? _levelUpConfig.GetHeroVisualSizeMultiplierForLevel(level)
            : 1f;
        return HeroVisualSize * multiplier;
    }
}

public enum PrimaryStatType
{
    None = 0,
    Endurance = 1,
    Strength = 2,
    Agility = 3,
    Intelligence = 4
}

[Serializable]
public sealed class PrimaryStats
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

        _endurance = statsConfig.GetPrimaryStatMinimumValue(PrimaryStatType.Endurance);
        _strength = statsConfig.GetPrimaryStatMinimumValue(PrimaryStatType.Strength);
        _agility = statsConfig.GetPrimaryStatMinimumValue(PrimaryStatType.Agility);
        _intelligence = statsConfig.GetPrimaryStatMinimumValue(PrimaryStatType.Intelligence);
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

    public bool TryAddPrimaryStatPoint(PrimaryStatType statType, StatsConfig statsConfig)
    {
        int previousValue = GetValue(statType);

        if (statType == PrimaryStatType.None)
        {
            return false;
        }

        Apply(statType, 1);
        Clamp(statsConfig);
        return GetValue(statType) > previousValue;
    }

    private void Apply(PrimaryStatType statType, int value)
    {
        if (value == 0)
        {
            return;
        }

        switch (statType)
        {
            case PrimaryStatType.Endurance:
                _endurance += value;
                break;
            case PrimaryStatType.Strength:
                _strength += value;
                break;
            case PrimaryStatType.Agility:
                _agility += value;
                break;
            case PrimaryStatType.Intelligence:
                _intelligence += value;
                break;
        }
    }

    public void ClampAfterCreation(StatsConfig statsConfig)
    {
        Clamp(statsConfig);
    }

    private void Clamp(StatsConfig statsConfig)
    {
        if (statsConfig != null)
        {
            _endurance = statsConfig.ClampPrimaryStat(PrimaryStatType.Endurance, _endurance);
            _strength = statsConfig.ClampPrimaryStat(PrimaryStatType.Strength, _strength);
            _agility = statsConfig.ClampPrimaryStat(PrimaryStatType.Agility, _agility);
            _intelligence = statsConfig.ClampPrimaryStat(PrimaryStatType.Intelligence, _intelligence);
        }
    }

    public int GetValue(PrimaryStatType statType)
    {
        switch (statType)
        {
            case PrimaryStatType.Endurance:
                return _endurance;
            case PrimaryStatType.Strength:
                return _strength;
            case PrimaryStatType.Agility:
                return _agility;
            case PrimaryStatType.Intelligence:
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

        return $"{GetDisplayName(statsConfig, PrimaryStatType.Endurance)}: {_endurance}\n" +
            $"{GetDisplayName(statsConfig, PrimaryStatType.Strength)}: {_strength}\n" +
            $"{GetDisplayName(statsConfig, PrimaryStatType.Agility)}: {_agility}\n" +
            $"{GetDisplayName(statsConfig, PrimaryStatType.Intelligence)}: {_intelligence}";
    }

    private static string GetDisplayName(StatsConfig statsConfig, PrimaryStatType statType)
    {
        if (statsConfig != null)
        {
            return statsConfig.GetPrimaryStatDisplayName(statType);
        }

        return DiceFaceDefinition.GetStatDisplayName(statType);
    }
}

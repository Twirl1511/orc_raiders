using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "World Map", menuName = "GAME/World Map")]
public sealed class WorldMapConfig : ScriptableObject
{
    [Header("Roster")]
    [SerializeField, Min(1)] private int _rosterSize = 3;

    [Header("Investigation")]
    [SerializeField, Min(0.1f)] private float _investigationSeconds = 4f;
    [SerializeField, Min(1)] private int _investigationBlockCount = 1;
    [SerializeField, Range(0f, 100f)] private float _investigationSecondsReductionPercentPerIntelligence = 3f;
    [SerializeField, Range(0f, 100f)] private float _investigationSecondsReductionPercentPerAgility = 2f;
    [SerializeField, Range(0f, 100f)] private float _baseInvestigationSuccessChancePercent = 55f;
    [SerializeField, Range(0f, 100f)] private float _investigationSuccessChancePercentPerLevel = 5f;
    [SerializeField, Range(0f, 100f)] private float _investigationSuccessChancePercentPerAgility = 2f;
    [SerializeField, Range(0f, 100f)] private float _investigationSuccessChancePercentPerIntelligence = 3f;
    [SerializeField, Range(0f, 100f)] private float _earlyInvestigationBlockSuccessBonusGapPercent = 50f;
    [SerializeField, Range(0f, 100f)] private float _investigationFailureAttackReadinessPercent = 20f;

    [Header("Attack Readiness")]
    [SerializeField, Min(1f)] private float _secondsUntilMapThreat = 180f;

    [SerializeField] private List<EnemyType> _rosterEnemyTypes = new List<EnemyType>
    {
        EnemyType.HumanWarrior,
        EnemyType.HumanArcher,
        EnemyType.HumanMage
    };

    public int RosterSize => Mathf.Max(1, _rosterSize);
    public float InvestigationSeconds => Mathf.Max(0.1f, _investigationSeconds);
    public int InvestigationBlockCount => Mathf.Max(1, _investigationBlockCount);
    public float InvestigationSecondsReductionPercentPerIntelligence => Mathf.Clamp(_investigationSecondsReductionPercentPerIntelligence, 0f, 100f);
    public float InvestigationSecondsReductionPercentPerAgility => Mathf.Clamp(_investigationSecondsReductionPercentPerAgility, 0f, 100f);
    public float BaseInvestigationSuccessChancePercent => Mathf.Clamp(_baseInvestigationSuccessChancePercent, 0f, 100f);
    public float InvestigationSuccessChancePercentPerLevel => Mathf.Clamp(_investigationSuccessChancePercentPerLevel, 0f, 100f);
    public float InvestigationSuccessChancePercentPerAgility => Mathf.Clamp(_investigationSuccessChancePercentPerAgility, 0f, 100f);
    public float InvestigationSuccessChancePercentPerIntelligence => Mathf.Clamp(_investigationSuccessChancePercentPerIntelligence, 0f, 100f);
    public float EarlyInvestigationBlockSuccessBonusGapPercent => Mathf.Clamp(_earlyInvestigationBlockSuccessBonusGapPercent, 0f, 100f);
    public float InvestigationFailureAttackReadinessPercent => Mathf.Clamp(_investigationFailureAttackReadinessPercent, 0f, 100f);
    public float SecondsUntilMapThreat => Mathf.Max(1f, _secondsUntilMapThreat);
    public IReadOnlyList<EnemyType> RosterEnemyTypes => _rosterEnemyTypes;

    public float GetInvestigationSeconds(PrimaryStats heroStats)
    {
        if (heroStats == null)
        {
            return InvestigationSeconds;
        }

        float reductionPercent =
            heroStats.Intelligence * InvestigationSecondsReductionPercentPerIntelligence +
            heroStats.Agility * InvestigationSecondsReductionPercentPerAgility;
        float multiplier = 1f - Mathf.Clamp(reductionPercent, 0f, 90f) / 100f;
        return Mathf.Max(0.1f, InvestigationSeconds * multiplier);
    }

    public float GetInvestigationSuccessChancePercent(HeroRuntimeData heroData)
    {
        float chance = BaseInvestigationSuccessChancePercent;

        if (heroData == null)
        {
            return chance;
        }

        chance += Mathf.Max(0, heroData.Level - 1) * InvestigationSuccessChancePercentPerLevel;

        PrimaryStats heroStats = heroData.Stats;
        if (heroStats != null)
        {
            chance += heroStats.Agility * InvestigationSuccessChancePercentPerAgility;
            chance += heroStats.Intelligence * InvestigationSuccessChancePercentPerIntelligence;
        }

        return Mathf.Clamp(chance, 0f, 100f);
    }

    public float GetInvestigationBlockSuccessChancePercent(HeroRuntimeData heroData, int blockIndex)
    {
        float finalChance = GetInvestigationSuccessChancePercent(heroData);
        int blockCount = InvestigationBlockCount;

        if (blockCount <= 1)
        {
            return finalChance;
        }

        int clampedBlockIndex = Mathf.Clamp(blockIndex, 0, blockCount - 1);
        if (clampedBlockIndex == blockCount - 1)
        {
            return finalChance;
        }

        float distanceFromFinalBlock = (blockCount - 1 - clampedBlockIndex) / (float)(blockCount - 1);
        float gapBonusMultiplier = EarlyInvestigationBlockSuccessBonusGapPercent / 100f * distanceFromFinalBlock;
        return Mathf.Clamp(finalChance + (100f - finalChance) * gapBonusMultiplier, 0f, 100f);
    }

    public bool ValidateForRuntime(EnemyConfig enemyConfig, Object context)
    {
        bool valid = true;

        if (_rosterSize < 1)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} roster size must be at least 1.", context);
            valid = false;
        }

        if (_investigationSeconds <= 0f)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} investigation seconds must be greater than 0.", context);
            valid = false;
        }

        if (_investigationBlockCount < 1)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} investigation block count must be at least 1.", context);
            valid = false;
        }

        if (_investigationSecondsReductionPercentPerIntelligence < 0f || _investigationSecondsReductionPercentPerAgility < 0f)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} investigation stat reductions cannot be negative.", context);
            valid = false;
        }

        if (_secondsUntilMapThreat <= 0f)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} map threat timer must be greater than 0.", context);
            valid = false;
        }

        if (_baseInvestigationSuccessChancePercent < 0f || _baseInvestigationSuccessChancePercent > 100f ||
            _investigationSuccessChancePercentPerLevel < 0f || _investigationSuccessChancePercentPerLevel > 100f ||
            _investigationSuccessChancePercentPerAgility < 0f || _investigationSuccessChancePercentPerAgility > 100f ||
            _investigationSuccessChancePercentPerIntelligence < 0f || _investigationSuccessChancePercentPerIntelligence > 100f ||
            _earlyInvestigationBlockSuccessBonusGapPercent < 0f || _earlyInvestigationBlockSuccessBonusGapPercent > 100f)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} investigation success chance values must be between 0 and 100.", context);
            valid = false;
        }

        if (_investigationFailureAttackReadinessPercent < 0f || _investigationFailureAttackReadinessPercent > 100f)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} investigation failure attack readiness percent must be between 0 and 100.", context);
            valid = false;
        }

        if (_rosterEnemyTypes == null || _rosterEnemyTypes.Count == 0)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} requires at least one roster enemy type.", context);
            return false;
        }

        if (enemyConfig == null)
        {
            Debug.LogError($"{nameof(WorldMapConfig)} requires {nameof(EnemyConfig)}.", context);
            return false;
        }

        for (int i = 0; i < _rosterEnemyTypes.Count; i++)
        {
            EnemyType enemyType = _rosterEnemyTypes[i];

            if (enemyType == EnemyType.None || !enemyConfig.TryGetEnemy(enemyType, out _))
            {
                Debug.LogError($"{nameof(WorldMapConfig)} roster enemy type '{enemyType}' is not configured in {nameof(EnemyConfig)}.", context);
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        _rosterSize = Mathf.Max(1, _rosterSize);
        _investigationSeconds = Mathf.Max(0.1f, _investigationSeconds);
        _investigationBlockCount = Mathf.Max(1, _investigationBlockCount);
        _investigationSecondsReductionPercentPerIntelligence = Mathf.Clamp(_investigationSecondsReductionPercentPerIntelligence, 0f, 100f);
        _investigationSecondsReductionPercentPerAgility = Mathf.Clamp(_investigationSecondsReductionPercentPerAgility, 0f, 100f);
        _baseInvestigationSuccessChancePercent = Mathf.Clamp(_baseInvestigationSuccessChancePercent, 0f, 100f);
        _investigationSuccessChancePercentPerLevel = Mathf.Clamp(_investigationSuccessChancePercentPerLevel, 0f, 100f);
        _investigationSuccessChancePercentPerAgility = Mathf.Clamp(_investigationSuccessChancePercentPerAgility, 0f, 100f);
        _investigationSuccessChancePercentPerIntelligence = Mathf.Clamp(_investigationSuccessChancePercentPerIntelligence, 0f, 100f);
        _earlyInvestigationBlockSuccessBonusGapPercent = Mathf.Clamp(_earlyInvestigationBlockSuccessBonusGapPercent, 0f, 100f);
        _investigationFailureAttackReadinessPercent = Mathf.Clamp(_investigationFailureAttackReadinessPercent, 0f, 100f);
        _secondsUntilMapThreat = Mathf.Max(1f, _secondsUntilMapThreat);
    }
}

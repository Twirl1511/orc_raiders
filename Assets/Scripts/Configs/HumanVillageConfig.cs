using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Human Village", menuName = "GAME/Human Village")]
public sealed class HumanVillageConfig : ScriptableObject
{
    [Header("Roster")]
    [SerializeField, Min(1)] private int _rosterSize = 3;

    [Header("Scouting")]
    [SerializeField, Min(0.1f)] private float _scoutingSeconds = 4f;
    [SerializeField, Min(1)] private int _scoutingBlockCount = 1;
    [SerializeField, Range(0f, 100f)] private float _scoutingSecondsReductionPercentPerIntelligence = 3f;
    [SerializeField, Range(0f, 100f)] private float _scoutingSecondsReductionPercentPerAgility = 2f;
    [SerializeField, Range(0f, 100f)] private float _baseScoutingSuccessChancePercent = 55f;
    [SerializeField, Range(0f, 100f)] private float _scoutingSuccessChancePercentPerLevel = 5f;
    [SerializeField, Range(0f, 100f)] private float _scoutingSuccessChancePercentPerAgility = 2f;
    [SerializeField, Range(0f, 100f)] private float _scoutingSuccessChancePercentPerIntelligence = 3f;
    [SerializeField, Range(0f, 100f)] private float _earlyScoutingBlockSuccessBonusGapPercent = 50f;
    [SerializeField, Range(0f, 100f)] private float _scoutingFailureAttackReadinessPercent = 20f;

    [Header("Attack Readiness")]
    [SerializeField, Min(1f)] private float _secondsUntilVillageAttack = 180f;

    [SerializeField] private List<EnemyType> _rosterEnemyTypes = new List<EnemyType>
    {
        EnemyType.HumanWarrior,
        EnemyType.HumanArcher,
        EnemyType.HumanMage
    };

    public int RosterSize => Mathf.Max(1, _rosterSize);
    public float ScoutingSeconds => Mathf.Max(0.1f, _scoutingSeconds);
    public int ScoutingBlockCount => Mathf.Max(1, _scoutingBlockCount);
    public float ScoutingSecondsReductionPercentPerIntelligence => Mathf.Clamp(_scoutingSecondsReductionPercentPerIntelligence, 0f, 100f);
    public float ScoutingSecondsReductionPercentPerAgility => Mathf.Clamp(_scoutingSecondsReductionPercentPerAgility, 0f, 100f);
    public float BaseScoutingSuccessChancePercent => Mathf.Clamp(_baseScoutingSuccessChancePercent, 0f, 100f);
    public float ScoutingSuccessChancePercentPerLevel => Mathf.Clamp(_scoutingSuccessChancePercentPerLevel, 0f, 100f);
    public float ScoutingSuccessChancePercentPerAgility => Mathf.Clamp(_scoutingSuccessChancePercentPerAgility, 0f, 100f);
    public float ScoutingSuccessChancePercentPerIntelligence => Mathf.Clamp(_scoutingSuccessChancePercentPerIntelligence, 0f, 100f);
    public float EarlyScoutingBlockSuccessBonusGapPercent => Mathf.Clamp(_earlyScoutingBlockSuccessBonusGapPercent, 0f, 100f);
    public float ScoutingFailureAttackReadinessPercent => Mathf.Clamp(_scoutingFailureAttackReadinessPercent, 0f, 100f);
    public float SecondsUntilVillageAttack => Mathf.Max(1f, _secondsUntilVillageAttack);
    public IReadOnlyList<EnemyType> RosterEnemyTypes => _rosterEnemyTypes;

    public float GetScoutingSeconds(PrimaryStats heroStats)
    {
        if (heroStats == null)
        {
            return ScoutingSeconds;
        }

        float reductionPercent =
            heroStats.Intelligence * ScoutingSecondsReductionPercentPerIntelligence +
            heroStats.Agility * ScoutingSecondsReductionPercentPerAgility;
        float multiplier = 1f - Mathf.Clamp(reductionPercent, 0f, 90f) / 100f;
        return Mathf.Max(0.1f, ScoutingSeconds * multiplier);
    }

    public float GetScoutingSuccessChancePercent(HeroRuntimeData heroData)
    {
        float chance = BaseScoutingSuccessChancePercent;

        if (heroData == null)
        {
            return chance;
        }

        chance += Mathf.Max(0, heroData.Level - 1) * ScoutingSuccessChancePercentPerLevel;

        PrimaryStats heroStats = heroData.Stats;
        if (heroStats != null)
        {
            chance += heroStats.Agility * ScoutingSuccessChancePercentPerAgility;
            chance += heroStats.Intelligence * ScoutingSuccessChancePercentPerIntelligence;
        }

        return Mathf.Clamp(chance, 0f, 100f);
    }

    public float GetScoutingBlockSuccessChancePercent(HeroRuntimeData heroData, int blockIndex)
    {
        float finalChance = GetScoutingSuccessChancePercent(heroData);
        int blockCount = ScoutingBlockCount;

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
        float gapBonusMultiplier = EarlyScoutingBlockSuccessBonusGapPercent / 100f * distanceFromFinalBlock;
        return Mathf.Clamp(finalChance + (100f - finalChance) * gapBonusMultiplier, 0f, 100f);
    }

    public bool ValidateForRuntime(EnemyConfig enemyConfig, Object context)
    {
        bool valid = true;

        if (_rosterSize < 1)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} roster size must be at least 1.", context);
            valid = false;
        }

        if (_scoutingSeconds <= 0f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting seconds must be greater than 0.", context);
            valid = false;
        }

        if (_scoutingBlockCount < 1)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting block count must be at least 1.", context);
            valid = false;
        }

        if (_scoutingSecondsReductionPercentPerIntelligence < 0f || _scoutingSecondsReductionPercentPerAgility < 0f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting stat reductions cannot be negative.", context);
            valid = false;
        }

        if (_secondsUntilVillageAttack <= 0f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} village attack timer must be greater than 0.", context);
            valid = false;
        }

        if (_baseScoutingSuccessChancePercent < 0f || _baseScoutingSuccessChancePercent > 100f ||
            _scoutingSuccessChancePercentPerLevel < 0f || _scoutingSuccessChancePercentPerLevel > 100f ||
            _scoutingSuccessChancePercentPerAgility < 0f || _scoutingSuccessChancePercentPerAgility > 100f ||
            _scoutingSuccessChancePercentPerIntelligence < 0f || _scoutingSuccessChancePercentPerIntelligence > 100f ||
            _earlyScoutingBlockSuccessBonusGapPercent < 0f || _earlyScoutingBlockSuccessBonusGapPercent > 100f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting success chance values must be between 0 and 100.", context);
            valid = false;
        }

        if (_scoutingFailureAttackReadinessPercent < 0f || _scoutingFailureAttackReadinessPercent > 100f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting failure attack readiness percent must be between 0 and 100.", context);
            valid = false;
        }

        if (_rosterEnemyTypes == null || _rosterEnemyTypes.Count == 0)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} requires at least one roster enemy type.", context);
            return false;
        }

        if (enemyConfig == null)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} requires {nameof(EnemyConfig)}.", context);
            return false;
        }

        for (int i = 0; i < _rosterEnemyTypes.Count; i++)
        {
            EnemyType enemyType = _rosterEnemyTypes[i];

            if (enemyType == EnemyType.None || !enemyConfig.TryGetEnemy(enemyType, out _))
            {
                Debug.LogError($"{nameof(HumanVillageConfig)} roster enemy type '{enemyType}' is not configured in {nameof(EnemyConfig)}.", context);
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        _rosterSize = Mathf.Max(1, _rosterSize);
        _scoutingSeconds = Mathf.Max(0.1f, _scoutingSeconds);
        _scoutingBlockCount = Mathf.Max(1, _scoutingBlockCount);
        _scoutingSecondsReductionPercentPerIntelligence = Mathf.Clamp(_scoutingSecondsReductionPercentPerIntelligence, 0f, 100f);
        _scoutingSecondsReductionPercentPerAgility = Mathf.Clamp(_scoutingSecondsReductionPercentPerAgility, 0f, 100f);
        _baseScoutingSuccessChancePercent = Mathf.Clamp(_baseScoutingSuccessChancePercent, 0f, 100f);
        _scoutingSuccessChancePercentPerLevel = Mathf.Clamp(_scoutingSuccessChancePercentPerLevel, 0f, 100f);
        _scoutingSuccessChancePercentPerAgility = Mathf.Clamp(_scoutingSuccessChancePercentPerAgility, 0f, 100f);
        _scoutingSuccessChancePercentPerIntelligence = Mathf.Clamp(_scoutingSuccessChancePercentPerIntelligence, 0f, 100f);
        _earlyScoutingBlockSuccessBonusGapPercent = Mathf.Clamp(_earlyScoutingBlockSuccessBonusGapPercent, 0f, 100f);
        _scoutingFailureAttackReadinessPercent = Mathf.Clamp(_scoutingFailureAttackReadinessPercent, 0f, 100f);
        _secondsUntilVillageAttack = Mathf.Max(1f, _secondsUntilVillageAttack);
    }
}

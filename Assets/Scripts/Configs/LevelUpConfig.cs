using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level Up", menuName = "GAME/Level Up")]
public sealed class LevelUpConfig : ScriptableObject
{
    [SerializeField] private List<PrimaryStatLevelBonus> _primaryStatBonuses = new List<PrimaryStatLevelBonus>();
    [SerializeField, Min(0)] private int _freePrimaryStatPointsPerLevel = 1;
    [SerializeField] private List<LevelExperienceRequirement> _experienceRequirements = new List<LevelExperienceRequirement>();

    public IReadOnlyList<PrimaryStatLevelBonus> PrimaryStatBonuses => _primaryStatBonuses;
    public int FreePrimaryStatPointsPerLevel => Mathf.Max(0, _freePrimaryStatPointsPerLevel);
    public IReadOnlyList<LevelExperienceRequirement> ExperienceRequirements => _experienceRequirements;

    public bool TryGetRequiredExperience(int level, out int requiredExperience)
    {
        if (_experienceRequirements != null)
        {
            for (int i = 0; i < _experienceRequirements.Count; i++)
            {
                LevelExperienceRequirement requirement = _experienceRequirements[i];

                if (requirement != null && requirement.Level == level)
                {
                    requiredExperience = requirement.RequiredExperience;
                    return requiredExperience > 0;
                }
            }
        }

        requiredExperience = 0;
        return false;
    }

    public bool ValidateForRuntime()
    {
        if (_primaryStatBonuses == null || _primaryStatBonuses.Count == 0 ||
            _experienceRequirements == null || _experienceRequirements.Count == 0)
        {
            return false;
        }

        HashSet<OrcStatType> usedPrimaryStats = new HashSet<OrcStatType>();
        HashSet<int> usedLevels = new HashSet<int>();
        int maxConfiguredLevel = 0;

        for (int i = 0; i < _primaryStatBonuses.Count; i++)
        {
            PrimaryStatLevelBonus bonus = _primaryStatBonuses[i];

            if (bonus == null || bonus.StatType == OrcStatType.None || bonus.ValuePerLevel < 0 ||
                !usedPrimaryStats.Add(bonus.StatType))
            {
                return false;
            }
        }

        if (!HasAllPrimaryStats(usedPrimaryStats))
        {
            return false;
        }

        for (int i = 0; i < _experienceRequirements.Count; i++)
        {
            LevelExperienceRequirement requirement = _experienceRequirements[i];

            if (requirement == null || requirement.Level < 1 || requirement.RequiredExperience < 1 ||
                !usedLevels.Add(requirement.Level))
            {
                return false;
            }

            maxConfiguredLevel = Mathf.Max(maxConfiguredLevel, requirement.Level);
        }

        for (int level = 1; level <= maxConfiguredLevel; level++)
        {
            if (!usedLevels.Contains(level))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAllPrimaryStats(HashSet<OrcStatType> usedPrimaryStats)
    {
        return usedPrimaryStats.Contains(OrcStatType.Endurance) &&
            usedPrimaryStats.Contains(OrcStatType.Strength) &&
            usedPrimaryStats.Contains(OrcStatType.Agility) &&
            usedPrimaryStats.Contains(OrcStatType.Intelligence);
    }
}

[Serializable]
public sealed class PrimaryStatLevelBonus
{
    [SerializeField] private OrcStatType _statType = OrcStatType.None;
    [SerializeField, Min(0)] private int _valuePerLevel = 1;

    public OrcStatType StatType => _statType;
    public int ValuePerLevel => Mathf.Max(0, _valuePerLevel);
}

[Serializable]
public sealed class LevelExperienceRequirement
{
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(1)] private int _requiredExperience = 100;

    public int Level => Mathf.Max(1, _level);
    public int RequiredExperience => Mathf.Max(1, _requiredExperience);
}

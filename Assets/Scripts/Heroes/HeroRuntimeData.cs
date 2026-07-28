using System.Collections.Generic;
using UnityEngine;

public enum HeroActivityState
{
    OnBase = 0,
    Resting = 1,
    InRaid = 2
}

public sealed class HeroRuntimeData
{
    public const int EquipmentSlotCount = 3;

    private readonly List<string> _rollTexts;
    private readonly ItemRuntimeData[] _equippedItems = new ItemRuntimeData[EquipmentSlotCount];

    public HeroRuntimeData(string name, PrimaryStats stats, List<string> rollTexts, float maxHp)
    {
        Name = name;
        Stats = stats;
        _rollTexts = rollTexts ?? new List<string>();
        State = HeroActivityState.OnBase;
        SetMaxHp(maxHp, true);
    }

    public string Name { get; }
    public PrimaryStats Stats { get; }
    public IReadOnlyList<string> RollTexts => _rollTexts;
    public HeroActivityState State { get; private set; }
    public GameObject ViewObject { get; private set; }
    public Vector2 MapPosition { get; private set; }
    public float CurrentHp { get; private set; }
    public float MaxHp { get; private set; }
    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int FreePrimaryStatPoints { get; private set; }
    public bool IsFullyHealed => CurrentHp >= MaxHp;

    public void AttachView(GameObject viewObject)
    {
        ViewObject = viewObject;
    }

    public void SetState(HeroActivityState state)
    {
        State = state;
    }

    public void SetMapPosition(Vector2 mapPosition)
    {
        MapPosition = mapPosition;
    }

    public void SetMaxHp(float maxHp, bool fillCurrentHp)
    {
        MaxHp = Mathf.Max(1f, maxHp);

        if (fillCurrentHp)
        {
            CurrentHp = MaxHp;
            return;
        }

        CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp);
    }

    public void SetCurrentHp(float hp)
    {
        CurrentHp = Mathf.Clamp(hp, 0f, MaxHp);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetCurrentHp(CurrentHp + amount);
    }

    public ItemRuntimeData GetEquippedItem(int slotIndex)
    {
        return IsEquipmentSlotIndexValid(slotIndex) ? _equippedItems[slotIndex] : null;
    }

    public bool TryEquipItem(int slotIndex, ItemRuntimeData item, out ItemRuntimeData replacedItem)
    {
        replacedItem = null;

        if (!IsEquipmentSlotIndexValid(slotIndex) || item == null)
        {
            return false;
        }

        replacedItem = _equippedItems[slotIndex];
        _equippedItems[slotIndex] = item;
        return true;
    }

    public bool TryUnequipItem(int slotIndex, out ItemRuntimeData item)
    {
        item = null;

        if (!IsEquipmentSlotIndexValid(slotIndex) || _equippedItems[slotIndex] == null)
        {
            return false;
        }

        item = _equippedItems[slotIndex];
        _equippedItems[slotIndex] = null;
        return true;
    }

    public PrimaryStats GetEffectivePrimaryStats(StatsConfig statsConfig)
    {
        PrimaryStats effectiveStats = Stats.Copy();

        for (int i = 0; i < _equippedItems.Length; i++)
        {
            ItemDefinition item = _equippedItems[i] != null ? _equippedItems[i].Definition : null;

            if (item == null)
            {
                continue;
            }

            IReadOnlyList<ItemStatModifier> modifiers = item.StatModifiers;

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                ItemStatModifier modifier = modifiers[modifierIndex];

                if (modifier != null && modifier.Target == ItemStatTarget.Primary)
                {
                    effectiveStats.ApplyItemModifier(
                        modifier.PrimaryStat,
                        Mathf.RoundToInt(modifier.Value),
                        statsConfig);
                }
            }
        }

        return effectiveStats;
    }

    public SecondaryStatsSnapshot GetEffectiveSecondaryStats(StatsConfig statsConfig)
    {
        if (statsConfig == null)
        {
            return default;
        }

        SecondaryStatsSnapshot snapshot = statsConfig.CalculateSecondaryStats(GetEffectivePrimaryStats(statsConfig));

        for (int i = 0; i < _equippedItems.Length; i++)
        {
            ItemDefinition item = _equippedItems[i] != null ? _equippedItems[i].Definition : null;

            if (item == null)
            {
                continue;
            }

            IReadOnlyList<ItemStatModifier> modifiers = item.StatModifiers;

            for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
            {
                ItemStatModifier modifier = modifiers[modifierIndex];

                if (modifier != null && modifier.Target == ItemStatTarget.Secondary)
                {
                    snapshot.Add(modifier.SecondaryStat, modifier.Value);
                }
            }
        }

        return snapshot;
    }

    public void AddExperience(int amount, LevelUpConfig levelUpConfig, StatsConfig statsConfig)
    {
        if (amount <= 0 || levelUpConfig == null)
        {
            return;
        }

        Experience += amount;

        while (levelUpConfig.TryGetRequiredExperience(Level, out int requiredExperience) &&
            Experience >= requiredExperience)
        {
            Level++;
            FreePrimaryStatPoints += levelUpConfig.FreePrimaryStatPointsPerLevel;
            Stats.ApplyLevelBonuses(levelUpConfig, statsConfig);
        }
    }

    public bool CanSpendFreePrimaryStatPoint(PrimaryStatType statType, StatsConfig statsConfig)
    {
        if (FreePrimaryStatPoints <= 0 || statType == PrimaryStatType.None)
        {
            return false;
        }

        if (statsConfig == null)
        {
            return true;
        }

        return Stats.GetValue(statType) < statsConfig.MaxPrimaryStatValue;
    }

    public bool TrySpendFreePrimaryStatPoint(PrimaryStatType statType, StatsConfig statsConfig)
    {
        if (!CanSpendFreePrimaryStatPoint(statType, statsConfig))
        {
            return false;
        }

        if (!Stats.TryAddPrimaryStatPoint(statType, statsConfig))
        {
            return false;
        }

        FreePrimaryStatPoints--;
        return true;
    }

    public string GetExperienceDisplay(LevelUpConfig levelUpConfig)
    {
        if (levelUpConfig != null && levelUpConfig.TryGetRequiredExperience(Level, out int requiredExperience))
        {
            return $"{Experience}/{requiredExperience}";
        }

        return $"{Experience}/-";
    }

    public string GetStateDisplayName()
    {
        switch (State)
        {
            case HeroActivityState.OnBase:
                return "На базе";
            case HeroActivityState.Resting:
                return "Отдыхает";
            case HeroActivityState.InRaid:
                return "В рейде";
            default:
                return State.ToString();
        }
    }

    private static bool IsEquipmentSlotIndexValid(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < EquipmentSlotCount;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class HeroInfoPanelView : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private CanvasGroup _canvasGroup = null;
    [SerializeField] private TextMeshProUGUI _titleText = null;

    [Header("Summary")]
    [SerializeField] private TextMeshProUGUI _stateValueText = null;
    [SerializeField] private TextMeshProUGUI _levelValueText = null;
    [SerializeField] private TextMeshProUGUI _experienceValueText = null;
    [SerializeField] private GameObject _freeStatPointsRoot = null;
    [SerializeField] private TextMeshProUGUI _freeStatPointsValueText = null;
    [SerializeField] private HeroStatRowView _healthRow = null;

    [Header("Stats")]
    [SerializeField] private HeroStatRowView[] _primaryStatRows = new HeroStatRowView[4];
    [SerializeField] private HeroStatRowView[] _secondaryStatRows = new HeroStatRowView[7];

    [Header("Tooltip")]
    [SerializeField] private ItemTooltipView _tooltipView = null;

    private static readonly PrimaryStatType[] _fallbackPrimaryStatOrder =
    {
        PrimaryStatType.Endurance,
        PrimaryStatType.Strength,
        PrimaryStatType.Agility,
        PrimaryStatType.Intelligence
    };

    private static readonly SecondaryStatType[] _fallbackSecondaryStatOrder =
    {
        SecondaryStatType.MaxHp,
        SecondaryStatType.MeleeDamage,
        SecondaryStatType.RangedDamage,
        SecondaryStatType.AttackInterval,
        SecondaryStatType.Armor,
        SecondaryStatType.ExtraLootChance,
        SecondaryStatType.DodgeChance
    };

    public event Action<PrimaryStatType> PrimaryStatUpgradeRequested;

    private void Awake()
    {
        ConfigureRows();
    }

    private void OnEnable()
    {
        ConfigureRows();
    }

    private void OnDisable()
    {
        _tooltipView?.Hide();
    }

    public void Configure(ItemTooltipView tooltipView)
    {
        _tooltipView = tooltipView;
        ConfigureRows();
    }

    public bool HasRequiredReferences()
    {
        return _canvasGroup != null &&
            _titleText != null &&
            _stateValueText != null &&
            _levelValueText != null &&
            _experienceValueText != null &&
            _freeStatPointsRoot != null &&
            _freeStatPointsValueText != null &&
            _healthRow != null &&
            HasRows(_primaryStatRows, _fallbackPrimaryStatOrder.Length) &&
            HasRows(_secondaryStatRows, _fallbackSecondaryStatOrder.Length);
    }

    public void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;

        if (!visible)
        {
            _tooltipView?.Hide();
        }
    }

    public void ShowHero(
        HeroRuntimeData heroData,
        StatsConfig statsConfig,
        LevelUpConfig levelUpConfig,
        bool allowEditing)
    {
        if (heroData == null)
        {
            ClearHero();
            return;
        }

        ConfigureRows();

        if (_titleText != null)
        {
            _titleText.text = heroData.Name;
        }

        if (_stateValueText != null)
        {
            _stateValueText.text = heroData.GetStateDisplayName();
        }

        if (_levelValueText != null)
        {
            _levelValueText.text = heroData.Level.ToString(CultureInfo.InvariantCulture);
        }

        if (_experienceValueText != null)
        {
            _experienceValueText.text = heroData.GetExperienceDisplay(levelUpConfig);
        }

        bool hasFreePoints = heroData.FreePrimaryStatPoints > 0;
        if (_freeStatPointsRoot != null)
        {
            _freeStatPointsRoot.SetActive(hasFreePoints);
        }

        if (_freeStatPointsValueText != null)
        {
            _freeStatPointsValueText.text = heroData.FreePrimaryStatPoints.ToString(CultureInfo.InvariantCulture);
        }

        PrimaryStats effectivePrimaryStats = heroData.GetEffectivePrimaryStats(statsConfig);
        SecondaryStatsSnapshot effectiveSecondaryStats = heroData.GetEffectiveSecondaryStats(statsConfig);

        RefreshHealth(heroData, statsConfig);
        RefreshPrimaryStats(heroData, statsConfig, effectivePrimaryStats, allowEditing);
        RefreshSecondaryStats(statsConfig, effectiveSecondaryStats);
    }

    public void ClearHero()
    {
        if (_titleText != null)
        {
            _titleText.text = "";
        }

        if (_stateValueText != null)
        {
            _stateValueText.text = "";
        }

        if (_levelValueText != null)
        {
            _levelValueText.text = "";
        }

        if (_experienceValueText != null)
        {
            _experienceValueText.text = "";
        }

        if (_freeStatPointsRoot != null)
        {
            _freeStatPointsRoot.SetActive(false);
        }

        _healthRow?.SetVisible(false);
        HideRows(_primaryStatRows);
        HideRows(_secondaryStatRows);
        _tooltipView?.Hide();
    }

    private void RefreshHealth(HeroRuntimeData heroData, StatsConfig statsConfig)
    {
        if (_healthRow == null)
        {
            return;
        }

        int currentHp = Mathf.CeilToInt(heroData.CurrentHp);
        int maxHp = Mathf.CeilToInt(heroData.MaxHp);
        string displayName = statsConfig != null
            ? statsConfig.GetSecondaryStatDisplayName(SecondaryStatType.MaxHp)
            : SecondaryStatType.MaxHp.ToString();

        _healthRow.SetVisible(true);
        _healthRow.SetText(displayName, $"{currentHp}/{maxHp}");
        _healthRow.SetBar(heroData.MaxHp <= 0f ? 0f : heroData.CurrentHp / heroData.MaxHp);
        _healthRow.SetUpgradeVisible(false, false);
        _healthRow.SetUpgradeHandler(null);
        _healthRow.SetTooltip(displayName, BuildSecondaryTooltipBody(statsConfig, SecondaryStatType.MaxHp));
    }

    private void RefreshPrimaryStats(
        HeroRuntimeData heroData,
        StatsConfig statsConfig,
        PrimaryStats effectivePrimaryStats,
        bool allowEditing)
    {
        if (_primaryStatRows == null)
        {
            return;
        }

        int rowIndex = 0;
        IReadOnlyList<PrimaryStatDefinition> definitions = statsConfig != null ? statsConfig.PrimaryStats : null;

        if (definitions != null && definitions.Count > 0)
        {
            for (int i = 0; i < definitions.Count && rowIndex < _primaryStatRows.Length; i++)
            {
                PrimaryStatDefinition definition = definitions[i];

                if (definition == null || definition.StatType == PrimaryStatType.None)
                {
                    continue;
                }

                FillPrimaryRow(rowIndex, heroData, statsConfig, effectivePrimaryStats, definition.StatType, allowEditing);
                rowIndex++;
            }
        }
        else
        {
            for (int i = 0; i < _fallbackPrimaryStatOrder.Length && rowIndex < _primaryStatRows.Length; i++)
            {
                FillPrimaryRow(rowIndex, heroData, statsConfig, effectivePrimaryStats, _fallbackPrimaryStatOrder[i], allowEditing);
                rowIndex++;
            }
        }

        HideRemainingRows(_primaryStatRows, rowIndex);
    }

    private void FillPrimaryRow(
        int rowIndex,
        HeroRuntimeData heroData,
        StatsConfig statsConfig,
        PrimaryStats effectivePrimaryStats,
        PrimaryStatType statType,
        bool allowEditing)
    {
        HeroStatRowView row = _primaryStatRows[rowIndex];
        if (row == null)
        {
            return;
        }

        int value = effectivePrimaryStats != null ? effectivePrimaryStats.GetValue(statType) : 0;
        int maxValue = statsConfig != null ? statsConfig.MaxPrimaryStatValue : Mathf.Max(1, value);
        string displayName = statsConfig != null ? statsConfig.GetPrimaryStatDisplayName(statType) : statType.ToString();
        PrimaryStatType capturedStatType = statType;
        bool hasFreePoints = heroData != null && heroData.FreePrimaryStatPoints > 0;
        bool canUpgrade = hasFreePoints && allowEditing && heroData.CanSpendFreePrimaryStatPoint(statType, statsConfig);

        row.SetVisible(true);
        row.SetText(displayName, $"{value}/{maxValue}");
        row.SetBar(maxValue <= 0 ? 0f : (float)value / maxValue);
        row.SetUpgradeVisible(hasFreePoints, canUpgrade);
        row.SetUpgradeHandler(() => PrimaryStatUpgradeRequested?.Invoke(capturedStatType));
        row.SetTooltip(displayName, BuildPrimaryTooltipBody(statsConfig, statType));
    }

    private void RefreshSecondaryStats(StatsConfig statsConfig, SecondaryStatsSnapshot snapshot)
    {
        if (_secondaryStatRows == null)
        {
            return;
        }

        int rowIndex = 0;
        IReadOnlyList<SecondaryStatDefinition> definitions = statsConfig != null ? statsConfig.SecondaryStats : null;

        if (definitions != null && definitions.Count > 0)
        {
            for (int i = 0; i < definitions.Count && rowIndex < _secondaryStatRows.Length; i++)
            {
                SecondaryStatDefinition definition = definitions[i];

                if (definition == null || definition.StatType == SecondaryStatType.None)
                {
                    continue;
                }

                FillSecondaryRow(rowIndex, statsConfig, snapshot, definition.StatType);
                rowIndex++;
            }
        }
        else
        {
            for (int i = 0; i < _fallbackSecondaryStatOrder.Length && rowIndex < _secondaryStatRows.Length; i++)
            {
                FillSecondaryRow(rowIndex, statsConfig, snapshot, _fallbackSecondaryStatOrder[i]);
                rowIndex++;
            }
        }

        HideRemainingRows(_secondaryStatRows, rowIndex);
    }

    private void FillSecondaryRow(
        int rowIndex,
        StatsConfig statsConfig,
        SecondaryStatsSnapshot snapshot,
        SecondaryStatType statType)
    {
        HeroStatRowView row = _secondaryStatRows[rowIndex];
        if (row == null)
        {
            return;
        }

        float value = snapshot.GetValue(statType);
        string displayName = statsConfig != null ? statsConfig.GetSecondaryStatDisplayName(statType) : statType.ToString();
        string valueText = statsConfig != null
            ? statsConfig.FormatSecondaryStatValue(statType, value)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

        row.SetVisible(true);
        row.SetText(displayName, valueText);
        row.SetBar(0f);
        row.SetUpgradeVisible(false, false);
        row.SetUpgradeHandler(null);
        row.SetTooltip(displayName, BuildSecondaryTooltipBody(statsConfig, statType));
    }

    private string BuildPrimaryTooltipBody(StatsConfig statsConfig, PrimaryStatType statType)
    {
        if (statsConfig == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        AppendTooltipBlock(builder, statsConfig.GetPrimaryStatDescription(statType));

        IReadOnlyList<StatScalingRule> rules = statsConfig.ScalingRules;
        for (int i = 0; i < rules.Count; i++)
        {
            StatScalingRule rule = rules[i];

            if (rule != null && rule.SourceStat == statType)
            {
                AppendTooltipBlock(builder, rule.Description);
            }
        }

        return builder.ToString();
    }

    private string BuildSecondaryTooltipBody(StatsConfig statsConfig, SecondaryStatType statType)
    {
        if (statsConfig == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        AppendTooltipBlock(builder, statsConfig.GetSecondaryStatDescription(statType));

        IReadOnlyList<StatScalingRule> rules = statsConfig.ScalingRules;
        for (int i = 0; i < rules.Count; i++)
        {
            StatScalingRule rule = rules[i];

            if (rule != null && rule.TargetStat == statType)
            {
                AppendTooltipBlock(builder, rule.Description);
            }
        }

        return builder.ToString();
    }

    private void ConfigureRows()
    {
        _healthRow?.ConfigureTooltip(_tooltipView);
        ConfigureRows(_primaryStatRows);
        ConfigureRows(_secondaryStatRows);
    }

    private void ConfigureRows(HeroStatRowView[] rows)
    {
        if (rows == null)
        {
            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null)
            {
                rows[i].ConfigureTooltip(_tooltipView);
            }
        }
    }

    private static bool HasRows(HeroStatRowView[] rows, int minimumCount)
    {
        if (rows == null || rows.Length < minimumCount)
        {
            return false;
        }

        for (int i = 0; i < minimumCount; i++)
        {
            if (rows[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private static void HideRows(HeroStatRowView[] rows)
    {
        if (rows == null)
        {
            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null)
            {
                rows[i].SetVisible(false);
            }
        }
    }

    private static void HideRemainingRows(HeroStatRowView[] rows, int firstHiddenIndex)
    {
        if (rows == null)
        {
            return;
        }

        for (int i = firstHiddenIndex; i < rows.Length; i++)
        {
            if (rows[i] != null)
            {
                rows[i].SetVisible(false);
            }
        }
    }

    private static void AppendTooltipBlock(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
        }

        builder.Append(text.Trim());
    }
}

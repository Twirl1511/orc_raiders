using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "Traits", menuName = "GAME/Traits")]
public sealed class TraitsConfig : ScriptableObject
{
    [SerializeField] private List<ActionTagDefinition> _actionTags = new List<ActionTagDefinition>();
    [SerializeField] private List<TraitDefinition> _traits = new List<TraitDefinition>();

    public IReadOnlyList<ActionTagDefinition> ActionTags => _actionTags;
    public IReadOnlyList<TraitDefinition> Traits => _traits;

    public ActionTagDefinition GetActionTag(ActionTagType tagType)
    {
        for (int i = 0; i < _actionTags.Count; i++)
        {
            ActionTagDefinition definition = _actionTags[i];

            if (definition != null && definition.TagType == tagType)
            {
                return definition;
            }
        }

        return null;
    }

    public TraitDefinition GetTrait(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string normalizedId = id.Trim();

        for (int i = 0; i < _traits.Count; i++)
        {
            TraitDefinition trait = _traits[i];

            if (trait != null && string.Equals(trait.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return trait;
            }
        }

        return null;
    }

    public string GetActionTagDisplayName(ActionTagType tagType)
    {
        ActionTagDefinition definition = GetActionTag(tagType);
        return definition != null ? definition.DisplayName : GetFallbackActionTagDisplayName(tagType);
    }

    public float GetTraitEffectValue(string traitId, ActionTagType actionTag, TraitEffectType effectType, float traitStrength)
    {
        TraitDefinition trait = GetTrait(traitId);
        return trait != null ? trait.GetEffectValue(actionTag, effectType, traitStrength) : 0f;
    }

    public string GetTraitInfluenceSummary(string traitId, float traitStrength)
    {
        TraitDefinition trait = GetTrait(traitId);
        return trait != null
            ? trait.GetInfluenceSummary(this, traitStrength)
            : "";
    }

    public bool ValidateForRuntime(UnityEngine.Object context)
    {
        bool valid = true;
        HashSet<ActionTagType> tagTypes = new HashSet<ActionTagType>();
        HashSet<string> traitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_actionTags.Count == 0)
        {
            Debug.LogError($"{nameof(TraitsConfig)} requires at least one action tag definition.", context);
            valid = false;
        }

        if (_traits.Count == 0)
        {
            Debug.LogError($"{nameof(TraitsConfig)} requires at least one trait definition.", context);
            valid = false;
        }

        for (int i = 0; i < _actionTags.Count; i++)
        {
            ActionTagDefinition tag = _actionTags[i];

            if (tag == null)
            {
                Debug.LogError($"{nameof(TraitsConfig)} has an empty action tag entry at index {i}.", context);
                valid = false;
                continue;
            }

            if (tag.TagType == ActionTagType.None)
            {
                Debug.LogError($"{nameof(TraitsConfig)} action tag at index {i} must not be None.", context);
                valid = false;
            }
            else if (!Enum.IsDefined(typeof(ActionTagType), tag.TagType))
            {
                Debug.LogError($"{nameof(TraitsConfig)} action tag at index {i} uses unknown tag value '{tag.TagType}'.", context);
                valid = false;
            }
            else if (!tagTypes.Add(tag.TagType))
            {
                Debug.LogError($"{nameof(TraitsConfig)} action tag '{tag.TagType}' is duplicated.", context);
                valid = false;
            }
        }

        for (int i = 0; i < _traits.Count; i++)
        {
            TraitDefinition trait = _traits[i];

            if (trait == null)
            {
                Debug.LogError($"{nameof(TraitsConfig)} has an empty trait entry at index {i}.", context);
                valid = false;
                continue;
            }

            string id = trait.Id;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError($"{nameof(TraitsConfig)} trait at index {i} has empty id.", context);
                valid = false;
            }
            else if (!traitIds.Add(id))
            {
                Debug.LogError($"{nameof(TraitsConfig)} trait id '{id}' is duplicated. Trait ids must be unique.", context);
                valid = false;
            }

            if (!trait.ValidateForRuntime(context, i))
            {
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        ValidateForRuntime(this);
    }

    private static string GetFallbackActionTagDisplayName(ActionTagType tagType)
    {
        switch (tagType)
        {
            case ActionTagType.Risky:
                return "Риск";
            case ActionTagType.Safe:
                return "Безопасность";
            case ActionTagType.Combat:
                return "Бой";
            case ActionTagType.Protect:
                return "Защита";
            case ActionTagType.Loot:
                return "Добыча";
            case ActionTagType.Sacrifice:
                return "Жертва";
            case ActionTagType.Selfish:
                return "Эгоизм";
            case ActionTagType.Social:
                return "Социальное";
            case ActionTagType.Travel:
                return "Путь";
            case ActionTagType.Rest:
                return "Отдых";
            case ActionTagType.Magic:
                return "Магия";
            case ActionTagType.CampBenefit:
                return "Польза гильдии";
            case ActionTagType.AllyInDanger:
                return "Союзник в опасности";
            case ActionTagType.Outnumbered:
                return "Врагов больше";
            default:
                return tagType.ToString();
        }
    }
}

public enum ActionTagType
{
    None = 0,
    Risky = 1,
    Safe = 2,
    Combat = 3,
    Protect = 4,
    Loot = 5,
    Sacrifice = 6,
    Selfish = 7,
    Social = 8,
    Travel = 9,
    Rest = 10,
    Magic = 11,
    CampBenefit = 12,
    AllyInDanger = 13,
    Outnumbered = 14
}

public enum TraitEffectType
{
    None = 0,
    DecisionWeight = 1,
    SuccessChancePercent = 2,
    Stress = 3,
    Morale = 4
}

public enum TraitEffectValueMode
{
    Fixed = 0,
    ScaledByTraitStrength = 1
}

[Serializable]
public sealed class ActionTagDefinition
{
    [SerializeField] private ActionTagType _tagType = ActionTagType.Risky;
    [SerializeField] private string _displayName = "Тег";
    [SerializeField, TextArea] private string _description = "";

    public ActionTagType TagType => _tagType;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _tagType.ToString() : _displayName;
    public string Description => _description;
}

[Serializable]
public sealed class TraitDefinition
{
    [SerializeField] private string _id = "trait_id";
    [SerializeField] private string _displayName = "Черта";
    [SerializeField, TextArea] private string _description = "";
    [SerializeField, Range(0f, 100f)] private float _defaultStrength = 50f;
    [SerializeField] private List<TraitInfluenceRule> _rules = new List<TraitInfluenceRule>();

    public string Id => string.IsNullOrWhiteSpace(_id) ? "" : _id.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? Id : _displayName;
    public string Description => _description;
    public float DefaultStrength => Mathf.Clamp(_defaultStrength, 0f, 100f);
    public IReadOnlyList<TraitInfluenceRule> Rules => _rules;

    public float GetEffectValue(ActionTagType actionTag, TraitEffectType effectType, float traitStrength)
    {
        float value = 0f;

        for (int i = 0; i < _rules.Count; i++)
        {
            TraitInfluenceRule rule = _rules[i];

            if (rule != null && rule.ActionTag == actionTag && rule.EffectType == effectType)
            {
                value += rule.GetValue(traitStrength);
            }
        }

        return value;
    }

    public string GetInfluenceSummary(TraitsConfig config, float traitStrength)
    {
        if (_rules.Count == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < _rules.Count; i++)
        {
            TraitInfluenceRule rule = _rules[i];

            if (rule == null || rule.ActionTag == ActionTagType.None || rule.EffectType == TraitEffectType.None)
            {
                continue;
            }

            string tagName = config != null ? config.GetActionTagDisplayName(rule.ActionTag) : rule.ActionTag.ToString();
            string valueText = rule.GetValue(traitStrength).ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append($"{tagName}: {GetEffectDisplayName(rule.EffectType)} {valueText}");

            if (!string.IsNullOrWhiteSpace(rule.Explanation))
            {
                builder.Append($" - {rule.Explanation}");
            }
        }

        return builder.ToString();
    }

    public bool ValidateForRuntime(UnityEngine.Object context, int traitIndex)
    {
        bool valid = true;
        string traitLabel = string.IsNullOrWhiteSpace(Id) ? $"trait at index {traitIndex}" : $"trait '{Id}'";

        if (_rules.Count == 0)
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} requires at least one influence rule.", context);
            valid = false;
        }

        for (int i = 0; i < _rules.Count; i++)
        {
            TraitInfluenceRule rule = _rules[i];

            if (rule == null)
            {
                Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} has an empty rule at index {i}.", context);
                valid = false;
                continue;
            }

            if (!rule.ValidateForRuntime(context, traitLabel, i))
            {
                valid = false;
            }
        }

        return valid;
    }

    private static string GetEffectDisplayName(TraitEffectType effectType)
    {
        switch (effectType)
        {
            case TraitEffectType.DecisionWeight:
                return "желание";
            case TraitEffectType.SuccessChancePercent:
                return "шанс успеха";
            case TraitEffectType.Stress:
                return "стресс";
            case TraitEffectType.Morale:
                return "мораль";
            default:
                return effectType.ToString();
        }
    }
}

[Serializable]
public sealed class TraitInfluenceRule
{
    [SerializeField] private ActionTagType _actionTag = ActionTagType.Risky;
    [SerializeField] private TraitEffectType _effectType = TraitEffectType.DecisionWeight;
    [SerializeField] private TraitEffectValueMode _valueMode = TraitEffectValueMode.ScaledByTraitStrength;
    [SerializeField] private float _value = 0f;
    [SerializeField, TextArea] private string _explanation = "";

    public ActionTagType ActionTag => _actionTag;
    public TraitEffectType EffectType => _effectType;
    public TraitEffectValueMode ValueMode => _valueMode;
    public float Value => _value;
    public string Explanation => _explanation;

    public float GetValue(float traitStrength)
    {
        if (_valueMode == TraitEffectValueMode.Fixed)
        {
            return _value;
        }

        return _value * Mathf.Clamp(traitStrength, 0f, 100f) / 100f;
    }

    public bool ValidateForRuntime(UnityEngine.Object context, string traitLabel, int ruleIndex)
    {
        bool valid = true;

        if (_actionTag == ActionTagType.None)
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} must select an action tag.", context);
            valid = false;
        }
        else if (!Enum.IsDefined(typeof(ActionTagType), _actionTag))
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} uses unknown action tag value '{_actionTag}'.", context);
            valid = false;
        }

        if (_effectType == TraitEffectType.None)
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} must select an effect type.", context);
            valid = false;
        }
        else if (!Enum.IsDefined(typeof(TraitEffectType), _effectType))
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} uses unknown effect type value '{_effectType}'.", context);
            valid = false;
        }

        if (!Enum.IsDefined(typeof(TraitEffectValueMode), _valueMode))
        {
            Debug.LogError($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} uses unknown value mode '{_valueMode}'.", context);
            valid = false;
        }

        if (Mathf.Approximately(_value, 0f))
        {
            Debug.LogWarning($"{nameof(TraitsConfig)} {traitLabel} rule {ruleIndex} has a zero value.", context);
        }

        return valid;
    }
}

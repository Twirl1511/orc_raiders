using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dice", menuName = "GAME/Dice")]
public sealed class DiceConfig : ScriptableObject
{
    [SerializeField] private List<DiceDefinition> _dice = new List<DiceDefinition>();

    public IReadOnlyList<DiceDefinition> Dice => _dice;

    public bool ValidateForRuntime(UnityEngine.Object context)
    {
        bool valid = true;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_dice.Count == 0)
        {
            Debug.LogError($"{nameof(DiceConfig)} requires at least one dice definition.", context);
            return false;
        }

        for (int i = 0; i < _dice.Count; i++)
        {
            DiceDefinition dice = _dice[i];

            if (dice == null)
            {
                Debug.LogError($"{nameof(DiceConfig)} has an empty dice entry at index {i}.", context);
                valid = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(dice.Id))
            {
                Debug.LogError($"{nameof(DiceConfig)} dice at index {i} has empty id.", context);
                valid = false;
            }
            else if (!ids.Add(dice.Id.Trim()))
            {
                Debug.LogError($"{nameof(DiceConfig)} dice id '{dice.Id}' is duplicated. Dice ids must be unique.", context);
                valid = false;
            }

            if (dice.Faces.Count == 0)
            {
                Debug.LogError($"{nameof(DiceConfig)} dice '{dice.DisplayName}' has no faces.", context);
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        ValidateForRuntime(this);
    }
}

[Serializable]
public sealed class DiceDefinition
{
    [SerializeField] private string _id = "dice";
    [SerializeField] private string _displayName = "D6";
    [SerializeField] private List<DiceFaceDefinition> _faces = new List<DiceFaceDefinition>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public IReadOnlyList<DiceFaceDefinition> Faces => _faces;

    public DiceFaceDefinition Roll()
    {
        if (_faces.Count == 0)
        {
            return DiceFaceDefinition.Zero;
        }

        return _faces[UnityEngine.Random.Range(0, _faces.Count)];
    }
}

[Serializable]
public sealed class DiceFaceDefinition
{
    public static readonly DiceFaceDefinition Zero = new DiceFaceDefinition();

    [SerializeField] private DiceStatChange _add = DiceStatChange.Empty;
    [SerializeField] private DiceStatChange _remove = DiceStatChange.Empty;

    public DiceStatChange Add => _add;
    public DiceStatChange Remove => _remove;

    public DiceFaceDefinition()
    {
    }

    public DiceFaceDefinition(DiceStatChange add, DiceStatChange remove)
    {
        _add = add;
        _remove = remove;
    }

    public string GetDisplayText()
    {
        bool hasAdd = _add.HasValue;
        bool hasRemove = _remove.HasValue;

        if (!hasAdd && !hasRemove)
        {
            return "0";
        }

        if (hasAdd && hasRemove)
        {
            return $"{_add.GetDisplayText("+")}, {_remove.GetDisplayText("-")}";
        }

        return hasAdd ? _add.GetDisplayText("+") : _remove.GetDisplayText("-");
    }

    public static string GetStatDisplayName(OrcStatType statType)
    {
        switch (statType)
        {
            case OrcStatType.Endurance:
                return "выносливость";
            case OrcStatType.Strength:
                return "сила";
            case OrcStatType.Agility:
                return "ловкость";
            case OrcStatType.Intelligence:
                return "интеллект";
            default:
                return "нет";
        }
    }
}

[Serializable]
public struct DiceStatChange
{
    public static readonly DiceStatChange Empty = new DiceStatChange(OrcStatType.None, 0);

    [SerializeField] private OrcStatType _statType;
    [SerializeField, Min(0)] private int _value;

    public OrcStatType StatType => _statType;
    public int Value => _value;
    public bool HasValue => _statType != OrcStatType.None && _value > 0;

    public DiceStatChange(OrcStatType statType, int value)
    {
        _statType = statType;
        _value = Mathf.Max(0, value);
    }

    public string GetDisplayText(string sign)
    {
        if (!HasValue)
        {
            return "0";
        }

        return $"{sign}{_value} {DiceFaceDefinition.GetStatDisplayName(_statType)}";
    }
}

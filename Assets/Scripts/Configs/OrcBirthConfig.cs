using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Orc Birth", menuName = "GAME/Orc Birth")]
public sealed class OrcBirthConfig : ScriptableObject
{
    [Header("Dice")]
    [SerializeField, Min(1)] private int _requiredDiceCount = 6;
    [SerializeField, Min(0)] private int _startingDiceCount = 12;
    [SerializeField] private List<DiceTemplateDefinition> _diceTemplates = new List<DiceTemplateDefinition>();

    [Header("Orc")]
    [SerializeField, Min(1)] private int _minimumHealthAfterBirth = 1;
    [SerializeField] private Vector2 _firstOrcSpawnPosition = new Vector2(-5.5f, -1.7f);
    [SerializeField] private Vector2 _orcSpawnSpacing = new Vector2(1.45f, 0f);
    [SerializeField, Min(1)] private int _maxOrcsPerRow = 6;

    public int RequiredDiceCount => _requiredDiceCount;
    public int StartingDiceCount => _startingDiceCount;
    public int MinimumHealthAfterBirth => _minimumHealthAfterBirth;
    public Vector2 FirstOrcSpawnPosition => _firstOrcSpawnPosition;
    public Vector2 OrcSpawnSpacing => _orcSpawnSpacing;
    public int MaxOrcsPerRow => _maxOrcsPerRow;

    public DiceTemplateDefinition GetDiceTemplate(int diceIndex)
    {
        if (_diceTemplates.Count == 0)
        {
            return DiceTemplateDefinition.CreateFallback();
        }

        int templateIndex = Mathf.Abs(diceIndex) % _diceTemplates.Count;
        return _diceTemplates[templateIndex];
    }
}

public enum OrcStatType
{
    None = 0,
    Health = 1,
    Strength = 2,
    Agility = 3,
    Intelligence = 4
}

[Serializable]
public sealed class DiceTemplateDefinition
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

    public static DiceTemplateDefinition CreateFallback()
    {
        return new DiceTemplateDefinition("default_d6", "D6", new List<DiceFaceDefinition>
        {
            new DiceFaceDefinition(OrcStatType.Strength, 1),
            new DiceFaceDefinition(OrcStatType.Strength, 2),
            new DiceFaceDefinition(OrcStatType.Strength, 3),
            new DiceFaceDefinition(OrcStatType.Strength, -1),
            DiceFaceDefinition.Zero,
            new DiceFaceDefinition(OrcStatType.Health, 2)
        });
    }

    public DiceTemplateDefinition()
    {
    }

    private DiceTemplateDefinition(string id, string displayName, List<DiceFaceDefinition> faces)
    {
        _id = id;
        _displayName = displayName;
        _faces = faces;
    }
}

[Serializable]
public sealed class DiceFaceDefinition
{
    public static readonly DiceFaceDefinition Zero = new DiceFaceDefinition(OrcStatType.None, 0);

    [SerializeField] private OrcStatType _statType = OrcStatType.None;
    [SerializeField] private int _value;

    public OrcStatType StatType => _statType;
    public int Value => _value;

    public DiceFaceDefinition()
    {
    }

    public DiceFaceDefinition(OrcStatType statType, int value)
    {
        _statType = statType;
        _value = value;
    }

    public string GetDisplayText()
    {
        if (_statType == OrcStatType.None || _value == 0)
        {
            return "0";
        }

        string sign = _value > 0 ? "+" : "";
        return $"{sign}{_value} {GetStatDisplayName(_statType)}";
    }

    public static string GetStatDisplayName(OrcStatType statType)
    {
        switch (statType)
        {
            case OrcStatType.Health:
                return "здоровье";
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
public sealed class OrcStats
{
    [SerializeField] private int _health;
    [SerializeField] private int _strength;
    [SerializeField] private int _agility;
    [SerializeField] private int _intelligence;

    public int Health => _health;
    public int Strength => _strength;
    public int Agility => _agility;
    public int Intelligence => _intelligence;

    public void Apply(DiceFaceDefinition face)
    {
        switch (face.StatType)
        {
            case OrcStatType.Health:
                _health += face.Value;
                break;
            case OrcStatType.Strength:
                _strength += face.Value;
                break;
            case OrcStatType.Agility:
                _agility += face.Value;
                break;
            case OrcStatType.Intelligence:
                _intelligence += face.Value;
                break;
        }
    }

    public void ClampAfterBirth(int minimumHealth)
    {
        _health = Mathf.Max(minimumHealth, _health);
    }

    public string GetSummary()
    {
        return $"Здоровье: {_health}\nСила: {_strength}\nЛовкость: {_agility}\nИнтеллект: {_intelligence}";
    }
}

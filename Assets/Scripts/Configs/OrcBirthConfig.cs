using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Orc Birth", menuName = "GAME/Orc Birth")]
public sealed class OrcBirthConfig : ScriptableObject
{
    [Header("Dice")]
    [SerializeField, Min(1)] private int _requiredDiceCount = 6;
    [SerializeField] private DiceConfig _diceConfig = null;

    [Header("Orc")]
    [SerializeField, Min(1)] private int _minimumHealthAfterBirth = 1;
    [SerializeField] private Vector2 _firstOrcSpawnPosition = new Vector2(-5.5f, -1.7f);
    [SerializeField] private Vector2 _orcSpawnSpacing = new Vector2(1.45f, 0f);
    [SerializeField, Min(1)] private int _maxOrcsPerRow = 6;

    public int RequiredDiceCount => _requiredDiceCount;
    public DiceConfig DiceConfig => _diceConfig;
    public int MinimumHealthAfterBirth => _minimumHealthAfterBirth;
    public Vector2 FirstOrcSpawnPosition => _firstOrcSpawnPosition;
    public Vector2 OrcSpawnSpacing => _orcSpawnSpacing;
    public int MaxOrcsPerRow => _maxOrcsPerRow;
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
        Apply(face.Add.StatType, face.Add.Value);
        Apply(face.Remove.StatType, -face.Remove.Value);
    }

    private void Apply(OrcStatType statType, int value)
    {
        if (value == 0)
        {
            return;
        }

        switch (statType)
        {
            case OrcStatType.Health:
                _health += value;
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

    public void ClampAfterBirth(int minimumHealth)
    {
        _health = Mathf.Max(minimumHealth, _health);
    }

    public string GetSummary()
    {
        return $"Здоровье: {_health}\nСила: {_strength}\nЛовкость: {_agility}\nИнтеллект: {_intelligence}";
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unit Balance", menuName = "GAME/Unit Balance")]
public sealed class UnitBalanceConfig : ScriptableObject
{
    [SerializeField] private List<UnitBalanceEntry> _units = new List<UnitBalanceEntry>();

    public IReadOnlyList<UnitBalanceEntry> Units => _units;

    public bool TryGetUnit(string id, out UnitBalanceEntry entry)
    {
        for (int i = 0; i < _units.Count; i++)
        {
            UnitBalanceEntry unit = _units[i];

            if (unit != null && string.Equals(unit.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                entry = unit;
                return true;
            }
        }

        entry = null;
        return false;
    }
}

[Serializable]
public sealed class UnitBalanceEntry
{
    [SerializeField] private string _id = "unit";
    [SerializeField] private string _displayName = "Unit";
    [SerializeField] private Sprite _icon = null;
    [SerializeField] private GameObject _prefab = null;
    [SerializeField, Min(0)] private int _cost = 1;
    [SerializeField, Min(1)] private int _maxHealth = 10;
    [SerializeField, Min(0f)] private float _damage = 1f;
    [SerializeField, Min(0.01f)] private float _attackCooldownSeconds = 1f;
    [SerializeField, Min(0f)] private float _moveSpeed = 2f;

    public string Id => _id;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public GameObject Prefab => _prefab;
    public int Cost => _cost;
    public int MaxHealth => _maxHealth;
    public float Damage => _damage;
    public float AttackCooldownSeconds => _attackCooldownSeconds;
    public float MoveSpeed => _moveSpeed;
}

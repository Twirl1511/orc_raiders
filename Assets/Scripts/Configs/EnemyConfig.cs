using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Enemies", menuName = "GAME/Enemies")]
public sealed class EnemyConfig : ScriptableObject
{
    [SerializeField] private List<EnemyDefinition> _enemies = new List<EnemyDefinition>();

    public IReadOnlyList<EnemyDefinition> Enemies => _enemies;

    public bool ValidateForRuntime()
    {
        if (_enemies == null || _enemies.Count == 0)
        {
            return false;
        }

        HashSet<EnemyType> usedTypes = new HashSet<EnemyType>();

        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyDefinition enemy = _enemies[i];

            if (enemy == null || enemy.EnemyType == EnemyType.None || string.IsNullOrWhiteSpace(enemy.DisplayName) ||
                enemy.Stats == null || enemy.MinimumHp < 0f || enemy.AttackIntervalSeconds <= 0f ||
                enemy.ExperienceReward < 0 ||
                !usedTypes.Add(enemy.EnemyType))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryGetEnemy(EnemyType enemyType, out EnemyDefinition enemy)
    {
        if (_enemies == null)
        {
            enemy = null;
            return false;
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyDefinition currentEnemy = _enemies[i];

            if (currentEnemy != null && currentEnemy.EnemyType == enemyType)
            {
                enemy = currentEnemy;
                return true;
            }
        }

        enemy = null;
        return false;
    }
}

public enum EnemyType
{
    None = 0,
    GoblinWithClub = 1,
    GoblinWithBow = 2,
    HumanWarrior = 3,
    HumanArcher = 4,
    HumanMage = 5
}

[Serializable]
public sealed class EnemyDefinition
{
    [SerializeField] private string _displayName = "Враг";
    [SerializeField] private EnemyType _enemyType = EnemyType.None;
    [SerializeField, Min(0f)] private float _minimumHp = 0f;
    [SerializeField, Min(0.01f)] private float _attackIntervalSeconds = 1f;
    [SerializeField, Min(0)] private int _experienceReward = 10;
    [SerializeField] private PrimaryStats _stats = new PrimaryStats();

    public string DisplayName => _displayName;
    public EnemyType EnemyType => _enemyType;
    public float MinimumHp => _minimumHp;
    public float AttackIntervalSeconds => _attackIntervalSeconds;
    public int ExperienceReward => Mathf.Max(0, _experienceReward);
    public PrimaryStats Stats => _stats;
}

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
                enemy.Stats == null || !usedTypes.Add(enemy.EnemyType))
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
    SkeletonWithSword = 3,
    SkeletonWithBow = 4
}

[Serializable]
public sealed class EnemyDefinition
{
    [SerializeField] private string _displayName = "Враг";
    [SerializeField] private EnemyType _enemyType = EnemyType.None;
    [SerializeField] private OrcStats _stats = new OrcStats();

    public string DisplayName => _displayName;
    public EnemyType EnemyType => _enemyType;
    public OrcStats Stats => _stats;
}

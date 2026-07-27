using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Human Village", menuName = "GAME/Human Village")]
public sealed class HumanVillageConfig : ScriptableObject
{
    [SerializeField, Min(1)] private int _rosterSize = 3;
    [SerializeField, Min(0.1f)] private float _scoutingSeconds = 4f;
    [SerializeField] private List<EnemyType> _rosterEnemyTypes = new List<EnemyType>
    {
        EnemyType.HumanWarrior,
        EnemyType.HumanArcher,
        EnemyType.HumanMage
    };

    public int RosterSize => Mathf.Max(1, _rosterSize);
    public float ScoutingSeconds => Mathf.Max(0.1f, _scoutingSeconds);
    public IReadOnlyList<EnemyType> RosterEnemyTypes => _rosterEnemyTypes;

    public bool ValidateForRuntime(EnemyConfig enemyConfig, Object context)
    {
        bool valid = true;

        if (_rosterSize < 1)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} roster size must be at least 1.", context);
            valid = false;
        }

        if (_scoutingSeconds <= 0f)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} scouting seconds must be greater than 0.", context);
            valid = false;
        }

        if (_rosterEnemyTypes == null || _rosterEnemyTypes.Count == 0)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} requires at least one roster enemy type.", context);
            return false;
        }

        if (enemyConfig == null)
        {
            Debug.LogError($"{nameof(HumanVillageConfig)} requires {nameof(EnemyConfig)}.", context);
            return false;
        }

        for (int i = 0; i < _rosterEnemyTypes.Count; i++)
        {
            EnemyType enemyType = _rosterEnemyTypes[i];

            if (enemyType == EnemyType.None || !enemyConfig.TryGetEnemy(enemyType, out _))
            {
                Debug.LogError($"{nameof(HumanVillageConfig)} roster enemy type '{enemyType}' is not configured in {nameof(EnemyConfig)}.", context);
                valid = false;
            }
        }

        return valid;
    }

    private void OnValidate()
    {
        _rosterSize = Mathf.Max(1, _rosterSize);
        _scoutingSeconds = Mathf.Max(0.1f, _scoutingSeconds);
    }
}

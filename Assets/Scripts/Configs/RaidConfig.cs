using UnityEngine;

[CreateAssetMenu(fileName = "Raids", menuName = "GAME/Raids")]
public sealed class RaidConfig : ScriptableObject
{
    [Header("Spawn")]
    [SerializeField, Min(0)] private int _startingRaidCount = 3;
    [SerializeField, Min(1f)] private float _newRaidIntervalSeconds = 60f;

    [Header("Gold Reward")]
    [SerializeField, Min(0)] private int _minGoldReward = 10;
    [SerializeField, Min(0)] private int _maxGoldReward = 35;

    [Header("Enemies")]
    [SerializeField, Min(1)] private int _minEnemies = 1;
    [SerializeField, Min(1)] private int _maxEnemies = 3;
    [SerializeField, Min(1)] private int _maxEnemiesPerBattle = 2;
    [SerializeField, Min(0f)] private float _battleTransitionDelaySeconds = 1.5f;

    public int StartingRaidCount => _startingRaidCount;
    public float NewRaidIntervalSeconds => _newRaidIntervalSeconds;
    public int MinGoldReward => _minGoldReward;
    public int MaxGoldReward => _maxGoldReward;
    public int MinEnemies => _minEnemies;
    public int MaxEnemies => _maxEnemies;
    public int MaxEnemiesPerBattle => _maxEnemiesPerBattle;
    public float BattleTransitionDelaySeconds => Mathf.Max(0f, _battleTransitionDelaySeconds);

    public bool ValidateForRuntime()
    {
        return _startingRaidCount >= 0 &&
            _newRaidIntervalSeconds > 0f &&
            _minGoldReward >= 0 &&
            _maxGoldReward >= _minGoldReward &&
            _minEnemies > 0 &&
            _maxEnemies >= _minEnemies &&
            _maxEnemiesPerBattle > 0 &&
            _maxEnemiesPerBattle <= _maxEnemies &&
            _battleTransitionDelaySeconds >= 0f;
    }

    private void OnValidate()
    {
        _startingRaidCount = Mathf.Max(0, _startingRaidCount);
        _newRaidIntervalSeconds = Mathf.Max(1f, _newRaidIntervalSeconds);
        _minGoldReward = Mathf.Max(0, _minGoldReward);
        _maxGoldReward = Mathf.Max(_minGoldReward, _maxGoldReward);
        _minEnemies = Mathf.Max(1, _minEnemies);
        _maxEnemies = Mathf.Max(_minEnemies, _maxEnemies);
        _maxEnemiesPerBattle = Mathf.Clamp(_maxEnemiesPerBattle, 1, _maxEnemies);
        _battleTransitionDelaySeconds = Mathf.Max(0f, _battleTransitionDelaySeconds);
    }
}

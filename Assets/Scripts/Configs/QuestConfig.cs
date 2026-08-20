using UnityEngine;

[CreateAssetMenu(fileName = "Quests", menuName = "GAME/Quests")]
public sealed class QuestConfig : ScriptableObject
{
    [Header("Spawn")]
    [SerializeField, Min(0)] private int _startingQuestCount = 3;
    [SerializeField, Min(1f)] private float _newQuestIntervalSeconds = 60f;
    [SerializeField] private bool _useManualQuestSpawnButton;
    [SerializeField, Min(1f)] private float _waitingQuestLifetimeSeconds = 60f;

    [Header("Heroes")]
    [SerializeField, Min(1)] private int _minHeroSlots = 1;
    [SerializeField, Range(1, 3)] private int _maxHeroSlots = 3;
    [SerializeField, Min(0f)] private float _additionalHeroWindowSeconds = 4f;

    [Header("Gold Reward")]
    [SerializeField, Min(0)] private int _minGoldReward = 10;
    [SerializeField, Min(0)] private int _maxGoldReward = 35;

    [Header("Enemies")]
    [SerializeField, Min(1)] private int _minEnemies = 1;
    [SerializeField, Min(1)] private int _maxEnemies = 3;
    [SerializeField, Min(1)] private int _minEnemiesPerBattle = 1;
    [SerializeField, Min(1)] private int _maxEnemiesPerBattle = 2;
    [SerializeField, Min(0f)] private float _battleTransitionDelaySeconds = 3f;

    public int StartingQuestCount => _startingQuestCount;
    public float NewQuestIntervalSeconds => _newQuestIntervalSeconds;
    public bool UseManualQuestSpawnButton => _useManualQuestSpawnButton;
    public float WaitingQuestLifetimeSeconds => _waitingQuestLifetimeSeconds;
    public int MinHeroSlots => Mathf.Clamp(_minHeroSlots, 1, 3);
    public int MaxHeroSlots => Mathf.Clamp(_maxHeroSlots, MinHeroSlots, 3);
    public float AdditionalHeroWindowSeconds => Mathf.Max(0f, _additionalHeroWindowSeconds);
    public int MinGoldReward => _minGoldReward;
    public int MaxGoldReward => _maxGoldReward;
    public int MinEnemies => _minEnemies;
    public int MaxEnemies => _maxEnemies;
    public int MinEnemiesPerBattle => _minEnemiesPerBattle;
    public int MaxEnemiesPerBattle => _maxEnemiesPerBattle;
    public float BattleTransitionDelaySeconds => Mathf.Max(0f, _battleTransitionDelaySeconds);

    public bool ValidateForRuntime()
    {
        return _startingQuestCount >= 0 &&
            _newQuestIntervalSeconds > 0f &&
            _waitingQuestLifetimeSeconds > 0f &&
            _minHeroSlots >= 1 &&
            _maxHeroSlots >= _minHeroSlots &&
            _maxHeroSlots <= 3 &&
            _additionalHeroWindowSeconds >= 0f &&
            _minGoldReward >= 0 &&
            _maxGoldReward >= _minGoldReward &&
            _minEnemies > 0 &&
            _maxEnemies >= _minEnemies &&
            _minEnemiesPerBattle > 0 &&
            _maxEnemiesPerBattle >= _minEnemiesPerBattle &&
            _maxEnemiesPerBattle <= _maxEnemies &&
            _battleTransitionDelaySeconds >= 0f;
    }

    private void OnValidate()
    {
        _startingQuestCount = Mathf.Max(0, _startingQuestCount);
        _newQuestIntervalSeconds = Mathf.Max(1f, _newQuestIntervalSeconds);
        _waitingQuestLifetimeSeconds = Mathf.Max(1f, _waitingQuestLifetimeSeconds);
        _minHeroSlots = Mathf.Clamp(_minHeroSlots, 1, 3);
        _maxHeroSlots = Mathf.Clamp(_maxHeroSlots, _minHeroSlots, 3);
        _additionalHeroWindowSeconds = Mathf.Max(0f, _additionalHeroWindowSeconds);
        _minGoldReward = Mathf.Max(0, _minGoldReward);
        _maxGoldReward = Mathf.Max(_minGoldReward, _maxGoldReward);
        _minEnemies = Mathf.Max(1, _minEnemies);
        _maxEnemies = Mathf.Max(_minEnemies, _maxEnemies);
        _minEnemiesPerBattle = Mathf.Clamp(_minEnemiesPerBattle, 1, _maxEnemies);
        _maxEnemiesPerBattle = Mathf.Clamp(_maxEnemiesPerBattle, _minEnemiesPerBattle, _maxEnemies);
        _battleTransitionDelaySeconds = Mathf.Max(0f, _battleTransitionDelaySeconds);
    }
}

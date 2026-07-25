using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Gameplay Config", menuName = "GAME/Gameplay Config")]
public sealed class GameplayConfig : ScriptableObject
{
    [Header("Core")]
    [SerializeField] private GameConfig _gameConfig = null;
    [SerializeField] private EconomyConfig _economy = null;
    [SerializeField] private UnitBalanceConfig _unitBalance = null;
    [SerializeField] private RaidWaveConfig _raidWaves = null;
    [SerializeField] private UiBalanceConfig _uiBalance = null;
    [SerializeField] private CameraConfig _camera = null;

    public GameConfig GameConfig => _gameConfig;
    public EconomyConfig Economy => _economy;
    public UnitBalanceConfig UnitBalance => _unitBalance;
    public RaidWaveConfig RaidWaves => _raidWaves;
    public UiBalanceConfig UiBalance => _uiBalance;
    public CameraConfig Camera => _camera;

    public void Validate()
    {
        Require(_gameConfig, nameof(_gameConfig));
        Require(_economy, nameof(_economy));
        Require(_unitBalance, nameof(_unitBalance));
        Require(_raidWaves, nameof(_raidWaves));
        Require(_uiBalance, nameof(_uiBalance));
        Require(_camera, nameof(_camera));
    }

    private static void Require(UnityEngine.Object config, string fieldName)
    {
        if (config == null)
        {
            throw new InvalidOperationException($"{nameof(GameplayConfig)} requires {fieldName}.");
        }
    }
}

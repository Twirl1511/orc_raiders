using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class GameplayConfigProvider : MonoBehaviour
{
    public static GameplayConfigProvider Instance { get; private set; }

    [SerializeField] private GameplayConfig _gameplayConfig = null;

    public GameplayConfig GameplayConfig => _gameplayConfig;
    public GameConfig GameConfig => _gameplayConfig.GameConfig;
    public EconomyConfig Economy => _gameplayConfig.Economy;
    public UnitBalanceConfig UnitBalance => _gameplayConfig.UnitBalance;
    public RaidWaveConfig RaidWaves => _gameplayConfig.RaidWaves;
    public UiBalanceConfig UiBalance => _gameplayConfig.UiBalance;
    public CameraConfig Camera => _gameplayConfig.Camera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            throw new InvalidOperationException($"Only one {nameof(GameplayConfigProvider)} can exist in a scene.");
        }

        Instance = this;
        Validate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Validate()
    {
        if (_gameplayConfig == null)
        {
            throw new InvalidOperationException($"{nameof(GameplayConfigProvider)} requires {nameof(_gameplayConfig)}.");
        }

        _gameplayConfig.Validate();
    }
}

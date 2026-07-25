using UnityEngine;

[CreateAssetMenu(fileName = "Game Config", menuName = "GAME/Game Config")]
public sealed class GameConfig : ScriptableObject
{
    [Header("Prototype")]
    [SerializeField, Min(0.01f)] private float _simulationTickSeconds = 0.1f;
    [SerializeField, Min(0f)] private float _startDelaySeconds = 0.25f;
    [SerializeField, Range(0f, 4f)] private float _prototypeTimeScale = 1f;

    public float SimulationTickSeconds => _simulationTickSeconds;
    public float StartDelaySeconds => _startDelaySeconds;
    public float PrototypeTimeScale => _prototypeTimeScale;
}

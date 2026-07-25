using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Raid Waves", menuName = "GAME/Raid Waves")]
public sealed class RaidWaveConfig : ScriptableObject
{
    [SerializeField] private List<RaidWaveDefinition> _waves = new List<RaidWaveDefinition>();

    public IReadOnlyList<RaidWaveDefinition> Waves => _waves;
}

[Serializable]
public sealed class RaidWaveDefinition
{
    [SerializeField] private string _id = "wave";
    [SerializeField, Min(0f)] private float _startDelaySeconds = 1f;
    [SerializeField] private List<RaidWaveSpawnEntry> _spawns = new List<RaidWaveSpawnEntry>();

    public string Id => _id;
    public float StartDelaySeconds => _startDelaySeconds;
    public IReadOnlyList<RaidWaveSpawnEntry> Spawns => _spawns;
}

[Serializable]
public sealed class RaidWaveSpawnEntry
{
    [SerializeField] private string _unitId = "unit";
    [SerializeField, Min(1)] private int _count = 1;
    [SerializeField, Min(0f)] private float _intervalSeconds = 0.25f;

    public string UnitId => _unitId;
    public int Count => _count;
    public float IntervalSeconds => _intervalSeconds;
}

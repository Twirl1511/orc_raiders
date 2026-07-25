using System.Collections.Generic;
using UnityEngine;

public enum OrcActivityState
{
    OnBase = 0,
    Resting = 1,
    InRaid = 2
}

public sealed class OrcRuntimeData
{
    private readonly List<string> _rollTexts;

    public OrcRuntimeData(string name, OrcStats stats, List<string> rollTexts)
    {
        Name = name;
        Stats = stats;
        _rollTexts = rollTexts ?? new List<string>();
        State = OrcActivityState.OnBase;
    }

    public string Name { get; }
    public OrcStats Stats { get; }
    public IReadOnlyList<string> RollTexts => _rollTexts;
    public OrcActivityState State { get; private set; }
    public GameObject ViewObject { get; private set; }
    public Vector2 MapPosition { get; private set; }

    public void AttachView(GameObject viewObject)
    {
        ViewObject = viewObject;
    }

    public void SetState(OrcActivityState state)
    {
        State = state;
    }

    public void SetMapPosition(Vector2 mapPosition)
    {
        MapPosition = mapPosition;
    }

    public string GetStateDisplayName()
    {
        switch (State)
        {
            case OrcActivityState.OnBase:
                return "На базе";
            case OrcActivityState.Resting:
                return "Отдыхает";
            case OrcActivityState.InRaid:
                return "В рейде";
            default:
                return State.ToString();
        }
    }
}

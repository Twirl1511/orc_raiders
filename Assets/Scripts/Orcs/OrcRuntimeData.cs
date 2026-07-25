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

    public OrcRuntimeData(string name, OrcStats stats, List<string> rollTexts, float maxHp)
    {
        Name = name;
        Stats = stats;
        _rollTexts = rollTexts ?? new List<string>();
        State = OrcActivityState.OnBase;
        SetMaxHp(maxHp, true);
    }

    public string Name { get; }
    public OrcStats Stats { get; }
    public IReadOnlyList<string> RollTexts => _rollTexts;
    public OrcActivityState State { get; private set; }
    public GameObject ViewObject { get; private set; }
    public Vector2 MapPosition { get; private set; }
    public float CurrentHp { get; private set; }
    public float MaxHp { get; private set; }
    public bool IsFullyHealed => CurrentHp >= MaxHp;

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

    public void SetMaxHp(float maxHp, bool fillCurrentHp)
    {
        MaxHp = Mathf.Max(1f, maxHp);

        if (fillCurrentHp)
        {
            CurrentHp = MaxHp;
            return;
        }

        CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp);
    }

    public void SetCurrentHp(float hp)
    {
        CurrentHp = Mathf.Clamp(hp, 0f, MaxHp);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetCurrentHp(CurrentHp + amount);
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

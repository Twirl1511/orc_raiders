using UnityEngine;

[CreateAssetMenu(fileName = "Rest", menuName = "GAME/Rest")]
public sealed class RestConfig : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float _healTickSeconds = 1f;
    [SerializeField, Range(0f, 100f)] private float _maxHpPercentHealedPerTick = 10f;

    public float HealTickSeconds => Mathf.Max(0.01f, _healTickSeconds);
    public float MaxHpPercentHealedPerTick => Mathf.Clamp(_maxHpPercentHealedPerTick, 0f, 100f);

    public float GetHealAmount(float maxHp)
    {
        return Mathf.Max(0f, maxHp) * MaxHpPercentHealedPerTick / 100f;
    }

    public bool ValidateForRuntime()
    {
        return _healTickSeconds > 0f &&
            _maxHpPercentHealedPerTick >= 0f &&
            _maxHpPercentHealedPerTick <= 100f;
    }

    private void OnValidate()
    {
        _healTickSeconds = Mathf.Max(0.01f, _healTickSeconds);
        _maxHpPercentHealedPerTick = Mathf.Clamp(_maxHpPercentHealedPerTick, 0f, 100f);
    }
}

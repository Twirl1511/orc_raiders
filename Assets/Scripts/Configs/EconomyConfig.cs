using UnityEngine;

[CreateAssetMenu(fileName = "Economy", menuName = "GAME/Economy")]
public sealed class EconomyConfig : ScriptableObject
{
    [Header("Run Start")]
    [SerializeField, Min(0)] private int _startingGold = 10;

    [Header("Rewards")]
    [SerializeField, Min(0)] private int _baseRaidReward = 3;
    [SerializeField, Min(0)] private int _perfectRaidBonus = 2;

    [Header("Shop")]
    [SerializeField, Min(0)] private int _rerollCost = 1;
    [SerializeField, Range(0f, 1f)] private float _sellRefund = 0.5f;

    public int StartingGold => _startingGold;
    public int BaseRaidReward => _baseRaidReward;
    public int PerfectRaidBonus => _perfectRaidBonus;
    public int RerollCost => _rerollCost;
    public float SellRefund => _sellRefund;
}

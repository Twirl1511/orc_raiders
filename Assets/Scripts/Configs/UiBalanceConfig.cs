using UnityEngine;

[CreateAssetMenu(fileName = "UI Balance", menuName = "GAME/UI Balance")]
public sealed class UiBalanceConfig : ScriptableObject
{
    [Header("Interaction")]
    [SerializeField, Min(1f)] private float _minimumClickTargetSize = 44f;
    [SerializeField, Min(0f)] private float _tooltipDelaySeconds = 0.25f;
    [SerializeField, Min(0f)] private float _dragThresholdPixels = 8f;

    [Header("Feedback")]
    [SerializeField, Min(0f)] private float _shortFeedbackSeconds = 0.12f;
    [SerializeField, Min(0f)] private float _errorFeedbackSeconds = 0.25f;

    public float MinimumClickTargetSize => _minimumClickTargetSize;
    public float TooltipDelaySeconds => _tooltipDelaySeconds;
    public float DragThresholdPixels => _dragThresholdPixels;
    public float ShortFeedbackSeconds => _shortFeedbackSeconds;
    public float ErrorFeedbackSeconds => _errorFeedbackSeconds;
}

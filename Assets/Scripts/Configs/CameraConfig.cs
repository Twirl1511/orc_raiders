using UnityEngine;

[CreateAssetMenu(fileName = "Camera", menuName = "GAME/Camera")]
public sealed class CameraConfig : ScriptableObject
{
    [Header("Movement")]
    [SerializeField] private bool _enableMouseEdgeMovement = true;
    [SerializeField, Min(0f)] private float _edgeSizePixels = 28f;
    [SerializeField, Min(0f)] private float _panSpeed = 8f;

    [Header("Bounds")]
    [SerializeField] private Vector2 _minPosition = new Vector2(-12f, -4f);
    [SerializeField] private Vector2 _maxPosition = new Vector2(12f, 4f);

    public bool EnableMouseEdgeMovement => _enableMouseEdgeMovement;
    public float EdgeSizePixels => _edgeSizePixels;
    public float PanSpeed => _panSpeed;
    public Vector2 MinPosition => _minPosition;
    public Vector2 MaxPosition => _maxPosition;
}

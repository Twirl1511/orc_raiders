using UnityEngine;
using UnityEngine.InputSystem;

public sealed class EdgeCameraPan : MonoBehaviour
{
    [SerializeField] private CameraConfig _config = null;
    [SerializeField, Min(0f)] private float _edgeSizePixels = 28f;
    [SerializeField, Min(0f)] private float _panSpeed = 8f;
    [SerializeField] private Vector2 _minPosition = new Vector2(-12f, -4f);
    [SerializeField] private Vector2 _maxPosition = new Vector2(12f, 4f);

    private void Update()
    {
        Vector2 direction = Vector2.zero;
        direction += GetKeyboardDirection();

        if (IsMouseEdgeMovementEnabled())
        {
            direction += GetMouseEdgeDirection();
        }

        if (direction.sqrMagnitude <= 0f)
        {
            return;
        }

        MoveCamera(direction.normalized);
    }

    public void Configure(CameraConfig config)
    {
        _config = config;
    }

    private Vector2 GetKeyboardDirection()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 direction = Vector2.zero;

        if (keyboard.aKey.isPressed)
        {
            direction.x -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            direction.x += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            direction.y -= 1f;
        }

        if (keyboard.wKey.isPressed)
        {
            direction.y += 1f;
        }

        return direction;
    }

    private Vector2 GetMouseEdgeDirection()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return Vector2.zero;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        Vector2 direction = Vector2.zero;
        float edgeSizePixels = GetEdgeSizePixels();

        if (mousePosition.x <= edgeSizePixels)
        {
            direction.x -= 1f;
        }
        else if (mousePosition.x >= Screen.width - edgeSizePixels)
        {
            direction.x += 1f;
        }

        if (mousePosition.y <= edgeSizePixels)
        {
            direction.y -= 1f;
        }
        else if (mousePosition.y >= Screen.height - edgeSizePixels)
        {
            direction.y += 1f;
        }

        return direction;
    }

    private void MoveCamera(Vector2 direction)
    {
        Vector3 position = transform.position;
        Vector2 offset = direction * GetPanSpeed() * Time.unscaledDeltaTime;
        Vector2 minPosition = GetMinPosition();
        Vector2 maxPosition = GetMaxPosition();

        position.x = Mathf.Clamp(position.x + offset.x, minPosition.x, maxPosition.x);
        position.y = Mathf.Clamp(position.y + offset.y, minPosition.y, maxPosition.y);
        transform.position = position;
    }

    private bool IsMouseEdgeMovementEnabled()
    {
        return _config == null || _config.EnableMouseEdgeMovement;
    }

    private float GetEdgeSizePixels()
    {
        return _config != null ? _config.EdgeSizePixels : _edgeSizePixels;
    }

    private float GetPanSpeed()
    {
        return _config != null ? _config.PanSpeed : _panSpeed;
    }

    private Vector2 GetMinPosition()
    {
        return _config != null ? _config.MinPosition : _minPosition;
    }

    private Vector2 GetMaxPosition()
    {
        return _config != null ? _config.MaxPosition : _maxPosition;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class DraggableUiPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform _dragTarget = null;
    [SerializeField] private bool _blockDragFromSelectables = true;

    private RectTransform _parentRect;
    private Vector2 _pointerOffset;
    private bool _isDragging;

    private void Awake()
    {
        if (_dragTarget == null)
        {
            _dragTarget = (RectTransform)transform;
        }

        _parentRect = _dragTarget.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = false;

        if (eventData.button != PointerEventData.InputButton.Left || _parentRect == null)
        {
            return;
        }

        if (_blockDragFromSelectables && PointerStartedOnSelectable(eventData))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            return;
        }

        _pointerOffset = _dragTarget.anchoredPosition - pointerPosition;
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            _dragTarget.anchoredPosition = pointerPosition + _pointerOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
    }

    private bool PointerStartedOnSelectable(PointerEventData eventData)
    {
        GameObject startedObject = eventData.pointerPressRaycast.gameObject != null
            ? eventData.pointerPressRaycast.gameObject
            : eventData.pointerEnter;

        if (startedObject == null)
        {
            return false;
        }

        Selectable selectable = startedObject.GetComponentInParent<Selectable>();
        return selectable != null && selectable.transform.IsChildOf(_dragTarget);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class UiWindowMinimizeController : MonoBehaviour
{
    [SerializeField] private RectTransform _windowRoot = null;
    [SerializeField] private Button _toggleButton = null;
    [SerializeField] private TextMeshProUGUI _toggleLabel = null;
    [SerializeField] private GameObject[] _contentRoots = new GameObject[0];
    [SerializeField, Min(1f)] private float _collapsedHeight = 58f;
    [SerializeField] private bool _reserveToggleSpace = true;
    [SerializeField, Min(0f)] private float _toggleReservedPadding = 8f;
    [SerializeField] private string _expandedLabel = "-";
    [SerializeField] private string _collapsedLabel = "+";

    private Vector2 _expandedSize;
    private bool[] _expandedContentActiveStates;
    private int _baseLayoutPaddingRight;
    private bool _hasBaseLayoutPaddingRight;
    private bool _hasExpandedSize;
    private bool _isMinimized;

    public bool IsMinimized => _isMinimized;

    private void Awake()
    {
        if (_windowRoot == null)
        {
            _windowRoot = (RectTransform)transform;
        }

        CacheExpandedSize();
        ReserveToggleSpaceInLayout();
        MoveToggleToFront();
        RefreshToggleLabel();
    }

    private void OnEnable()
    {
        if (_toggleButton != null)
        {
            _toggleButton.onClick.RemoveListener(Toggle);
            _toggleButton.onClick.AddListener(Toggle);
        }

        ReserveToggleSpaceInLayout();
        RefreshCurrentState();
    }

    private void OnDisable()
    {
        if (_toggleButton != null)
        {
            _toggleButton.onClick.RemoveListener(Toggle);
        }
    }

    private void Toggle()
    {
        SetMinimized(!_isMinimized);
    }

    public void SetMinimized(bool isMinimized)
    {
        if (_windowRoot == null)
        {
            return;
        }

        if (_isMinimized == isMinimized)
        {
            RefreshCurrentState();
            return;
        }

        if (isMinimized)
        {
            CacheExpandedSize();
            CacheExpandedContentActiveStates();
            _isMinimized = true;
            SetContentVisible(false);
        }
        else
        {
            _isMinimized = false;
            RestoreExpandedContentActiveStates();
        }

        ApplyWindowSize();
        MoveToggleToFront();
        RefreshToggleLabel();
    }

    public void SetContentRoots(params GameObject[] contentRoots)
    {
        _contentRoots = contentRoots ?? new GameObject[0];
        _expandedContentActiveStates = null;
        RefreshCurrentState();
    }

    public float GetReservedRightPadding()
    {
        return _reserveToggleSpace ? GetToggleReservedRightPadding() : 0f;
    }

    public void PrepareContentRefresh()
    {
        if (_isMinimized)
        {
            RestoreExpandedContentActiveStates();
        }
    }

    public void CaptureContentAndRefreshState()
    {
        RefreshCurrentState(true);
    }

    public void RefreshCurrentState()
    {
        RefreshCurrentState(false);
    }

    private void RefreshCurrentState(bool captureContentState)
    {
        if (_isMinimized)
        {
            if (captureContentState)
            {
                CacheExpandedContentActiveStates();
            }

            SetContentVisible(false);
        }

        ApplyWindowSize();
        MoveToggleToFront();
        RefreshToggleLabel();
    }

    private void ReserveToggleSpaceInLayout()
    {
        if (!_reserveToggleSpace || _windowRoot == null || _toggleButton == null)
        {
            return;
        }

        HorizontalOrVerticalLayoutGroup layoutGroup = _windowRoot.GetComponent<HorizontalOrVerticalLayoutGroup>();

        if (layoutGroup == null)
        {
            return;
        }

        if (!_hasBaseLayoutPaddingRight)
        {
            _baseLayoutPaddingRight = layoutGroup.padding.right;
            _hasBaseLayoutPaddingRight = true;
        }

        RectOffset padding = layoutGroup.padding;
        padding.right = Mathf.Max(_baseLayoutPaddingRight, Mathf.CeilToInt(GetToggleReservedRightPadding()));
        layoutGroup.padding = padding;
        LayoutRebuilder.MarkLayoutForRebuild(_windowRoot);
    }

    private float GetToggleReservedRightPadding()
    {
        if (_toggleButton == null)
        {
            return 0f;
        }

        RectTransform toggleRect = (RectTransform)_toggleButton.transform;
        float toggleWidth = Mathf.Max(toggleRect.rect.width, toggleRect.sizeDelta.x);
        float rightInset = Mathf.Abs(toggleRect.anchoredPosition.x);
        return toggleWidth + rightInset + _toggleReservedPadding;
    }

    private void CacheExpandedSize()
    {
        if (_windowRoot == null || _isMinimized)
        {
            return;
        }

        _expandedSize = _windowRoot.sizeDelta;
        _hasExpandedSize = true;
    }

    private void ApplyWindowSize()
    {
        if (_windowRoot != null)
        {
            Vector2 size = _isMinimized && _hasExpandedSize
                ? new Vector2(_expandedSize.x, _collapsedHeight)
                : _expandedSize;

            if (_hasExpandedSize)
            {
                _windowRoot.sizeDelta = size;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_windowRoot);
        }
    }

    private void SetContentVisible(bool visible)
    {
        for (int i = 0; i < _contentRoots.Length; i++)
        {
            if (_contentRoots[i] == null)
            {
                continue;
            }

            if (_contentRoots[i].activeSelf != visible)
            {
                _contentRoots[i].SetActive(visible);
            }
        }
    }

    private void CacheExpandedContentActiveStates()
    {
        if (_expandedContentActiveStates == null || _expandedContentActiveStates.Length != _contentRoots.Length)
        {
            _expandedContentActiveStates = new bool[_contentRoots.Length];
        }

        for (int i = 0; i < _contentRoots.Length; i++)
        {
            _expandedContentActiveStates[i] = _contentRoots[i] != null && _contentRoots[i].activeSelf;
        }
    }

    private void RestoreExpandedContentActiveStates()
    {
        for (int i = 0; i < _contentRoots.Length; i++)
        {
            if (_contentRoots[i] == null)
            {
                continue;
            }

            bool visible = true;
            if (_expandedContentActiveStates != null && i < _expandedContentActiveStates.Length)
            {
                visible = _expandedContentActiveStates[i];
            }

            if (_contentRoots[i].activeSelf != visible)
            {
                _contentRoots[i].SetActive(visible);
            }
        }
    }

    private void RefreshToggleLabel()
    {
        if (_toggleLabel != null)
        {
            _toggleLabel.text = _isMinimized ? _collapsedLabel : _expandedLabel;
        }
    }

    private void MoveToggleToFront()
    {
        if (_toggleButton != null)
        {
            _toggleButton.transform.SetAsLastSibling();
        }
    }
}

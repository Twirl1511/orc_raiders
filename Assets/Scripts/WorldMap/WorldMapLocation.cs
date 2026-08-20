using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WorldMapLocationType
{
    Tavern = 0,
    Quest = 1,
    Village = 2,
    Castle = 3,
    Forest = 4,
    Mountain = 5,
    Swamp = 6,
    Ruins = 7,
    Decoration = 8
}

public sealed class WorldMapLocation : MonoBehaviour
{
    [SerializeField] private string _id = "location";
    [SerializeField] private string _displayName = "Location";
    [SerializeField] private WorldMapLocationType _type = WorldMapLocationType.Quest;
    [SerializeField] private WorldMapNode _node = null;
    [SerializeField] private bool _canStartQuest = true;
    [SerializeField] private Button _button = null;
    [SerializeField] private TextMeshProUGUI _label = null;

    public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
    public WorldMapLocationType Type => _type;
    public WorldMapNode Node => _node;
    public bool CanStartQuest => _canStartQuest && _type == WorldMapLocationType.Quest && _node != null;

    public event Action<WorldMapLocation> Clicked;

    private void Awake()
    {
        ResolveLocalReferences();
        RefreshLabel();
    }

    private void OnEnable()
    {
        ResolveLocalReferences();
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked);
            _button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void RefreshLabel()
    {
        if (_label != null)
        {
            _label.text = DisplayName;
        }
    }

    private void ResolveLocalReferences()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_label == null)
        {
            _label = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            _id = name;
        }

        ResolveLocalReferences();
        RefreshLabel();
    }
}
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OrcBirthSystem : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private OrcBirthConfig _config = null;
    [SerializeField] private Camera _camera = null;

    [Header("Scene UI")]
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _availableDiceRoot = null;
    [SerializeField] private RectTransform _selectedDiceRoot = null;
    [SerializeField] private Button _diceButtonTemplate = null;
    [SerializeField] private TextMeshProUGUI _selectedDiceLabel = null;
    [SerializeField] private TextMeshProUGUI _statusText = null;
    [SerializeField] private TextMeshProUGUI _orcInfoText = null;
    [SerializeField] private Button _createOrcButton = null;

    private readonly List<DiceRuntimeData> _availableDice = new List<DiceRuntimeData>();
    private readonly List<DiceRuntimeData> _selectedDice = new List<DiceRuntimeData>();
    private readonly List<GameObject> _runtimeDiceButtons = new List<GameObject>();
    private readonly Dictionary<GameObject, OrcRuntimeData> _orcDataByObject = new Dictionary<GameObject, OrcRuntimeData>();

    private Sprite _whiteSprite;
    private int _nextDiceId = 1;
    private int _nextOrcId = 1;
    private bool _initialized;

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    private void OnDisable()
    {
        if (_createOrcButton != null)
        {
            _createOrcButton.onClick.RemoveListener(CreateOrc);
        }
    }

    private void Update()
    {
        if (!_initialized || _camera == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = _camera.ScreenToWorldPoint(mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(worldPosition);

        if (hit != null && _orcDataByObject.TryGetValue(hit.gameObject, out OrcRuntimeData orcData))
        {
            ShowOrcInfo(orcData);
        }
    }

    public bool Configure(OrcBirthConfig config, Camera sceneCamera)
    {
        bool changed = _config != config || _camera != sceneCamera;
        _config = config;
        _camera = sceneCamera;
        return changed;
    }

    public bool ConfigureUi(OrcBirthUiReferences uiReferences)
    {
        bool changed =
            _canvas != uiReferences.Canvas ||
            _availableDiceRoot != uiReferences.AvailableDiceRoot ||
            _selectedDiceRoot != uiReferences.SelectedDiceRoot ||
            _diceButtonTemplate != uiReferences.DiceButtonTemplate ||
            _selectedDiceLabel != uiReferences.SelectedDiceLabel ||
            _statusText != uiReferences.StatusText ||
            _orcInfoText != uiReferences.OrcInfoText ||
            _createOrcButton != uiReferences.CreateOrcButton;

        _canvas = uiReferences.Canvas;
        _availableDiceRoot = uiReferences.AvailableDiceRoot;
        _selectedDiceRoot = uiReferences.SelectedDiceRoot;
        _diceButtonTemplate = uiReferences.DiceButtonTemplate;
        _selectedDiceLabel = uiReferences.SelectedDiceLabel;
        _statusText = uiReferences.StatusText;
        _orcInfoText = uiReferences.OrcInfoText;
        _createOrcButton = uiReferences.CreateOrcButton;
        return changed;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();

        _initialized = true;
        _whiteSprite = CreateWhiteSprite();
        _diceButtonTemplate.gameObject.SetActive(false);
        _createOrcButton.onClick.RemoveListener(CreateOrc);
        _createOrcButton.onClick.AddListener(CreateOrc);

        CreateInitialDicePool();
        ResetInfoText();
        RefreshUi();
    }

    private void ValidateReferences()
    {
        if (_config == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires {nameof(OrcBirthConfig)}.");
        }

        if (_camera == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires scene camera.");
        }

        if (_canvas == null || _availableDiceRoot == null || _selectedDiceRoot == null || _diceButtonTemplate == null ||
            _selectedDiceLabel == null || _statusText == null || _orcInfoText == null || _createOrcButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires scene UI references.");
        }
    }

    private void CreateInitialDicePool()
    {
        _availableDice.Clear();
        _selectedDice.Clear();
        _nextDiceId = 1;

        for (int i = 0; i < _config.StartingDiceCount; i++)
        {
            _availableDice.Add(new DiceRuntimeData(_nextDiceId, _config.GetDiceTemplate(i)));
            _nextDiceId++;
        }
    }

    private void ResetInfoText()
    {
        _statusText.text = $"Выбери минимум {_config.RequiredDiceCount} кубиков и нажми кнопку.";
        _orcInfoText.text = "Созданные орки появятся рядом с котлом.\nКлик по орку покажет статы.";
    }

    private void RefreshUi()
    {
        ClearRuntimeDiceButtons();
        RefreshDiceGrid(_availableDiceRoot, _availableDice, SelectDice);
        RefreshDiceGrid(_selectedDiceRoot, _selectedDice, UnselectDice);

        _createOrcButton.interactable = _selectedDice.Count >= _config.RequiredDiceCount;
        _selectedDiceLabel.text = $"Кубики в котле: {_selectedDice.Count}/{_config.RequiredDiceCount}";
    }

    private void RefreshDiceGrid(RectTransform root, List<DiceRuntimeData> dice, System.Action<DiceRuntimeData> clickAction)
    {
        for (int i = 0; i < dice.Count; i++)
        {
            DiceRuntimeData diceData = dice[i];
            Button diceButton = CreateDiceButton(root, $"{diceData.Id}\n{diceData.Template.DisplayName}");
            diceButton.onClick.AddListener(() => clickAction(diceData));
        }
    }

    private Button CreateDiceButton(Transform parent, string text)
    {
        Button button = Instantiate(_diceButtonTemplate, parent, false);
        button.gameObject.name = "Dice Button";
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            label.text = text;
        }

        _runtimeDiceButtons.Add(button.gameObject);
        return button;
    }

    private void SelectDice(DiceRuntimeData diceData)
    {
        _availableDice.Remove(diceData);
        _selectedDice.Add(diceData);
        RefreshUi();
    }

    private void UnselectDice(DiceRuntimeData diceData)
    {
        _selectedDice.Remove(diceData);
        _availableDice.Add(diceData);
        RefreshUi();
    }

    private void CreateOrc()
    {
        if (_selectedDice.Count < _config.RequiredDiceCount)
        {
            _statusText.text = $"Нужно минимум {_config.RequiredDiceCount} кубиков.";
            return;
        }

        OrcStats stats = new OrcStats();
        List<string> rollTexts = new List<string>();

        for (int i = 0; i < _selectedDice.Count; i++)
        {
            DiceFaceDefinition face = _selectedDice[i].Template.Roll();
            stats.Apply(face);
            rollTexts.Add(face.GetDisplayText());
        }

        stats.ClampAfterBirth(_config.MinimumHealthAfterBirth);

        OrcRuntimeData orcData = new OrcRuntimeData($"Орк {_nextOrcId}", stats, rollTexts);
        SpawnOrc(orcData);

        _nextOrcId++;
        _selectedDice.Clear();
        _statusText.text = $"{orcData.Name} рожден.\nБроски: {string.Join(", ", rollTexts)}";
        ShowOrcInfo(orcData);
        RefreshUi();
    }

    private void SpawnOrc(OrcRuntimeData orcData)
    {
        int orcIndex = _orcDataByObject.Count;
        int row = orcIndex / _config.MaxOrcsPerRow;
        int column = orcIndex % _config.MaxOrcsPerRow;
        Vector2 spawnPosition = _config.FirstOrcSpawnPosition + new Vector2(_config.OrcSpawnSpacing.x * column, _config.OrcSpawnSpacing.y - row * 1.2f);

        GameObject orcObject = new GameObject(orcData.Name);
        orcObject.transform.position = spawnPosition;
        orcObject.transform.localScale = Vector3.one;

        SpriteRenderer renderer = orcObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 6;

        BoxCollider2D collider = orcObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        CreateWorldLabel(orcObject.transform, orcData.Name, new Vector3(0f, 0.75f, -0.1f));
        _orcDataByObject.Add(orcObject, orcData);
    }

    private void CreateWorldLabel(Transform parent, string text, Vector3 localPosition)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        labelObject.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 4f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.sortingOrder = 12;
        label.rectTransform.sizeDelta = new Vector2(5f, 1f);
    }

    private void ShowOrcInfo(OrcRuntimeData orcData)
    {
        _orcInfoText.text = $"{orcData.Name}\n{orcData.Stats.GetSummary()}\n\nБроски:\n{string.Join(", ", orcData.RollTexts)}";
    }

    private Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void ClearRuntimeDiceButtons()
    {
        for (int i = _runtimeDiceButtons.Count - 1; i >= 0; i--)
        {
            if (_runtimeDiceButtons[i] != null)
            {
                Destroy(_runtimeDiceButtons[i]);
            }
        }

        _runtimeDiceButtons.Clear();
    }

    private sealed class DiceRuntimeData
    {
        public readonly int Id;
        public readonly DiceTemplateDefinition Template;

        public DiceRuntimeData(int id, DiceTemplateDefinition template)
        {
            Id = id;
            Template = template;
        }
    }

    private sealed class OrcRuntimeData
    {
        public readonly string Name;
        public readonly OrcStats Stats;
        public readonly List<string> RollTexts;

        public OrcRuntimeData(string name, OrcStats stats, List<string> rollTexts)
        {
            Name = name;
            Stats = stats;
            RollTexts = rollTexts;
        }
    }
}

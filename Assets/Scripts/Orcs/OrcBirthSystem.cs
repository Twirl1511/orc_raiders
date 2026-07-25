using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OrcBirthSystem : MonoBehaviour
{
    private const int _orcInfoHpBarCells = 14;
    private const char _filledHpCell = '#';
    private const char _emptyHpCell = '-';

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
    [SerializeField] private TextMeshProUGUI _orcInfoTitle = null;
    [SerializeField] private TextMeshProUGUI _orcInfoText = null;
    [SerializeField] private Button _createOrcButton = null;

    [Header("Rest Zone")]
    [SerializeField] private Collider2D _restZoneCollider = null;

    [Header("Raids")]
    [SerializeField] private RaidSystem _raidSystem = null;

    private readonly List<DiceRuntimeData> _availableDice = new List<DiceRuntimeData>();
    private readonly List<DiceRuntimeData> _selectedDice = new List<DiceRuntimeData>();
    private readonly List<GameObject> _runtimeDiceButtons = new List<GameObject>();
    private readonly List<OrcRuntimeData> _orcs = new List<OrcRuntimeData>();
    private readonly Dictionary<GameObject, OrcRuntimeData> _orcDataByObject = new Dictionary<GameObject, OrcRuntimeData>();
    private readonly Dictionary<OrcRuntimeData, float> _restHealTimers = new Dictionary<OrcRuntimeData, float>();

    private Sprite _whiteSprite;
    private OrcRuntimeData _selectedOrc;
    private OrcRuntimeData _draggedOrc;
    private Vector3 _dragOffset;
    private int _nextOrcId = 1;
    private bool _initialized;

    public IReadOnlyList<OrcRuntimeData> Orcs => _orcs;

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
        if (!_initialized)
        {
            return;
        }

        UpdateRestingOrcs(Time.deltaTime);

        if (_camera == null || Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryBeginOrcDrag();
        }

        if (_draggedOrc != null && Mouse.current.leftButton.isPressed)
        {
            UpdateOrcDrag();
        }

        if (_draggedOrc != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndOrcDrag();
        }
    }

    public bool Configure(OrcBirthConfig config, Camera sceneCamera)
    {
        bool changed = _config != config || _camera != sceneCamera;
        _config = config;
        _camera = sceneCamera;
        return changed;
    }

    public void SetOrcState(OrcRuntimeData orcData, OrcActivityState state)
    {
        if (orcData == null || !_orcs.Contains(orcData))
        {
            return;
        }

        orcData.SetState(state);

        if (state != OrcActivityState.InRaid)
        {
            orcData.SetMapPosition(GetDefaultOrcPositionForState(orcData, state));
        }

        RefreshOrcAfterStateChange(orcData);
    }

    private void SetOrcStateAtPosition(OrcRuntimeData orcData, OrcActivityState state, Vector2 mapPosition)
    {
        if (orcData == null || !_orcs.Contains(orcData))
        {
            return;
        }

        orcData.SetState(state);
        orcData.SetMapPosition(mapPosition);
        RefreshOrcAfterStateChange(orcData);
    }

    private void RefreshOrcAfterStateChange(OrcRuntimeData orcData)
    {
        if (orcData.State != OrcActivityState.Resting)
        {
            _restHealTimers.Remove(orcData);
        }

        RefreshOrcVisualStates();

        if (_selectedOrc == orcData)
        {
            ShowOrcInfo(orcData);
        }
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

        if (_config.DiceConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires {nameof(DiceConfig)} in {nameof(OrcBirthConfig)}.");
        }

        if (_config.StatsConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires {nameof(StatsConfig)} in {nameof(OrcBirthConfig)}.");
        }

        if (_config.RestConfig == null || !_config.RestConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires valid {nameof(RestConfig)} in {nameof(OrcBirthConfig)}.");
        }

        if (!_config.DiceConfig.ValidateForRuntime(_config))
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires valid dice config.");
        }

        if (!_config.StatsConfig.ValidateForRuntime(_config))
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires valid stats config.");
        }

        if (_camera == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires scene camera.");
        }

        if (_canvas == null || _availableDiceRoot == null || _selectedDiceRoot == null || _diceButtonTemplate == null ||
            _selectedDiceLabel == null || _statusText == null || _orcInfoTitle == null || _orcInfoText == null ||
            _createOrcButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires scene UI references.");
        }

        if (_restZoneCollider == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires rest zone collider reference.");
        }

        if (_raidSystem == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires raid system reference.");
        }
    }

    private void TryBeginOrcDrag()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector3 worldPosition = GetMouseWorldPosition();
        OrcRuntimeData orcData = GetOrcAtWorldPosition(worldPosition);

        if (orcData == null || orcData.State == OrcActivityState.InRaid || orcData.ViewObject == null)
        {
            return;
        }

        _draggedOrc = orcData;
        _dragOffset = orcData.ViewObject.transform.position - worldPosition;
        ShowOrcInfo(orcData);
    }

    private void UpdateOrcDrag()
    {
        if (_draggedOrc.ViewObject == null)
        {
            _draggedOrc = null;
            return;
        }

        _draggedOrc.ViewObject.transform.position = GetMouseWorldPosition() + _dragOffset;
    }

    private void EndOrcDrag()
    {
        OrcRuntimeData orcData = _draggedOrc;
        _draggedOrc = null;

        if (orcData == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (_raidSystem.TryAcceptDroppedOrc(orcData, screenPosition))
        {
            _statusText.text = $"{orcData.Name}: {orcData.GetStateDisplayName()}.";
            return;
        }

        OrcActivityState nextState = IsPointInsideRestZone(GetMouseWorldPosition())
            ? OrcActivityState.Resting
            : OrcActivityState.OnBase;
        SetOrcStateAtPosition(orcData, nextState, orcData.ViewObject.transform.position);
        _statusText.text = $"{orcData.Name}: {orcData.GetStateDisplayName()}.";
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = _camera.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0f;
        return worldPosition;
    }

    private OrcRuntimeData GetOrcAtWorldPosition(Vector2 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && _orcDataByObject.TryGetValue(hits[i].gameObject, out OrcRuntimeData orcData))
            {
                return orcData;
            }
        }

        return null;
    }

    private bool IsPointInsideRestZone(Vector2 worldPosition)
    {
        return _restZoneCollider != null && _restZoneCollider.OverlapPoint(worldPosition);
    }

    private void CreateInitialDicePool()
    {
        _availableDice.Clear();
        _selectedDice.Clear();

        IReadOnlyList<DiceDefinition> configuredDice = _config.DiceConfig.Dice;

        for (int i = 0; i < configuredDice.Count; i++)
        {
            _availableDice.Add(new DiceRuntimeData(configuredDice[i]));
        }
    }

    private void ResetInfoText()
    {
        _statusText.text = $"Выбери минимум {_config.RequiredDiceCount} кубиков и нажми кнопку.";
        _orcInfoTitle.text = "Орк";
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
            Button diceButton = CreateDiceButton(root, diceData.Definition.DisplayName);
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
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = 14f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
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
        stats.SetToMinimums(_config.StatsConfig);
        List<string> rollTexts = new List<string>();

        for (int i = 0; i < _selectedDice.Count; i++)
        {
            DiceDefinition dice = _selectedDice[i].Definition;
            DiceFaceDefinition face = dice.Roll();
            stats.Apply(face);
            rollTexts.Add($"{dice.DisplayName}: {face.GetDisplayText()}");
        }

        stats.ClampAfterBirth(_config.StatsConfig);

        float maxHp = CalculateOrcMaxHp(stats);
        OrcRuntimeData orcData = new OrcRuntimeData($"Орк {_nextOrcId}", stats, rollTexts, maxHp);
        SpawnOrc(orcData);

        _nextOrcId++;
        _selectedDice.Clear();
        _statusText.text = $"{orcData.Name} рожден.";
        ShowOrcInfo(orcData);
        RefreshUi();
    }

    private void SpawnOrc(OrcRuntimeData orcData)
    {
        Vector2 visualSize = _config.OrcVisualSize;
        GameObject orcObject = new GameObject(orcData.Name);
        orcObject.transform.SetParent(transform, false);
        orcObject.transform.localScale = Vector3.one;

        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(orcObject.transform, false);

        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = visualSize;
        renderer.color = _config.OrcVisualColor;
        renderer.sortingOrder = _config.OrcSpriteSortingOrder;

        GameObject colliderObject = new GameObject("Collider");
        colliderObject.transform.SetParent(orcObject.transform, false);

        BoxCollider2D collider = colliderObject.AddComponent<BoxCollider2D>();
        collider.size = visualSize;

        CreateOrcLabel(orcObject.transform, orcData.Name, visualSize);
        orcData.SetMapPosition(GetDefaultOrcPositionForState(orcData, OrcActivityState.OnBase));
        _orcs.Add(orcData);
        orcData.AttachView(orcObject);
        _orcDataByObject.Add(colliderObject, orcData);
        RefreshOrcVisualStates();
    }

    private void RefreshOrcVisualStates()
    {
        for (int i = 0; i < _orcs.Count; i++)
        {
            OrcRuntimeData orcData = _orcs[i];

            if (orcData.ViewObject == null)
            {
                continue;
            }

            bool isVisible = orcData.State != OrcActivityState.InRaid;
            orcData.ViewObject.SetActive(isVisible);

            if (isVisible)
            {
                orcData.ViewObject.transform.position = orcData.MapPosition;
            }
        }
    }

    private Vector2 GetDefaultOrcPositionForState(OrcRuntimeData targetOrc, OrcActivityState state)
    {
        Vector2 firstPosition = state == OrcActivityState.Resting
            ? _config.FirstRestingOrcPosition
            : _config.FirstOrcSpawnPosition;
        Vector2 spacing = state == OrcActivityState.Resting
            ? _config.RestingOrcSpacing
            : _config.OrcSpawnSpacing;
        int maxOrcsPerRow = state == OrcActivityState.Resting
            ? _config.MaxRestingOrcsPerRow
            : _config.MaxOrcsPerRow;
        int indexInState = 0;

        for (int i = 0; i < _orcs.Count; i++)
        {
            OrcRuntimeData orcData = _orcs[i];

            if (orcData != targetOrc && orcData.State == state)
            {
                indexInState++;
            }
        }

        int row = indexInState / maxOrcsPerRow;
        int column = indexInState % maxOrcsPerRow;
        return firstPosition + new Vector2(spacing.x * column, spacing.y - row * 1.2f);
    }

    private void CreateOrcLabel(Transform parent, string text, Vector2 visualSize)
    {
        const float labelScale = 0.25f;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = new Vector3(labelScale, labelScale, 1f);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = 4f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = 4f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.sortingOrder = _config.OrcLabelSortingOrder;
        label.rectTransform.sizeDelta = new Vector2(visualSize.x * 0.92f / labelScale, visualSize.y * 0.9f / labelScale);
    }

    private void ShowOrcInfo(OrcRuntimeData orcData)
    {
        _selectedOrc = orcData;
        _orcInfoTitle.text = orcData.Name;
        _orcInfoText.text = $"Состояние: {orcData.GetStateDisplayName()}\n{FormatOrcHealthLine(orcData)}\n\n{orcData.Stats.GetSummary(_config.StatsConfig)}\n\nВторичные статы:\n{_config.StatsConfig.GetSecondaryStatsSummary(orcData.Stats)}";
    }

    private void UpdateRestingOrcs(float deltaTime)
    {
        RestConfig restConfig = _config.RestConfig;

        if (restConfig == null)
        {
            return;
        }

        float tickSeconds = restConfig.HealTickSeconds;

        for (int i = 0; i < _orcs.Count; i++)
        {
            OrcRuntimeData orcData = _orcs[i];

            if (orcData.State != OrcActivityState.Resting || orcData.IsFullyHealed)
            {
                _restHealTimers.Remove(orcData);
                continue;
            }

            float healAmount = restConfig.GetHealAmount(orcData.MaxHp);

            if (healAmount <= 0f)
            {
                continue;
            }

            _restHealTimers.TryGetValue(orcData, out float timer);
            timer += deltaTime;
            bool healed = false;

            while (timer >= tickSeconds && !orcData.IsFullyHealed)
            {
                timer -= tickSeconds;
                orcData.Heal(healAmount);
                healed = true;
            }

            if (orcData.IsFullyHealed)
            {
                timer = 0f;
            }

            _restHealTimers[orcData] = timer;

            if (healed && _selectedOrc == orcData)
            {
                ShowOrcInfo(orcData);
            }
        }
    }

    private float CalculateOrcMaxHp(OrcStats stats)
    {
        return Mathf.Max(1f, _config.StatsConfig.CalculateSecondaryStats(stats).MaxHp);
    }

    private static string FormatOrcHealthLine(OrcRuntimeData orcData)
    {
        int currentHp = Mathf.CeilToInt(orcData.CurrentHp);
        int maxHp = Mathf.CeilToInt(orcData.MaxHp);
        return $"Здоровье: {currentHp}/{maxHp} <mspace=0.45em>{FormatHpBar(orcData.CurrentHp, orcData.MaxHp)}</mspace>";
    }

    private static string FormatHpBar(float currentHp, float maxHp)
    {
        float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(currentHp / maxHp);
        int filledCells = Mathf.Clamp(Mathf.RoundToInt(ratio * _orcInfoHpBarCells), 0, _orcInfoHpBarCells);
        int emptyCells = _orcInfoHpBarCells - filledCells;
        return $"<color=#FFFFFF>{new string(_filledHpCell, filledCells)}</color><color=#5B6570>{new string(_emptyHpCell, emptyCells)}</color>";
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
        public readonly DiceDefinition Definition;

        public DiceRuntimeData(DiceDefinition definition)
        {
            Definition = definition;
        }
    }
}

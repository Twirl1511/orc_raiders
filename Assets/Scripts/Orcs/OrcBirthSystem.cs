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
    private static readonly Vector2 _primaryStatUpgradeButtonSize = new Vector2(20f, 18f);
    private static readonly Color _selectedOrcOutlineColor = new Color(1f, 0.84f, 0.12f, 1f);
    private static readonly Vector2 _selectedOrcOutlinePadding = new Vector2(0.14f, 0.14f);
    private const float _orcLabelScale = 0.25f;
    private const float _primaryStatUpgradeButtonXRatio = 0.37f;
    private const int _primaryStatLineIndexWithoutFreeStats = 4;
    private const int _primaryStatLineIndexWithFreeStats = 5;

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

    [Header("Primary Stat Upgrade Buttons")]
    [SerializeField] private Button _enduranceUpgradeButton = null;
    [SerializeField] private Button _strengthUpgradeButton = null;
    [SerializeField] private Button _agilityUpgradeButton = null;
    [SerializeField] private Button _intelligenceUpgradeButton = null;

    [Header("Rest Zone")]
    [SerializeField] private Collider2D _restZoneCollider = null;

    [Header("Raids")]
    [SerializeField] private RaidSystem _raidSystem = null;

    private readonly List<DiceRuntimeData> _availableDice = new List<DiceRuntimeData>();
    private readonly List<DiceRuntimeData> _selectedDice = new List<DiceRuntimeData>();
    private readonly List<GameObject> _runtimeDiceButtons = new List<GameObject>();
    private readonly List<OrcRuntimeData> _orcs = new List<OrcRuntimeData>();
    private readonly Dictionary<GameObject, OrcRuntimeData> _orcDataByObject = new Dictionary<GameObject, OrcRuntimeData>();
    private readonly Dictionary<OrcRuntimeData, GameObject> _selectionOutlineByOrc = new Dictionary<OrcRuntimeData, GameObject>();
    private readonly Dictionary<OrcRuntimeData, OrcMapHealthBar> _healthBarByOrc = new Dictionary<OrcRuntimeData, OrcMapHealthBar>();
    private readonly Dictionary<OrcRuntimeData, OrcMapVisual> _mapVisualByOrc = new Dictionary<OrcRuntimeData, OrcMapVisual>();
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

        RemovePrimaryStatUpgradeListeners();
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

    public void AddExperienceToOrc(OrcRuntimeData orcData, int amount)
    {
        if (orcData == null || !_orcs.Contains(orcData) || amount <= 0)
        {
            return;
        }

        int levelBefore = orcData.Level;
        orcData.AddExperience(amount, _config.LevelUpConfig, _config.StatsConfig);
        orcData.SetMaxHp(CalculateOrcMaxHp(orcData.Stats), false);
        if (orcData.Level != levelBefore)
        {
            RefreshOrcMapVisualSize(orcData);
        }

        RefreshOrcHealthBar(orcData);

        if (_selectedOrc == orcData)
        {
            ShowOrcInfo(orcData);
        }
    }

    public DiceDefinition AddRandomDiceFromConfigToPool()
    {
        IReadOnlyList<DiceDefinition> configuredDice = _config.DiceConfig.Dice;

        if (configuredDice.Count == 0)
        {
            return null;
        }

        DiceDefinition rewardDice = configuredDice[Random.Range(0, configuredDice.Count)];
        _availableDice.Add(new DiceRuntimeData(rewardDice));
        _statusText.text = $"Рейд принес кубик: {rewardDice.DisplayName}.";
        RefreshUi();
        return rewardDice;
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
        ConfigurePrimaryStatUpgradeButtonVisuals();
        AddPrimaryStatUpgradeListeners();

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

        if (_config.LevelUpConfig == null || !_config.LevelUpConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires valid {nameof(LevelUpConfig)} in {nameof(OrcBirthConfig)}.");
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

        if (_enduranceUpgradeButton == null || _strengthUpgradeButton == null || _agilityUpgradeButton == null ||
            _intelligenceUpgradeButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(OrcBirthSystem)} requires primary stat upgrade button references.");
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
        RefreshPrimaryStatUpgradeButtons(null);
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
        Vector2 visualSize = _config.GetOrcVisualSizeForLevel(orcData.Level);
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

        GameObject selectionOutline = CreateOrcSelectionOutline(orcObject.transform, visualSize);
        OrcMapHealthBar healthBar = CreateOrcHealthBar(orcObject.transform, visualSize);
        TextMeshPro label = CreateOrcLabel(orcObject.transform, orcData.Name, visualSize);
        orcData.SetMapPosition(GetDefaultOrcPositionForState(orcData, OrcActivityState.OnBase));
        _orcs.Add(orcData);
        orcData.AttachView(orcObject);
        _orcDataByObject.Add(colliderObject, orcData);
        _selectionOutlineByOrc.Add(orcData, selectionOutline);
        _healthBarByOrc.Add(orcData, healthBar);
        _mapVisualByOrc.Add(orcData, new OrcMapVisual(renderer, collider, selectionOutline.GetComponent<SpriteRenderer>(), label, healthBar));
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

            if (_selectionOutlineByOrc.TryGetValue(orcData, out GameObject selectionOutline) &&
                selectionOutline != null)
            {
                selectionOutline.SetActive(isVisible && orcData == _selectedOrc);
            }

            RefreshOrcHealthBar(orcData);
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

    private TextMeshPro CreateOrcLabel(Transform parent, string text, Vector2 visualSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = new Vector3(_orcLabelScale, _orcLabelScale, 1f);

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
        label.rectTransform.sizeDelta = GetOrcLabelRectSize(visualSize);
        return label;
    }

    private GameObject CreateOrcSelectionOutline(Transform parent, Vector2 visualSize)
    {
        GameObject outlineObject = new GameObject("Selection Outline");
        outlineObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = outlineObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = visualSize + _selectedOrcOutlinePadding;
        renderer.color = _selectedOrcOutlineColor;
        renderer.sortingOrder = _config.OrcSpriteSortingOrder - 1;

        outlineObject.SetActive(false);
        return outlineObject;
    }

    private OrcMapHealthBar CreateOrcHealthBar(Transform parent, Vector2 visualSize)
    {
        Vector2 barSize = GetOrcHealthBarSize(visualSize);
        Vector3 barPosition = GetOrcHealthBarPosition(visualSize);

        GameObject rootObject = new GameObject("HP Bar");
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.localPosition = barPosition;

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(rootObject.transform, false);

        SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
        background.sprite = _whiteSprite;
        background.drawMode = SpriteDrawMode.Sliced;
        background.size = barSize;
        background.color = new Color(0.09f, 0.1f, 0.11f, 1f);
        background.sortingOrder = _config.OrcLabelSortingOrder + 1;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(rootObject.transform, false);

        SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
        fill.sprite = _whiteSprite;
        fill.drawMode = SpriteDrawMode.Sliced;
        fill.size = barSize;
        fill.color = new Color(0.42f, 0.9f, 0.42f, 1f);
        fill.sortingOrder = _config.OrcLabelSortingOrder + 2;

        return new OrcMapHealthBar(rootObject, background, fill, barSize);
    }

    private void RefreshOrcMapVisualSize(OrcRuntimeData orcData)
    {
        if (orcData == null || !_mapVisualByOrc.TryGetValue(orcData, out OrcMapVisual mapVisual))
        {
            return;
        }

        mapVisual.SetSize(_config.GetOrcVisualSizeForLevel(orcData.Level), _selectedOrcOutlinePadding);
    }

    private static Vector2 GetOrcLabelRectSize(Vector2 visualSize)
    {
        return new Vector2(visualSize.x * 0.92f / _orcLabelScale, visualSize.y * 0.9f / _orcLabelScale);
    }

    private static Vector2 GetOrcHealthBarSize(Vector2 visualSize)
    {
        return new Vector2(visualSize.x * 0.9f, 0.08f);
    }

    private static Vector3 GetOrcHealthBarPosition(Vector2 visualSize)
    {
        return new Vector3(0f, visualSize.y * 0.5f + 0.16f, -0.1f);
    }

    private void RefreshOrcHealthBar(OrcRuntimeData orcData)
    {
        if (orcData == null || !_healthBarByOrc.TryGetValue(orcData, out OrcMapHealthBar healthBar))
        {
            return;
        }

        bool isVisible = orcData.State != OrcActivityState.InRaid;
        healthBar.SetVisible(isVisible);

        if (isVisible)
        {
            healthBar.SetHealth(orcData.CurrentHp, orcData.MaxHp);
        }
    }

    private void ShowOrcInfo(OrcRuntimeData orcData)
    {
        _selectedOrc = orcData;
        RefreshOrcVisualStates();
        _orcInfoTitle.text = orcData.Name;
        string freeStatsLine = orcData.FreePrimaryStatPoints > 0
            ? $"Свободные статы: {orcData.FreePrimaryStatPoints}\n"
            : "";

        _orcInfoText.text = $"Состояние: {orcData.GetStateDisplayName()}\n" +
            $"Уровень: {orcData.Level}    Опыт: {orcData.GetExperienceDisplay(_config.LevelUpConfig)}\n" +
            freeStatsLine +
            $"{FormatOrcHealthLine(orcData)}\n\n" +
            $"{orcData.Stats.GetSummary(_config.StatsConfig)}\n\n" +
            $"Вторичные статы:\n{_config.StatsConfig.GetSecondaryStatsSummary(orcData.Stats)}";
        RefreshPrimaryStatUpgradeButtons(orcData);
    }

    private void AddPrimaryStatUpgradeListeners()
    {
        _enduranceUpgradeButton.onClick.RemoveListener(SpendEndurancePoint);
        _strengthUpgradeButton.onClick.RemoveListener(SpendStrengthPoint);
        _agilityUpgradeButton.onClick.RemoveListener(SpendAgilityPoint);
        _intelligenceUpgradeButton.onClick.RemoveListener(SpendIntelligencePoint);

        _enduranceUpgradeButton.onClick.AddListener(SpendEndurancePoint);
        _strengthUpgradeButton.onClick.AddListener(SpendStrengthPoint);
        _agilityUpgradeButton.onClick.AddListener(SpendAgilityPoint);
        _intelligenceUpgradeButton.onClick.AddListener(SpendIntelligencePoint);
    }

    private void ConfigurePrimaryStatUpgradeButtonVisuals()
    {
        ConfigurePrimaryStatUpgradeButtonVisual(_enduranceUpgradeButton);
        ConfigurePrimaryStatUpgradeButtonVisual(_strengthUpgradeButton);
        ConfigurePrimaryStatUpgradeButtonVisual(_agilityUpgradeButton);
        ConfigurePrimaryStatUpgradeButtonVisual(_intelligenceUpgradeButton);
    }

    private static void ConfigurePrimaryStatUpgradeButtonVisual(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();

        if (image != null)
        {
            image.color = new Color(0.92f, 0.92f, 0.9f, 1f);
            image.raycastTarget = true;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            label.text = "+";
            label.fontSize = 16f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }
    }

    private void RemovePrimaryStatUpgradeListeners()
    {
        if (_enduranceUpgradeButton != null)
        {
            _enduranceUpgradeButton.onClick.RemoveListener(SpendEndurancePoint);
        }

        if (_strengthUpgradeButton != null)
        {
            _strengthUpgradeButton.onClick.RemoveListener(SpendStrengthPoint);
        }

        if (_agilityUpgradeButton != null)
        {
            _agilityUpgradeButton.onClick.RemoveListener(SpendAgilityPoint);
        }

        if (_intelligenceUpgradeButton != null)
        {
            _intelligenceUpgradeButton.onClick.RemoveListener(SpendIntelligencePoint);
        }
    }

    private void RefreshPrimaryStatUpgradeButtons(OrcRuntimeData orcData)
    {
        _orcInfoText.ForceMeshUpdate();

        PositionPrimaryStatUpgradeButton(_enduranceUpgradeButton, orcData, 0);
        PositionPrimaryStatUpgradeButton(_strengthUpgradeButton, orcData, 1);
        PositionPrimaryStatUpgradeButton(_agilityUpgradeButton, orcData, 2);
        PositionPrimaryStatUpgradeButton(_intelligenceUpgradeButton, orcData, 3);

        RefreshPrimaryStatUpgradeButton(_enduranceUpgradeButton, orcData, OrcStatType.Endurance);
        RefreshPrimaryStatUpgradeButton(_strengthUpgradeButton, orcData, OrcStatType.Strength);
        RefreshPrimaryStatUpgradeButton(_agilityUpgradeButton, orcData, OrcStatType.Agility);
        RefreshPrimaryStatUpgradeButton(_intelligenceUpgradeButton, orcData, OrcStatType.Intelligence);
    }

    private void PositionPrimaryStatUpgradeButton(Button button, OrcRuntimeData orcData, int rowIndex)
    {
        if (button == null || _orcInfoText == null)
        {
            return;
        }

        RectTransform rectTransform = button.transform as RectTransform;

        if (rectTransform == null)
        {
            return;
        }

        RectTransform textRectTransform = _orcInfoText.rectTransform;
        rectTransform.SetParent(textRectTransform, false);
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = _primaryStatUpgradeButtonSize;

        TMP_TextInfo textInfo = _orcInfoText.textInfo;
        int primaryStatLineIndex = (orcData != null && orcData.FreePrimaryStatPoints > 0
            ? _primaryStatLineIndexWithFreeStats
            : _primaryStatLineIndexWithoutFreeStats) + rowIndex;

        if (primaryStatLineIndex < 0 || primaryStatLineIndex >= textInfo.lineCount)
        {
            return;
        }

        TMP_LineInfo lineInfo = textInfo.lineInfo[primaryStatLineIndex];
        Rect textRect = textRectTransform.rect;
        float x = textRect.width * _primaryStatUpgradeButtonXRatio;
        float y = ((lineInfo.ascender + lineInfo.descender) * 0.5f) - textRect.yMax;
        rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void RefreshPrimaryStatUpgradeButton(Button button, OrcRuntimeData orcData, OrcStatType statType)
    {
        if (button == null)
        {
            return;
        }

        bool visible = orcData != null && orcData.FreePrimaryStatPoints > 0;
        button.gameObject.SetActive(visible);
        button.interactable = visible && orcData.CanSpendFreePrimaryStatPoint(statType, _config.StatsConfig);
    }

    private void SpendEndurancePoint()
    {
        SpendPrimaryStatPoint(OrcStatType.Endurance);
    }

    private void SpendStrengthPoint()
    {
        SpendPrimaryStatPoint(OrcStatType.Strength);
    }

    private void SpendAgilityPoint()
    {
        SpendPrimaryStatPoint(OrcStatType.Agility);
    }

    private void SpendIntelligencePoint()
    {
        SpendPrimaryStatPoint(OrcStatType.Intelligence);
    }

    private void SpendPrimaryStatPoint(OrcStatType statType)
    {
        if (_selectedOrc == null || !_selectedOrc.TrySpendFreePrimaryStatPoint(statType, _config.StatsConfig))
        {
            return;
        }

        _selectedOrc.SetMaxHp(CalculateOrcMaxHp(_selectedOrc.Stats), false);
        RefreshOrcHealthBar(_selectedOrc);
        _raidSystem.RefreshOrcCombatStats(_selectedOrc);
        ShowOrcInfo(_selectedOrc);
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
                RefreshOrcHealthBar(orcData);
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

    private sealed class OrcMapVisual
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly BoxCollider2D _collider;
        private readonly SpriteRenderer _selectionOutline;
        private readonly TextMeshPro _label;
        private readonly OrcMapHealthBar _healthBar;

        public OrcMapVisual(
            SpriteRenderer spriteRenderer,
            BoxCollider2D collider,
            SpriteRenderer selectionOutline,
            TextMeshPro label,
            OrcMapHealthBar healthBar)
        {
            _spriteRenderer = spriteRenderer;
            _collider = collider;
            _selectionOutline = selectionOutline;
            _label = label;
            _healthBar = healthBar;
        }

        public void SetSize(Vector2 visualSize, Vector2 outlinePadding)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.size = visualSize;
            }

            if (_collider != null)
            {
                _collider.size = visualSize;
            }

            if (_selectionOutline != null)
            {
                _selectionOutline.size = visualSize + outlinePadding;
            }

            if (_label != null)
            {
                _label.rectTransform.sizeDelta = GetOrcLabelRectSize(visualSize);
            }

            _healthBar?.SetSize(GetOrcHealthBarSize(visualSize), GetOrcHealthBarPosition(visualSize));
        }
    }

    private sealed class OrcMapHealthBar
    {
        private readonly GameObject _rootObject;
        private readonly SpriteRenderer _background;
        private readonly SpriteRenderer _fill;
        private Vector2 _size;

        public OrcMapHealthBar(GameObject rootObject, SpriteRenderer background, SpriteRenderer fill, Vector2 size)
        {
            _rootObject = rootObject;
            _background = background;
            _fill = fill;
            _size = size;
        }

        public void SetVisible(bool visible)
        {
            if (_rootObject != null)
            {
                _rootObject.SetActive(visible);
            }
        }

        public void SetHealth(float currentHp, float maxHp)
        {
            if (_fill == null)
            {
                return;
            }

            float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(currentHp / maxHp);
            float fillWidth = _size.x * ratio;
            _fill.size = new Vector2(fillWidth, _size.y);
            _fill.transform.localPosition = new Vector3((-_size.x + fillWidth) * 0.5f, 0f, 0f);
        }

        public void SetSize(Vector2 size, Vector3 localPosition)
        {
            _size = size;

            if (_rootObject != null)
            {
                _rootObject.transform.localPosition = localPosition;
            }

            if (_background != null)
            {
                _background.size = size;
            }
        }
    }
}

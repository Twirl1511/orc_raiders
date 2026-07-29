using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class NecropolisSystem : MonoBehaviour
{
    private static readonly Color _selectedHeroOutlineColor = new Color(1f, 0.84f, 0.12f, 1f);
    private static readonly Vector2 _selectedHeroOutlinePadding = new Vector2(0.14f, 0.14f);
    private const float _heroLabelScale = 0.25f;

    [Header("Config")]
    [SerializeField] private NecropolisConfig _config = null;
    [SerializeField] private TooltipConfig _tooltipConfig = null;
    [SerializeField] private Camera _camera = null;

    [Header("Scene UI")]
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _availableDiceRoot = null;
    [SerializeField] private RectTransform _selectedDiceRoot = null;
    [SerializeField] private Button _diceButtonTemplate = null;
    [SerializeField] private TextMeshProUGUI _selectedDiceLabel = null;
    [SerializeField] private TextMeshProUGUI _statusText = null;
    [SerializeField] private HeroInfoPanelView _heroInfoPanel = null;
    [SerializeField] private Button _createHeroButton = null;
    [SerializeField] private ItemStorageSystem _itemStorage = null;
    [SerializeField] private ItemTooltipView _itemTooltip = null;
    [SerializeField] private HeroItemSlotView[] _heroItemSlots = new HeroItemSlotView[HeroRuntimeData.EquipmentSlotCount];

    [Header("Rest Zone")]
    [SerializeField] private Collider2D _restZoneCollider = null;

    [Header("Raids")]
    [SerializeField] private RaidSystem _raidSystem = null;

    [Header("Human Village")]
    [SerializeField] private HumanVillageSystem _humanVillageSystem = null;

    private readonly List<DiceRuntimeData> _availableDice = new List<DiceRuntimeData>();
    private readonly List<DiceRuntimeData> _selectedDice = new List<DiceRuntimeData>();
    private readonly List<GameObject> _runtimeDiceButtons = new List<GameObject>();
    private readonly List<HeroRuntimeData> _heroes = new List<HeroRuntimeData>();
    private readonly Dictionary<GameObject, HeroRuntimeData> _heroDataByObject = new Dictionary<GameObject, HeroRuntimeData>();
    private readonly Dictionary<HeroRuntimeData, GameObject> _selectionOutlineByHero = new Dictionary<HeroRuntimeData, GameObject>();
    private readonly Dictionary<HeroRuntimeData, HeroMapHealthBar> _healthBarByHero = new Dictionary<HeroRuntimeData, HeroMapHealthBar>();
    private readonly Dictionary<HeroRuntimeData, HeroMapVisual> _mapVisualByHero = new Dictionary<HeroRuntimeData, HeroMapVisual>();
    private readonly Dictionary<HeroRuntimeData, float> _restHealTimers = new Dictionary<HeroRuntimeData, float>();

    private Sprite _whiteSprite;
    private HeroRuntimeData _selectedHero;
    private HeroRuntimeData _draggedHero;
    private Vector3 _dragOffset;
    private int _nextHeroId = 1;
    private bool _initialized;

    public IReadOnlyList<HeroRuntimeData> Heroes => _heroes;
    public HeroRuntimeData SelectedHero => _selectedHero;

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
        if (_createHeroButton != null)
        {
            _createHeroButton.onClick.RemoveListener(CreateHero);
        }

        if (_heroInfoPanel != null)
        {
            _heroInfoPanel.PrimaryStatUpgradeRequested -= SpendPrimaryStatPoint;
        }

        _itemTooltip?.Hide();
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        UpdateRestingHeroes(Time.deltaTime);

        if (_camera == null || Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryBeginHeroDrag();
        }

        if (_draggedHero != null && Mouse.current.leftButton.isPressed)
        {
            UpdateHeroDrag();
        }

        if (_draggedHero != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndHeroDrag();
        }
    }

    public bool Configure(NecropolisConfig config, Camera sceneCamera)
    {
        bool changed = _config != config || _camera != sceneCamera;
        _config = config;
        _camera = sceneCamera;
        return changed;
    }

    public void SetHeroState(HeroRuntimeData heroData, HeroActivityState state)
    {
        if (heroData == null || !_heroes.Contains(heroData))
        {
            return;
        }

        bool clearSelection = _selectedHero == heroData && state == HeroActivityState.InRaid;
        heroData.SetState(state);

        if (state != HeroActivityState.InRaid)
        {
            heroData.SetMapPosition(GetDefaultHeroPositionForState(heroData, state));
        }

        if (clearSelection)
        {
            if (state != HeroActivityState.Resting)
            {
                _restHealTimers.Remove(heroData);
            }

            ClearHeroSelection();
            return;
        }

        RefreshHeroAfterStateChange(heroData);
    }

    public void SelectHeroFromRaid(HeroRuntimeData heroData)
    {
        if (!_initialized || heroData == null || !_heroes.Contains(heroData) || heroData.State != HeroActivityState.InRaid)
        {
            return;
        }

        ShowHeroInfo(heroData);
        _statusText.text = $"{heroData.Name}: {heroData.GetStateDisplayName()}.";
    }

    public void AddExperienceToHero(HeroRuntimeData heroData, int amount)
    {
        if (heroData == null || !_heroes.Contains(heroData) || amount <= 0)
        {
            return;
        }

        int levelBefore = heroData.Level;
        heroData.AddExperience(amount, _config.LevelUpConfig, _config.StatsConfig);
        heroData.SetMaxHp(CalculateHeroMaxHp(heroData), false);
        if (heroData.Level != levelBefore)
        {
            RefreshHeroMapVisualSize(heroData);
        }

        RefreshHeroHealthBar(heroData);

        if (_selectedHero == heroData)
        {
            ShowHeroInfo(heroData);
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
        _statusText.text = $"Рейд принес кость: {rewardDice.DisplayName}.";
        RefreshUi();
        return rewardDice;
    }

    public bool TryEquipItemFromStorage(
        HeroRuntimeData heroData,
        int slotIndex,
        ItemRuntimeData item,
        ItemStorageSystem itemStorage)
    {
        if (!_initialized || heroData == null || item == null || itemStorage == null || !_heroes.Contains(heroData))
        {
            return false;
        }

        if (!CanEditHeroPanel(heroData))
        {
            return false;
        }

        if (!itemStorage.TryEquipItemToHero(item, heroData, slotIndex))
        {
            return false;
        }

        RefreshHeroAfterEquipmentChange(heroData);

        _statusText.text = $"{heroData.Name}: экипирован {item.DisplayName}.";
        return true;
    }

    public bool TryUnequipItemToStorage(HeroRuntimeData heroData, int slotIndex)
    {
        if (!_initialized || heroData == null || _itemStorage == null || !_heroes.Contains(heroData))
        {
            return false;
        }

        if (!CanEditHeroPanel(heroData))
        {
            return false;
        }

        if (!heroData.TryUnequipItem(slotIndex, out ItemRuntimeData item))
        {
            return false;
        }

        if (!_itemStorage.AddItem(item))
        {
            heroData.TryEquipItem(slotIndex, item, out _);
            return false;
        }

        RefreshHeroAfterEquipmentChange(heroData);
        _statusText.text = $"{heroData.Name}: {item.DisplayName} возвращен в инвентарь.";
        return true;
    }

    private void SetHeroStateAtPosition(HeroRuntimeData heroData, HeroActivityState state, Vector2 mapPosition)
    {
        if (heroData == null || !_heroes.Contains(heroData))
        {
            return;
        }

        heroData.SetState(state);
        heroData.SetMapPosition(mapPosition);
        RefreshHeroAfterStateChange(heroData);
    }

    private void RefreshHeroAfterStateChange(HeroRuntimeData heroData)
    {
        if (heroData.State != HeroActivityState.Resting)
        {
            _restHealTimers.Remove(heroData);
        }

        RefreshHeroVisualStates();

        if (_selectedHero == heroData)
        {
            ShowHeroInfo(heroData);
        }
    }

    private void RefreshHeroAfterEquipmentChange(HeroRuntimeData heroData)
    {
        if (heroData == null)
        {
            return;
        }

        heroData.SetMaxHp(CalculateHeroMaxHp(heroData), false);
        RefreshHeroHealthBar(heroData);
        _raidSystem.RefreshHeroCombatStats(heroData);

        if (_selectedHero == heroData)
        {
            ShowHeroInfo(heroData);
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
        _createHeroButton.onClick.RemoveListener(CreateHero);
        _createHeroButton.onClick.AddListener(CreateHero);
        _itemTooltip.Configure(_canvas, _config.StatsConfig, _tooltipConfig);
        _heroInfoPanel.Configure(_itemTooltip);
        _heroInfoPanel.PrimaryStatUpgradeRequested -= SpendPrimaryStatPoint;
        _heroInfoPanel.PrimaryStatUpgradeRequested += SpendPrimaryStatPoint;
        ConfigureHeroItemSlots();

        CreateInitialDicePool();
        ResetInfoText();
        RefreshUi();
    }

    private void ValidateReferences()
    {
        if (_config == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires {nameof(NecropolisConfig)}.");
        }

        if (_config.DiceConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires {nameof(DiceConfig)} in {nameof(NecropolisConfig)}.");
        }

        if (_config.StatsConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires {nameof(StatsConfig)} in {nameof(NecropolisConfig)}.");
        }

        if (_config.RestConfig == null || !_config.RestConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires valid {nameof(RestConfig)} in {nameof(NecropolisConfig)}.");
        }

        if (_config.LevelUpConfig == null || !_config.LevelUpConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires valid {nameof(LevelUpConfig)} in {nameof(NecropolisConfig)}.");
        }

        if (_tooltipConfig == null || !_tooltipConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires valid {nameof(TooltipConfig)}.");
        }

        if (!_config.DiceConfig.ValidateForRuntime(_config))
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires valid dice config.");
        }

        if (!_config.StatsConfig.ValidateForRuntime(_config))
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires valid stats config.");
        }

        if (_camera == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires scene camera.");
        }

        if (_canvas == null || _availableDiceRoot == null || _selectedDiceRoot == null || _diceButtonTemplate == null ||
            _selectedDiceLabel == null || _statusText == null || _heroInfoPanel == null || _createHeroButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires scene UI references.");
        }

        if (!_heroInfoPanel.HasRequiredReferences())
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires configured {nameof(HeroInfoPanelView)} references.");
        }

        if (_itemStorage == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires {nameof(ItemStorageSystem)} reference.");
        }

        if (_itemTooltip == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires {nameof(ItemTooltipView)} reference.");
        }

        if (_heroItemSlots == null || _heroItemSlots.Length < HeroRuntimeData.EquipmentSlotCount)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires hero item slot references.");
        }

        for (int i = 0; i < HeroRuntimeData.EquipmentSlotCount; i++)
        {
            if (_heroItemSlots[i] == null)
            {
                throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires hero item slot {i + 1} reference.");
            }
        }

        if (_restZoneCollider == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires rest zone collider reference.");
        }

        if (_raidSystem == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires raid system reference.");
        }

        if (_humanVillageSystem == null)
        {
            throw new System.InvalidOperationException($"{nameof(NecropolisSystem)} requires human village system reference.");
        }
    }

    private void TryBeginHeroDrag()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector3 worldPosition = GetMouseWorldPosition();
        HeroRuntimeData heroData = GetHeroAtWorldPosition(worldPosition);

        if (heroData == null || heroData.State == HeroActivityState.InRaid || heroData.ViewObject == null)
        {
            return;
        }

        _draggedHero = heroData;
        _dragOffset = heroData.ViewObject.transform.position - worldPosition;
        ShowHeroInfo(heroData);
    }

    private void UpdateHeroDrag()
    {
        if (_draggedHero.ViewObject == null)
        {
            _draggedHero = null;
            return;
        }

        _draggedHero.ViewObject.transform.position = GetMouseWorldPosition() + _dragOffset;
    }

    private void EndHeroDrag()
    {
        HeroRuntimeData heroData = _draggedHero;
        _draggedHero = null;

        if (heroData == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (_raidSystem.TryAcceptDroppedHero(heroData, screenPosition))
        {
            _statusText.text = $"{heroData.Name}: {heroData.GetStateDisplayName()}.";
            return;
        }

        if (_humanVillageSystem.TryAcceptDroppedHero(heroData, screenPosition))
        {
            _statusText.text = $"{heroData.Name}: {heroData.GetStateDisplayName()}.";
            return;
        }

        HeroActivityState nextState = IsPointInsideRestZone(GetMouseWorldPosition())
            ? HeroActivityState.Resting
            : HeroActivityState.OnBase;
        SetHeroStateAtPosition(heroData, nextState, heroData.ViewObject.transform.position);
        _statusText.text = $"{heroData.Name}: {heroData.GetStateDisplayName()}.";
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = _camera.ScreenToWorldPoint(mousePosition);
        worldPosition.z = 0f;
        return worldPosition;
    }

    private HeroRuntimeData GetHeroAtWorldPosition(Vector2 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && _heroDataByObject.TryGetValue(hits[i].gameObject, out HeroRuntimeData heroData))
            {
                return heroData;
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
        _statusText.text = $"Выбери минимум {_config.RequiredDiceCount} костей и нажми кнопку.";
        ClearHeroSelection();
    }

    private void RefreshUi()
    {
        ClearRuntimeDiceButtons();
        RefreshDiceGrid(_availableDiceRoot, _availableDice, SelectDice);
        RefreshDiceGrid(_selectedDiceRoot, _selectedDice, UnselectDice);

        _createHeroButton.interactable = _selectedDice.Count >= _config.RequiredDiceCount;
        _selectedDiceLabel.text = $"Кости в Некрополе: {_selectedDice.Count}/{_config.RequiredDiceCount}";
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

    private void CreateHero()
    {
        if (_selectedDice.Count < _config.RequiredDiceCount)
        {
            _statusText.text = $"Нужно минимум {_config.RequiredDiceCount} костей.";
            return;
        }

        PrimaryStats stats = new PrimaryStats();
        stats.SetToMinimums(_config.StatsConfig);
        List<string> rollTexts = new List<string>();

        for (int i = 0; i < _selectedDice.Count; i++)
        {
            DiceDefinition dice = _selectedDice[i].Definition;
            DiceFaceDefinition face = dice.Roll();
            stats.Apply(face);
            rollTexts.Add($"{dice.DisplayName}: {face.GetDisplayText()}");
        }

        stats.ClampAfterCreation(_config.StatsConfig);

        float maxHp = CalculateBaseHeroMaxHp(stats);
        HeroRuntimeData heroData = new HeroRuntimeData($"Герой {_nextHeroId}", stats, rollTexts, maxHp);
        SpawnHero(heroData);

        _nextHeroId++;
        _selectedDice.Clear();
        _statusText.text = $"{heroData.Name} рожден.";
        ShowHeroInfo(heroData);
        RefreshUi();
    }

    private void SpawnHero(HeroRuntimeData heroData)
    {
        Vector2 visualSize = _config.GetHeroVisualSizeForLevel(heroData.Level);
        GameObject heroObject = new GameObject(heroData.Name);
        heroObject.transform.SetParent(transform, false);
        heroObject.transform.localScale = Vector3.one;

        GameObject spriteObject = new GameObject("Sprite");
        spriteObject.transform.SetParent(heroObject.transform, false);

        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = visualSize;
        renderer.color = _config.HeroVisualColor;
        renderer.sortingOrder = _config.HeroSpriteSortingOrder;

        GameObject colliderObject = new GameObject("Collider");
        colliderObject.transform.SetParent(heroObject.transform, false);

        BoxCollider2D collider = colliderObject.AddComponent<BoxCollider2D>();
        collider.size = visualSize;

        GameObject selectionOutline = CreateHeroSelectionOutline(heroObject.transform, visualSize);
        HeroMapHealthBar healthBar = CreateHeroHealthBar(heroObject.transform, visualSize);
        TextMeshPro label = CreateHeroLabel(heroObject.transform, heroData.Name, visualSize);
        heroData.SetMapPosition(GetDefaultHeroPositionForState(heroData, HeroActivityState.OnBase));
        _heroes.Add(heroData);
        heroData.AttachView(heroObject);
        _heroDataByObject.Add(colliderObject, heroData);
        _selectionOutlineByHero.Add(heroData, selectionOutline);
        _healthBarByHero.Add(heroData, healthBar);
        _mapVisualByHero.Add(heroData, new HeroMapVisual(renderer, collider, selectionOutline.GetComponent<SpriteRenderer>(), label, healthBar));
        RefreshHeroVisualStates();
    }

    private void RefreshHeroVisualStates()
    {
        for (int i = 0; i < _heroes.Count; i++)
        {
            HeroRuntimeData heroData = _heroes[i];

            if (heroData.ViewObject == null)
            {
                continue;
            }

            bool isVisible = heroData.State != HeroActivityState.InRaid;
            heroData.ViewObject.SetActive(isVisible);

            if (isVisible)
            {
                heroData.ViewObject.transform.position = heroData.MapPosition;
            }

            if (_selectionOutlineByHero.TryGetValue(heroData, out GameObject selectionOutline) &&
                selectionOutline != null)
            {
                selectionOutline.SetActive(isVisible && heroData == _selectedHero);
            }

            RefreshHeroHealthBar(heroData);
        }
    }

    private void ConfigureHeroItemSlots()
    {
        for (int i = 0; i < HeroRuntimeData.EquipmentSlotCount; i++)
        {
            _heroItemSlots[i].Configure(this, i, _itemTooltip);
        }
    }

    private Vector2 GetDefaultHeroPositionForState(HeroRuntimeData targetHero, HeroActivityState state)
    {
        Vector2 firstPosition = state == HeroActivityState.Resting
            ? _config.FirstRestingHeroPosition
            : _config.FirstHeroSpawnPosition;
        Vector2 spacing = state == HeroActivityState.Resting
            ? _config.RestingHeroSpacing
            : _config.HeroSpawnSpacing;
        int maxHeroesPerRow = state == HeroActivityState.Resting
            ? _config.MaxRestingHeroesPerRow
            : _config.MaxHeroesPerRow;
        int indexInState = 0;

        for (int i = 0; i < _heroes.Count; i++)
        {
            HeroRuntimeData heroData = _heroes[i];

            if (heroData != targetHero && heroData.State == state)
            {
                indexInState++;
            }
        }

        int row = indexInState / maxHeroesPerRow;
        int column = indexInState % maxHeroesPerRow;
        return firstPosition + new Vector2(spacing.x * column, spacing.y - row * 1.2f);
    }

    private TextMeshPro CreateHeroLabel(Transform parent, string text, Vector2 visualSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        labelObject.transform.localScale = new Vector3(_heroLabelScale, _heroLabelScale, 1f);

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
        label.sortingOrder = _config.HeroLabelSortingOrder;
        label.rectTransform.sizeDelta = GetHeroLabelRectSize(visualSize);
        return label;
    }

    private GameObject CreateHeroSelectionOutline(Transform parent, Vector2 visualSize)
    {
        GameObject outlineObject = new GameObject("Selection Outline");
        outlineObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = outlineObject.AddComponent<SpriteRenderer>();
        renderer.sprite = _whiteSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = visualSize + _selectedHeroOutlinePadding;
        renderer.color = _selectedHeroOutlineColor;
        renderer.sortingOrder = _config.HeroSpriteSortingOrder - 1;

        outlineObject.SetActive(false);
        return outlineObject;
    }

    private HeroMapHealthBar CreateHeroHealthBar(Transform parent, Vector2 visualSize)
    {
        Vector2 barSize = GetHeroHealthBarSize(visualSize);
        Vector3 barPosition = GetHeroHealthBarPosition(visualSize);

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
        background.sortingOrder = _config.HeroLabelSortingOrder + 1;

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(rootObject.transform, false);

        SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
        fill.sprite = _whiteSprite;
        fill.drawMode = SpriteDrawMode.Sliced;
        fill.size = barSize;
        fill.color = new Color(0.42f, 0.9f, 0.42f, 1f);
        fill.sortingOrder = _config.HeroLabelSortingOrder + 2;

        return new HeroMapHealthBar(rootObject, background, fill, barSize);
    }

    private void RefreshHeroMapVisualSize(HeroRuntimeData heroData)
    {
        if (heroData == null || !_mapVisualByHero.TryGetValue(heroData, out HeroMapVisual mapVisual))
        {
            return;
        }

        mapVisual.SetSize(_config.GetHeroVisualSizeForLevel(heroData.Level), _selectedHeroOutlinePadding);
    }

    private static Vector2 GetHeroLabelRectSize(Vector2 visualSize)
    {
        return new Vector2(visualSize.x * 0.92f / _heroLabelScale, visualSize.y * 0.9f / _heroLabelScale);
    }

    private static Vector2 GetHeroHealthBarSize(Vector2 visualSize)
    {
        return new Vector2(visualSize.x * 0.9f, 0.08f);
    }

    private static Vector3 GetHeroHealthBarPosition(Vector2 visualSize)
    {
        return new Vector3(0f, visualSize.y * 0.5f + 0.16f, -0.1f);
    }

    private void RefreshHeroHealthBar(HeroRuntimeData heroData)
    {
        if (heroData == null || !_healthBarByHero.TryGetValue(heroData, out HeroMapHealthBar healthBar))
        {
            return;
        }

        bool isVisible = heroData.State != HeroActivityState.InRaid;
        healthBar.SetVisible(isVisible);

        if (isVisible)
        {
            healthBar.SetHealth(heroData.CurrentHp, heroData.MaxHp);
        }
    }

    private void ShowHeroInfo(HeroRuntimeData heroData)
    {
        if (heroData == null)
        {
            ClearHeroSelection();
            return;
        }

        _selectedHero = heroData;
        SetHeroInfoPanelVisible(true);
        RefreshHeroVisualStates();
        RefreshHeroItemSlots(heroData);
        _heroInfoPanel.ShowHero(heroData, _config.StatsConfig, _config.LevelUpConfig, CanEditHeroPanel(heroData));
        _raidSystem?.RefreshHeroSelectionVisuals();
    }

    private void ClearHeroSelection()
    {
        _selectedHero = null;
        _itemTooltip?.Hide();
        _heroInfoPanel?.ClearHero();
        SetHeroInfoPanelVisible(false);
        RefreshHeroItemSlots(null);
        RefreshHeroVisualStates();
        _raidSystem?.RefreshHeroSelectionVisuals();
    }

    private void SetHeroInfoPanelVisible(bool visible)
    {
        _heroInfoPanel?.SetVisible(visible);
    }

    private void RefreshHeroItemSlots(HeroRuntimeData heroData)
    {
        if (_heroItemSlots == null)
        {
            return;
        }

        for (int i = 0; i < _heroItemSlots.Length; i++)
        {
            if (_heroItemSlots[i] != null)
            {
                _heroItemSlots[i].SetHero(heroData, CanEditHeroPanel(heroData));
            }
        }
    }

    private void SpendPrimaryStatPoint(PrimaryStatType statType)
    {
        if (!CanEditHeroPanel(_selectedHero) || !_selectedHero.TrySpendFreePrimaryStatPoint(statType, _config.StatsConfig))
        {
            return;
        }

        _selectedHero.SetMaxHp(CalculateHeroMaxHp(_selectedHero), false);
        RefreshHeroHealthBar(_selectedHero);
        _raidSystem.RefreshHeroCombatStats(_selectedHero);
        ShowHeroInfo(_selectedHero);
    }

    private static bool CanEditHeroPanel(HeroRuntimeData heroData)
    {
        return heroData != null &&
            (heroData.State == HeroActivityState.OnBase || heroData.State == HeroActivityState.Resting);
    }

    private void UpdateRestingHeroes(float deltaTime)
    {
        RestConfig restConfig = _config.RestConfig;

        if (restConfig == null)
        {
            return;
        }

        float tickSeconds = restConfig.HealTickSeconds;

        for (int i = 0; i < _heroes.Count; i++)
        {
            HeroRuntimeData heroData = _heroes[i];

            if (heroData.State != HeroActivityState.Resting || heroData.IsFullyHealed)
            {
                _restHealTimers.Remove(heroData);
                continue;
            }

            float healAmount = restConfig.GetHealAmount(heroData.MaxHp);

            if (healAmount <= 0f)
            {
                continue;
            }

            _restHealTimers.TryGetValue(heroData, out float timer);
            timer += deltaTime;
            bool healed = false;

            while (timer >= tickSeconds && !heroData.IsFullyHealed)
            {
                timer -= tickSeconds;
                heroData.Heal(healAmount);
                RefreshHeroHealthBar(heroData);
                healed = true;
            }

            if (heroData.IsFullyHealed)
            {
                timer = 0f;
            }

            _restHealTimers[heroData] = timer;

            if (healed && _selectedHero == heroData)
            {
                ShowHeroInfo(heroData);
            }
        }
    }

    private float CalculateBaseHeroMaxHp(PrimaryStats stats)
    {
        return Mathf.Max(1f, _config.StatsConfig.CalculateSecondaryStats(stats).MaxHp);
    }

    private float CalculateHeroMaxHp(HeroRuntimeData heroData)
    {
        return heroData != null
            ? Mathf.Max(1f, heroData.GetEffectiveSecondaryStats(_config.StatsConfig).MaxHp)
            : 1f;
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

    private sealed class HeroMapVisual
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly BoxCollider2D _collider;
        private readonly SpriteRenderer _selectionOutline;
        private readonly TextMeshPro _label;
        private readonly HeroMapHealthBar _healthBar;

        public HeroMapVisual(
            SpriteRenderer spriteRenderer,
            BoxCollider2D collider,
            SpriteRenderer selectionOutline,
            TextMeshPro label,
            HeroMapHealthBar healthBar)
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
                _label.rectTransform.sizeDelta = GetHeroLabelRectSize(visualSize);
            }

            _healthBar?.SetSize(GetHeroHealthBarSize(visualSize), GetHeroHealthBarPosition(visualSize));
        }
    }

    private sealed class HeroMapHealthBar
    {
        private readonly GameObject _rootObject;
        private readonly SpriteRenderer _background;
        private readonly SpriteRenderer _fill;
        private Vector2 _size;

        public HeroMapHealthBar(GameObject rootObject, SpriteRenderer background, SpriteRenderer fill, Vector2 size)
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

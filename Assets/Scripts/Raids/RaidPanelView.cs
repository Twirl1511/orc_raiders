using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RaidPanelView : MonoBehaviour
{
    private static readonly Vector2 _fallbackPanelSize = new Vector2(440f, 390f);
    private const float _headerLeft = 16f;
    private const float _headerTitleTop = -12f;
    private const float _headerTextTop = -14f;
    private const float _headerGap = 8f;
    private const float _headerTitleWidth = 110f;
    private const float _headerTimerWidth = 78f;
    private const float _headerMinimumStatusWidth = 80f;

    private readonly List<RaidHeroRowView> _heroRows = new List<RaidHeroRowView>();
    private readonly List<RaidEnemyRowView> _enemyRows = new List<RaidEnemyRowView>();

    private RectTransform _root;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _timerText;
    private TextMeshProUGUI _heroNameText;
    private Image _heroHpFill;
    private Image _heroAttackFill;
    private RectTransform _heroHpBarRoot;
    private RectTransform _heroAttackBarRoot;
    private RectTransform _heroRowsRoot;
    private RectTransform _enemyRowsRoot;
    private TextMeshProUGUI _enemyAreaMessage;
    private TextMeshProUGUI _logText;
    private TextMeshProUGUI _raidProgressText;
    private Image _raidProgressFill;
    private RectTransform _raidProgressBarRoot;
    private Button _closeButton;
    private RectTransform _effectRoot;
    private Image _effectParticle;
    private UiWindowMinimizeController _minimizeController;
    private bool _built;

    public RectTransform Root => _root;
    public event Action CloseRequested;
    public event Action<int> HeroClicked;

    private void Awake()
    {
        BuildIfNeeded();
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }
    }

    public void InitializeRuntime()
    {
        BuildIfNeeded();
        ClearHeroes();
        ClearEnemies();
        _closeButton.gameObject.SetActive(false);
        _effectParticle.gameObject.SetActive(false);
        RefreshMinimizeState();
    }

    public void RefreshMinimizeState()
    {
        ApplyHeaderLayout();
        _minimizeController?.CaptureContentAndRefreshState();
    }

    public bool ContainsScreenPoint(Vector2 screenPosition, Camera uiCamera)
    {
        BuildIfNeeded();
        return RectTransformUtility.RectangleContainsScreenPoint(_root, screenPosition, uiCamera);
    }

    public void SetAnchoredPosition(Vector2 anchoredPosition)
    {
        BuildIfNeeded();
        _root.anchoredPosition = anchoredPosition;
    }

    public void ShowWaiting(int raidNumber, float remainingSeconds, int assignedHeroes, int maxHeroSlots)
    {
        BuildIfNeeded();
        PrepareMinimizedContentRefresh();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = "Исчезнет через";
        _timerText.text = FormatTimer(remainingSeconds);
        _heroNameText.text = $"Герои: {assignedHeroes}/{maxHeroSlots}";
        SetHeroSetupControlsVisible(true);
        SetBarFill(_heroHpFill, 0f);
        SetBarFill(_heroAttackFill, 0f);
        SetRaidProgress(0, 0, 0f);
        ClearHeroes();
        ClearEnemies();
        HideEnemyAreaMessage();
        _logText.text = "Перетащи героя в рейд";
        _closeButton.gameObject.SetActive(false);
        RefreshMinimizeState();
    }

    public void ShowRecruiting(
        int raidNumber,
        float remainingSeconds,
        int assignedHeroes,
        int maxHeroSlots,
        IReadOnlyList<RaidHeroViewData> heroes)
    {
        BuildIfNeeded();
        PrepareMinimizedContentRefresh();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = "Добор отряда";
        _timerText.text = FormatTimer(remainingSeconds);
        SetHeroSetupControlsVisible(false);
        SetHeroes(heroes);
        SetRaidProgress(0, 0, 0f);
        ClearEnemies();
        HideEnemyAreaMessage();
        _logText.text = $"Герои: {assignedHeroes}/{maxHeroSlots}\nМожно добавить героя";
        _closeButton.gameObject.SetActive(false);
        RefreshMinimizeState();
    }

    public void ShowBattle(
        int raidNumber,
        int battleNumber,
        int battleCount,
        IReadOnlyList<RaidHeroViewData> heroes,
        int killedEnemies,
        int totalEnemies,
        float raidProgress,
        int goldFound,
        int experienceGained,
        IReadOnlyList<RaidEnemyViewData> enemies)
    {
        BuildIfNeeded();
        PrepareMinimizedContentRefresh();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = $"Бой {battleNumber}/{battleCount}";
        _timerText.text = "";
        SetHeroSetupControlsVisible(false);
        SetHeroes(heroes);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        SetEnemies(enemies);
        HideEnemyAreaMessage();
        SetRaidStatsLog(goldFound, experienceGained);
        _closeButton.gameObject.SetActive(false);
        RefreshMinimizeState();
    }

    public void ShowBattleTransition(
        int raidNumber,
        int nextBattleNumber,
        int battleCount,
        IReadOnlyList<RaidHeroViewData> heroes,
        float raidProgress,
        int killedEnemies,
        int totalEnemies,
        bool completeAfterLoot,
        int goldFound,
        int experienceGained)
    {
        BuildIfNeeded();
        PrepareMinimizedContentRefresh();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = completeAfterLoot ? "Собирает лут" : $"К группе {nextBattleNumber}/{battleCount}";
        _timerText.text = "";
        SetHeroSetupControlsVisible(false);
        SetHeroes(heroes);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        ClearEnemies();
        ShowEnemyAreaMessage("Собирает лут");
        SetRaidStatsLog(goldFound, experienceGained);
        _closeButton.gameObject.SetActive(false);
        RefreshMinimizeState();
    }

    public void ShowCompleted(
        int raidNumber,
        bool success,
        string message,
        int killedEnemies,
        int totalEnemies,
        float raidProgress,
        int goldFound,
        int experienceGained,
        IReadOnlyList<RaidHeroViewData> heroes)
    {
        BuildIfNeeded();
        PrepareMinimizedContentRefresh();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = success ? "Завершен" : "Провален";
        _timerText.text = "";
        SetHeroSetupControlsVisible(false);
        SetHeroes(heroes);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        ClearEnemies();
        HideEnemyAreaMessage();
        _logText.text = $"{message}\nУбито врагов: {killedEnemies}/{totalEnemies}\nЗолото найдено: {goldFound}\nОпыт получен: {experienceGained}";
        _closeButton.gameObject.SetActive(true);
        RefreshMinimizeState();
    }

    public IEnumerator PlayHeroAttackEffect(int heroIndex, int enemyIndex)
    {
        BuildIfNeeded();

        Vector2 start = new Vector2(196f, -82f - heroIndex * 52f);
        Vector2 end = new Vector2(225f, -82f - enemyIndex * 52f);
        _effectParticle.gameObject.SetActive(true);
        _effectParticle.rectTransform.anchoredPosition = start;

        const float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _effectParticle.rectTransform.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        _effectParticle.gameObject.SetActive(false);
    }

    public IEnumerator ShakeHeroHpBar(int heroIndex)
    {
        if (heroIndex < 0 || heroIndex >= _heroRows.Count)
        {
            yield break;
        }

        yield return _heroRows[heroIndex].ShakeHpBar();
    }

    public IEnumerator ShakeEnemyHpBar(int enemyIndex)
    {
        if (enemyIndex < 0 || enemyIndex >= _enemyRows.Count)
        {
            yield break;
        }

        yield return _enemyRows[enemyIndex].ShakeHpBar();
    }

    public void SetSelectedHero(HeroRuntimeData selectedHero)
    {
        bool canShowSelection = selectedHero != null && selectedHero.State == HeroActivityState.InRaid;

        for (int i = 0; i < _heroRows.Count; i++)
        {
            if (_heroRows[i].gameObject.activeSelf)
            {
                _heroRows[i].SetSelected(canShowSelection && _heroRows[i].Hero == selectedHero);
            }
        }
    }

    private void BuildIfNeeded()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        _root = (RectTransform)transform;

        if (_root.sizeDelta.x <= 0f || _root.sizeDelta.y <= 0f)
        {
            _root.sizeDelta = _fallbackPanelSize;
        }

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0.08f, 0.09f, 0.1f, 0.9f);
            background.raycastTarget = true;
        }

        _titleText = CreateText("Title", "Рейд", new Vector2(16f, -12f), new Vector2(110f, 34f), 24f, TextAlignmentOptions.Left);
        _statusText = CreateText("Status", "Ожидает героя", new Vector2(142f, -14f), new Vector2(160f, 30f), 16f, TextAlignmentOptions.Center);
        _timerText = CreateText("Timer", "00:00", new Vector2(320f, -14f), new Vector2(100f, 30f), 16f, TextAlignmentOptions.Right);
        _heroNameText = CreateText("Hero HP Label", "Назначь героя", new Vector2(20f, -58f), new Vector2(175f, 30f), 15f, TextAlignmentOptions.Left);
        _heroHpBarRoot = CreateBar("Hero HP", new Vector2(20f, -86f), new Vector2(175f, 24f), new Color(0.17f, 0.2f, 0.22f, 1f), new Color(0.45f, 0.9f, 0.45f, 1f), out _heroHpFill);
        _heroAttackBarRoot = CreateBar("Hero Attack", new Vector2(20f, -116f), new Vector2(175f, 8f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f), out _heroAttackFill);

        _heroRowsRoot = CreateRect("Heroes", new Vector2(20f, -58f), new Vector2(175f, 150f));
        _enemyRowsRoot = CreateRect("Enemies", new Vector2(212f, -58f), new Vector2(208f, 150f));
        _enemyAreaMessage = CreateText(_enemyRowsRoot, "Enemy Area Message", "Собирает лут", new Vector2(0f, -42f), new Vector2(208f, 64f), 18f, TextAlignmentOptions.Center);
        _enemyAreaMessage.gameObject.SetActive(false);
        _logText = CreateText("Log", "Ожидание", new Vector2(20f, -216f), new Vector2(400f, 70f), 14f, TextAlignmentOptions.Left);
        _raidProgressText = CreateText("Raid Progress Label", "Прогресс рейда", new Vector2(20f, -292f), new Vector2(400f, 20f), 14f, TextAlignmentOptions.Left);
        _raidProgressBarRoot = CreateBar("Raid Progress", new Vector2(20f, -316f), new Vector2(400f, 18f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f), out _raidProgressFill);
        _closeButton = CreateButton("Close Button", "Закрыть", new Vector2(320f, -350f), new Vector2(100f, 30f));
        _closeButton.onClick.AddListener(HandleCloseClicked);
        _closeButton.gameObject.SetActive(false);
        _effectRoot = CreateRect("Effects", Vector2.zero, _root.sizeDelta);
        _effectParticle = CreateImage("Hero Attack Particle", _effectRoot, new Vector2(0f, 0f), new Vector2(14f, 14f), new Color(1f, 0.86f, 0.2f, 1f));
        _effectParticle.gameObject.SetActive(false);
        SetHeroSetupControlsVisible(true);
        ClearHeroes();
        ConfigureMinimizeController();
        ApplyHeaderLayout();
    }

    private void PrepareMinimizedContentRefresh()
    {
        _minimizeController?.PrepareContentRefresh();
    }

    private void ConfigureMinimizeController()
    {
        _minimizeController = GetComponent<UiWindowMinimizeController>();

        if (_minimizeController == null)
        {
            return;
        }

        _minimizeController.SetContentRoots(
            _statusText.gameObject,
            _timerText.gameObject,
            _heroNameText.gameObject,
            _heroHpBarRoot.gameObject,
            _heroAttackBarRoot.gameObject,
            _heroRowsRoot.gameObject,
            _enemyRowsRoot.gameObject,
            _logText.gameObject,
            _raidProgressText.gameObject,
            _raidProgressBarRoot.gameObject,
            _closeButton.gameObject,
            _effectRoot.gameObject);
    }

    private void ApplyHeaderLayout()
    {
        if (_root == null || _titleText == null || _statusText == null || _timerText == null)
        {
            return;
        }

        float panelWidth = Mathf.Max(_fallbackPanelSize.x, Mathf.Max(_root.rect.width, _root.sizeDelta.x));
        float reservedRight = _minimizeController != null ? _minimizeController.GetReservedRightPadding() : 0f;
        float rightLimit = Mathf.Max(
            _headerLeft + _headerTitleWidth + _headerGap + _headerMinimumStatusWidth + _headerGap + _headerTimerWidth,
            panelWidth - reservedRight);
        float timerX = rightLimit - _headerTimerWidth;
        float statusX = _headerLeft + _headerTitleWidth + _headerGap;
        float statusWidth = Mathf.Max(_headerMinimumStatusWidth, timerX - statusX - _headerGap);

        SetRect(_titleText.rectTransform, _headerLeft, _headerTitleTop, _headerTitleWidth, 34f);
        SetRect(_statusText.rectTransform, statusX, _headerTextTop, statusWidth, 30f);
        SetRect(_timerText.rectTransform, timerX, _headerTextTop, _headerTimerWidth, 30f);
    }

    private static void SetRect(RectTransform rectTransform, float x, float y, float width, float height)
    {
        rectTransform.anchoredPosition = new Vector2(x, y);
        rectTransform.sizeDelta = new Vector2(width, height);
    }

    private void SetHeroes(IReadOnlyList<RaidHeroViewData> heroes)
    {
        for (int i = 0; i < heroes.Count; i++)
        {
            RaidHeroRowView row = GetHeroRow(i);
            row.gameObject.SetActive(true);
            row.SetData(heroes[i], i, HandleHeroRowClicked);
        }

        for (int i = heroes.Count; i < _heroRows.Count; i++)
        {
            _heroRows[i].gameObject.SetActive(false);
        }
    }

    private RaidHeroRowView GetHeroRow(int index)
    {
        while (_heroRows.Count <= index)
        {
            RaidHeroRowView row = RaidHeroRowView.Create(_heroRowsRoot, _heroRows.Count);
            _heroRows.Add(row);
        }

        return _heroRows[index];
    }

    private void ClearHeroes()
    {
        for (int i = 0; i < _heroRows.Count; i++)
        {
            _heroRows[i].gameObject.SetActive(false);
        }
    }

    private void SetHeroSetupControlsVisible(bool visible)
    {
        _heroNameText.gameObject.SetActive(visible);
        _heroHpBarRoot.gameObject.SetActive(visible);
        _heroAttackBarRoot.gameObject.SetActive(visible);
    }

    private void SetEnemies(IReadOnlyList<RaidEnemyViewData> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            RaidEnemyRowView row = GetEnemyRow(i);
            row.gameObject.SetActive(true);
            row.SetData(enemies[i]);
        }

        for (int i = enemies.Count; i < _enemyRows.Count; i++)
        {
            _enemyRows[i].gameObject.SetActive(false);
        }
    }

    private RaidEnemyRowView GetEnemyRow(int index)
    {
        while (_enemyRows.Count <= index)
        {
            RaidEnemyRowView row = RaidEnemyRowView.Create(_enemyRowsRoot, _enemyRows.Count);
            _enemyRows.Add(row);
        }

        return _enemyRows[index];
    }

    private void ClearEnemies()
    {
        for (int i = 0; i < _enemyRows.Count; i++)
        {
            _enemyRows[i].gameObject.SetActive(false);
        }
    }

    private void ShowEnemyAreaMessage(string message)
    {
        _enemyAreaMessage.text = message;
        _enemyAreaMessage.gameObject.SetActive(true);
    }

    private void HideEnemyAreaMessage()
    {
        if (_enemyAreaMessage != null)
        {
            _enemyAreaMessage.gameObject.SetActive(false);
        }
    }

    private void SetRaidStatsLog(int goldFound, int experienceGained)
    {
        _logText.text = $"Золото найдено: {goldFound}\nОпыт получен: {experienceGained}";
    }

    private TextMeshProUGUI CreateText(string name, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        return CreateText(_root, name, text, anchoredPosition, size, fontSize, alignment);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)textObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Button CreateButton(string name, string text, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(_root, false);

        RectTransform rectTransform = (RectTransform)buttonObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.92f, 0.92f, 0.9f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 14f;
        label.color = Color.black;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return button;
    }

    private RectTransform CreateBar(string name, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color fillColor, out Image fillImage)
    {
        RectTransform barRoot = CreateRect(name, anchoredPosition, size);
        CreateImage("Background", barRoot, Vector2.zero, size, backgroundColor);
        fillImage = CreateImage("Fill", barRoot, Vector2.zero, size, fillColor);
        SetBarFill(fillImage, 0f);
        return barRoot;
    }

    private RectTransform CreateRect(string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(_root, false);

        RectTransform rectTransform = (RectTransform)rectObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        return rectTransform;
    }

    private Image CreateImage(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)imageObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private IEnumerator ShakeRect(RectTransform rectTransform, float amplitude, float duration)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 90f) * amplitude;
            rectTransform.anchoredPosition = startPosition + new Vector2(offset, 0f);
            yield return null;
        }

        rectTransform.anchoredPosition = startPosition;
    }

    private void SetRaidProgress(int killedEnemies, int totalEnemies, float progressRatio)
    {
        int safeTotal = Mathf.Max(0, totalEnemies);
        int safeKilled = Mathf.Clamp(killedEnemies, 0, safeTotal);
        _raidProgressText.text = safeTotal <= 0
            ? "Прогресс рейда: ждет героя"
            : $"Прогресс рейда: {safeKilled}/{safeTotal}";
        SetBarFill(_raidProgressFill, progressRatio);
    }

    private void HandleCloseClicked()
    {
        CloseRequested?.Invoke();
    }

    private void HandleHeroRowClicked(int heroIndex)
    {
        HeroClicked?.Invoke(heroIndex);
    }

    private static void SetBarFill(Image fillImage, float value)
    {
        RectTransform rectTransform = fillImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static float GetRatio(float current, float max)
    {
        return max <= 0f ? 0f : Mathf.Clamp01(current / max);
    }

    private static string FormatTimer(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return $"{minutes:00}:{remainder:00}";
    }
}

public readonly struct RaidHeroViewData
{
    public RaidHeroViewData(HeroRuntimeData hero, string name, float hp, float maxHp, float attackProgress, bool isSelected)
    {
        Hero = hero;
        Name = name;
        Hp = hp;
        MaxHp = maxHp;
        AttackProgress = attackProgress;
        IsSelected = isSelected;
    }

    public HeroRuntimeData Hero { get; }
    public string Name { get; }
    public float Hp { get; }
    public float MaxHp { get; }
    public float AttackProgress { get; }
    public bool IsSelected { get; }
}

public sealed class RaidHeroRowView : MonoBehaviour, IPointerClickHandler
{
    private const float _rowWidth = 175f;
    private const float _rowHeight = 46f;
    private const float _selectionBorderThickness = 2f;
    private static readonly Color _selectionBorderColor = new Color(1f, 0.84f, 0.12f, 1f);

    private TextMeshProUGUI _nameText;
    private RectTransform _hpBarRoot;
    private Image _hpFill;
    private Image _attackFill;
    private readonly List<Image> _selectionBorders = new List<Image>();
    private HeroRuntimeData _hero;
    private Action<int> _clicked;
    private int _index;

    public HeroRuntimeData Hero => _hero;

    public static RaidHeroRowView Create(RectTransform parent, int index)
    {
        GameObject rowObject = new GameObject($"Hero Row {index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RaidHeroRowView));
        rowObject.transform.SetParent(parent, false);

        RectTransform root = (RectTransform)rowObject.transform;
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(0f, -index * 52f);
        root.sizeDelta = new Vector2(_rowWidth, _rowHeight);

        RaidHeroRowView row = rowObject.GetComponent<RaidHeroRowView>();
        row.Build(root);
        return row;
    }

    public void SetData(RaidHeroViewData data, int index, Action<int> clicked)
    {
        _hero = data.Hero;
        _index = index;
        _clicked = clicked;
        _nameText.text = $"{data.Name}  {Mathf.CeilToInt(data.Hp)}/{Mathf.CeilToInt(data.MaxHp)} HP";
        SetBarFill(_hpFill, GetRatio(data.Hp, data.MaxHp));
        SetBarFill(_attackFill, data.AttackProgress);
        SetSelected(data.IsSelected);
    }

    public void SetSelected(bool selected)
    {
        for (int i = 0; i < _selectionBorders.Count; i++)
        {
            _selectionBorders[i].gameObject.SetActive(selected);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _clicked?.Invoke(_index);
    }

    public IEnumerator ShakeHpBar()
    {
        Vector2 startPosition = _hpBarRoot.anchoredPosition;
        const float duration = 0.16f;
        const float amplitude = 5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 90f) * amplitude;
            _hpBarRoot.anchoredPosition = startPosition + new Vector2(offset, 0f);
            yield return null;
        }

        _hpBarRoot.anchoredPosition = startPosition;
    }

    private void Build(RectTransform root)
    {
        Image clickArea = root.GetComponent<Image>();

        if (clickArea != null)
        {
            clickArea.color = new Color(1f, 1f, 1f, 0f);
            clickArea.raycastTarget = true;
        }

        _nameText = CreateText(root, "Name", new Vector2(0f, 0f), new Vector2(_rowWidth, 20f), 13f);
        _hpFill = CreateBar(root, "HP", new Vector2(0f, -24f), new Vector2(_rowWidth, 14f), new Color(0.17f, 0.2f, 0.22f, 1f), new Color(0.45f, 0.9f, 0.45f, 1f), out _hpBarRoot);
        _attackFill = CreateBar(root, "Attack", new Vector2(0f, -42f), new Vector2(_rowWidth, 6f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f));
        CreateSelectionBorder(root, "Selection Top", Vector2.zero, new Vector2(_rowWidth, _selectionBorderThickness));
        CreateSelectionBorder(root, "Selection Bottom", new Vector2(0f, -_rowHeight + _selectionBorderThickness), new Vector2(_rowWidth, _selectionBorderThickness));
        CreateSelectionBorder(root, "Selection Left", Vector2.zero, new Vector2(_selectionBorderThickness, _rowHeight));
        CreateSelectionBorder(root, "Selection Right", new Vector2(_rowWidth - _selectionBorderThickness, 0f), new Vector2(_selectionBorderThickness, _rowHeight));
        SetSelected(false);
    }

    private void CreateSelectionBorder(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject borderObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderObject.transform.SetParent(parent, false);
        SetupRect(borderObject, anchoredPosition, size);

        Image border = borderObject.GetComponent<Image>();
        border.color = _selectionBorderColor;
        border.raycastTarget = false;
        border.gameObject.SetActive(false);
        _selectionBorders.Add(border);
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)textObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Image CreateBar(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color fillColor)
    {
        return CreateBar(parent, name, anchoredPosition, size, backgroundColor, fillColor, out _);
    }

    private Image CreateBar(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color fillColor, out RectTransform barRoot)
    {
        GameObject backgroundObject = new GameObject($"{name} Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);
        RectTransform backgroundRect = SetupRect(backgroundObject, anchoredPosition, size);
        barRoot = backgroundRect;
        Image background = backgroundObject.GetComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false;

        GameObject fillObject = new GameObject($"{name} Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(backgroundRect, false);
        RectTransform fillRect = SetupRect(fillObject, Vector2.zero, size);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.color = fillColor;
        fill.raycastTarget = false;
        return fill;
    }

    private RectTransform SetupRect(GameObject gameObject, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rectTransform = (RectTransform)gameObject.transform;
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        return rectTransform;
    }

    private static void SetBarFill(Image fillImage, float value)
    {
        RectTransform rectTransform = fillImage.rectTransform;
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static float GetRatio(float current, float max)
    {
        return max <= 0f ? 0f : Mathf.Clamp01(current / max);
    }
}

public readonly struct RaidEnemyViewData
{
    public RaidEnemyViewData(string name, float hp, float maxHp, float attackProgress)
    {
        Name = name;
        Hp = hp;
        MaxHp = maxHp;
        AttackProgress = attackProgress;
    }

    public string Name { get; }
    public float Hp { get; }
    public float MaxHp { get; }
    public float AttackProgress { get; }
}

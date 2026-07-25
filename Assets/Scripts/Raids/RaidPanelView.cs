using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidPanelView : MonoBehaviour
{
    private static readonly Vector2 _fallbackPanelSize = new Vector2(440f, 390f);

    private readonly List<RaidEnemyRowView> _enemyRows = new List<RaidEnemyRowView>();

    private RectTransform _root;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _timerText;
    private TextMeshProUGUI _orcNameText;
    private Image _orcHpFill;
    private Image _orcAttackFill;
    private RectTransform _orcHpBarRoot;
    private RectTransform _enemyRowsRoot;
    private TextMeshProUGUI _enemyAreaMessage;
    private TextMeshProUGUI _logText;
    private TextMeshProUGUI _raidProgressText;
    private Image _raidProgressFill;
    private Button _closeButton;
    private RectTransform _effectRoot;
    private Image _effectParticle;
    private bool _built;

    public RectTransform Root => _root;
    public event Action CloseRequested;

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
        ClearEnemies();
        _closeButton.gameObject.SetActive(false);
        _effectParticle.gameObject.SetActive(false);
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

    public void ShowWaiting(int raidNumber, float remainingSeconds)
    {
        BuildIfNeeded();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = "Исчезнет через";
        _timerText.text = FormatTimer(remainingSeconds);
        _orcNameText.text = "Перетащи орка сюда";
        SetBarFill(_orcHpFill, 0f);
        SetBarFill(_orcAttackFill, 0f);
        SetRaidProgress(0, 0, 0f);
        ClearEnemies();
        HideEnemyAreaMessage();
        _logText.text = "Ожидание";
        _closeButton.gameObject.SetActive(false);
    }

    public void ShowBattle(
        int raidNumber,
        int battleNumber,
        int battleCount,
        string orcName,
        float orcHp,
        float orcMaxHp,
        float orcAttackProgress,
        int killedEnemies,
        int totalEnemies,
        float raidProgress,
        int goldFound,
        int experienceGained,
        IReadOnlyList<RaidEnemyViewData> enemies)
    {
        BuildIfNeeded();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = $"Бой {battleNumber}/{battleCount}";
        _timerText.text = "";
        _orcNameText.text = $"{orcName}  {Mathf.CeilToInt(orcHp)}/{Mathf.CeilToInt(orcMaxHp)} HP";
        SetBarFill(_orcHpFill, GetRatio(orcHp, orcMaxHp));
        SetBarFill(_orcAttackFill, orcAttackProgress);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        SetEnemies(enemies);
        HideEnemyAreaMessage();
        SetRaidStatsLog(goldFound, experienceGained);
        _closeButton.gameObject.SetActive(false);
    }

    public void ShowBattleTransition(
        int raidNumber,
        int nextBattleNumber,
        int battleCount,
        string orcName,
        float orcHp,
        float orcMaxHp,
        float raidProgress,
        int killedEnemies,
        int totalEnemies,
        bool completeAfterLoot,
        int goldFound,
        int experienceGained)
    {
        BuildIfNeeded();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = completeAfterLoot ? "Собирает лут" : $"К группе {nextBattleNumber}/{battleCount}";
        _timerText.text = "";
        _orcNameText.text = $"{orcName}  {Mathf.CeilToInt(orcHp)}/{Mathf.CeilToInt(orcMaxHp)} HP";
        SetBarFill(_orcHpFill, GetRatio(orcHp, orcMaxHp));
        SetBarFill(_orcAttackFill, 0f);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        ClearEnemies();
        ShowEnemyAreaMessage("Собирает лут");
        SetRaidStatsLog(goldFound, experienceGained);
        _closeButton.gameObject.SetActive(false);
    }

    public void ShowCompleted(int raidNumber, bool success, string message, int killedEnemies, int totalEnemies, float raidProgress, int goldFound, int experienceGained)
    {
        BuildIfNeeded();
        _titleText.text = $"Рейд {raidNumber}";
        _statusText.text = success ? "Завершен" : "Провален";
        _timerText.text = "";
        SetBarFill(_orcAttackFill, 0f);
        SetRaidProgress(killedEnemies, totalEnemies, raidProgress);
        ClearEnemies();
        HideEnemyAreaMessage();
        _logText.text = $"{message}\nУбито врагов: {killedEnemies}/{totalEnemies}\nЗолото найдено: {goldFound}\nОпыт получен: {experienceGained}";
        _closeButton.gameObject.SetActive(true);
    }

    public IEnumerator PlayOrcAttackEffect(int enemyIndex)
    {
        BuildIfNeeded();

        Vector2 start = new Vector2(196f, -98f);
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

    public IEnumerator ShakeOrcHpBar()
    {
        yield return ShakeRect(_orcHpBarRoot, 5f, 0.16f);
    }

    public IEnumerator ShakeEnemyHpBar(int enemyIndex)
    {
        if (enemyIndex < 0 || enemyIndex >= _enemyRows.Count)
        {
            yield break;
        }

        yield return _enemyRows[enemyIndex].ShakeHpBar();
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
        _statusText = CreateText("Status", "Ожидает орка", new Vector2(142f, -14f), new Vector2(160f, 30f), 16f, TextAlignmentOptions.Center);
        _timerText = CreateText("Timer", "00:00", new Vector2(320f, -14f), new Vector2(100f, 30f), 16f, TextAlignmentOptions.Right);
        _orcNameText = CreateText("Orc HP Label", "Перетащи орка сюда", new Vector2(20f, -58f), new Vector2(175f, 30f), 15f, TextAlignmentOptions.Left);
        _orcHpBarRoot = CreateBar("Orc HP", new Vector2(20f, -86f), new Vector2(175f, 24f), new Color(0.17f, 0.2f, 0.22f, 1f), new Color(0.45f, 0.9f, 0.45f, 1f), out _orcHpFill);
        CreateBar("Orc Attack", new Vector2(20f, -116f), new Vector2(175f, 8f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f), out _orcAttackFill);

        _enemyRowsRoot = CreateRect("Enemies", new Vector2(212f, -58f), new Vector2(208f, 150f));
        _enemyAreaMessage = CreateText(_enemyRowsRoot, "Enemy Area Message", "Собирает лут", new Vector2(0f, -42f), new Vector2(208f, 64f), 18f, TextAlignmentOptions.Center);
        _enemyAreaMessage.gameObject.SetActive(false);
        _logText = CreateText("Log", "Ожидание", new Vector2(20f, -216f), new Vector2(400f, 70f), 14f, TextAlignmentOptions.Left);
        _raidProgressText = CreateText("Raid Progress Label", "Прогресс рейда", new Vector2(20f, -292f), new Vector2(400f, 20f), 14f, TextAlignmentOptions.Left);
        CreateBar("Raid Progress", new Vector2(20f, -316f), new Vector2(400f, 18f), new Color(0.15f, 0.16f, 0.18f, 1f), new Color(1f, 0.82f, 0.25f, 1f), out _raidProgressFill);
        _closeButton = CreateButton("Close Button", "Закрыть", new Vector2(320f, -350f), new Vector2(100f, 30f));
        _closeButton.onClick.AddListener(HandleCloseClicked);
        _closeButton.gameObject.SetActive(false);
        _effectRoot = CreateRect("Effects", Vector2.zero, _root.sizeDelta);
        _effectParticle = CreateImage("Orc Attack Particle", _effectRoot, new Vector2(0f, 0f), new Vector2(14f, 14f), new Color(1f, 0.86f, 0.2f, 1f));
        _effectParticle.gameObject.SetActive(false);
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
            ? "Прогресс рейда: ждет орка"
            : $"Прогресс рейда: {safeKilled}/{safeTotal}";
        SetBarFill(_raidProgressFill, progressRatio);
    }

    private void HandleCloseClicked()
    {
        CloseRequested?.Invoke();
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

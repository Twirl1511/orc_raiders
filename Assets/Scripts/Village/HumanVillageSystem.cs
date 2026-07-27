using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HumanVillageSystem : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private HumanVillageConfig _config = null;
    [SerializeField] private EnemyConfig _enemyConfig = null;

    [Header("Scene References")]
    [SerializeField] private NecropolisSystem _necropolisSystem = null;
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _scoutDropSlot = null;
    [SerializeField] private TextMeshProUGUI _titleText = null;
    [SerializeField] private TextMeshProUGUI _statusText = null;
    [SerializeField] private TextMeshProUGUI _scoutSlotText = null;
    [SerializeField] private Image _scoutProgressFill = null;
    [SerializeField] private Image[] _enemySlotBackgrounds = new Image[0];
    [SerializeField] private TextMeshProUGUI[] _enemySlotLabels = new TextMeshProUGUI[0];

    private readonly List<EnemyType> _roster = new List<EnemyType>();
    private readonly HashSet<int> _revealedRosterSlots = new HashSet<int>();
    private HeroRuntimeData _scoutingHero;
    private float _scoutTimer;
    private bool _initialized;

    private Camera UiCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
    }

    private void Update()
    {
        if (!_initialized || _scoutingHero == null)
        {
            return;
        }

        _scoutTimer += Time.deltaTime;

        if (_scoutTimer >= _config.ScoutingSeconds)
        {
            CompleteScouting();
            return;
        }

        RefreshScoutSlot();
    }

    public bool TryAcceptDroppedHero(HeroRuntimeData heroData, Vector2 screenPosition)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (heroData == null || _scoutDropSlot == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(_scoutDropSlot, screenPosition, UiCamera))
        {
            return false;
        }

        if (_scoutingHero != null)
        {
            ReturnRejectedHeroToBase(heroData, "Разведка уже идет.");
            return true;
        }

        if (!HasHiddenRosterSlot())
        {
            ReturnRejectedHeroToBase(heroData, "Ростер деревни уже раскрыт.");
            return true;
        }

        StartScouting(heroData);
        return true;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();
        _initialized = true;
        RollVillageRoster();
        RefreshUi();
    }

    private void ValidateReferences()
    {
        if (_config == null || !_config.ValidateForRuntime(_enemyConfig, this))
        {
            throw new System.InvalidOperationException($"{nameof(HumanVillageSystem)} requires valid {nameof(HumanVillageConfig)}.");
        }

        if (_enemyConfig == null || !_enemyConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(HumanVillageSystem)} requires valid {nameof(EnemyConfig)}.");
        }

        if (_necropolisSystem == null || _canvas == null || _scoutDropSlot == null ||
            _titleText == null || _statusText == null || _scoutSlotText == null || _scoutProgressFill == null)
        {
            throw new System.InvalidOperationException($"{nameof(HumanVillageSystem)} requires scene UI references.");
        }

        if (_enemySlotBackgrounds == null || _enemySlotLabels == null ||
            _enemySlotBackgrounds.Length == 0 || _enemySlotLabels.Length == 0 ||
            _enemySlotBackgrounds.Length != _enemySlotLabels.Length)
        {
            throw new System.InvalidOperationException($"{nameof(HumanVillageSystem)} requires matching enemy slot backgrounds and labels.");
        }
    }

    private void RollVillageRoster()
    {
        _roster.Clear();
        _revealedRosterSlots.Clear();

        IReadOnlyList<EnemyType> possibleEnemies = _config.RosterEnemyTypes;
        int rosterSize = Mathf.Min(_config.RosterSize, _enemySlotLabels.Length);

        for (int i = 0; i < rosterSize; i++)
        {
            _roster.Add(possibleEnemies[Random.Range(0, possibleEnemies.Count)]);
        }
    }

    private void StartScouting(HeroRuntimeData heroData)
    {
        _scoutingHero = heroData;
        _scoutTimer = 0f;
        _necropolisSystem.SetHeroState(heroData, HeroActivityState.InRaid);
        _statusText.text = $"{heroData.Name} изучает деревню.";
        RefreshUi();
    }

    private void CompleteScouting()
    {
        HeroRuntimeData heroData = _scoutingHero;
        _scoutingHero = null;
        _scoutTimer = 0f;

        int revealedIndex = RevealRandomHiddenRosterSlot();

        if (heroData != null)
        {
            _necropolisSystem.SetHeroState(heroData, HeroActivityState.OnBase);
        }

        if (revealedIndex >= 0)
        {
            _statusText.text = $"Разведка раскрыла: {GetEnemyDisplayName(_roster[revealedIndex])}.";
        }
        else
        {
            _statusText.text = "Разведка завершена. Новых целей нет.";
        }

        RefreshUi();
    }

    private void ReturnRejectedHeroToBase(HeroRuntimeData heroData, string status)
    {
        _statusText.text = status;
        _necropolisSystem.SetHeroState(heroData, HeroActivityState.OnBase);
        RefreshUi();
    }

    private int RevealRandomHiddenRosterSlot()
    {
        List<int> hiddenSlots = new List<int>();

        for (int i = 0; i < _roster.Count; i++)
        {
            if (!_revealedRosterSlots.Contains(i))
            {
                hiddenSlots.Add(i);
            }
        }

        if (hiddenSlots.Count == 0)
        {
            return -1;
        }

        int index = hiddenSlots[Random.Range(0, hiddenSlots.Count)];
        _revealedRosterSlots.Add(index);
        return index;
    }

    private bool HasHiddenRosterSlot()
    {
        return _revealedRosterSlots.Count < _roster.Count;
    }

    private void RefreshUi()
    {
        _titleText.text = "Деревня людей";

        if (_roster.Count == 0)
        {
            _statusText.text = "Ростер деревни не создан.";
        }
        else if (_scoutingHero == null && string.IsNullOrWhiteSpace(_statusText.text))
        {
            _statusText.text = "Ростер скрыт. Отправь героя в разведку.";
        }

        RefreshEnemySlots();
        RefreshScoutSlot();
    }

    private void RefreshEnemySlots()
    {
        for (int i = 0; i < _enemySlotLabels.Length; i++)
        {
            bool hasRosterSlot = i < _roster.Count;
            bool revealed = hasRosterSlot && _revealedRosterSlots.Contains(i);

            _enemySlotLabels[i].text = revealed ? GetEnemyDisplayName(_roster[i]) : "?";

            if (_enemySlotBackgrounds[i] != null)
            {
                _enemySlotBackgrounds[i].color = revealed
                    ? new Color(0.22f, 0.34f, 0.3f, 1f)
                    : new Color(0.12f, 0.13f, 0.15f, 1f);
            }
        }
    }

    private void RefreshScoutSlot()
    {
        if (_scoutingHero == null)
        {
            _scoutSlotText.text = HasHiddenRosterSlot()
                ? "Разведка\nперетащи героя"
                : "Разведка\nвсе раскрыто";
            SetScoutProgress(0f);
            return;
        }

        float progress = Mathf.Clamp01(_scoutTimer / _config.ScoutingSeconds);
        _scoutSlotText.text = $"Разведка\n{_scoutingHero.Name}";
        SetScoutProgress(progress);
    }

    private void SetScoutProgress(float progress)
    {
        RectTransform fillRect = _scoutProgressFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private string GetEnemyDisplayName(EnemyType enemyType)
    {
        return _enemyConfig.TryGetEnemy(enemyType, out EnemyDefinition enemy)
            ? enemy.DisplayName
            : enemyType.ToString();
    }
}

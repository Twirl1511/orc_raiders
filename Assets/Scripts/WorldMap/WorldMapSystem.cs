using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapSystem : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private WorldMapConfig _config = null;
    [SerializeField] private EnemyConfig _enemyConfig = null;

    [Header("Scene References")]
    [SerializeField] private GuildSystem _guildSystem = null;
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _investigationDropSlot = null;
    [SerializeField] private TextMeshProUGUI _titleText = null;
    [SerializeField] private TextMeshProUGUI _statusText = null;
    [SerializeField] private TextMeshProUGUI _investigationSlotText = null;
    [SerializeField] private Image _investigationProgressFill = null;
    [SerializeField] private InvestigationBlockDividerView _investigationBlockDividerView = null;
    [SerializeField] private Image _attackReadinessFill = null;
    [SerializeField] private Image[] _enemySlotBackgrounds = new Image[0];
    [SerializeField] private TextMeshProUGUI[] _enemySlotLabels = new TextMeshProUGUI[0];

    private readonly List<EnemyType> _roster = new List<EnemyType>();
    private readonly HashSet<int> _revealedRosterSlots = new HashSet<int>();
    private HeroRuntimeData _investigationHero;
    private float _investigationTimer;
    private float _currentInvestigationSeconds;
    private int _completedInvestigationBlocks;
    private int _currentInvestigationBlockCount = 1;
    private float _attackReadinessPercent;
    private bool _attackReadyAnnounced;
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
        if (!_initialized)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateAttackReadiness(deltaTime);

        if (_investigationHero == null)
        {
            return;
        }

        _investigationTimer = Mathf.Min(_investigationTimer + deltaTime, _currentInvestigationSeconds);
        ProcessCompletedInvestigationBlocks();

        if (_investigationTimer >= _currentInvestigationSeconds)
        {
            FinishInvestigation();
            return;
        }

        RefreshInvestigationSlot();
    }

    public bool TryAcceptDroppedHero(HeroRuntimeData heroData, Vector2 screenPosition)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (heroData == null || _investigationDropSlot == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(_investigationDropSlot, screenPosition, UiCamera))
        {
            return false;
        }

        if (_investigationHero != null)
        {
            ReturnRejectedHeroToBase(heroData, "Исследование уже идет.");
            return true;
        }

        if (!HasHiddenRosterSlot())
        {
            ReturnRejectedHeroToBase(heroData, "Ростер карты уже раскрыт.");
            return true;
        }

        StartInvestigation(heroData);
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
        RollMapRoster();
        RefreshUi();
    }

    private void ValidateReferences()
    {
        if (_config == null || !_config.ValidateForRuntime(_enemyConfig, this))
        {
            throw new System.InvalidOperationException($"{nameof(WorldMapSystem)} requires valid {nameof(WorldMapConfig)}.");
        }

        if (_enemyConfig == null || !_enemyConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(WorldMapSystem)} requires valid {nameof(EnemyConfig)}.");
        }

        if (_guildSystem == null || _canvas == null || _investigationDropSlot == null ||
            _titleText == null || _statusText == null || _investigationSlotText == null ||
            _investigationProgressFill == null || _investigationBlockDividerView == null || _attackReadinessFill == null)
        {
            throw new System.InvalidOperationException($"{nameof(WorldMapSystem)} requires scene UI references.");
        }

        if (_enemySlotBackgrounds == null || _enemySlotLabels == null ||
            _enemySlotBackgrounds.Length == 0 || _enemySlotLabels.Length == 0 ||
            _enemySlotBackgrounds.Length != _enemySlotLabels.Length)
        {
            throw new System.InvalidOperationException($"{nameof(WorldMapSystem)} requires matching enemy slot backgrounds and labels.");
        }
    }

    private void RollMapRoster()
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

    private void StartInvestigation(HeroRuntimeData heroData)
    {
        _investigationHero = heroData;
        _investigationTimer = 0f;
        _currentInvestigationSeconds = _config.GetInvestigationSeconds(heroData.Stats);
        _completedInvestigationBlocks = 0;
        _currentInvestigationBlockCount = _config.InvestigationBlockCount;
        _guildSystem.SetHeroState(heroData, HeroActivityState.InQuest);
        float firstBlockChance = _config.GetInvestigationBlockSuccessChancePercent(heroData, 0);
        _statusText.text = $"{heroData.Name} изучает карту. Блок 1/{_currentInvestigationBlockCount}, шанс: {firstBlockChance:0}%.";
        RefreshUi();
    }

    private void ProcessCompletedInvestigationBlocks()
    {
        while (_completedInvestigationBlocks < _currentInvestigationBlockCount)
        {
            float blockEndTime = _currentInvestigationSeconds * (_completedInvestigationBlocks + 1) / _currentInvestigationBlockCount;
            if (_investigationTimer + Mathf.Epsilon < blockEndTime)
            {
                break;
            }

            ResolveInvestigationBlock(_completedInvestigationBlocks);
            _completedInvestigationBlocks++;

            if (!HasHiddenRosterSlot())
            {
                _investigationTimer = _currentInvestigationSeconds;
                break;
            }
        }
    }

    private void ResolveInvestigationBlock(int blockIndex)
    {
        float successChance = _config.GetInvestigationBlockSuccessChancePercent(_investigationHero, blockIndex);
        bool investigationSucceeded = IsInvestigationSuccessful(successChance);
        string blockLabel = $"{blockIndex + 1}/{_currentInvestigationBlockCount}";

        if (investigationSucceeded)
        {
            int revealedIndex = RevealRandomHiddenRosterSlot();

            if (revealedIndex >= 0)
            {
                EnemyType revealedEnemyType = _roster[revealedIndex];
                int experienceReward = AwardInvestigationExperienceForRevealedEnemy(_investigationHero, revealedEnemyType);
                string experienceSuffix = experienceReward > 0 ? $" +{experienceReward} опыта." : string.Empty;
                string allRevealedSuffix = HasHiddenRosterSlot() ? string.Empty : " Ростер полностью раскрыт.";
                _statusText.text = $"Исследование {blockLabel} раскрыла: {GetEnemyDisplayName(revealedEnemyType)}.{experienceSuffix}{allRevealedSuffix}";
                AppendNextBlockChance(blockIndex);
                RefreshEnemySlots();
                return;
            }

            _statusText.text = $"Исследование {blockLabel}: новых целей нет.";
            AppendNextBlockChance(blockIndex);
            return;
        }

        AddAttackReadiness(_config.InvestigationFailureAttackReadinessPercent);
        _statusText.text = $"Исследование {blockLabel} провалена. Готовность карты +{_config.InvestigationFailureAttackReadinessPercent:0}%.";

        if (_attackReadinessPercent >= 100f)
        {
            _attackReadyAnnounced = true;
            _statusText.text += " Карта готова к нападению.";
        }

        AppendNextBlockChance(blockIndex);
    }

    private void AppendNextBlockChance(int completedBlockIndex)
    {
        int nextBlockIndex = completedBlockIndex + 1;
        if (nextBlockIndex >= _currentInvestigationBlockCount || !HasHiddenRosterSlot())
        {
            return;
        }

        float nextChance = _config.GetInvestigationBlockSuccessChancePercent(_investigationHero, nextBlockIndex);
        _statusText.text += $" След. шанс: {nextChance:0}%.";
    }

    private int AwardInvestigationExperienceForRevealedEnemy(HeroRuntimeData heroData, EnemyType enemyType)
    {
        if (heroData == null || !_enemyConfig.TryGetEnemy(enemyType, out EnemyDefinition enemy))
        {
            return 0;
        }

        int experienceReward = enemy.ExperienceReward;
        _guildSystem.AddExperienceToHero(heroData, experienceReward);
        return experienceReward;
    }

    private void FinishInvestigation()
    {
        HeroRuntimeData heroData = _investigationHero;
        _investigationHero = null;
        _investigationTimer = 0f;
        _currentInvestigationSeconds = 0f;
        _completedInvestigationBlocks = 0;
        _currentInvestigationBlockCount = 1;

        if (heroData != null)
        {
            _guildSystem.SetHeroState(heroData, HeroActivityState.OnBase);
        }

        if (string.IsNullOrWhiteSpace(_statusText.text))
        {
            _statusText.text = "Исследование завершена.";
        }

        RefreshUi();
    }

    private void ReturnRejectedHeroToBase(HeroRuntimeData heroData, string status)
    {
        _statusText.text = status;
        _guildSystem.SetHeroState(heroData, HeroActivityState.OnBase);
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
        _titleText.text = "Мировая карта";

        if (_roster.Count == 0)
        {
            _statusText.text = "Ростер карты не создан.";
        }
        else if (_investigationHero == null && string.IsNullOrWhiteSpace(_statusText.text))
        {
            _statusText.text = "Ростер скрыт. Отправь героя в исследование.";
        }

        RefreshEnemySlots();
        RefreshInvestigationSlot();
        RefreshInvestigationBlockDividers();
        RefreshAttackReadiness();
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

    private void RefreshInvestigationSlot()
    {
        if (_investigationHero == null)
        {
            _investigationSlotText.text = HasHiddenRosterSlot()
                ? "Исследование\nперетащи героя"
                : "Исследование\nвсе раскрыто";
            SetBarFill(_investigationProgressFill, 0f);
            return;
        }

        float progress = Mathf.Clamp01(_investigationTimer / Mathf.Max(0.1f, _currentInvestigationSeconds));
        int nextBlock = Mathf.Min(_completedInvestigationBlocks + 1, _currentInvestigationBlockCount);
        _investigationSlotText.text = $"Исследование\n{_investigationHero.Name} ({nextBlock}/{_currentInvestigationBlockCount})";
        SetBarFill(_investigationProgressFill, progress);
    }

    private void RefreshInvestigationBlockDividers()
    {
        int blockCount = _investigationHero == null ? _config.InvestigationBlockCount : _currentInvestigationBlockCount;
        _investigationBlockDividerView.SetBlockCount(blockCount);
    }

    private void UpdateAttackReadiness(float deltaTime)
    {
        if (deltaTime <= 0f || _attackReadinessPercent >= 100f)
        {
            return;
        }

        float previousPercent = _attackReadinessPercent;
        _attackReadinessPercent = Mathf.Clamp(
            _attackReadinessPercent + 100f * deltaTime / _config.SecondsUntilMapThreat,
            0f,
            100f);

        if (!Mathf.Approximately(previousPercent, _attackReadinessPercent))
        {
            RefreshAttackReadiness();
        }

        AnnounceAttackReadyIfNeeded();
    }

    private void AddAttackReadiness(float percent)
    {
        if (percent <= 0f)
        {
            return;
        }

        _attackReadinessPercent = Mathf.Clamp(_attackReadinessPercent + percent, 0f, 100f);
        RefreshAttackReadiness();
    }

    private void RefreshAttackReadiness()
    {
        SetBarFill(_attackReadinessFill, _attackReadinessPercent / 100f);
    }

    private void AnnounceAttackReadyIfNeeded()
    {
        if (_attackReadyAnnounced || _attackReadinessPercent < 100f)
        {
            return;
        }

        _attackReadyAnnounced = true;

        if (_investigationHero == null)
        {
            _statusText.text = "Карта готова к нападению.";
        }
    }

    private bool IsInvestigationSuccessful(float successChance)
    {
        return Random.Range(0f, 100f) < successChance;
    }

    private void SetBarFill(Image fill, float progress)
    {
        if (fill == null)
        {
            return;
        }

        RectTransform fillRect = fill.rectTransform;
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

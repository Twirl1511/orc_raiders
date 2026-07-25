using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RaidSystem : MonoBehaviour
{
    private const float _minimumProgressSegmentSeconds = 0.01f;

    [Header("Configs")]
    [SerializeField] private RaidConfig _raidConfig = null;
    [SerializeField] private EnemyConfig _enemyConfig = null;
    [SerializeField] private StatsConfig _statsConfig = null;

    [Header("Scene References")]
    [SerializeField] private OrcBirthSystem _orcBirthSystem = null;
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _panelsRoot = null;
    [SerializeField] private RaidPanelView _panelTemplate = null;
    [SerializeField] private TextMeshProUGUI _goldText = null;
    [SerializeField] private Button _spawnRaidButton = null;

    [Header("Layout")]
    [SerializeField] private Vector2 _firstPanelAnchoredPosition = new Vector2(20f, -70f);
    [SerializeField] private Vector2 _panelSpacing = new Vector2(20f, 20f);
    [SerializeField, Min(1)] private int _panelsPerRow = 2;

    private readonly List<RaidRuntimeData> _raids = new List<RaidRuntimeData>();
    private readonly List<RaidEnemyViewData> _enemyViewData = new List<RaidEnemyViewData>();

    private float _newRaidTimer;
    private int _nextRaidId = 1;
    private int _gold;
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
        RefreshSpawnRaidButtonState();
        UpdateRaidSpawn(deltaTime);

        for (int i = _raids.Count - 1; i >= 0; i--)
        {
            UpdateRaid(_raids[i], deltaTime);
        }
    }

    private void OnDisable()
    {
        if (_spawnRaidButton != null)
        {
            _spawnRaidButton.onClick.RemoveListener(CreateRaid);
        }
    }

    public bool TryAcceptDroppedOrc(OrcRuntimeData orcData, Vector2 screenPosition)
    {
        if (!_initialized || orcData == null)
        {
            return false;
        }

        for (int i = 0; i < _raids.Count; i++)
        {
            RaidRuntimeData raid = _raids[i];

            if (raid.State == RaidState.Waiting && raid.Panel.ContainsScreenPoint(screenPosition, UiCamera))
            {
                StartRaid(raid, orcData);
                return true;
            }
        }

        return false;
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        ValidateReferences();
        _initialized = true;
        _panelTemplate.gameObject.SetActive(false);
        ConfigureSpawnRaidButton();
        _newRaidTimer = _raidConfig.NewRaidIntervalSeconds;
        RefreshGoldText();

        for (int i = 0; i < _raidConfig.StartingRaidCount; i++)
        {
            CreateRaid();
        }
    }

    private void ValidateReferences()
    {
        if (_raidConfig == null || !_raidConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(RaidSystem)} requires valid {nameof(RaidConfig)}.");
        }

        if (_enemyConfig == null || !_enemyConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(RaidSystem)} requires valid {nameof(EnemyConfig)}.");
        }

        if (_statsConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(RaidSystem)} requires {nameof(StatsConfig)}.");
        }

        if (_orcBirthSystem == null || _canvas == null || _panelsRoot == null || _panelTemplate == null ||
            _goldText == null || _spawnRaidButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(RaidSystem)} requires scene references.");
        }
    }

    private void ConfigureSpawnRaidButton()
    {
        _spawnRaidButton.onClick.RemoveListener(CreateRaid);
        _spawnRaidButton.onClick.AddListener(CreateRaid);

        Image image = _spawnRaidButton.GetComponent<Image>();

        if (image != null)
        {
            image.color = new Color(0.92f, 0.92f, 0.9f, 1f);
            image.raycastTarget = true;
        }

        TextMeshProUGUI label = _spawnRaidButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            label.text = "Добавить новый рейд";
            label.fontSize = 15f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        RefreshSpawnRaidButtonState();
    }

    private void RefreshSpawnRaidButtonState()
    {
        bool shouldShow = _raidConfig.UseManualRaidSpawnButton;

        if (_spawnRaidButton.gameObject.activeSelf != shouldShow)
        {
            _spawnRaidButton.gameObject.SetActive(shouldShow);
        }
    }

    private void UpdateRaidSpawn(float deltaTime)
    {
        if (_raidConfig.UseManualRaidSpawnButton)
        {
            return;
        }

        _newRaidTimer -= deltaTime;

        if (_newRaidTimer > 0f)
        {
            return;
        }

        CreateRaid();
        _newRaidTimer = _raidConfig.NewRaidIntervalSeconds;
    }

    private bool TryExpireWaitingRaid(RaidRuntimeData raid)
    {
        if (raid.WaitingSeconds < _raidConfig.WaitingRaidLifetimeSeconds)
        {
            return false;
        }

        RemoveRaid(raid);
        return true;
    }

    private void RefreshWaitingRaidUi(RaidRuntimeData raid)
    {
        float remainingSeconds = Mathf.Max(0f, _raidConfig.WaitingRaidLifetimeSeconds - raid.WaitingSeconds);
        raid.Panel.ShowWaiting(raid.Id, remainingSeconds);
    }

    private void UpdateRaid(RaidRuntimeData raid, float deltaTime)
    {
        switch (raid.State)
        {
            case RaidState.Waiting:
                raid.WaitingSeconds += deltaTime;
                if (TryExpireWaitingRaid(raid))
                {
                    return;
                }

                RefreshWaitingRaidUi(raid);
                break;
            case RaidState.InProgress:
                UpdateRaidBattle(raid, deltaTime);
                break;
            case RaidState.BattleTransition:
                UpdateBattleTransition(raid, deltaTime);
                break;
            case RaidState.Completed:
                break;
        }
    }

    private void CreateRaid()
    {
        int layoutSlot = GetNextFreeLayoutSlot();
        RaidPanelView panel = Instantiate(_panelTemplate, _panelsRoot, false);
        panel.gameObject.name = $"Raid Panel {_nextRaidId}";
        panel.gameObject.SetActive(true);
        panel.InitializeRuntime();

        RaidRuntimeData raid = new RaidRuntimeData(_nextRaidId, panel, layoutSlot);
        panel.CloseRequested += () => RemoveRaid(raid);

        _nextRaidId++;
        _raids.Add(raid);
        panel.SetAnchoredPosition(GetRaidSlotPosition(layoutSlot));
        RefreshWaitingRaidUi(raid);
    }

    private void StartRaid(RaidRuntimeData raid, OrcRuntimeData orcData)
    {
        raid.State = RaidState.InProgress;
        raid.Orc = orcData;
        RefreshRaidOrcCombatStats(raid);
        raid.OrcAttackProgress = 0f;
        raid.KilledEnemies = 0;
        raid.ExperienceGained = 0;
        raid.TotalGold = Random.Range(_raidConfig.MinGoldReward, _raidConfig.MaxGoldReward + 1);
        raid.GoldFound = 0;
        raid.GoldEarnedFromKills = 0;
        raid.PendingGold = 0;
        raid.LootGoldStart = 0;
        raid.LootGoldTarget = 0;
        raid.CompleteAfterLoot = false;

        GenerateEnemies(raid);
        BuildRaidProgressTimeline(raid);
        BeginProgressSegment(raid, GetBattleProgressSegmentIndex(raid.CurrentBattleNumber));
        _orcBirthSystem.SetOrcState(orcData, OrcActivityState.InRaid);
        RefreshRaidBattleUi(raid);
    }

    private void GenerateEnemies(RaidRuntimeData raid)
    {
        raid.Enemies.Clear();

        int enemyCount = Random.Range(_raidConfig.MinEnemies, _raidConfig.MaxEnemies + 1);
        IReadOnlyList<EnemyDefinition> definitions = _enemyConfig.Enemies;

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyDefinition definition = definitions[Random.Range(0, definitions.Count)];
            SecondaryStatsSnapshot secondaryStats = _statsConfig.CalculateSecondaryStats(definition.Stats);
            float enemyMaxHp = CalculateEnemyMaxHp(definition, secondaryStats);
            raid.Enemies.Add(new EnemyRuntimeData(definition, secondaryStats, enemyMaxHp));
        }

        raid.CurrentBattleStartIndex = 0;
        raid.CurrentBattleNumber = 1;
        raid.BattleCount = Mathf.CeilToInt(raid.Enemies.Count / (float)_raidConfig.MaxEnemiesPerBattle);
    }

    private void BuildRaidProgressTimeline(RaidRuntimeData raid)
    {
        raid.ProgressSegmentDurations.Clear();
        raid.TotalProgressSeconds = 0f;

        for (int battleNumber = 1; battleNumber <= raid.BattleCount; battleNumber++)
        {
            int battleStartIndex = (battleNumber - 1) * _raidConfig.MaxEnemiesPerBattle;
            int battleEndIndex = Mathf.Min(battleStartIndex + _raidConfig.MaxEnemiesPerBattle, raid.Enemies.Count);
            AddProgressSegment(raid, EstimateBattleProgressSegmentSeconds(raid, battleStartIndex, battleEndIndex));

            AddProgressSegment(raid, _raidConfig.BattleTransitionDelaySeconds);
        }
    }

    private void AddProgressSegment(RaidRuntimeData raid, float durationSeconds)
    {
        float safeDuration = Mathf.Max(0f, durationSeconds);
        raid.ProgressSegmentDurations.Add(safeDuration);
        raid.TotalProgressSeconds += safeDuration;
    }

    private float EstimateBattleProgressSegmentSeconds(RaidRuntimeData raid, int startIndex, int endIndex)
    {
        int attacksNeeded = 0;
        float damage = Mathf.Max(1f, raid.OrcDamage);

        for (int i = startIndex; i < endIndex; i++)
        {
            attacksNeeded += Mathf.Max(1, Mathf.CeilToInt(raid.Enemies[i].MaxHp / damage));
        }

        return Mathf.Max(_minimumProgressSegmentSeconds, attacksNeeded * Mathf.Max(0.01f, raid.OrcAttackInterval));
    }

    private void BeginProgressSegment(RaidRuntimeData raid, int segmentIndex)
    {
        raid.ProgressSegmentIndex = Mathf.Clamp(segmentIndex, 0, Mathf.Max(0, raid.ProgressSegmentDurations.Count - 1));
        raid.ProgressSegmentTimer = 0f;
    }

    private void AdvanceRaidProgressSegmentTimer(RaidRuntimeData raid, float deltaTime)
    {
        raid.ProgressSegmentTimer += deltaTime;
    }

    private void CompleteCurrentProgressSegment(RaidRuntimeData raid)
    {
        if (raid.ProgressSegmentIndex < 0 || raid.ProgressSegmentIndex >= raid.ProgressSegmentDurations.Count)
        {
            return;
        }

        raid.ProgressSegmentTimer = raid.ProgressSegmentDurations[raid.ProgressSegmentIndex];
    }

    private void CompleteAllProgressSegments(RaidRuntimeData raid)
    {
        raid.ProgressSegmentIndex = raid.ProgressSegmentDurations.Count;
        raid.ProgressSegmentTimer = 0f;
    }

    private float GetRaidProgressRatio(RaidRuntimeData raid)
    {
        if (raid.TotalProgressSeconds <= 0f)
        {
            return 0f;
        }

        if (raid.ProgressSegmentIndex >= raid.ProgressSegmentDurations.Count)
        {
            return 1f;
        }

        float completedSeconds = 0f;
        int safeSegmentIndex = Mathf.Clamp(raid.ProgressSegmentIndex, 0, raid.ProgressSegmentDurations.Count - 1);

        for (int i = 0; i < safeSegmentIndex; i++)
        {
            completedSeconds += raid.ProgressSegmentDurations[i];
        }

        float currentSegmentSeconds = raid.ProgressSegmentDurations[safeSegmentIndex];
        completedSeconds += currentSegmentSeconds <= 0f
            ? 0f
            : Mathf.Clamp(raid.ProgressSegmentTimer, 0f, currentSegmentSeconds);
        return Mathf.Clamp01(completedSeconds / raid.TotalProgressSeconds);
    }

    private static int GetBattleProgressSegmentIndex(int battleNumber)
    {
        return (Mathf.Max(1, battleNumber) - 1) * 2;
    }

    private static int GetTransitionProgressSegmentIndex(int battleNumber)
    {
        return GetBattleProgressSegmentIndex(battleNumber) + 1;
    }

    private void UpdateRaidBattle(RaidRuntimeData raid, float deltaTime)
    {
        if (raid.OrcHp <= 0f)
        {
            CompleteRaid(raid, false);
            return;
        }

        if (!HasAliveEnemyInCurrentBattle(raid))
        {
            StartBattleTransitionOrCompleteRaid(raid);
            return;
        }

        AdvanceRaidProgressSegmentTimer(raid, deltaTime);
        UpdateOrcAttack(raid, deltaTime);
        UpdateEnemyAttacks(raid, deltaTime);

        if (raid.State != RaidState.InProgress)
        {
            return;
        }

        if (!HasAliveEnemyInCurrentBattle(raid))
        {
            StartBattleTransitionOrCompleteRaid(raid);
            return;
        }

        RefreshRaidBattleUi(raid);
    }

    private void UpdateOrcAttack(RaidRuntimeData raid, float deltaTime)
    {
        raid.OrcAttackProgress += deltaTime / raid.OrcAttackInterval;

        if (raid.OrcAttackProgress < 1f)
        {
            return;
        }

        raid.OrcAttackProgress = 0f;
        EnemyRuntimeData target = GetFirstAliveEnemyInCurrentBattle(raid, out int enemyIndexInBattle);

        if (target == null)
        {
            return;
        }

        float hpBeforeAttack = target.Hp;
        target.Hp = Mathf.Max(0f, target.Hp - raid.OrcDamage);

        if (hpBeforeAttack > 0f && target.Hp <= 0f)
        {
            raid.KilledEnemies++;
            raid.ExperienceGained += target.ExperienceReward;
            _orcBirthSystem.AddExperienceToOrc(raid.Orc, target.ExperienceReward);
            RefreshRaidOrcCombatStats(raid);
            QueueGoldForKill(raid);
        }

        StartCoroutine(raid.Panel.PlayOrcAttackEffect(enemyIndexInBattle));
        StartCoroutine(raid.Panel.ShakeEnemyHpBar(enemyIndexInBattle));
    }

    private void UpdateEnemyAttacks(RaidRuntimeData raid, float deltaTime)
    {
        int endIndex = GetCurrentBattleEndIndex(raid);

        for (int i = raid.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = raid.Enemies[i];

            if (enemy.Hp <= 0f)
            {
                continue;
            }

            enemy.AttackProgress += deltaTime / enemy.AttackInterval;

            if (enemy.AttackProgress < 1f)
            {
                continue;
            }

            enemy.AttackProgress = 0f;
            float blockedDamagePercent = _statsConfig.CalculateArmorBlockedDamagePercent(raid.OrcSecondaryStats.Armor);
            float damageMultiplier = Mathf.Clamp01(1f - blockedDamagePercent / 100f);
            raid.OrcHp = Mathf.Max(0f, raid.OrcHp - Mathf.Max(1f, enemy.Damage * damageMultiplier));
            raid.Orc.SetCurrentHp(raid.OrcHp);
            StartCoroutine(raid.Panel.ShakeOrcHpBar());

            if (raid.OrcHp <= 0f)
            {
                CompleteRaid(raid, false);
                return;
            }
        }
    }

    private void StartBattleTransitionOrCompleteRaid(RaidRuntimeData raid)
    {
        CompleteCurrentProgressSegment(raid);
        int nextBattleStartIndex = raid.CurrentBattleStartIndex + _raidConfig.MaxEnemiesPerBattle;
        raid.CompleteAfterLoot = nextBattleStartIndex >= raid.Enemies.Count;

        raid.State = RaidState.BattleTransition;
        raid.BattleTransitionTimer = 0f;
        raid.BattleTransitionSeconds = _raidConfig.BattleTransitionDelaySeconds;
        raid.OrcAttackProgress = 0f;
        BeginProgressSegment(raid, GetTransitionProgressSegmentIndex(raid.CurrentBattleNumber));
        BeginLootGoldCollection(raid);

        if (raid.BattleTransitionSeconds <= 0f)
        {
            CompleteLootGoldCollection(raid);
            CompleteCurrentProgressSegment(raid);
            FinishBattleTransition(raid);
            return;
        }

        RefreshBattleTransitionUi(raid);
    }

    private void UpdateBattleTransition(RaidRuntimeData raid, float deltaTime)
    {
        raid.BattleTransitionTimer += deltaTime;
        raid.ProgressSegmentTimer = raid.BattleTransitionTimer;
        UpdateLootGoldCollection(raid);

        if (raid.BattleTransitionTimer >= raid.BattleTransitionSeconds)
        {
            CompleteLootGoldCollection(raid);
            CompleteCurrentProgressSegment(raid);
            FinishBattleTransition(raid);
            return;
        }

        RefreshBattleTransitionUi(raid);
    }

    private void AdvanceToNextBattle(RaidRuntimeData raid)
    {
        raid.State = RaidState.InProgress;
        raid.CurrentBattleStartIndex += _raidConfig.MaxEnemiesPerBattle;
        raid.CurrentBattleNumber++;
        raid.OrcAttackProgress = 0f;
        raid.BattleTransitionTimer = 0f;
        raid.CompleteAfterLoot = false;
        BeginProgressSegment(raid, GetBattleProgressSegmentIndex(raid.CurrentBattleNumber));
        RefreshRaidBattleUi(raid);
    }

    private void FinishBattleTransition(RaidRuntimeData raid)
    {
        if (raid.CompleteAfterLoot)
        {
            CompleteRaid(raid, true);
            return;
        }

        AdvanceToNextBattle(raid);
    }

    private void CompleteRaid(RaidRuntimeData raid, bool success)
    {
        if (raid.State == RaidState.Completed)
        {
            return;
        }

        raid.State = RaidState.Completed;

        if (success)
        {
            raid.KilledEnemies = raid.Enemies.Count;
            CompleteLootGoldCollection(raid);
            CompleteAllProgressSegments(raid);
            raid.RewardDice = _orcBirthSystem.AddRandomDiceFromConfigToPool();
        }

        _gold += raid.GoldFound;
        RefreshGoldText();

        if (raid.Orc != null)
        {
            raid.Orc.SetCurrentHp(raid.OrcHp);
            _orcBirthSystem.SetOrcState(raid.Orc, success ? OrcActivityState.OnBase : OrcActivityState.Resting);
        }

        string message = success ? GetSuccessMessage(raid) : "Орк проиграл бой и ушел отдыхать.";
        raid.Panel.ShowCompleted(raid.Id, success, message, raid.KilledEnemies, raid.Enemies.Count, GetRaidProgressRatio(raid), raid.GoldFound, raid.ExperienceGained);
    }

    private static string GetSuccessMessage(RaidRuntimeData raid)
    {
        string message = "Орк прошел рейд и вернулся на базу.";

        if (raid.RewardDice != null)
        {
            message += $"\nНайден кубик: {raid.RewardDice.DisplayName}";
        }

        return message;
    }

    public void RefreshOrcCombatStats(OrcRuntimeData orcData)
    {
        if (!_initialized || orcData == null)
        {
            return;
        }

        for (int i = 0; i < _raids.Count; i++)
        {
            RaidRuntimeData raid = _raids[i];

            if (raid.Orc != orcData || raid.State == RaidState.Waiting || raid.State == RaidState.Completed)
            {
                continue;
            }

            RefreshRaidOrcCombatStats(raid);

            if (raid.State == RaidState.BattleTransition)
            {
                RefreshBattleTransitionUi(raid);
            }
            else
            {
                RefreshRaidBattleUi(raid);
            }
        }
    }

    private void RemoveRaid(RaidRuntimeData raid)
    {
        if (!_raids.Remove(raid))
        {
            return;
        }

        if (raid.Panel != null)
        {
            Destroy(raid.Panel.gameObject);
        }
    }

    private int GetNextFreeLayoutSlot()
    {
        for (int slot = 0; ; slot++)
        {
            if (!DoesLayoutSlotOverlapExistingPanels(slot))
            {
                return slot;
            }
        }
    }

    private bool DoesLayoutSlotOverlapExistingPanels(int slot)
    {
        Rect candidateRect = GetPanelRect(GetRaidSlotPosition(slot), GetRaidPanelSize());

        for (int i = 0; i < _raids.Count; i++)
        {
            RectTransform panelRoot = _raids[i].Panel != null ? _raids[i].Panel.Root : null;

            if (panelRoot == null)
            {
                continue;
            }

            Rect panelRect = GetPanelRect(panelRoot.anchoredPosition, panelRoot.sizeDelta);

            if (candidateRect.Overlaps(panelRect))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetRaidSlotPosition(int slot)
    {
        Vector2 panelSize = GetRaidPanelSize();
        int row = slot / _panelsPerRow;
        int column = slot % _panelsPerRow;
        return _firstPanelAnchoredPosition + new Vector2(
            column * (panelSize.x + _panelSpacing.x),
            -row * (panelSize.y + _panelSpacing.y));
    }

    private Vector2 GetRaidPanelSize()
    {
        RectTransform templateRoot = (RectTransform)_panelTemplate.transform;
        Vector2 panelSize = templateRoot.sizeDelta;

        if (panelSize.x <= 0f || panelSize.y <= 0f)
        {
            panelSize = new Vector2(440f, 390f);
        }

        return panelSize;
    }

    private static Rect GetPanelRect(Vector2 topLeftPosition, Vector2 size)
    {
        return new Rect(topLeftPosition.x, topLeftPosition.y - size.y, size.x, size.y);
    }

    private void RefreshRaidBattleUi(RaidRuntimeData raid)
    {
        _enemyViewData.Clear();

        int endIndex = GetCurrentBattleEndIndex(raid);

        for (int i = raid.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = raid.Enemies[i];
            _enemyViewData.Add(new RaidEnemyViewData(enemy.DisplayName, enemy.Hp, enemy.MaxHp, enemy.AttackProgress));
        }

        raid.Panel.ShowBattle(
            raid.Id,
            raid.CurrentBattleNumber,
            raid.BattleCount,
            raid.Orc.Name,
            raid.OrcHp,
            raid.OrcMaxHp,
            raid.OrcAttackProgress,
            raid.KilledEnemies,
            raid.Enemies.Count,
            GetRaidProgressRatio(raid),
            raid.GoldFound,
            raid.ExperienceGained,
            _enemyViewData);
    }

    private void RefreshBattleTransitionUi(RaidRuntimeData raid)
    {
        raid.Panel.ShowBattleTransition(
            raid.Id,
            raid.CurrentBattleNumber + 1,
            raid.BattleCount,
            raid.Orc.Name,
            raid.OrcHp,
            raid.OrcMaxHp,
            GetRaidProgressRatio(raid),
            raid.KilledEnemies,
            raid.Enemies.Count,
            raid.CompleteAfterLoot,
            raid.GoldFound,
            raid.ExperienceGained);
    }

    private void QueueGoldForKill(RaidRuntimeData raid)
    {
        int totalEnemies = Mathf.Max(1, raid.Enemies.Count);
        int targetGoldEarned = Mathf.RoundToInt(raid.TotalGold * Mathf.Clamp01(raid.KilledEnemies / (float)totalEnemies));
        int goldToQueue = Mathf.Max(0, targetGoldEarned - raid.GoldEarnedFromKills);
        raid.GoldEarnedFromKills = Mathf.Max(raid.GoldEarnedFromKills, targetGoldEarned);
        raid.PendingGold += goldToQueue;
    }

    private void BeginLootGoldCollection(RaidRuntimeData raid)
    {
        raid.LootGoldStart = raid.GoldFound;
        raid.LootGoldTarget = raid.GoldFound + Mathf.Max(0, raid.PendingGold);
        raid.PendingGold = 0;
    }

    private void UpdateLootGoldCollection(RaidRuntimeData raid)
    {
        int goldToCollect = raid.LootGoldTarget - raid.LootGoldStart;

        if (goldToCollect <= 0)
        {
            return;
        }

        float progress = raid.BattleTransitionSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(raid.BattleTransitionTimer / raid.BattleTransitionSeconds);
        raid.GoldFound = raid.LootGoldStart + Mathf.FloorToInt(goldToCollect * progress);
    }

    private void CompleteLootGoldCollection(RaidRuntimeData raid)
    {
        raid.GoldFound = Mathf.Max(raid.GoldFound, raid.LootGoldTarget);
    }

    private void RefreshGoldText()
    {
        _goldText.text = $"Золото: {_gold}";
    }

    private void RefreshRaidOrcCombatStats(RaidRuntimeData raid)
    {
        raid.OrcSecondaryStats = _statsConfig.CalculateSecondaryStats(raid.Orc.Stats);
        raid.Orc.SetMaxHp(Mathf.Max(1f, raid.OrcSecondaryStats.MaxHp), false);
        raid.OrcMaxHp = raid.Orc.MaxHp;
        raid.OrcHp = Mathf.Clamp(raid.Orc.CurrentHp, 0f, raid.OrcMaxHp);
        raid.OrcAttackInterval = Mathf.Max(0.01f, raid.OrcSecondaryStats.AttackSpeed);
        raid.OrcDamage = Mathf.Max(1f, raid.OrcSecondaryStats.MeleeDamage);
    }

    private float CalculateEnemyMaxHp(EnemyDefinition definition, SecondaryStatsSnapshot secondaryStats)
    {
        float sharedMinimumHp = _statsConfig.GetSecondaryStatMinimumValue(OrcSecondaryStatType.MaxHp);
        float statBonusHp = secondaryStats.MaxHp - sharedMinimumHp;
        return Mathf.Max(1f, definition.MinimumHp + statBonusHp);
    }

    private bool HasAliveEnemyInCurrentBattle(RaidRuntimeData raid)
    {
        return GetFirstAliveEnemyInCurrentBattle(raid, out _) != null;
    }

    private EnemyRuntimeData GetFirstAliveEnemyInCurrentBattle(RaidRuntimeData raid, out int enemyIndexInBattle)
    {
        int endIndex = GetCurrentBattleEndIndex(raid);

        for (int i = raid.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = raid.Enemies[i];

            if (enemy.Hp > 0f)
            {
                enemyIndexInBattle = i - raid.CurrentBattleStartIndex;
                return enemy;
            }
        }

        enemyIndexInBattle = -1;
        return null;
    }

    private int GetCurrentBattleEndIndex(RaidRuntimeData raid)
    {
        return Mathf.Min(raid.CurrentBattleStartIndex + _raidConfig.MaxEnemiesPerBattle, raid.Enemies.Count);
    }

    private enum RaidState
    {
        Waiting = 0,
        InProgress = 1,
        BattleTransition = 2,
        Completed = 3
    }

    private sealed class RaidRuntimeData
    {
        public readonly int Id;
        public readonly RaidPanelView Panel;
        public readonly int LayoutSlot;
        public readonly List<EnemyRuntimeData> Enemies = new List<EnemyRuntimeData>();
        public readonly List<float> ProgressSegmentDurations = new List<float>();

        public RaidState State;
        public float WaitingSeconds;
        public OrcRuntimeData Orc;
        public SecondaryStatsSnapshot OrcSecondaryStats;
        public float OrcHp;
        public float OrcMaxHp;
        public float OrcDamage;
        public float OrcAttackInterval;
        public float OrcAttackProgress;
        public int CurrentBattleStartIndex;
        public int CurrentBattleNumber;
        public int BattleCount;
        public int KilledEnemies;
        public int ExperienceGained;
        public int TotalGold;
        public int GoldFound;
        public int GoldEarnedFromKills;
        public int PendingGold;
        public int LootGoldStart;
        public int LootGoldTarget;
        public bool CompleteAfterLoot;
        public DiceDefinition RewardDice;
        public float BattleTransitionTimer;
        public float BattleTransitionSeconds;
        public int ProgressSegmentIndex;
        public float ProgressSegmentTimer;
        public float TotalProgressSeconds;

        public RaidRuntimeData(int id, RaidPanelView panel, int layoutSlot)
        {
            Id = id;
            Panel = panel;
            LayoutSlot = layoutSlot;
            State = RaidState.Waiting;
        }
    }

    private sealed class EnemyRuntimeData
    {
        public readonly string DisplayName;
        public readonly float MaxHp;
        public readonly float Damage;
        public readonly float AttackInterval;
        public readonly int ExperienceReward;

        public float Hp;
        public float AttackProgress;

        public EnemyRuntimeData(EnemyDefinition definition, SecondaryStatsSnapshot secondaryStats, float maxHp)
        {
            DisplayName = definition.DisplayName;
            MaxHp = Mathf.Max(1f, maxHp);
            Hp = MaxHp;
            Damage = Mathf.Max(1f, Mathf.Max(secondaryStats.MeleeDamage, secondaryStats.RangedDamage));
            AttackInterval = Mathf.Max(0.01f, definition.AttackIntervalSeconds);
            ExperienceReward = definition.ExperienceReward;
        }
    }
}

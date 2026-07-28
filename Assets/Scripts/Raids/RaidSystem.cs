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
    [SerializeField] private NecropolisSystem _necropolisSystem = null;
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
    private readonly List<RaidHeroViewData> _heroViewData = new List<RaidHeroViewData>();
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

    public bool TryAcceptDroppedHero(HeroRuntimeData heroData, Vector2 screenPosition)
    {
        if (!_initialized || heroData == null)
        {
            return false;
        }

        for (int i = 0; i < _raids.Count; i++)
        {
            RaidRuntimeData raid = _raids[i];

            bool canAcceptHero = raid.State == RaidState.Waiting || raid.State == RaidState.Recruiting;

            if (canAcceptHero && raid.Panel.ContainsScreenPoint(screenPosition, UiCamera))
            {
                return TryAddHeroToRaid(raid, heroData);
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

        if (_necropolisSystem == null || _canvas == null || _panelsRoot == null || _panelTemplate == null ||
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
        raid.Panel.ShowWaiting(raid.Id, remainingSeconds, raid.Heroes.Count, raid.MaxHeroSlots);
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
            case RaidState.Recruiting:
                UpdateRaidRecruiting(raid, deltaTime);
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

        RaidRuntimeData raid = new RaidRuntimeData(_nextRaidId, panel, layoutSlot, GetRandomHeroSlotCount());
        panel.CloseRequested += () => RemoveRaid(raid);
        panel.HeroClicked += heroIndex => HandleRaidHeroClicked(raid, heroIndex);

        _nextRaidId++;
        _raids.Add(raid);
        panel.SetAnchoredPosition(GetRaidSlotPosition(layoutSlot));
        RefreshWaitingRaidUi(raid);
    }

    private int GetRandomHeroSlotCount()
    {
        return Random.Range(_raidConfig.MinHeroSlots, _raidConfig.MaxHeroSlots + 1);
    }

    private bool TryAddHeroToRaid(RaidRuntimeData raid, HeroRuntimeData heroData)
    {
        if (heroData == null || raid.Heroes.Count >= raid.MaxHeroSlots || ContainsHero(raid, heroData))
        {
            return false;
        }

        AddHeroToRaid(raid, heroData);

        if (raid.Heroes.Count >= raid.MaxHeroSlots || _raidConfig.AdditionalHeroWindowSeconds <= 0f)
        {
            StartRaidBattle(raid);
            return true;
        }

        raid.State = RaidState.Recruiting;
        raid.RecruitingSecondsRemaining = _raidConfig.AdditionalHeroWindowSeconds;
        RefreshRecruitingRaidUi(raid);
        return true;
    }

    private void AddHeroToRaid(RaidRuntimeData raid, HeroRuntimeData heroData)
    {
        RaidHeroRuntimeData raidHero = new RaidHeroRuntimeData(heroData);
        raid.Heroes.Add(raidHero);
        RefreshRaidHeroCombatStats(raidHero);
        _necropolisSystem.SetHeroState(heroData, HeroActivityState.InRaid);
    }

    public void RefreshHeroSelectionVisuals()
    {
        if (!_initialized)
        {
            return;
        }

        HeroRuntimeData selectedHero = _necropolisSystem != null ? _necropolisSystem.SelectedHero : null;

        for (int i = 0; i < _raids.Count; i++)
        {
            if (_raids[i].Panel != null)
            {
                _raids[i].Panel.SetSelectedHero(selectedHero);
            }
        }
    }

    private void HandleRaidHeroClicked(RaidRuntimeData raid, int heroIndex)
    {
        if (!_initialized || raid == null || heroIndex < 0 || heroIndex >= raid.Heroes.Count)
        {
            return;
        }

        _necropolisSystem.SelectHeroFromRaid(raid.Heroes[heroIndex].Hero);
        RefreshHeroSelectionVisuals();
    }

    private bool ContainsHero(RaidRuntimeData raid, HeroRuntimeData heroData)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (raid.Heroes[i].Hero == heroData)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateRaidRecruiting(RaidRuntimeData raid, float deltaTime)
    {
        raid.RecruitingSecondsRemaining -= deltaTime;

        if (raid.RecruitingSecondsRemaining <= 0f)
        {
            StartRaidBattle(raid);
            return;
        }

        RefreshRecruitingRaidUi(raid);
    }

    private void RefreshRecruitingRaidUi(RaidRuntimeData raid)
    {
        BuildHeroViewData(raid, false);
        raid.Panel.ShowRecruiting(
            raid.Id,
            raid.RecruitingSecondsRemaining,
            raid.Heroes.Count,
            raid.MaxHeroSlots,
            _heroViewData);
    }

    private void StartRaidBattle(RaidRuntimeData raid)
    {
        if (raid.Heroes.Count == 0)
        {
            return;
        }

        raid.State = RaidState.InProgress;
        RefreshRaidHeroCombatStats(raid);
        ResetHeroAttackProgress(raid);
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

        GenerateBattleGroups(raid);
    }

    private void GenerateBattleGroups(RaidRuntimeData raid)
    {
        raid.BattleStartIndices.Clear();
        raid.BattleEnemyCounts.Clear();

        int startIndex = 0;

        while (startIndex < raid.Enemies.Count)
        {
            int enemyCount = GetRandomBattleEnemyCount(raid.Enemies.Count - startIndex);
            raid.BattleStartIndices.Add(startIndex);
            raid.BattleEnemyCounts.Add(enemyCount);
            startIndex += enemyCount;
        }

        raid.CurrentBattleNumber = 1;
        raid.BattleCount = raid.BattleEnemyCounts.Count;
        raid.CurrentBattleStartIndex = GetBattleStartIndex(raid, raid.CurrentBattleNumber);
    }

    private int GetRandomBattleEnemyCount(int remainingEnemies)
    {
        int minEnemiesPerBattle = Mathf.Max(1, _raidConfig.MinEnemiesPerBattle);
        int maxEnemiesPerBattle = Mathf.Max(minEnemiesPerBattle, _raidConfig.MaxEnemiesPerBattle);
        int minCount = Mathf.Min(minEnemiesPerBattle, remainingEnemies);
        int maxCount = Mathf.Min(maxEnemiesPerBattle, remainingEnemies);
        int validCountOptions = 0;

        for (int count = minCount; count <= maxCount; count++)
        {
            int enemiesAfterBattle = remainingEnemies - count;

            if (enemiesAfterBattle == 0 || enemiesAfterBattle >= minEnemiesPerBattle)
            {
                validCountOptions++;
            }
        }

        if (validCountOptions <= 0)
        {
            return Random.Range(minCount, maxCount + 1);
        }

        int selectedOption = Random.Range(0, validCountOptions);

        for (int count = minCount; count <= maxCount; count++)
        {
            int enemiesAfterBattle = remainingEnemies - count;

            if (enemiesAfterBattle != 0 && enemiesAfterBattle < minEnemiesPerBattle)
            {
                continue;
            }

            if (selectedOption == 0)
            {
                return count;
            }

            selectedOption--;
        }

        return maxCount;
    }

    private void BuildRaidProgressTimeline(RaidRuntimeData raid)
    {
        raid.ProgressSegmentDurations.Clear();
        raid.TotalProgressSeconds = 0f;

        for (int battleNumber = 1; battleNumber <= raid.BattleCount; battleNumber++)
        {
            int battleStartIndex = GetBattleStartIndex(raid, battleNumber);
            int battleEndIndex = GetBattleEndIndex(raid, battleNumber);
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
        float totalEnemyHp = 0f;

        for (int i = startIndex; i < endIndex; i++)
        {
            totalEnemyHp += raid.Enemies[i].MaxHp;
        }

        return Mathf.Max(_minimumProgressSegmentSeconds, totalEnemyHp / GetRaidDamagePerSecond(raid));
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
        if (!HasAliveHero(raid))
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
        UpdateHeroAttack(raid, deltaTime);
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

    private void UpdateHeroAttack(RaidRuntimeData raid, float deltaTime)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            RaidHeroRuntimeData raidHero = raid.Heroes[i];

            if (raidHero.Hp <= 0f)
            {
                raidHero.AttackProgress = 0f;
                raidHero.TargetEnemyIndex = -1;
                continue;
            }

            raidHero.AttackProgress += deltaTime / raidHero.AttackInterval;

            if (raidHero.AttackProgress < 1f)
            {
                continue;
            }

            raidHero.AttackProgress = 0f;
            EnemyRuntimeData target = GetHeroTargetEnemyInCurrentBattle(raid, raidHero, out int enemyIndexInBattle);

            if (target == null)
            {
                return;
            }

            float hpBeforeAttack = target.Hp;
            target.Hp = Mathf.Max(0f, target.Hp - raidHero.Damage);

            if (hpBeforeAttack > 0f && target.Hp <= 0f)
            {
                raid.KilledEnemies++;
                AwardExperienceToRaidHeroes(raid, target.ExperienceReward);
                QueueGoldForKill(raid);
                raidHero.TargetEnemyIndex = -1;
            }

            StartCoroutine(raid.Panel.PlayHeroAttackEffect(i, enemyIndexInBattle));
            StartCoroutine(raid.Panel.ShakeEnemyHpBar(enemyIndexInBattle));
        }
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

            RaidHeroRuntimeData targetHero = GetRandomAliveHero(raid, out int heroIndex);

            if (targetHero == null)
            {
                CompleteRaid(raid, false);
                return;
            }

            enemy.AttackProgress = 0f;
            float blockedDamagePercent = _statsConfig.CalculateArmorBlockedDamagePercent(targetHero.SecondaryStats.Armor);
            float damageMultiplier = Mathf.Clamp01(1f - blockedDamagePercent / 100f);
            targetHero.Hp = Mathf.Max(0f, targetHero.Hp - Mathf.Max(1f, enemy.Damage * damageMultiplier));
            targetHero.Hero.SetCurrentHp(targetHero.Hp);
            StartCoroutine(raid.Panel.ShakeHeroHpBar(heroIndex));

            if (!HasAliveHero(raid))
            {
                CompleteRaid(raid, false);
                return;
            }
        }
    }

    private void StartBattleTransitionOrCompleteRaid(RaidRuntimeData raid)
    {
        CompleteCurrentProgressSegment(raid);
        raid.CompleteAfterLoot = raid.CurrentBattleNumber >= raid.BattleCount;

        raid.State = RaidState.BattleTransition;
        raid.BattleTransitionTimer = 0f;
        raid.BattleTransitionSeconds = _raidConfig.BattleTransitionDelaySeconds;
        ResetHeroAttackProgress(raid);
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
        raid.CurrentBattleNumber++;
        raid.CurrentBattleStartIndex = GetBattleStartIndex(raid, raid.CurrentBattleNumber);
        ResetHeroAttackProgress(raid);
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
            raid.RewardDice = _necropolisSystem.AddRandomDiceFromConfigToPool();
        }

        _gold += raid.GoldFound;
        RefreshGoldText();

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            RaidHeroRuntimeData raidHero = raid.Heroes[i];
            raidHero.Hero.SetCurrentHp(raidHero.Hp);
            HeroActivityState heroState = success && raidHero.Hp > 0f ? HeroActivityState.OnBase : HeroActivityState.Resting;
            _necropolisSystem.SetHeroState(raidHero.Hero, heroState);
        }

        string message = success ? GetSuccessMessage(raid) : "Отряд проиграл бой. Раненые герои ушли отдыхать.";
        BuildHeroViewData(raid, false);
        raid.Panel.ShowCompleted(
            raid.Id,
            success,
            message,
            raid.KilledEnemies,
            raid.Enemies.Count,
            GetRaidProgressRatio(raid),
            raid.GoldFound,
            raid.ExperienceGained,
            _heroViewData);
    }

    private static string GetSuccessMessage(RaidRuntimeData raid)
    {
        string message = "Отряд прошел рейд и вернулся на базу.";

        if (raid.RewardDice != null)
        {
            message += $"\nНайдена кость: {raid.RewardDice.DisplayName}";
        }

        return message;
    }

    public void RefreshHeroCombatStats(HeroRuntimeData heroData)
    {
        if (!_initialized || heroData == null)
        {
            return;
        }

        for (int i = 0; i < _raids.Count; i++)
        {
            RaidRuntimeData raid = _raids[i];
            RaidHeroRuntimeData raidHero = GetRaidHero(raid, heroData);

            if (raidHero == null || raid.State == RaidState.Waiting || raid.State == RaidState.Completed)
            {
                continue;
            }

            RefreshRaidHeroCombatStats(raidHero);

            if (raid.State == RaidState.Recruiting)
            {
                RefreshRecruitingRaidUi(raid);
                continue;
            }

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
        BuildHeroViewData(raid, true);
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
            _heroViewData,
            raid.KilledEnemies,
            raid.Enemies.Count,
            GetRaidProgressRatio(raid),
            raid.GoldFound,
            raid.ExperienceGained,
            _enemyViewData);
    }

    private void BuildHeroViewData(RaidRuntimeData raid, bool showAttackProgress)
    {
        _heroViewData.Clear();

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            RaidHeroRuntimeData hero = raid.Heroes[i];
            float attackProgress = showAttackProgress && hero.Hp > 0f ? hero.AttackProgress : 0f;
            bool isSelected = _necropolisSystem != null &&
                _necropolisSystem.SelectedHero == hero.Hero &&
                hero.Hero.State == HeroActivityState.InRaid;
            _heroViewData.Add(new RaidHeroViewData(hero.Hero, hero.Hero.Name, hero.Hp, hero.MaxHp, attackProgress, isSelected));
        }
    }

    private void RefreshBattleTransitionUi(RaidRuntimeData raid)
    {
        BuildHeroViewData(raid, false);
        raid.Panel.ShowBattleTransition(
            raid.Id,
            raid.CurrentBattleNumber + 1,
            raid.BattleCount,
            _heroViewData,
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

    private void AwardExperienceToRaidHeroes(RaidRuntimeData raid, int totalExperience)
    {
        if (totalExperience <= 0 || raid.Heroes.Count == 0)
        {
            return;
        }

        raid.ExperienceGained += totalExperience;

        int[] experienceShares = new int[raid.Heroes.Count];
        int baseExperience = totalExperience / raid.Heroes.Count;
        int remainder = totalExperience % raid.Heroes.Count;

        for (int i = 0; i < experienceShares.Length; i++)
        {
            experienceShares[i] = baseExperience;
        }

        while (remainder > 0)
        {
            int weakestHeroIndex = GetRandomWeakestHeroIndex(raid, experienceShares);
            experienceShares[weakestHeroIndex]++;
            remainder--;
        }

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (experienceShares[i] > 0)
            {
                _necropolisSystem.AddExperienceToHero(raid.Heroes[i].Hero, experienceShares[i]);
            }
        }

        RefreshRaidHeroCombatStats(raid);
    }

    private void RefreshGoldText()
    {
        _goldText.text = $"Золото: {_gold}";
    }

    private void RefreshRaidHeroCombatStats(RaidRuntimeData raid)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            RefreshRaidHeroCombatStats(raid.Heroes[i]);
        }
    }

    private void RefreshRaidHeroCombatStats(RaidHeroRuntimeData raidHero)
    {
        raidHero.SecondaryStats = raidHero.Hero.GetEffectiveSecondaryStats(_statsConfig);
        raidHero.Hero.SetMaxHp(Mathf.Max(1f, raidHero.SecondaryStats.MaxHp), false);
        raidHero.MaxHp = raidHero.Hero.MaxHp;

        float currentHp = raidHero.Hp > 0f ? raidHero.Hp : raidHero.Hero.CurrentHp;
        raidHero.Hp = Mathf.Clamp(currentHp, 0f, raidHero.MaxHp);
        raidHero.AttackInterval = Mathf.Max(0.01f, raidHero.SecondaryStats.AttackInterval);
        raidHero.Damage = Mathf.Max(1f, raidHero.SecondaryStats.MeleeDamage);
        raidHero.Hero.SetCurrentHp(raidHero.Hp);
    }

    private static void ResetHeroAttackProgress(RaidRuntimeData raid)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            raid.Heroes[i].AttackProgress = 0f;
            raid.Heroes[i].TargetEnemyIndex = -1;
        }
    }

    private static float GetRaidDamagePerSecond(RaidRuntimeData raid)
    {
        float damagePerSecond = 0f;

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            RaidHeroRuntimeData raidHero = raid.Heroes[i];
            damagePerSecond += Mathf.Max(1f, raidHero.Damage) / Mathf.Max(0.01f, raidHero.AttackInterval);
        }

        return Mathf.Max(0.01f, damagePerSecond);
    }

    private static bool HasAliveHero(RaidRuntimeData raid)
    {
        return GetFirstAliveHero(raid) != null;
    }

    private static RaidHeroRuntimeData GetRandomAliveHero(RaidRuntimeData raid, out int heroIndex)
    {
        int aliveCount = 0;

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (raid.Heroes[i].Hp > 0f)
            {
                aliveCount++;
            }
        }

        if (aliveCount == 0)
        {
            heroIndex = -1;
            return null;
        }

        int selectedAliveIndex = Random.Range(0, aliveCount);

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (raid.Heroes[i].Hp <= 0f)
            {
                continue;
            }

            if (selectedAliveIndex == 0)
            {
                heroIndex = i;
                return raid.Heroes[i];
            }

            selectedAliveIndex--;
        }

        heroIndex = -1;
        return null;
    }

    private static RaidHeroRuntimeData GetFirstAliveHero(RaidRuntimeData raid)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (raid.Heroes[i].Hp > 0f)
            {
                return raid.Heroes[i];
            }
        }

        return null;
    }

    private static int GetRandomWeakestHeroIndex(RaidRuntimeData raid, int[] pendingExperience)
    {
        int selectedIndex = -1;
        int equalWeakestCount = 0;

        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (selectedIndex < 0 ||
                IsWeakerForExperienceRemainder(raid.Heroes[i], pendingExperience[i], raid.Heroes[selectedIndex], pendingExperience[selectedIndex]))
            {
                selectedIndex = i;
                equalWeakestCount = 1;
                continue;
            }

            if (HasEqualWeaknessForExperienceRemainder(raid.Heroes[i], pendingExperience[i], raid.Heroes[selectedIndex], pendingExperience[selectedIndex]))
            {
                equalWeakestCount++;

                if (Random.Range(0, equalWeakestCount) == 0)
                {
                    selectedIndex = i;
                }
            }
        }

        return Mathf.Max(0, selectedIndex);
    }

    private static bool IsWeakerForExperienceRemainder(
        RaidHeroRuntimeData candidate,
        int candidatePendingExperience,
        RaidHeroRuntimeData current,
        int currentPendingExperience)
    {
        if (candidate.Hero.Level != current.Hero.Level)
        {
            return candidate.Hero.Level < current.Hero.Level;
        }

        int candidateStats = GetPrimaryStatsTotal(candidate.Hero.Stats);
        int currentStats = GetPrimaryStatsTotal(current.Hero.Stats);

        if (candidateStats != currentStats)
        {
            return candidateStats < currentStats;
        }

        return candidate.Hero.Experience + candidatePendingExperience < current.Hero.Experience + currentPendingExperience;
    }

    private static bool HasEqualWeaknessForExperienceRemainder(
        RaidHeroRuntimeData candidate,
        int candidatePendingExperience,
        RaidHeroRuntimeData current,
        int currentPendingExperience)
    {
        return candidate.Hero.Level == current.Hero.Level &&
            GetPrimaryStatsTotal(candidate.Hero.Stats) == GetPrimaryStatsTotal(current.Hero.Stats) &&
            candidate.Hero.Experience + candidatePendingExperience == current.Hero.Experience + currentPendingExperience;
    }

    private static int GetPrimaryStatsTotal(PrimaryStats stats)
    {
        if (stats == null)
        {
            return 0;
        }

        return stats.Endurance + stats.Strength + stats.Agility + stats.Intelligence;
    }

    private static RaidHeroRuntimeData GetRaidHero(RaidRuntimeData raid, HeroRuntimeData heroData)
    {
        for (int i = 0; i < raid.Heroes.Count; i++)
        {
            if (raid.Heroes[i].Hero == heroData)
            {
                return raid.Heroes[i];
            }
        }

        return null;
    }

    private float CalculateEnemyMaxHp(EnemyDefinition definition, SecondaryStatsSnapshot secondaryStats)
    {
        float sharedMinimumHp = _statsConfig.GetSecondaryStatMinimumValue(SecondaryStatType.MaxHp);
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

    private EnemyRuntimeData GetHeroTargetEnemyInCurrentBattle(
        RaidRuntimeData raid,
        RaidHeroRuntimeData raidHero,
        out int enemyIndexInBattle)
    {
        if (!IsEnemyTargetValidInCurrentBattle(raid, raidHero.TargetEnemyIndex))
        {
            raidHero.TargetEnemyIndex = GetRandomAliveEnemyIndexInCurrentBattle(raid);
        }

        if (raidHero.TargetEnemyIndex < 0)
        {
            enemyIndexInBattle = -1;
            return null;
        }

        enemyIndexInBattle = raidHero.TargetEnemyIndex - raid.CurrentBattleStartIndex;
        return raid.Enemies[raidHero.TargetEnemyIndex];
    }

    private bool IsEnemyTargetValidInCurrentBattle(RaidRuntimeData raid, int enemyIndex)
    {
        return enemyIndex >= raid.CurrentBattleStartIndex &&
            enemyIndex < GetCurrentBattleEndIndex(raid) &&
            raid.Enemies[enemyIndex].Hp > 0f;
    }

    private int GetRandomAliveEnemyIndexInCurrentBattle(RaidRuntimeData raid)
    {
        int aliveCount = 0;
        int endIndex = GetCurrentBattleEndIndex(raid);

        for (int i = raid.CurrentBattleStartIndex; i < endIndex; i++)
        {
            if (raid.Enemies[i].Hp > 0f)
            {
                aliveCount++;
            }
        }

        if (aliveCount == 0)
        {
            return -1;
        }

        int selectedAliveIndex = Random.Range(0, aliveCount);

        for (int i = raid.CurrentBattleStartIndex; i < endIndex; i++)
        {
            if (raid.Enemies[i].Hp <= 0f)
            {
                continue;
            }

            if (selectedAliveIndex == 0)
            {
                return i;
            }

            selectedAliveIndex--;
        }

        return -1;
    }

    private int GetCurrentBattleEndIndex(RaidRuntimeData raid)
    {
        return GetBattleEndIndex(raid, raid.CurrentBattleNumber);
    }

    private int GetBattleStartIndex(RaidRuntimeData raid, int battleNumber)
    {
        if (raid.BattleStartIndices.Count == 0)
        {
            return 0;
        }

        int battleIndex = Mathf.Clamp(battleNumber - 1, 0, raid.BattleStartIndices.Count - 1);
        return raid.BattleStartIndices[battleIndex];
    }

    private int GetBattleEnemyCount(RaidRuntimeData raid, int battleNumber)
    {
        if (raid.BattleEnemyCounts.Count == 0)
        {
            return 0;
        }

        int battleIndex = Mathf.Clamp(battleNumber - 1, 0, raid.BattleEnemyCounts.Count - 1);
        return raid.BattleEnemyCounts[battleIndex];
    }

    private int GetBattleEndIndex(RaidRuntimeData raid, int battleNumber)
    {
        int startIndex = GetBattleStartIndex(raid, battleNumber);
        return Mathf.Min(startIndex + GetBattleEnemyCount(raid, battleNumber), raid.Enemies.Count);
    }

    private enum RaidState
    {
        Waiting = 0,
        Recruiting = 1,
        InProgress = 2,
        BattleTransition = 3,
        Completed = 4
    }

    private sealed class RaidRuntimeData
    {
        public readonly int Id;
        public readonly RaidPanelView Panel;
        public readonly int LayoutSlot;
        public readonly List<EnemyRuntimeData> Enemies = new List<EnemyRuntimeData>();
        public readonly List<RaidHeroRuntimeData> Heroes = new List<RaidHeroRuntimeData>();
        public readonly List<int> BattleStartIndices = new List<int>();
        public readonly List<int> BattleEnemyCounts = new List<int>();
        public readonly List<float> ProgressSegmentDurations = new List<float>();
        public readonly int MaxHeroSlots;

        public RaidState State;
        public float WaitingSeconds;
        public float RecruitingSecondsRemaining;
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

        public RaidRuntimeData(int id, RaidPanelView panel, int layoutSlot, int maxHeroSlots)
        {
            Id = id;
            Panel = panel;
            LayoutSlot = layoutSlot;
            MaxHeroSlots = Mathf.Clamp(maxHeroSlots, 1, 3);
            State = RaidState.Waiting;
        }
    }

    private sealed class RaidHeroRuntimeData
    {
        public readonly HeroRuntimeData Hero;

        public SecondaryStatsSnapshot SecondaryStats;
        public float Hp;
        public float MaxHp;
        public float Damage;
        public float AttackInterval;
        public float AttackProgress;
        public int TargetEnemyIndex = -1;

        public RaidHeroRuntimeData(HeroRuntimeData hero)
        {
            Hero = hero;
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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestSystem : MonoBehaviour
{
    private const float _minimumProgressSegmentSeconds = 0.01f;

    [Header("Configs")]
    [SerializeField] private QuestConfig _questConfig = null;
    [SerializeField] private EnemyConfig _enemyConfig = null;
    [SerializeField] private StatsConfig _statsConfig = null;

    [Header("Scene References")]
    [SerializeField] private GuildSystem _guildSystem = null;
    [SerializeField] private Canvas _canvas = null;
    [SerializeField] private RectTransform _panelsRoot = null;
    [SerializeField] private QuestPanelView _panelTemplate = null;
    [SerializeField] private TextMeshProUGUI _goldText = null;
    [SerializeField] private Button _spawnQuestButton = null;

    [Header("Layout")]
    [SerializeField] private Vector2 _firstPanelAnchoredPosition = new Vector2(20f, -70f);
    [SerializeField] private Vector2 _panelSpacing = new Vector2(20f, 20f);
    [SerializeField, Min(1)] private int _panelsPerRow = 2;

    private readonly List<QuestRuntimeData> _quests = new List<QuestRuntimeData>();
    private readonly List<QuestHeroViewData> _heroViewData = new List<QuestHeroViewData>();
    private readonly List<QuestEnemyViewData> _enemyViewData = new List<QuestEnemyViewData>();

    private float _newQuestTimer;
    private int _nextQuestId = 1;
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
        RefreshSpawnQuestButtonState();
        UpdateQuestSpawn(deltaTime);

        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            UpdateQuest(_quests[i], deltaTime);
        }
    }

    private void OnDisable()
    {
        if (_spawnQuestButton != null)
        {
            _spawnQuestButton.onClick.RemoveListener(CreateQuest);
        }
    }

    public bool TryAcceptDroppedHero(HeroRuntimeData heroData, Vector2 screenPosition)
    {
        if (!_initialized || heroData == null)
        {
            return false;
        }

        for (int i = 0; i < _quests.Count; i++)
        {
            QuestRuntimeData quest = _quests[i];

            bool canAcceptHero = quest.State == QuestState.Waiting || quest.State == QuestState.Recruiting;

            if (canAcceptHero && quest.Panel.ContainsScreenPoint(screenPosition, UiCamera))
            {
                return TryAddHeroToQuest(quest, heroData);
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
        ConfigureSpawnQuestButton();
        _newQuestTimer = _questConfig.NewQuestIntervalSeconds;
        RefreshGoldText();

        for (int i = 0; i < _questConfig.StartingQuestCount; i++)
        {
            CreateQuest();
        }
    }

    private void ValidateReferences()
    {
        if (_questConfig == null || !_questConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(QuestSystem)} requires valid {nameof(QuestConfig)}.");
        }

        if (_enemyConfig == null || !_enemyConfig.ValidateForRuntime())
        {
            throw new System.InvalidOperationException($"{nameof(QuestSystem)} requires valid {nameof(EnemyConfig)}.");
        }

        if (_statsConfig == null)
        {
            throw new System.InvalidOperationException($"{nameof(QuestSystem)} requires {nameof(StatsConfig)}.");
        }

        if (_guildSystem == null || _canvas == null || _panelsRoot == null || _panelTemplate == null ||
            _goldText == null || _spawnQuestButton == null)
        {
            throw new System.InvalidOperationException($"{nameof(QuestSystem)} requires scene references.");
        }
    }

    private void ConfigureSpawnQuestButton()
    {
        _spawnQuestButton.onClick.RemoveListener(CreateQuest);
        _spawnQuestButton.onClick.AddListener(CreateQuest);

        Image image = _spawnQuestButton.GetComponent<Image>();

        if (image != null)
        {
            image.color = new Color(0.92f, 0.92f, 0.9f, 1f);
            image.raycastTarget = true;
        }

        TextMeshProUGUI label = _spawnQuestButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            label.text = "Добавить новый квест";
            label.fontSize = 15f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        RefreshSpawnQuestButtonState();
    }

    private void RefreshSpawnQuestButtonState()
    {
        bool shouldShow = _questConfig.UseManualQuestSpawnButton;

        if (_spawnQuestButton.gameObject.activeSelf != shouldShow)
        {
            _spawnQuestButton.gameObject.SetActive(shouldShow);
        }
    }

    private void UpdateQuestSpawn(float deltaTime)
    {
        if (_questConfig.UseManualQuestSpawnButton)
        {
            return;
        }

        _newQuestTimer -= deltaTime;

        if (_newQuestTimer > 0f)
        {
            return;
        }

        CreateQuest();
        _newQuestTimer = _questConfig.NewQuestIntervalSeconds;
    }

    private bool TryExpireWaitingQuest(QuestRuntimeData quest)
    {
        if (quest.WaitingSeconds < _questConfig.WaitingQuestLifetimeSeconds)
        {
            return false;
        }

        RemoveQuest(quest);
        return true;
    }

    private void RefreshWaitingQuestUi(QuestRuntimeData quest)
    {
        float remainingSeconds = Mathf.Max(0f, _questConfig.WaitingQuestLifetimeSeconds - quest.WaitingSeconds);
        quest.Panel.ShowWaiting(quest.Id, remainingSeconds, quest.Heroes.Count, quest.MaxHeroSlots);
    }

    private void UpdateQuest(QuestRuntimeData quest, float deltaTime)
    {
        switch (quest.State)
        {
            case QuestState.Waiting:
                quest.WaitingSeconds += deltaTime;
                if (TryExpireWaitingQuest(quest))
                {
                    return;
                }

                RefreshWaitingQuestUi(quest);
                break;
            case QuestState.Recruiting:
                UpdateQuestRecruiting(quest, deltaTime);
                break;
            case QuestState.InProgress:
                UpdateQuestBattle(quest, deltaTime);
                break;
            case QuestState.BattleTransition:
                UpdateBattleTransition(quest, deltaTime);
                break;
            case QuestState.Completed:
                break;
        }
    }

    private void CreateQuest()
    {
        int layoutSlot = GetNextFreeLayoutSlot();
        QuestPanelView panel = Instantiate(_panelTemplate, _panelsRoot, false);
        panel.gameObject.name = $"Quest Panel {_nextQuestId}";
        panel.gameObject.SetActive(true);
        panel.InitializeRuntime();

        QuestRuntimeData quest = new QuestRuntimeData(_nextQuestId, panel, layoutSlot, GetRandomHeroSlotCount());
        panel.CloseRequested += () => RemoveQuest(quest);
        panel.HeroClicked += heroIndex => HandleQuestHeroClicked(quest, heroIndex);

        _nextQuestId++;
        _quests.Add(quest);
        panel.SetAnchoredPosition(GetQuestSlotPosition(layoutSlot));
        RefreshWaitingQuestUi(quest);
    }

    private int GetRandomHeroSlotCount()
    {
        return Random.Range(_questConfig.MinHeroSlots, _questConfig.MaxHeroSlots + 1);
    }

    private bool TryAddHeroToQuest(QuestRuntimeData quest, HeroRuntimeData heroData)
    {
        if (heroData == null || quest.Heroes.Count >= quest.MaxHeroSlots || ContainsHero(quest, heroData))
        {
            return false;
        }

        AddHeroToQuest(quest, heroData);

        if (quest.Heroes.Count >= quest.MaxHeroSlots || _questConfig.AdditionalHeroWindowSeconds <= 0f)
        {
            StartQuestBattle(quest);
            return true;
        }

        quest.State = QuestState.Recruiting;
        quest.RecruitingSecondsRemaining = _questConfig.AdditionalHeroWindowSeconds;
        RefreshRecruitingQuestUi(quest);
        return true;
    }

    private void AddHeroToQuest(QuestRuntimeData quest, HeroRuntimeData heroData)
    {
        QuestHeroRuntimeData questHero = new QuestHeroRuntimeData(heroData);
        quest.Heroes.Add(questHero);
        RefreshQuestHeroCombatStats(questHero);
        _guildSystem.SetHeroState(heroData, HeroActivityState.InQuest);
    }

    public void RefreshHeroSelectionVisuals()
    {
        if (!_initialized)
        {
            return;
        }

        HeroRuntimeData selectedHero = _guildSystem != null ? _guildSystem.SelectedHero : null;

        for (int i = 0; i < _quests.Count; i++)
        {
            if (_quests[i].Panel != null)
            {
                _quests[i].Panel.SetSelectedHero(selectedHero);
            }
        }
    }

    private void HandleQuestHeroClicked(QuestRuntimeData quest, int heroIndex)
    {
        if (!_initialized || quest == null || heroIndex < 0 || heroIndex >= quest.Heroes.Count)
        {
            return;
        }

        _guildSystem.SelectHeroFromQuest(quest.Heroes[heroIndex].Hero);
        RefreshHeroSelectionVisuals();
    }

    private bool ContainsHero(QuestRuntimeData quest, HeroRuntimeData heroData)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (quest.Heroes[i].Hero == heroData)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateQuestRecruiting(QuestRuntimeData quest, float deltaTime)
    {
        quest.RecruitingSecondsRemaining -= deltaTime;

        if (quest.RecruitingSecondsRemaining <= 0f)
        {
            StartQuestBattle(quest);
            return;
        }

        RefreshRecruitingQuestUi(quest);
    }

    private void RefreshRecruitingQuestUi(QuestRuntimeData quest)
    {
        BuildHeroViewData(quest, false);
        quest.Panel.ShowRecruiting(
            quest.Id,
            quest.RecruitingSecondsRemaining,
            quest.Heroes.Count,
            quest.MaxHeroSlots,
            _heroViewData);
    }

    private void StartQuestBattle(QuestRuntimeData quest)
    {
        if (quest.Heroes.Count == 0)
        {
            return;
        }

        quest.State = QuestState.InProgress;
        RefreshQuestHeroCombatStats(quest);
        ResetHeroAttackProgress(quest);
        quest.KilledEnemies = 0;
        quest.ExperienceGained = 0;
        quest.TotalGold = Random.Range(_questConfig.MinGoldReward, _questConfig.MaxGoldReward + 1);
        quest.GoldFound = 0;
        quest.GoldEarnedFromKills = 0;
        quest.PendingGold = 0;
        quest.LootGoldStart = 0;
        quest.LootGoldTarget = 0;
        quest.CompleteAfterLoot = false;

        GenerateEnemies(quest);
        BuildQuestProgressTimeline(quest);
        BeginProgressSegment(quest, GetBattleProgressSegmentIndex(quest.CurrentBattleNumber));
        RefreshQuestBattleUi(quest);
    }

    private void GenerateEnemies(QuestRuntimeData quest)
    {
        quest.Enemies.Clear();

        int enemyCount = Random.Range(_questConfig.MinEnemies, _questConfig.MaxEnemies + 1);
        IReadOnlyList<EnemyDefinition> definitions = _enemyConfig.Enemies;

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyDefinition definition = definitions[Random.Range(0, definitions.Count)];
            SecondaryStatsSnapshot secondaryStats = _statsConfig.CalculateSecondaryStats(definition.Stats);
            float enemyMaxHp = CalculateEnemyMaxHp(definition, secondaryStats);
            quest.Enemies.Add(new EnemyRuntimeData(definition, secondaryStats, enemyMaxHp));
        }

        GenerateBattleGroups(quest);
    }

    private void GenerateBattleGroups(QuestRuntimeData quest)
    {
        quest.BattleStartIndices.Clear();
        quest.BattleEnemyCounts.Clear();

        int startIndex = 0;

        while (startIndex < quest.Enemies.Count)
        {
            int enemyCount = GetRandomBattleEnemyCount(quest.Enemies.Count - startIndex);
            quest.BattleStartIndices.Add(startIndex);
            quest.BattleEnemyCounts.Add(enemyCount);
            startIndex += enemyCount;
        }

        quest.CurrentBattleNumber = 1;
        quest.BattleCount = quest.BattleEnemyCounts.Count;
        quest.CurrentBattleStartIndex = GetBattleStartIndex(quest, quest.CurrentBattleNumber);
    }

    private int GetRandomBattleEnemyCount(int remainingEnemies)
    {
        int minEnemiesPerBattle = Mathf.Max(1, _questConfig.MinEnemiesPerBattle);
        int maxEnemiesPerBattle = Mathf.Max(minEnemiesPerBattle, _questConfig.MaxEnemiesPerBattle);
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

    private void BuildQuestProgressTimeline(QuestRuntimeData quest)
    {
        quest.ProgressSegmentDurations.Clear();
        quest.TotalProgressSeconds = 0f;

        for (int battleNumber = 1; battleNumber <= quest.BattleCount; battleNumber++)
        {
            int battleStartIndex = GetBattleStartIndex(quest, battleNumber);
            int battleEndIndex = GetBattleEndIndex(quest, battleNumber);
            AddProgressSegment(quest, EstimateBattleProgressSegmentSeconds(quest, battleStartIndex, battleEndIndex));

            AddProgressSegment(quest, _questConfig.BattleTransitionDelaySeconds);
        }
    }

    private void AddProgressSegment(QuestRuntimeData quest, float durationSeconds)
    {
        float safeDuration = Mathf.Max(0f, durationSeconds);
        quest.ProgressSegmentDurations.Add(safeDuration);
        quest.TotalProgressSeconds += safeDuration;
    }

    private float EstimateBattleProgressSegmentSeconds(QuestRuntimeData quest, int startIndex, int endIndex)
    {
        float totalEnemyHp = 0f;

        for (int i = startIndex; i < endIndex; i++)
        {
            totalEnemyHp += quest.Enemies[i].MaxHp;
        }

        return Mathf.Max(_minimumProgressSegmentSeconds, totalEnemyHp / GetQuestDamagePerSecond(quest));
    }

    private void BeginProgressSegment(QuestRuntimeData quest, int segmentIndex)
    {
        quest.ProgressSegmentIndex = Mathf.Clamp(segmentIndex, 0, Mathf.Max(0, quest.ProgressSegmentDurations.Count - 1));
        quest.ProgressSegmentTimer = 0f;
    }

    private void AdvanceQuestProgressSegmentTimer(QuestRuntimeData quest, float deltaTime)
    {
        quest.ProgressSegmentTimer += deltaTime;
    }

    private void CompleteCurrentProgressSegment(QuestRuntimeData quest)
    {
        if (quest.ProgressSegmentIndex < 0 || quest.ProgressSegmentIndex >= quest.ProgressSegmentDurations.Count)
        {
            return;
        }

        quest.ProgressSegmentTimer = quest.ProgressSegmentDurations[quest.ProgressSegmentIndex];
    }

    private void CompleteAllProgressSegments(QuestRuntimeData quest)
    {
        quest.ProgressSegmentIndex = quest.ProgressSegmentDurations.Count;
        quest.ProgressSegmentTimer = 0f;
    }

    private float GetQuestProgressRatio(QuestRuntimeData quest)
    {
        if (quest.TotalProgressSeconds <= 0f)
        {
            return 0f;
        }

        if (quest.ProgressSegmentIndex >= quest.ProgressSegmentDurations.Count)
        {
            return 1f;
        }

        float completedSeconds = 0f;
        int safeSegmentIndex = Mathf.Clamp(quest.ProgressSegmentIndex, 0, quest.ProgressSegmentDurations.Count - 1);

        for (int i = 0; i < safeSegmentIndex; i++)
        {
            completedSeconds += quest.ProgressSegmentDurations[i];
        }

        float currentSegmentSeconds = quest.ProgressSegmentDurations[safeSegmentIndex];
        completedSeconds += currentSegmentSeconds <= 0f
            ? 0f
            : Mathf.Clamp(quest.ProgressSegmentTimer, 0f, currentSegmentSeconds);
        return Mathf.Clamp01(completedSeconds / quest.TotalProgressSeconds);
    }

    private static int GetBattleProgressSegmentIndex(int battleNumber)
    {
        return (Mathf.Max(1, battleNumber) - 1) * 2;
    }

    private static int GetTransitionProgressSegmentIndex(int battleNumber)
    {
        return GetBattleProgressSegmentIndex(battleNumber) + 1;
    }

    private void UpdateQuestBattle(QuestRuntimeData quest, float deltaTime)
    {
        if (!HasAliveHero(quest))
        {
            CompleteQuest(quest, false);
            return;
        }

        if (!HasAliveEnemyInCurrentBattle(quest))
        {
            StartBattleTransitionOrCompleteQuest(quest);
            return;
        }

        AdvanceQuestProgressSegmentTimer(quest, deltaTime);
        UpdateHeroAttack(quest, deltaTime);
        UpdateEnemyAttacks(quest, deltaTime);

        if (quest.State != QuestState.InProgress)
        {
            return;
        }

        if (!HasAliveEnemyInCurrentBattle(quest))
        {
            StartBattleTransitionOrCompleteQuest(quest);
            return;
        }

        RefreshQuestBattleUi(quest);
    }

    private void UpdateHeroAttack(QuestRuntimeData quest, float deltaTime)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            QuestHeroRuntimeData questHero = quest.Heroes[i];

            if (questHero.Hp <= 0f)
            {
                questHero.AttackProgress = 0f;
                questHero.TargetEnemyIndex = -1;
                continue;
            }

            questHero.AttackProgress += deltaTime / questHero.AttackInterval;

            if (questHero.AttackProgress < 1f)
            {
                continue;
            }

            questHero.AttackProgress = 0f;
            EnemyRuntimeData target = GetHeroTargetEnemyInCurrentBattle(quest, questHero, out int enemyIndexInBattle);

            if (target == null)
            {
                return;
            }

            float hpBeforeAttack = target.Hp;
            target.Hp = Mathf.Max(0f, target.Hp - questHero.Damage);

            if (hpBeforeAttack > 0f && target.Hp <= 0f)
            {
                quest.KilledEnemies++;
                AwardExperienceToQuestHeroes(quest, target.ExperienceReward);
                QueueGoldForKill(quest);
                questHero.TargetEnemyIndex = -1;
            }

            StartCoroutine(quest.Panel.PlayHeroAttackEffect(i, enemyIndexInBattle));
            StartCoroutine(quest.Panel.ShakeEnemyHpBar(enemyIndexInBattle));
        }
    }

    private void UpdateEnemyAttacks(QuestRuntimeData quest, float deltaTime)
    {
        int endIndex = GetCurrentBattleEndIndex(quest);

        for (int i = quest.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = quest.Enemies[i];

            if (enemy.Hp <= 0f)
            {
                continue;
            }

            enemy.AttackProgress += deltaTime / enemy.AttackInterval;

            if (enemy.AttackProgress < 1f)
            {
                continue;
            }

            QuestHeroRuntimeData targetHero = GetRandomAliveHero(quest, out int heroIndex);

            if (targetHero == null)
            {
                CompleteQuest(quest, false);
                return;
            }

            enemy.AttackProgress = 0f;
            float blockedDamagePercent = _statsConfig.CalculateArmorBlockedDamagePercent(targetHero.SecondaryStats.Armor);
            float damageMultiplier = Mathf.Clamp01(1f - blockedDamagePercent / 100f);
            targetHero.Hp = Mathf.Max(0f, targetHero.Hp - Mathf.Max(1f, enemy.Damage * damageMultiplier));
            targetHero.Hero.SetCurrentHp(targetHero.Hp);
            StartCoroutine(quest.Panel.ShakeHeroHpBar(heroIndex));

            if (!HasAliveHero(quest))
            {
                CompleteQuest(quest, false);
                return;
            }
        }
    }

    private void StartBattleTransitionOrCompleteQuest(QuestRuntimeData quest)
    {
        CompleteCurrentProgressSegment(quest);
        quest.CompleteAfterLoot = quest.CurrentBattleNumber >= quest.BattleCount;

        quest.State = QuestState.BattleTransition;
        quest.BattleTransitionTimer = 0f;
        quest.BattleTransitionSeconds = _questConfig.BattleTransitionDelaySeconds;
        ResetHeroAttackProgress(quest);
        BeginProgressSegment(quest, GetTransitionProgressSegmentIndex(quest.CurrentBattleNumber));
        BeginLootGoldCollection(quest);

        if (quest.BattleTransitionSeconds <= 0f)
        {
            CompleteLootGoldCollection(quest);
            CompleteCurrentProgressSegment(quest);
            FinishBattleTransition(quest);
            return;
        }

        RefreshBattleTransitionUi(quest);
    }

    private void UpdateBattleTransition(QuestRuntimeData quest, float deltaTime)
    {
        quest.BattleTransitionTimer += deltaTime;
        quest.ProgressSegmentTimer = quest.BattleTransitionTimer;
        UpdateLootGoldCollection(quest);

        if (quest.BattleTransitionTimer >= quest.BattleTransitionSeconds)
        {
            CompleteLootGoldCollection(quest);
            CompleteCurrentProgressSegment(quest);
            FinishBattleTransition(quest);
            return;
        }

        RefreshBattleTransitionUi(quest);
    }

    private void AdvanceToNextBattle(QuestRuntimeData quest)
    {
        quest.State = QuestState.InProgress;
        quest.CurrentBattleNumber++;
        quest.CurrentBattleStartIndex = GetBattleStartIndex(quest, quest.CurrentBattleNumber);
        ResetHeroAttackProgress(quest);
        quest.BattleTransitionTimer = 0f;
        quest.CompleteAfterLoot = false;
        BeginProgressSegment(quest, GetBattleProgressSegmentIndex(quest.CurrentBattleNumber));
        RefreshQuestBattleUi(quest);
    }

    private void FinishBattleTransition(QuestRuntimeData quest)
    {
        if (quest.CompleteAfterLoot)
        {
            CompleteQuest(quest, true);
            return;
        }

        AdvanceToNextBattle(quest);
    }

    private void CompleteQuest(QuestRuntimeData quest, bool success)
    {
        if (quest.State == QuestState.Completed)
        {
            return;
        }

        quest.State = QuestState.Completed;

        if (success)
        {
            quest.KilledEnemies = quest.Enemies.Count;
            CompleteLootGoldCollection(quest);
            CompleteAllProgressSegments(quest);
            quest.RewardDice = _guildSystem.AddRandomDiceFromConfigToPool();
        }

        _gold += quest.GoldFound;
        RefreshGoldText();

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            QuestHeroRuntimeData questHero = quest.Heroes[i];
            questHero.Hero.SetCurrentHp(questHero.Hp);
            HeroActivityState heroState = success && questHero.Hp > 0f ? HeroActivityState.OnBase : HeroActivityState.Resting;
            _guildSystem.SetHeroState(questHero.Hero, heroState);
        }

        string message = success ? GetSuccessMessage(quest) : "Отряд проиграл бой. Раненые герои ушли отдыхать.";
        BuildHeroViewData(quest, false);
        quest.Panel.ShowCompleted(
            quest.Id,
            success,
            message,
            quest.KilledEnemies,
            quest.Enemies.Count,
            GetQuestProgressRatio(quest),
            quest.GoldFound,
            quest.ExperienceGained,
            _heroViewData);
    }

    private static string GetSuccessMessage(QuestRuntimeData quest)
    {
        string message = "Отряд прошел квест и вернулся на базу.";

        if (quest.RewardDice != null)
        {
            message += $"\nНайден кубик: {quest.RewardDice.DisplayName}";
        }

        return message;
    }

    public void RefreshHeroCombatStats(HeroRuntimeData heroData)
    {
        if (!_initialized || heroData == null)
        {
            return;
        }

        for (int i = 0; i < _quests.Count; i++)
        {
            QuestRuntimeData quest = _quests[i];
            QuestHeroRuntimeData questHero = GetQuestHero(quest, heroData);

            if (questHero == null || quest.State == QuestState.Waiting || quest.State == QuestState.Completed)
            {
                continue;
            }

            RefreshQuestHeroCombatStats(questHero);

            if (quest.State == QuestState.Recruiting)
            {
                RefreshRecruitingQuestUi(quest);
                continue;
            }

            if (quest.State == QuestState.BattleTransition)
            {
                RefreshBattleTransitionUi(quest);
            }
            else
            {
                RefreshQuestBattleUi(quest);
            }
        }
    }

    private void RemoveQuest(QuestRuntimeData quest)
    {
        if (!_quests.Remove(quest))
        {
            return;
        }

        if (quest.Panel != null)
        {
            Destroy(quest.Panel.gameObject);
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
        Rect candidateRect = GetPanelRect(GetQuestSlotPosition(slot), GetQuestPanelSize());

        for (int i = 0; i < _quests.Count; i++)
        {
            RectTransform panelRoot = _quests[i].Panel != null ? _quests[i].Panel.Root : null;

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

    private Vector2 GetQuestSlotPosition(int slot)
    {
        Vector2 panelSize = GetQuestPanelSize();
        int row = slot / _panelsPerRow;
        int column = slot % _panelsPerRow;
        return _firstPanelAnchoredPosition + new Vector2(
            column * (panelSize.x + _panelSpacing.x),
            -row * (panelSize.y + _panelSpacing.y));
    }

    private Vector2 GetQuestPanelSize()
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

    private void RefreshQuestBattleUi(QuestRuntimeData quest)
    {
        BuildHeroViewData(quest, true);
        _enemyViewData.Clear();

        int endIndex = GetCurrentBattleEndIndex(quest);

        for (int i = quest.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = quest.Enemies[i];
            _enemyViewData.Add(new QuestEnemyViewData(enemy.DisplayName, enemy.Hp, enemy.MaxHp, enemy.AttackProgress));
        }

        quest.Panel.ShowBattle(
            quest.Id,
            quest.CurrentBattleNumber,
            quest.BattleCount,
            _heroViewData,
            quest.KilledEnemies,
            quest.Enemies.Count,
            GetQuestProgressRatio(quest),
            quest.GoldFound,
            quest.ExperienceGained,
            _enemyViewData);
    }

    private void BuildHeroViewData(QuestRuntimeData quest, bool showAttackProgress)
    {
        _heroViewData.Clear();

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            QuestHeroRuntimeData hero = quest.Heroes[i];
            float attackProgress = showAttackProgress && hero.Hp > 0f ? hero.AttackProgress : 0f;
            bool isSelected = _guildSystem != null &&
                _guildSystem.SelectedHero == hero.Hero &&
                hero.Hero.State == HeroActivityState.InQuest;
            _heroViewData.Add(new QuestHeroViewData(hero.Hero, hero.Hero.Name, hero.Hp, hero.MaxHp, attackProgress, isSelected));
        }
    }

    private void RefreshBattleTransitionUi(QuestRuntimeData quest)
    {
        BuildHeroViewData(quest, false);
        quest.Panel.ShowBattleTransition(
            quest.Id,
            quest.CurrentBattleNumber + 1,
            quest.BattleCount,
            _heroViewData,
            GetQuestProgressRatio(quest),
            quest.KilledEnemies,
            quest.Enemies.Count,
            quest.CompleteAfterLoot,
            quest.GoldFound,
            quest.ExperienceGained);
    }

    private void QueueGoldForKill(QuestRuntimeData quest)
    {
        int totalEnemies = Mathf.Max(1, quest.Enemies.Count);
        int targetGoldEarned = Mathf.RoundToInt(quest.TotalGold * Mathf.Clamp01(quest.KilledEnemies / (float)totalEnemies));
        int goldToQueue = Mathf.Max(0, targetGoldEarned - quest.GoldEarnedFromKills);
        quest.GoldEarnedFromKills = Mathf.Max(quest.GoldEarnedFromKills, targetGoldEarned);
        quest.PendingGold += goldToQueue;
    }

    private void BeginLootGoldCollection(QuestRuntimeData quest)
    {
        quest.LootGoldStart = quest.GoldFound;
        quest.LootGoldTarget = quest.GoldFound + Mathf.Max(0, quest.PendingGold);
        quest.PendingGold = 0;
    }

    private void UpdateLootGoldCollection(QuestRuntimeData quest)
    {
        int goldToCollect = quest.LootGoldTarget - quest.LootGoldStart;

        if (goldToCollect <= 0)
        {
            return;
        }

        float progress = quest.BattleTransitionSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(quest.BattleTransitionTimer / quest.BattleTransitionSeconds);
        quest.GoldFound = quest.LootGoldStart + Mathf.FloorToInt(goldToCollect * progress);
    }

    private void CompleteLootGoldCollection(QuestRuntimeData quest)
    {
        quest.GoldFound = Mathf.Max(quest.GoldFound, quest.LootGoldTarget);
    }

    private void AwardExperienceToQuestHeroes(QuestRuntimeData quest, int totalExperience)
    {
        if (totalExperience <= 0 || quest.Heroes.Count == 0)
        {
            return;
        }

        quest.ExperienceGained += totalExperience;

        int[] experienceShares = new int[quest.Heroes.Count];
        int baseExperience = totalExperience / quest.Heroes.Count;
        int remainder = totalExperience % quest.Heroes.Count;

        for (int i = 0; i < experienceShares.Length; i++)
        {
            experienceShares[i] = baseExperience;
        }

        while (remainder > 0)
        {
            int weakestHeroIndex = GetRandomWeakestHeroIndex(quest, experienceShares);
            experienceShares[weakestHeroIndex]++;
            remainder--;
        }

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (experienceShares[i] > 0)
            {
                _guildSystem.AddExperienceToHero(quest.Heroes[i].Hero, experienceShares[i]);
            }
        }

        RefreshQuestHeroCombatStats(quest);
    }

    private void RefreshGoldText()
    {
        _goldText.text = $"Золото: {_gold}";
    }

    private void RefreshQuestHeroCombatStats(QuestRuntimeData quest)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            RefreshQuestHeroCombatStats(quest.Heroes[i]);
        }
    }

    private void RefreshQuestHeroCombatStats(QuestHeroRuntimeData questHero)
    {
        questHero.SecondaryStats = questHero.Hero.GetEffectiveSecondaryStats(_statsConfig);
        questHero.Hero.SetMaxHp(Mathf.Max(1f, questHero.SecondaryStats.MaxHp), false);
        questHero.MaxHp = questHero.Hero.MaxHp;

        float currentHp = questHero.Hp > 0f ? questHero.Hp : questHero.Hero.CurrentHp;
        questHero.Hp = Mathf.Clamp(currentHp, 0f, questHero.MaxHp);
        questHero.AttackInterval = Mathf.Max(0.01f, questHero.SecondaryStats.AttackInterval);
        questHero.Damage = Mathf.Max(1f, questHero.SecondaryStats.MeleeDamage);
        questHero.Hero.SetCurrentHp(questHero.Hp);
    }

    private static void ResetHeroAttackProgress(QuestRuntimeData quest)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            quest.Heroes[i].AttackProgress = 0f;
            quest.Heroes[i].TargetEnemyIndex = -1;
        }
    }

    private static float GetQuestDamagePerSecond(QuestRuntimeData quest)
    {
        float damagePerSecond = 0f;

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            QuestHeroRuntimeData questHero = quest.Heroes[i];
            damagePerSecond += Mathf.Max(1f, questHero.Damage) / Mathf.Max(0.01f, questHero.AttackInterval);
        }

        return Mathf.Max(0.01f, damagePerSecond);
    }

    private static bool HasAliveHero(QuestRuntimeData quest)
    {
        return GetFirstAliveHero(quest) != null;
    }

    private static QuestHeroRuntimeData GetRandomAliveHero(QuestRuntimeData quest, out int heroIndex)
    {
        int aliveCount = 0;

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (quest.Heroes[i].Hp > 0f)
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

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (quest.Heroes[i].Hp <= 0f)
            {
                continue;
            }

            if (selectedAliveIndex == 0)
            {
                heroIndex = i;
                return quest.Heroes[i];
            }

            selectedAliveIndex--;
        }

        heroIndex = -1;
        return null;
    }

    private static QuestHeroRuntimeData GetFirstAliveHero(QuestRuntimeData quest)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (quest.Heroes[i].Hp > 0f)
            {
                return quest.Heroes[i];
            }
        }

        return null;
    }

    private static int GetRandomWeakestHeroIndex(QuestRuntimeData quest, int[] pendingExperience)
    {
        int selectedIndex = -1;
        int equalWeakestCount = 0;

        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (selectedIndex < 0 ||
                IsWeakerForExperienceRemainder(quest.Heroes[i], pendingExperience[i], quest.Heroes[selectedIndex], pendingExperience[selectedIndex]))
            {
                selectedIndex = i;
                equalWeakestCount = 1;
                continue;
            }

            if (HasEqualWeaknessForExperienceRemainder(quest.Heroes[i], pendingExperience[i], quest.Heroes[selectedIndex], pendingExperience[selectedIndex]))
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
        QuestHeroRuntimeData candidate,
        int candidatePendingExperience,
        QuestHeroRuntimeData current,
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
        QuestHeroRuntimeData candidate,
        int candidatePendingExperience,
        QuestHeroRuntimeData current,
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

    private static QuestHeroRuntimeData GetQuestHero(QuestRuntimeData quest, HeroRuntimeData heroData)
    {
        for (int i = 0; i < quest.Heroes.Count; i++)
        {
            if (quest.Heroes[i].Hero == heroData)
            {
                return quest.Heroes[i];
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

    private bool HasAliveEnemyInCurrentBattle(QuestRuntimeData quest)
    {
        return GetFirstAliveEnemyInCurrentBattle(quest, out _) != null;
    }

    private EnemyRuntimeData GetFirstAliveEnemyInCurrentBattle(QuestRuntimeData quest, out int enemyIndexInBattle)
    {
        int endIndex = GetCurrentBattleEndIndex(quest);

        for (int i = quest.CurrentBattleStartIndex; i < endIndex; i++)
        {
            EnemyRuntimeData enemy = quest.Enemies[i];

            if (enemy.Hp > 0f)
            {
                enemyIndexInBattle = i - quest.CurrentBattleStartIndex;
                return enemy;
            }
        }

        enemyIndexInBattle = -1;
        return null;
    }

    private EnemyRuntimeData GetHeroTargetEnemyInCurrentBattle(
        QuestRuntimeData quest,
        QuestHeroRuntimeData questHero,
        out int enemyIndexInBattle)
    {
        if (!IsEnemyTargetValidInCurrentBattle(quest, questHero.TargetEnemyIndex))
        {
            questHero.TargetEnemyIndex = GetRandomAliveEnemyIndexInCurrentBattle(quest);
        }

        if (questHero.TargetEnemyIndex < 0)
        {
            enemyIndexInBattle = -1;
            return null;
        }

        enemyIndexInBattle = questHero.TargetEnemyIndex - quest.CurrentBattleStartIndex;
        return quest.Enemies[questHero.TargetEnemyIndex];
    }

    private bool IsEnemyTargetValidInCurrentBattle(QuestRuntimeData quest, int enemyIndex)
    {
        return enemyIndex >= quest.CurrentBattleStartIndex &&
            enemyIndex < GetCurrentBattleEndIndex(quest) &&
            quest.Enemies[enemyIndex].Hp > 0f;
    }

    private int GetRandomAliveEnemyIndexInCurrentBattle(QuestRuntimeData quest)
    {
        int aliveCount = 0;
        int endIndex = GetCurrentBattleEndIndex(quest);

        for (int i = quest.CurrentBattleStartIndex; i < endIndex; i++)
        {
            if (quest.Enemies[i].Hp > 0f)
            {
                aliveCount++;
            }
        }

        if (aliveCount == 0)
        {
            return -1;
        }

        int selectedAliveIndex = Random.Range(0, aliveCount);

        for (int i = quest.CurrentBattleStartIndex; i < endIndex; i++)
        {
            if (quest.Enemies[i].Hp <= 0f)
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

    private int GetCurrentBattleEndIndex(QuestRuntimeData quest)
    {
        return GetBattleEndIndex(quest, quest.CurrentBattleNumber);
    }

    private int GetBattleStartIndex(QuestRuntimeData quest, int battleNumber)
    {
        if (quest.BattleStartIndices.Count == 0)
        {
            return 0;
        }

        int battleIndex = Mathf.Clamp(battleNumber - 1, 0, quest.BattleStartIndices.Count - 1);
        return quest.BattleStartIndices[battleIndex];
    }

    private int GetBattleEnemyCount(QuestRuntimeData quest, int battleNumber)
    {
        if (quest.BattleEnemyCounts.Count == 0)
        {
            return 0;
        }

        int battleIndex = Mathf.Clamp(battleNumber - 1, 0, quest.BattleEnemyCounts.Count - 1);
        return quest.BattleEnemyCounts[battleIndex];
    }

    private int GetBattleEndIndex(QuestRuntimeData quest, int battleNumber)
    {
        int startIndex = GetBattleStartIndex(quest, battleNumber);
        return Mathf.Min(startIndex + GetBattleEnemyCount(quest, battleNumber), quest.Enemies.Count);
    }

    private enum QuestState
    {
        Waiting = 0,
        Recruiting = 1,
        InProgress = 2,
        BattleTransition = 3,
        Completed = 4
    }

    private sealed class QuestRuntimeData
    {
        public readonly int Id;
        public readonly QuestPanelView Panel;
        public readonly int LayoutSlot;
        public readonly List<EnemyRuntimeData> Enemies = new List<EnemyRuntimeData>();
        public readonly List<QuestHeroRuntimeData> Heroes = new List<QuestHeroRuntimeData>();
        public readonly List<int> BattleStartIndices = new List<int>();
        public readonly List<int> BattleEnemyCounts = new List<int>();
        public readonly List<float> ProgressSegmentDurations = new List<float>();
        public readonly int MaxHeroSlots;

        public QuestState State;
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

        public QuestRuntimeData(int id, QuestPanelView panel, int layoutSlot, int maxHeroSlots)
        {
            Id = id;
            Panel = panel;
            LayoutSlot = layoutSlot;
            MaxHeroSlots = Mathf.Clamp(maxHeroSlots, 1, 3);
            State = QuestState.Waiting;
        }
    }

    private sealed class QuestHeroRuntimeData
    {
        public readonly HeroRuntimeData Hero;

        public SecondaryStatsSnapshot SecondaryStats;
        public float Hp;
        public float MaxHp;
        public float Damage;
        public float AttackInterval;
        public float AttackProgress;
        public int TargetEnemyIndex = -1;

        public QuestHeroRuntimeData(HeroRuntimeData hero)
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

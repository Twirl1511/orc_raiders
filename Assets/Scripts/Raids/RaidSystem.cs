using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class RaidSystem : MonoBehaviour
{
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

    [Header("Layout")]
    [SerializeField] private Vector2 _firstPanelAnchoredPosition = new Vector2(20f, -20f);
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
        UpdateRaidSpawn(deltaTime);

        for (int i = _raids.Count - 1; i >= 0; i--)
        {
            UpdateRaid(_raids[i], deltaTime);
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
            _goldText == null)
        {
            throw new System.InvalidOperationException($"{nameof(RaidSystem)} requires scene references.");
        }
    }

    private void UpdateRaidSpawn(float deltaTime)
    {
        _newRaidTimer -= deltaTime;

        if (_newRaidTimer > 0f)
        {
            return;
        }

        CreateRaid();
        _newRaidTimer = _raidConfig.NewRaidIntervalSeconds;
    }

    private void UpdateRaid(RaidRuntimeData raid, float deltaTime)
    {
        switch (raid.State)
        {
            case RaidState.Waiting:
                raid.WaitingSeconds += deltaTime;
                raid.Panel.ShowWaiting(raid.Id, raid.WaitingSeconds);
                break;
            case RaidState.InProgress:
                UpdateRaidBattle(raid, deltaTime);
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
        panel.ShowWaiting(raid.Id, raid.WaitingSeconds);
    }

    private void StartRaid(RaidRuntimeData raid, OrcRuntimeData orcData)
    {
        raid.State = RaidState.InProgress;
        raid.Orc = orcData;
        raid.OrcSecondaryStats = _statsConfig.CalculateSecondaryStats(orcData.Stats);
        orcData.SetMaxHp(Mathf.Max(1f, raid.OrcSecondaryStats.MaxHp), false);
        raid.OrcMaxHp = orcData.MaxHp;
        raid.OrcHp = Mathf.Clamp(orcData.CurrentHp, 0f, raid.OrcMaxHp);
        raid.OrcAttackInterval = Mathf.Max(0.01f, raid.OrcSecondaryStats.AttackSpeed);
        raid.OrcDamage = Mathf.Max(1f, raid.OrcSecondaryStats.MeleeDamage);
        raid.OrcAttackProgress = 0f;
        raid.KilledEnemies = 0;
        raid.TotalGold = Random.Range(_raidConfig.MinGoldReward, _raidConfig.MaxGoldReward + 1);
        raid.GoldFound = 0;

        GenerateEnemies(raid);
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

    private void UpdateRaidBattle(RaidRuntimeData raid, float deltaTime)
    {
        if (raid.OrcHp <= 0f)
        {
            CompleteRaid(raid, false);
            return;
        }

        if (!HasAliveEnemyInCurrentBattle(raid))
        {
            AdvanceBattleOrCompleteRaid(raid);
            return;
        }

        UpdateOrcAttack(raid, deltaTime);
        UpdateEnemyAttacks(raid, deltaTime);

        if (raid.State != RaidState.InProgress)
        {
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
            RefreshGoldFound(raid);
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

    private void AdvanceBattleOrCompleteRaid(RaidRuntimeData raid)
    {
        raid.CurrentBattleStartIndex += _raidConfig.MaxEnemiesPerBattle;

        if (raid.CurrentBattleStartIndex >= raid.Enemies.Count)
        {
            CompleteRaid(raid, true);
            return;
        }

        raid.CurrentBattleNumber++;
        raid.OrcAttackProgress = 0f;
        RefreshRaidBattleUi(raid);
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
            RefreshGoldFound(raid);
        }

        _gold += raid.GoldFound;
        RefreshGoldText();

        if (raid.Orc != null)
        {
            raid.Orc.SetCurrentHp(raid.OrcHp);
            _orcBirthSystem.SetOrcState(raid.Orc, success ? OrcActivityState.OnBase : OrcActivityState.Resting);
        }

        string message = success ? "Орк прошел рейд и вернулся на базу." : "Орк проиграл бой и ушел отдыхать.";
        raid.Panel.ShowCompleted(raid.Id, success, message, raid.KilledEnemies, raid.Enemies.Count, raid.GoldFound);
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
            raid.GoldFound,
            _enemyViewData);
    }

    private void RefreshGoldFound(RaidRuntimeData raid)
    {
        int totalEnemies = Mathf.Max(1, raid.Enemies.Count);
        float progress = Mathf.Clamp01(raid.KilledEnemies / (float)totalEnemies);
        raid.GoldFound = Mathf.RoundToInt(raid.TotalGold * progress);
    }

    private void RefreshGoldText()
    {
        _goldText.text = $"Золото: {_gold}";
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
        Completed = 2
    }

    private sealed class RaidRuntimeData
    {
        public readonly int Id;
        public readonly RaidPanelView Panel;
        public readonly int LayoutSlot;
        public readonly List<EnemyRuntimeData> Enemies = new List<EnemyRuntimeData>();

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
        public int TotalGold;
        public int GoldFound;

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

        public float Hp;
        public float AttackProgress;

        public EnemyRuntimeData(EnemyDefinition definition, SecondaryStatsSnapshot secondaryStats, float maxHp)
        {
            DisplayName = definition.DisplayName;
            MaxHp = Mathf.Max(1f, maxHp);
            Hp = MaxHp;
            Damage = Mathf.Max(1f, Mathf.Max(secondaryStats.MeleeDamage, secondaryStats.RangedDamage));
            AttackInterval = Mathf.Max(0.01f, definition.AttackIntervalSeconds);
        }
    }
}

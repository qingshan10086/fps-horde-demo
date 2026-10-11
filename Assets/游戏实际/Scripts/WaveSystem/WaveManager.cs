using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DSH.WaveSystem
{
    /// <summary>
    /// 波次生成管理器（FPS 割草 / Wave Shooter 通用）
    ///
    /// 设计要点：
    ///   - 所有怪物都走内部对象池：单例池 <预制体, Queue<GameObject>>，避免 Instantiate/Destroy 抖动。
    ///   - 怪物上自动挂 PooledEnemy，监听你现有 Health.OnDied 事件，死亡后自动归还池子。
    ///   - 归还时统一走 ResetInstanceState()：位置/旋转/速度/动画/血量全部复位，
    ///     并且反射调用项目里可能存在的 IResetable.ResetState() 之类的自定义重置方法（零编译耦合）。
    ///
    /// 最简接入步骤：
    ///   1. 场景里新建空物体 "WaveManager"，挂上本脚本；
    ///   2. 把怪物预制体拖到对应的 EnemyEntry.prefab；
    ///   3. 在场景里放几个空物体当生成点，拖进 spawnPoints；
    ///   4. waves 列表里配置每一波的怪种/数量/节奏/完成条件；
    ///   5. Play，调用 StartWaves() 或直接勾选 autoStartOnEnable。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DSH/Wave System/Wave Manager")]
    public class WaveManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        //  Inspector：生成点
        // ─────────────────────────────────────────────────────────────

        [Header("生成点 (Spawn Points)")]
        [Tooltip("怪物生成位置。留空则退化为使用本物体自身的位置。")]
        public List<Transform> spawnPoints = new List<Transform>();

        [Tooltip("生成点随机选择时使用的随机种子（0 = 使用 Unity 全局随机）。")]
        public int randomSeed = 0;

        // ─────────────────────────────────────────────────────────────
        //  Inspector：流程控制
        // ─────────────────────────────────────────────────────────────

        [Header("波次流程 (Flow)")]
        [Tooltip("波次列表：数组顺序就是推进顺序。")]
        public List<WaveData> waves = new List<WaveData>();

        [Min(0f)]
        [Tooltip("两波之间的间隔（秒）。上一次『完成』到下一次『开始』之间等待这么久。")]
        public float interWaveDelay = 5f;

        [Tooltip("勾选后：上一波完成后自动开始下一波；否则需要手动调用 StartNextWave()。")]
        public bool autoAdvance = true;

        [Min(0f)]
        [Tooltip("全部波次开始之前的前置延迟（秒）。")]
        public float startDelay = 0f;

        [Tooltip("全部波次打完后，是否从头循环（无尽的割草模式）。")]
        public bool loopWaves = false;

        [Tooltip("勾选后，OnEnable 时自动开始第一波。")]
        public bool autoStartOnEnable = false;

        [Tooltip("勾选后，波次中会输出详细日志（生成/回收/完成条件判定）。")]
        public bool verboseLog = false;

        // ─────────────────────────────────────────────────────────────
        //  Inspector：对象池
        // ─────────────────────────────────────────────────────────────

        [Header("对象池 (Object Pool)")]
        [Tooltip("池对象统一挂到这个父节点下，保持 Hierarchy 干净。留空则使用本物体。")]
        public Transform poolRoot;

        [Tooltip("开启后，游戏开始时为 waves 里出现过的怪种各预热 prewarmCount 个实例。")]
        public bool prewarmOnStart = true;

        [Min(0)]
        [Tooltip("每个怪种进入战斗前预生成的实例数量。")]
        public int prewarmCount = 4;

        [Min(1)]
        [Tooltip("每个怪种在池中保留的最大空闲实例数，超出部分直接 Destroy，防内存膨胀。")]
        public int maxPoolSizePerPrefab = 64;

        [Min(0f)]
        [Tooltip("怪物死亡后延迟多久归还池子（给死亡动画/尸体留时间）。0 = 立即归还。\n" +
                 "注意：这段时间里怪物仍被算作『存活』，否则会提前判定波次完成。")]
        public float deathReturnDelay = 0.75f;

        [Tooltip("怪物死亡瞬间停用 NavMeshAgent。\n" +
                 "强烈建议保持开启：否则死亡延迟期内 AI 脚本若还在 Update 里调用\n" +
                 "SetDestination() / isStopped=false，会刷出\n" +
                 "『\"SetDestination\"/\"Resume\" can only be called on an active agent...』报错。")]
        public bool disableNavAgentOnDeath = true;

        [Tooltip("归还池子时把实例挪到这个远离场景的坐标隐藏起来，避免残留碰撞体/视觉残留。")]
        public bool parkReturnedInstances = true;

        public Vector3 returnedParkPosition = new Vector3(0f, -5000f, 0f);

        [Tooltip("生成时是否把实例重新挂到 poolRoot 之外（保持世界坐标）。一般保持默认关闭。")]
        public bool keepWorldPositionOnSpawn = false;

        // ─────────────────────────────────────────────────────────────
        //  Inspector：事件
        // ─────────────────────────────────────────────────────────────

        [Header("全局事件 (Events)")]
        [Tooltip("全部波次开始时触发。")]
        public UnityEvent onWavesStarted = new UnityEvent();

        [Tooltip("每一波开始前触发，参数 = 波次索引（0 基）。")]
        public UnityEvent<int> onWaveStarted = new UnityEvent<int>();

        [Tooltip("每一波完成时触发，参数 = 波次索引（0 基）。在清场之前调用。")]
        public UnityEvent<int> onWaveCompleted = new UnityEvent<int>();

        [Tooltip("所有波次全部完成时触发。")]
        public UnityEvent onAllWavesCompleted = new UnityEvent();

        [Tooltip("波次流程被 StopWaves() 打断时触发。")]
        public UnityEvent onWavesStopped = new UnityEvent();

        [Tooltip("每次生成一只怪物时触发（可用于播报、UI 抖动）。")]
        public UnityEvent<GameObject> onEnemySpawned = new UnityEvent<GameObject>();

        [Tooltip("每次回收一只怪物时触发（用于统计击杀/掉落）。")]
        public UnityEvent<GameObject> onEnemyReturned = new UnityEvent<GameObject>();

        [Tooltip("怪物死亡瞬间触发（位置仍是死亡姿态，适合生成尸体/掉落/击杀播报）。")]
        public UnityEvent<GameObject> onEnemyDied = new UnityEvent<GameObject>();

        // ─────────────────────────────────────────────────────────────
        //  运行期状态
        // ─────────────────────────────────────────────────────────────

        private readonly Dictionary<int, Queue<GameObject>> _pool = new Dictionary<int, Queue<GameObject>>();

        /// <summary>已生成、尚未归还的实例（含死亡延迟期内的实例）。</summary>
        private readonly HashSet<GameObject> _liveEnemies = new HashSet<GameObject>();

        /// <summary>本波还没放出来的生成名额。</summary>
        private readonly List<EnemyEntry> _pendingSpawns = new List<EnemyEntry>();

        private Coroutine _waveLoop;

        private int _currentWaveIndex = -1;
        private bool _waveInProgress;
        private bool _skipRequested;
        private bool _customConditionMet;
        private bool _warnedMissingSpawnPoints;
        private bool _warnedMissingHealth;
        private bool _warnedNavMeshNotBuilt;

        private float _waveStartTime;
        private float _waveStartDelayRemaining;
        private int _waveSpawnedCount;
        private int _waveTotalCount;
        private int _lastSpawnPointIndex = -1;
        private int _sequentialSpawnPointIndex;

        private static readonly List<GameObject> s_ScratchDestroy = new List<GameObject>();
        private static readonly List<GameObject> s_ScratchAliveCheck = new List<GameObject>();

        private static readonly System.Comparison<EnemyEntry> s_EntryPrefabComparer =
            (a, b) => a.prefab.GetInstanceID().CompareTo(b.prefab.GetInstanceID());

        // ─────────────────────────────────────────────────────────────
        //  公开只读属性（给 UI / 调试 / 其它系统读）
        // ─────────────────────────────────────────────────────────────

        /// <summary>当前波次索引（0 基）。未开始时为 -1。</summary>
        public int CurrentWaveIndex => _currentWaveIndex;

        /// <summary>当前波次配置，未开始时为 null。</summary>
        public WaveData CurrentWave =>
            _currentWaveIndex >= 0 && _currentWaveIndex < waves.Count ? waves[_currentWaveIndex] : null;

        /// <summary>配置的波次总数。</summary>
        public int TotalWaves => waves.Count;

        /// <summary>当前波是否正在进行中（包含波间延迟阶段为 false）。</summary>
        public bool IsWaveInProgress => _waveInProgress;

        /// <summary>是否还有下一波可打。</summary>
        public bool HasNextWave => loopWaves || _currentWaveIndex + 1 < waves.Count;

        /// <summary>尚未生成的怪物数量（本波）。</summary>
        public int PendingSpawnCount => _pendingSpawns.Count;

        /// <summary>已生成、尚未归还的对象池实例数量（含死亡延迟中的怪物）。</summary>
        public int LiveEnemyCount => _liveEnemies.Count;

        /// <summary>真正还活着的怪物数量（扣除死亡延迟中的尸体）。给 UI 显示用。</summary>
        public int AliveEnemyCount
        {
            get
            {
                int alive = 0;
                s_ScratchAliveCheck.Clear();

                foreach (var enemy in _liveEnemies)
                {
                    if (enemy == null) { s_ScratchAliveCheck.Add(enemy); continue; }
                    var pooled = enemy.GetComponent<PooledEnemy>();
                    if (pooled != null && pooled.IsLive) alive++;
                }

                // 外部把怪物 Destroy 掉时，顺手从存活集合里剔除，避免统计残留。
                for (int i = 0; i < s_ScratchAliveCheck.Count; i++) _liveEnemies.Remove(s_ScratchAliveCheck[i]);
                s_ScratchAliveCheck.Clear();

                return alive;
            }
        }

        /// <summary>本波已进行的秒数（未开始为 0）。</summary>
        public float WaveElapsed => _waveInProgress ? Time.time - _waveStartTime : 0f;

        /// <summary>本波剩余时间（无 TimeLimit 条件时返回 -1）。</summary>
        public float WaveTimeRemaining
        {
            get
            {
                var wave = CurrentWave;
                if (wave == null || (wave.conditions & WaveConditionFlags.TimeLimit) == 0) return -1f;
                return Mathf.Max(0f, wave.timeLimit - WaveElapsed);
            }
        }

        /// <summary>本波已经生成的数量。</summary>
        public int WaveSpawnedCount => _waveSpawnedCount;

        /// <summary>本波计划生成的总数量。</summary>
        public int WaveTotalCount => _waveTotalCount;

        /// <summary>生成前倒计时（秒），没有延迟时返回 0。</summary>
        public float StartDelayRemaining
        {
            get
            {
                if (_waveInProgress) return 0f;
                return Mathf.Max(0f, _waveStartDelayRemaining);
            }
        }

        /// <summary>当前池中空闲实例总数。</summary>
        public int PooledIdleCount
        {
            get
            {
                int total = 0;
                foreach (var pair in _pool) total += pair.Value.Count;
                return total;
            }
        }

        /// <summary>死亡延迟是否已过（用于把「尸体」排除在完成判定之外）。</summary>
        public float DeathReturnDelay => deathReturnDelay;

        /// <summary>格式化后的调试字符串，可直接丢给 UI Text 显示。</summary>
        public string GetDebugInfo()
        {
            var wave = CurrentWave;
            string name = wave != null && !string.IsNullOrEmpty(wave.waveName) ? wave.waveName : "-";

            return string.Format(
                "Wave {0}/{1} ({2})\n" +
                "State: {3}\n" +
                "Alive: {4}   Live: {5}   Pending: {6}\n" +
                "Progress: {7}/{8}\n" +
                "Elapsed: {9:F1}s   Remaining: {10}\n" +
                "Pooled: {11}",
                _currentWaveIndex + 1,
                waves.Count,
                name,
                _waveInProgress ? "Fighting" : (StartDelayRemaining > 0f ? "Countdown " + StartDelayRemaining.ToString("F1") + "s" : "Idle"),
                AliveEnemyCount,
                _liveEnemies.Count,
                _pendingSpawns.Count,
                _waveSpawnedCount,
                _waveTotalCount,
                WaveElapsed,
                WaveTimeRemaining < 0f ? "N/A" : WaveTimeRemaining.ToString("F1") + "s",
                PooledIdleCount);
        }

        // ─────────────────────────────────────────────────────────────
        //  Unity 生命周期
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (poolRoot == null) poolRoot = transform;
            if (randomSeed != 0) UnityEngine.Random.InitState(randomSeed);
        }

        private void OnEnable()
        {
            if (autoStartOnEnable) StartWaves();
        }

        private void OnDisable()
        {
            // 组件被关掉时停掉协程，避免悬空引用；对象池与存活怪物保留，重新启用可继续。
            StopAllManagedCoroutines();
        }

        private void OnDestroy()
        {
            StopAllManagedCoroutines();
        }

        private void Update()
        {
            if (_waveInProgress) return;
            if (_waveStartDelayRemaining > 0f)
            {
                _waveStartDelayRemaining = Mathf.Max(0f, _waveStartDelayRemaining - Time.deltaTime);
            }
        }

        private void OnValidate()
        {
            // 配置纠错：随机间隔上下限写反时自动纠正。
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null) continue;
                if (wave.minSpawnInterval > wave.maxSpawnInterval)
                {
                    wave.maxSpawnInterval = wave.minSpawnInterval;
                }
                wave.timeLimit = Mathf.Max(0f, wave.timeLimit);
            }
        }

        private void StopAllManagedCoroutines()
        {
            if (_waveLoop != null) { StopCoroutine(_waveLoop); _waveLoop = null; }
            _waveInProgress = false;
        }

        // ─────────────────────────────────────────────────────────────
        //  公开 API：波次流程
        // ─────────────────────────────────────────────────────────────

        /// <summary>从第一波开始跑整个流程（会先清掉当前场上所有怪物并重置索引）。</summary>
        public void StartWaves()
        {
            StopWavesInternal(invokeEvent: false, resetIndex: true);
            _waveLoop = StartCoroutine(WaveLoop());
        }

        /// <summary>
        /// 手动开始下一波。
        ///   - 流程已经在跑：强制结束当前波 / 跳过波间等待，立刻推进到下一波；
        ///   - 流程没在跑（autoAdvance = false 的挂起状态、或从未开始）：启动循环并跳到下一波。
        /// </summary>
        public void StartNextWave()
        {
            if (_waveLoop == null)
            {
                // 流程没在跑：检查还有没有下一波可打。
                if (!loopWaves && _currentWaveIndex + 1 >= waves.Count) return;

                StopWavesInternal(invokeEvent: false, resetIndex: false);
                _waveLoop = StartCoroutine(WaveLoop(skipToNext: true));
                return;
            }

            // 流程在跑：打标，让 WaveLoop 自己收尾，避免并发协程。
            _skipRequested = true;
            if (_waveInProgress) _waveInProgress = false;   // 打断完成条件等待
        }

        /// <summary>停止波次流程（清场，保留对象池内容）。</summary>
        public void StopWaves()
        {
            StopWavesInternal(invokeEvent: true, resetIndex: false);
        }

        /// <summary>重置到「未开始」状态，并清空所有怪物的状态与池子。</summary>
        public void ResetWaves(bool destroyPools = false)
        {
            StopWavesInternal(invokeEvent: false, resetIndex: true);
            CleanupAllEnemies();
            if (destroyPools) ClearPoolInternal(destroyInstances: true);
        }

        /// <summary>强制结束当前波（视为「完成」），会触发完成事件并清场。</summary>
        public void ForceCompleteWave()
        {
            var wave = CurrentWave;
            if (!_waveInProgress || wave == null) return;
            if (!wave.allowManualComplete)
            {
                Log("当前波不允许手动结束（allowManualComplete = false）。");
                return;
            }
            _waveInProgress = false;
        }

        /// <summary>本波配置了 Custom 条件时，由外部系统调用一次来满足该条件。</summary>
        public void CompleteCustomCondition()
        {
            if (!_waveInProgress) return;
            _customConditionMet = true;
            CurrentWave?.onCustomCondition?.Invoke();
            Log("自定义完成条件已满足。");
        }

        /// <summary>立刻清掉场上所有怪物（全部归还池子），并把它们当作已击杀处理。</summary>
        public void CleanupAllEnemies()
        {
            s_ScratchDestroy.Clear();
            foreach (var enemy in _liveEnemies)
            {
                if (enemy != null) s_ScratchDestroy.Add(enemy);
            }

            for (int i = 0; i < s_ScratchDestroy.Count; i++)
            {
                ReturnEnemy(s_ScratchDestroy[i], countAsKilled: false);
            }
            s_ScratchDestroy.Clear();
        }

        /// <summary>清空对象池（destroyPools = true 时把空闲实例也 Destroy 掉）。</summary>
        public void ClearPool(bool destroyInstances = true)
        {
            ClearPoolInternal(destroyInstances);
        }

        /// <summary>为一个预制体预热指定数量的实例。</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance(prefab);
                if (instance == null) break;

                // 预热出的实例直接进池：先复位成「未生成」状态，再入队。
                ResetInstanceState(instance, instance.transform.position, Quaternion.identity);
                DropToPool(instance);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  内部：波次主循环
        // ─────────────────────────────────────────────────────────────

        private IEnumerator WaveLoop(bool skipToNext = false)
        {
            // ── 前置延迟 ──
            float delay = Mathf.Max(0f, startDelay);
            if (delay > 0f)
            {
                _waveStartDelayRemaining = delay;
                yield return new WaitForSeconds(delay);
            }
            _waveStartDelayRemaining = 0f;
            _skipRequested = false;

            if (skipToNext && _currentWaveIndex + 1 < waves.Count) _currentWaveIndex++;

            onWavesStarted?.Invoke();

            while (true)
            {
                // 1. 选中下一波
                int nextIndex = _currentWaveIndex + 1;
                if (nextIndex >= waves.Count)
                {
                    if (!loopWaves) break;         // 全部完成
                    nextIndex = 0;                 // 循环模式
                }
                _currentWaveIndex = nextIndex;

                var wave = waves[_currentWaveIndex];

                // 空配置：跳过，但如果整轮都跑完了还在遇到空配置，说明列表里全是空的，直接收尾。
                if (wave == null)
                {
                    Debug.LogWarning($"[WaveManager] 第 {_currentWaveIndex + 1} 波配置为空，已跳过。", this);
                    if (!loopWaves && _currentWaveIndex >= waves.Count - 1)
                    {
                        onAllWavesCompleted?.Invoke();
                        break;
                    }
                    continue;
                }

                // 2. 波内额外延迟
                if (wave.startDelay > 0f) yield return new WaitForSeconds(wave.startDelay);

                // 3. 执行本波
                yield return RunSingleWave(_currentWaveIndex, wave);

                // 4. 最后归一化的兜底清场（正常情况下 RunSingleWave 已完成）
                CleanupAllEnemies();

                // 5. 全部完成？
                bool isLast = !loopWaves && _currentWaveIndex >= waves.Count - 1;
                if (isLast)
                {
                    Log("所有波次已完成。");
                    onAllWavesCompleted?.Invoke();
                    break;
                }

                // 6. 下一波之前：
                //    - autoAdvance = true  → 等待 interWaveDelay 秒（期间调用 StartNextWave() 可跳过等待）
                //    - autoAdvance = false → 挂起，直到外部调用 StartNextWave() 或把 autoAdvance 改回 true
                if (!autoAdvance)
                {
                    Log("autoAdvance = false，等待手动调用 StartNextWave()。");
                    while (!autoAdvance && !_skipRequested) yield return null;
                    _skipRequested = false;
                }
                else if (interWaveDelay > 0f)
                {
                    // 由 Update() 统一倒计时（这样 Inspector 里能实时看到），这里只负责等待。
                    _waveStartDelayRemaining = interWaveDelay;
                    while (_waveStartDelayRemaining > 0f && !_skipRequested) yield return null;
                    _waveStartDelayRemaining = 0f;
                    _skipRequested = false;
                }
            }

            _waveInProgress = false;
            _waveStartDelayRemaining = 0f;
            _skipRequested = false;
            _waveLoop = null;
        }

        /// <summary>跑完一整波：刷怪 → 等待完成条件 → 触发事件 → 清场。返回时本波已彻底结束。</summary>
        private IEnumerator RunSingleWave(int waveIndex, WaveData wave)
        {
            // ── 初始化本波状态 ──
            _waveInProgress = true;
            _waveStartTime = Time.time;
            _customConditionMet = false;
            _waveSpawnedCount = 0;
            _waveTotalCount = wave.TotalEnemyCount;

            BuildPendingSpawns(wave);

            Log($"波次 {waveIndex + 1} 开始：共 {_waveTotalCount} 只，模式 {wave.spawnMode}，条件 {wave.conditions}。");
            wave.onWaveStarted?.Invoke(waveIndex);
            onWaveStarted?.Invoke(waveIndex);

            if (_waveTotalCount == 0)
            {
                Debug.LogWarning($"[WaveManager] 波次 {waveIndex + 1} 没有配置任何有效怪物，直接视为完成。", this);
            }

            // ── 生成阶段 ──
            yield return SpawnPhase(wave);

            // ── 完成判定阶段 ──
            while (_waveInProgress)
            {
                if (IsWaveComplete(wave)) break;
                yield return null;
            }

            // ── 本波结束 ──
            _waveInProgress = false;
            Log($"波次 {waveIndex + 1} 完成（耗时 {Time.time - _waveStartTime:F1}s，共生成 {_waveSpawnedCount} 只）。");

            // 先发完成事件，再清场 —— 让监听者能读到「这一波最终的存活数」。
            wave.onWaveCompleted?.Invoke(waveIndex);
            onWaveCompleted?.Invoke(waveIndex);

            // ── 清场：超时/自定义条件下剩余的怪物全部回收 ──
            if (_liveEnemies.Count > 0)
            {
                Log($"波次 {waveIndex + 1} 清场：回收 {_liveEnemies.Count} 只残留怪物。");
                CleanupAllEnemies();
            }

            _pendingSpawns.Clear();
        }

        /// <summary>
        /// 按 spawnMode 把本波所有名额生成完毕。
        /// 注意：每一轮都会检查 _waveInProgress —— 玩家点了「立即下一波」时，本波会立刻收尾，
        /// 剩余名额不再生成。
        /// </summary>
        private IEnumerator SpawnPhase(WaveData wave)
        {
            switch (wave.spawnMode)
            {
                case SpawnMode.Simultaneous:
                    {
                        int layoutIndex = 0;
                        int total = _pendingSpawns.Count;

                        while (_pendingSpawns.Count > 0 && _waveInProgress)
                        {
                            var entry = TakeNextEntry();
                            if (entry == null) break;

                            Vector3 offset = Vector3.zero;
                            Quaternion rotation = Quaternion.identity;

                            if (wave.multiSpawnLayout == MultiSpawnLayout.Ring && total > 1)
                            {
                                float angle = 360f * layoutIndex / total;
                                offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * wave.multiSpawnRadius);
                                rotation = Quaternion.Euler(0f, angle, 0f);
                            }

                            SpawnOne(entry.prefab, offset, rotation);
                            layoutIndex++;

                            // 一帧内不要无限循环，分帧生成避免卡顿峰值。
                            if (layoutIndex % 32 == 0) yield return null;
                        }
                        break;
                    }

                case SpawnMode.RandomInterval:
                    {
                        while (_pendingSpawns.Count > 0 && _waveInProgress)
                        {
                            float wait = UnityEngine.Random.Range(
                                Mathf.Min(wave.minSpawnInterval, wave.maxSpawnInterval),
                                Mathf.Max(wave.minSpawnInterval, wave.maxSpawnInterval));

                            if (wait > 0f) yield return new WaitForSeconds(wait);
                            if (!_waveInProgress) break;

                            var entry = TakeNextEntry();
                            if (entry == null) break;
                            SpawnOne(entry.prefab, Vector3.zero, Quaternion.identity);
                        }
                        break;
                    }

                case SpawnMode.Sequential:
                default:
                    {
                        bool first = true;
                        while (_pendingSpawns.Count > 0 && _waveInProgress)
                        {
                            if (!first && wave.spawnInterval > 0f)
                            {
                                yield return new WaitForSeconds(wave.spawnInterval);
                            }
                            first = false;

                            if (!_waveInProgress) break;

                            var entry = TakeNextEntry();
                            if (entry == null) break;
                            SpawnOne(entry.prefab, Vector3.zero, Quaternion.identity);
                        }
                        break;
                    }
            }

            _pendingSpawns.Clear();
        }

        /// <summary>完成条件判定。多个条件之间是 OR（任意一个满足即完成）。</summary>
        private bool IsWaveComplete(WaveData wave)
        {
            var conditions = wave.conditions;

            if (conditions == WaveConditionFlags.None)
            {
                // 没配条件时，退化为「所有怪物都生成了且没有存活怪物」。
                conditions = WaveConditionFlags.AllKilled;
            }

            // 生成还没结束：除了 TimeLimit，其它条件都不能算（否则怪还没出完就结束）。
            bool spawningDone = _pendingSpawns.Count == 0;

            if (spawningDone && (conditions & WaveConditionFlags.AllKilled) != 0 && _liveEnemies.Count == 0)
            {
                return true;
            }

            if ((conditions & WaveConditionFlags.TimeLimit) != 0 && WaveElapsed >= wave.timeLimit)
            {
                Log($"波次超时（{wave.timeLimit:F1}s），强制结束并清场。");
                return true;
            }

            if (spawningDone && (conditions & WaveConditionFlags.Custom) != 0 && _customConditionMet)
            {
                return true;
            }

            return false;
        }

        private void BuildPendingSpawns(WaveData wave)
        {
            _pendingSpawns.Clear();
            if (wave.enemies == null) return;

            for (int i = 0; i < wave.enemies.Count; i++)
            {
                var entry = wave.enemies[i];
                if (entry == null || !entry.IsValid)
                {
                    if (entry != null && entry.prefab == null)
                        Debug.LogWarning($"[WaveManager] 波次「{wave.waveName}」第 {i + 1} 条怪物配置缺少 prefab，已忽略。", this);
                    continue;
                }

                for (int c = 0; c < entry.count; c++) _pendingSpawns.Add(entry);
            }

            // 同时生成模式为了排布均匀，按预制体分组（同类挨在一起）。
            if (wave.spawnMode == SpawnMode.Simultaneous)
            {
                _pendingSpawns.Sort(s_EntryPrefabComparer);
            }
        }

        private EnemyEntry TakeNextEntry()
        {
            if (_pendingSpawns.Count == 0) return null;
            var entry = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            return entry;
        }

        // ─────────────────────────────────────────────────────────────
        //  内部：生成与回收
        // ─────────────────────────────────────────────────────────────

        private GameObject SpawnOne(GameObject prefab, Vector3 localOffset, Quaternion rotationOffset)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[WaveManager] 尝试生成空预制体，已忽略。", this);
                return null;
            }

            var instance = AcquireFromPool(prefab);
            if (instance == null)
            {
                Debug.LogError($"[WaveManager] 无法生成 {prefab.name}（池与实例化都失败）。", this);
                return null;
            }

            // 位置与朝向
            GetSpawnPose(out Vector3 position, out Quaternion rotation);
            position += rotation * localOffset;
            rotation *= rotationOffset;

            var pooled = instance.GetComponent<PooledEnemy>();

            // 先设位置再 SetActive，避免在原点闪一帧。
            if (keepWorldPositionOnSpawn)
            {
                instance.transform.SetParent(poolRoot, true);
            }
            else
            {
                instance.transform.SetParent(poolRoot, false);
            }

            if (!instance.activeSelf) instance.SetActive(true);

            instance.transform.SetPositionAndRotation(position, rotation);

            // 复用前复位：血量、速度、动画、自定义状态
            ResetInstanceState(instance, position, rotation);

            pooled?.MarkSpawned();
            _liveEnemies.Add(instance);
            _waveSpawnedCount++;

            onEnemySpawned?.Invoke(instance);

            if (verboseLog)
            {
                Log($"生成 #{_waveSpawnedCount}/{_waveTotalCount}：{prefab.name} @ {position}");
            }

            return instance;
        }

        /// <summary>挑选生成点，返回世界坐标位置与朝向。</summary>
        private void GetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                if (!_warnedMissingSpawnPoints)
                {
                    _warnedMissingSpawnPoints = true;
                    Debug.LogWarning("[WaveManager] 没有配置 spawnPoints，将使用 WaveManager 自身的位置作为生成点。", this);
                }
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            // 过滤掉被销毁的空引用
            int index = PickSpawnPointIndex();
            Transform point = spawnPoints[index];

            if (point == null)
            {
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            position = point.position;
            rotation = point.rotation;
        }

        private int PickSpawnPointIndex()
        {
            int count = spawnPoints.Count;
            var wave = CurrentWave;

            // 清理 null 生成点的影响：最多尝试 8 次找非 null 的
            SpawnPointMode mode = wave != null ? wave.spawnPointMode : SpawnPointMode.Random;

            int index;
            switch (mode)
            {
                case SpawnPointMode.Sequential:
                    index = _sequentialSpawnPointIndex % count;
                    _sequentialSpawnPointIndex = (index + 1) % count;
                    _lastSpawnPointIndex = index;
                    return index;

                case SpawnPointMode.AvoidLast:
                    if (count == 1) { _lastSpawnPointIndex = 0; return 0; }
                    do { index = UnityEngine.Random.Range(0, count); }
                    while (index == _lastSpawnPointIndex);
                    _lastSpawnPointIndex = index;
                    return index;

                case SpawnPointMode.Random:
                default:
                    index = UnityEngine.Random.Range(0, count);
                    _lastSpawnPointIndex = index;
                    return index;
            }
        }

        /// <summary>把怪物归还对象池。countAsKilled = true 表示这次回收来自击杀。</summary>
        internal void ReturnEnemy(GameObject instance, bool countAsKilled)
        {
            if (instance == null) return;

            if (!_liveEnemies.Remove(instance))
            {
                // 不在记录里（可能已被外部销毁）—— 仍然尝试回收，避免泄漏。
                if (verboseLog) Log($"回收未登记的怪物 {instance.name}（可能已被外部销毁）。");
            }

            var pooled = instance.GetComponent<PooledEnemy>();
            pooled?.MarkReturned();

            if (countAsKilled) onEnemyReturned?.Invoke(instance);

            // 归还前先停掉 NavMeshAgent：SetActive(false) 之后引擎内部的 agent 句柄会失效，
            // 残留的协程/事件若再碰它就会报错。
            DisableNavMeshAgent(instance);

            // 注意：这里不做 ResetInstanceState —— 复位统一在「下次生成时」做，
            // 死亡姿态的位置信息请通过 onEnemyDied 事件在死亡瞬间读取。
            if (parkReturnedInstances)
            {
                instance.transform.SetPositionAndRotation(returnedParkPosition, Quaternion.identity);
            }

            DropToPool(instance);
        }

        /// <summary>实例被外部 Destroy 时由 PooledEnemy 通知，避免统计残留。</summary>
        internal void NotifyEnemyDestroyed(GameObject instance)
        {
            if (instance == null) return;
            _liveEnemies.Remove(instance);
        }

        /// <summary>怪物死亡瞬间（位置仍为死亡姿态时）由 PooledEnemy 转发。</summary>
        internal void NotifyEnemyDied(GameObject instance)
        {
            if (instance == null) return;

            // 死亡瞬间就停掉 NavMeshAgent：
            // 死亡延迟期内 GameObject 仍然是激活的，此时 AI 脚本若还在 Update 里调用
            // SetDestination() 或 isStopped=false（内部走 Resume），会刷出
            // 『"SetDestination"/"Resume" can only be called on an active agent...』报错。
            if (disableNavAgentOnDeath) DisableNavMeshAgent(instance);

            onEnemyDied?.Invoke(instance);
        }

        /// <summary>停用实例上的 NavMeshAgent（失败不影响其它流程）。</summary>
        private static void DisableNavMeshAgent(GameObject instance)
        {
            var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.enabled) return;

            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = false;
        }

        private GameObject AcquireFromPool(GameObject prefab)
        {
            int key = prefab.GetInstanceID();

            if (!_pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>();
                _pool[key] = queue;
            }

            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();
                if (candidate == null) continue;      // 被外部销毁，跳过
                if (!candidate.activeSelf) return candidate;

                // 理论上不该出现（池里都是未激活的），兜底先停用再用。
                candidate.SetActive(false);
                return candidate;
            }

            return CreateInstance(prefab);
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var instance = Instantiate(prefab, poolRoot);
            if (instance == null) return null;

            instance.name = prefab.name + " (Pooled)";

            var pooled = instance.GetComponent<PooledEnemy>();
            if (pooled == null) pooled = instance.AddComponent<PooledEnemy>();
            pooled.Initialize(this, prefab);

            if (instance.activeSelf) instance.SetActive(false);
            return instance;
        }

        private void DropToPool(GameObject instance)
        {
            if (instance == null) return;

            var pooled = instance.GetComponent<PooledEnemy>();
            GameObject prefab = pooled != null ? pooled.SourcePrefab : null;

            if (prefab == null)
            {
                // 找不到来源预制体：无法归池，直接销毁，避免幽灵对象。
                if (verboseLog) Log($"无法确定 {instance.name} 的来源预制体，直接销毁。");
                Destroy(instance);
                return;
            }

            int key = prefab.GetInstanceID();
            if (!_pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>();
                _pool[key] = queue;
            }

            // 超容量裁剪：从队首丢掉最旧的实例。
            while (queue.Count >= maxPoolSizePerPrefab)
            {
                var oldest = queue.Dequeue();
                if (oldest != null) Destroy(oldest);
            }

            instance.transform.SetParent(poolRoot, false);
            if (instance.activeSelf) instance.SetActive(false);

            queue.Enqueue(instance);
        }

        /// <summary>
        /// 复用前复位实例状态：位置、旋转、刚体速度、动画器、血量、以及项目里自定义的 IResetable.ResetState()。
        /// 通过缓存 + 反射做到「有就调用、没有就跳过」，不会因为缺少某个类型而编译失败。
        /// </summary>
        private void ResetInstanceState(GameObject instance, Vector3 position, Quaternion rotation)
        {
            if (instance == null) return;

            instance.transform.SetPositionAndRotation(position, rotation);

            // 1) 刚体 / 角色控制器：清掉上一轮残留的速度与下落状态
            var body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
#if UNITY_6000_0_OR_NEWER
                    body.linearVelocity = Vector3.zero;
#else
                    body.velocity = Vector3.zero;
#endif
                }
                body.angularVelocity = Vector3.zero;
            }

            // 兼容旧输入系统 / 遗留 CharacterController 角色
            var cc = instance.GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;   // 必须先禁用，否则传送会被 CharacterController 拒绝
                instance.transform.SetPositionAndRotation(position, rotation);
                cc.enabled = true;
            }

            // 2) NavMeshAgent：必须先「停用 → 传送 → 启用」，
            //    原因：agent 处于启用状态时引擎每帧都会把物体拉回 NavMesh，
            //    直接 SetPositionAndRotation 会被覆盖；而且对未激活/离开 NavMesh 的 agent
            //    调用 SetDestination()/Resume() 会刷出
            //    『"SetDestination"/"Resume" can only be called on an active agent...』。
            ResetNavMeshAgent(instance, position, rotation);

            // 3) 动画器：回到默认状态，避免复用后卡在死亡姿势
            var animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null || animator.runtimeAnimatorController == null) continue;

                animator.Rebind();
                animator.Update(0f);
                animator.applyRootMotion = false;
                animator.speed = 1f;
            }

            // 4) Health：复活（血量回满、isDead 复位、闪红材质还原）
            var health = instance.GetComponent<Health>();
            if (health == null) health = instance.GetComponentInChildren<Health>(true);

            if (health != null)
            {
                if (health.isInstanceDestroy) health.isInstanceDestroy = false;  // 池化对象不能被 Destroy
                health.Revive();
            }
            else if (!_warnedMissingHealth)
            {
                _warnedMissingHealth = true;
                Debug.LogWarning("[WaveManager] 生成的怪物预制体上没有 Health 组件，" +
                                 "无法自动检测死亡/复位血量。请参考 README 接入 Health 或自行处理回收。", this);
            }

            // 5) 自定义重置：兼容你项目里的 IResetable 接口（零编译耦合，靠反射调用）
            InvokeCustomReset(instance);
        }

        /// <summary>
        /// NavMeshAgent 复位：停用 → 传送 → 重新启用 → 用 Warp 精确落到 NavMesh 上。
        ///
        /// 关键点：
        ///   - agent 启用状态下令牌被引擎接管，直接改 transform 会被拉回去，所以必须先禁用；
        ///   - Warp 之后才重新启用，避免启用瞬间用「旧位置」重新贴 NavMesh；
        ///   - 生成点如果不在 NavMesh 上（或场景没烘培 NavMesh），这里会给出一次明确警告，
        ///     否则玩家看到的现象是「怪物卡在原地不动」——比报错更难查。
        /// </summary>
        private void ResetNavMeshAgent(GameObject instance, Vector3 position, Quaternion rotation)
        {
            var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) return;

            if (!agent.enabled) agent.enabled = true;

            // 关闭自动位移，这样后续的人工地放置不会被引擎覆盖。
            agent.updatePosition = false;

            // Warp 在 agent 未激活时会被忽略，所以放在启用之后。
            agent.Warp(position);

            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;

            // 生成点必须落在 NavMesh 上，否则这波怪会原地发呆。
            if (!agent.isOnNavMesh && !_warnedNavMeshNotBuilt)
            {
                _warnedNavMeshNotBuilt = true;
                Debug.LogWarning(
                    "[WaveManager] 生成的 NavMeshAgent 不在 NavMesh 上。请检查：" +
                    "① 场景是否已烘焙 NavMesh；② 生成点是否离 NavMesh 太远（Warp 搜索半径有限）；" +
                    "③ 怪物的 Agent Type / 半径是否导致它无法落地。", this);
            }
        }

        private static readonly string[] k_ResetInterfaceNames = { "IResetable", "IResettable" };
        private static readonly string[] k_ResetMethodNames = { "ResetState", "ResetToDefault", "ResetSelf" };

        private readonly Dictionary<Type, System.Reflection.MethodInfo> _resetMethodCache =
            new Dictionary<Type, System.Reflection.MethodInfo>();

        private void InvokeCustomReset(GameObject instance)
        {
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null) continue;

                var type = behaviour.GetType();

                if (!_resetMethodCache.TryGetValue(type, out var method))
                {
                    method = FindResetMethod(type);
                    _resetMethodCache[type] = method;   // null 也会被缓存，避免重复反射
                }

                if (method == null) continue;

                try
                {
                    method.Invoke(behaviour, null);
                    if (verboseLog) Log($"自定义重置：{type.Name}.{method.Name}()");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WaveManager] 调用 {type.Name}.{method.Name}() 失败：{e.Message}", instance);
                }
            }
        }

        private static System.Reflection.MethodInfo FindResetMethod(Type type)
        {
            // 优先找实现了已知重置接口的方法
            var interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                string interfaceName = interfaces[i].Name;
                bool isResetInterface = false;
                for (int n = 0; n < k_ResetInterfaceNames.Length; n++)
                {
                    if (interfaceName == k_ResetInterfaceNames[n]) { isResetInterface = true; break; }
                }
                if (!isResetInterface) continue;

                var map = type.GetInterfaceMap(interfaces[i]);
                for (int m = 0; m < map.InterfaceMethods.Length; m++)
                {
                    if (map.InterfaceMethods[m].GetParameters().Length == 0)
                        return map.TargetMethods[m];
                }
            }

            // 退化：直接找名字匹配的无参公开方法
            for (int n = 0; n < k_ResetMethodNames.Length; n++)
            {
                var method = type.GetMethod(
                    k_ResetMethodNames[n],
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                    null, Type.EmptyTypes, null);
                if (method != null) return method;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────
        //  内部：杂项
        // ─────────────────────────────────────────────────────────────

        private void StopWavesInternal(bool invokeEvent, bool resetIndex)
        {
            bool wasRunning = _waveLoop != null || _waveInProgress;

            if (_waveLoop != null) { StopCoroutine(_waveLoop); _waveLoop = null; }

            _waveInProgress = false;
            _skipRequested = false;
            _waveStartDelayRemaining = 0f;
            _pendingSpawns.Clear();

            if (resetIndex) _currentWaveIndex = -1;

            CleanupAllEnemies();

            if (invokeEvent && wasRunning) onWavesStopped?.Invoke();
        }

        private void ClearPoolInternal(bool destroyInstances)
        {
            foreach (var pair in _pool)
            {
                var queue = pair.Value;
                while (queue.Count > 0)
                {
                    var instance = queue.Dequeue();
                    if (instance != null && destroyInstances) Destroy(instance);
                }
            }
            _pool.Clear();
        }

        private void Log(string message)
        {
            if (!verboseLog) return;
            Debug.Log($"[WaveManager] {message}", this);
        }

        // ─────────────────────────────────────────────────────────────
        //  调试可视化
        // ─────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;

            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                var point = spawnPoints[i];
                if (point == null) continue;

                Gizmos.DrawWireSphere(point.position, 0.6f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.5f);
                Gizmos.DrawWireCube(point.position + Vector3.up * 1f, new Vector3(0.4f, 2f, 0.4f));

#if UNITY_EDITOR
                UnityEditor.Handles.color = new Color(1f, 0.6f, 0.2f, 1f);
                UnityEditor.Handles.Label(point.position + Vector3.up * 2.2f, $"SP {i}");
#endif
            }
        }
    }
}

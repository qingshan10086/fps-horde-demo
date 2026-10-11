using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DSH.WaveSystem
{
    /// <summary>
    /// 单次生成使用的模式。
    /// </summary>
    public enum SpawnMode
    {
        /// <summary>顺序生成：每隔 spawnInterval 秒生成一个，直到本波名额用完。</summary>
        Sequential = 0,

        /// <summary>同时生成：在一帧内把本波所有怪物一次性放出来。</summary>
        Simultaneous = 1,

        /// <summary>随机间隔生成：每个怪物的间隔在 minSpawnInterval ~ maxSpawnInterval 之间随机。</summary>
        RandomInterval = 2,
    }

    /// <summary>
    /// 生成点选择方式。
    /// </summary>
    public enum SpawnPointMode
    {
        /// <summary>在所有生成点里完全随机挑一个（默认）。</summary>
        Random = 0,

        /// <summary>尽量避开上一次用过的生成点（割草游戏里避免怪物全从同一处涌出）。</summary>
        AvoidLast = 1,

        /// <summary>按顺序轮转生成点。</summary>
        Sequential = 2,
    }

    /// <summary>
    /// 一帧内生成多个怪物时，位置的排布方式（仅 Simultaneous 模式有意义）。
    /// </summary>
    public enum MultiSpawnLayout
    {
        /// <summary>全部叠在同一点（默认，怪物会自然挤开）。</summary>
        Stack = 0,

        /// <summary>绕生成点围成一圈，半径 = multiSpawnRadius。</summary>
        Ring = 1,
    }

    /// <summary>
    /// 波次完成条件（位标记，可同时勾选多个）。
    /// </summary>
    [Flags]
    public enum WaveConditionFlags
    {
        None = 0,

        /// <summary>本波所有怪物都被击杀。</summary>
        AllKilled = 1 << 0,

        /// <summary>到达 timeLimit 秒后仍未完成，则超时结束并清场。</summary>
        TimeLimit = 1 << 1,

        /// <summary>由外部代码 / 其它系统调用 CompleteCustomCondition() 触发。</summary>
        Custom = 1 << 2,
    }

    /// <summary>
    /// 一波里要生成的怪种。count 表示这一波生成多少只该怪种。
    /// </summary>
    [Serializable]
    public class EnemyEntry
    {
        [Tooltip("怪物预制体（建议上面挂 Health / Damageable / EnemyBase）。")]
        public GameObject prefab;

        [Min(1)]
        [Tooltip("这一波要生成多少只。")]
        public int count = 1;

        [Tooltip("勾选后预生成池会为这个怪种额外预热实例（例如 Boss）。")]
        public bool prewarmPool = true;

        [Tooltip("为该怪种覆盖生成权重以外的高级玩法预算（例如精英怪标记），可选。")]
        public string tag;

        public bool IsValid => prefab != null && count > 0;
    }

    /// <summary>
    /// 单个波次的完整配置。在 WaveManager.waves 列表里逐条配置。
    /// </summary>
    [Serializable]
    public class WaveData
    {
        [Header("基础信息")]
        [Tooltip("仅用于 Inspector 阅读，以及日志输出。")]
        public string waveName = "Wave";

        [Header("怪物与数量")]
        [Tooltip("本波要生成的怪种列表，数量相加得到本波总生成数。")]
        public List<EnemyEntry> enemies = new List<EnemyEntry>();

        [Header("生成节奏")]
        public SpawnMode spawnMode = SpawnMode.Sequential;

        [Min(0f)]
        [Tooltip("Sequential 模式：每个怪物之间的固定间隔（秒）。")]
        public float spawnInterval = 0.5f;

        [Min(0f)]
        [Tooltip("RandomInterval 模式：间隔下限（秒）。")]
        public float minSpawnInterval = 0.2f;

        [Min(0f)]
        [Tooltip("RandomInterval 模式：间隔上限（秒）。")]
        public float maxSpawnInterval = 1.5f;

        [Tooltip("本波开始前的额外准备延迟（秒），会叠加在 WaveManager.interWaveDelay 之上。")]
        [Min(0f)]
        public float startDelay = 0f;

        [Header("生成位置")]
        public SpawnPointMode spawnPointMode = SpawnPointMode.Random;

        public MultiSpawnLayout multiSpawnLayout = MultiSpawnLayout.Stack;

        [Min(0f)]
        public float multiSpawnRadius = 2f;

        [Header("完成条件")]
        [Tooltip("可多选；多个条件之间是『任意一个满足即完成』（OR）。\n" +
                 "注意：只勾 TimeLimit 时，波次会在时间到时强制结束并清场。")]
        public WaveConditionFlags conditions = WaveConditionFlags.AllKilled;

        [Min(0f)]
        [Tooltip("conditions 包含 TimeLimit 时生效：本波最长持续时间（秒）。")]
        public float timeLimit = 60f;

        [Tooltip("允许玩家手动提前结束本波（Inspector 按钮 / ForceCompleteWave()）。")]
        public bool allowManualComplete = true;

        [Header("事件")]
        [Tooltip("本波开始（第一只怪生成前）触发，参数为波次索引。")]
        public UnityEvent<int> onWaveStarted = new UnityEvent<int>();

        [Tooltip("本波完成时触发，参数为波次索引。注意：清场会在此事件之后进行。")]
        public UnityEvent<int> onWaveCompleted = new UnityEvent<int>();

        [Tooltip("conditions 包含 Custom 时，由外部调用 CompleteCustomCondition() 触发一次。")]
        public UnityEvent onCustomCondition = new UnityEvent();

        /// <summary>本波总生成数量。</summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                if (enemies == null) return 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != null && enemies[i].IsValid) total += enemies[i].count;
                }
                return total;
            }
        }

        /// <summary>本波是否存在可用的怪物配置。</summary>
        public bool HasEnemies => TotalEnemyCount > 0;
    }
}

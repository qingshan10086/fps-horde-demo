using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSH.WaveSystem
{
    /// <summary>
    /// 由 WaveManager 自动挂到每个生成出来的怪物身上的桥接组件。
    /// 职责：
    ///   1. 记住自己属于哪个预制体 / 哪个管理器（回收时才知道该放回哪个池）；
    ///   2. 监听 Health 的死亡事件，死亡后自动归还对象池；
    ///   3. 暴露「是否是活跃怪物」供 WaveManager 统计存活数量。
    ///
    /// 你不需要手动添加这个组件，WaveManager 会在实例化时 AddComponent。
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledEnemy : MonoBehaviour
    {
        /// <summary>该实例来源的预制体（池的键）。</summary>
        public GameObject SourcePrefab { get; private set; }

        /// <summary>所属的波次管理器。</summary>
        public WaveManager Manager { get; private set; }

        /// <summary>是否处于「已生成、尚未归还」的状态。</summary>
        public bool IsLive { get; private set; }

        private Health _health;
        private Coroutine _deathReturnRoutine;
        private bool _subscribed;

        /// <summary>由 WaveManager 在创建实例后调用一次。</summary>
        internal void Initialize(WaveManager manager, GameObject sourcePrefab)
        {
            Manager = manager;
            SourcePrefab = sourcePrefab;

            _health = GetComponent<Health>();

            // 反向登记：让 Health 知道这个实例由对象池持有，死亡时不要 Destroy。
            if (_health != null) _health.RegisterPoolOwner(this);

            CacheAndSubscribe();
        }

        private void Awake()
        {
            // 实例化时 WaveManager 还没调用 Initialize，这里先缓存一次，Initialize 会再确认订阅。
            if (_health == null) _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            // 池化复用走的是 SetActive(true)，生命周期回调会重新触发，这里补一次订阅。
            if (Manager != null) CacheAndSubscribe();
        }

        private void CacheAndSubscribe()
        {
            if (_health == null) _health = GetComponent<Health>();

            if (_health != null && !_subscribed)
            {
                _health.OnDied += HandleDied;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            // 归还池子时取消了订阅也没关系，OnEnable 会重新订阅；
            // 但为了「池外被 Destroy」时不残留引用，这里显式退订。
            if (_health != null && _subscribed)
            {
                _health.OnDied -= HandleDied;
                _subscribed = false;
            }

            if (_deathReturnRoutine != null)
            {
                StopCoroutine(_deathReturnRoutine);
                _deathReturnRoutine = null;
            }
        }

        /// <summary>由 WaveManager 在「从池中取出、准备生成」时调用。</summary>
        internal void MarkSpawned()
        {
            IsLive = true;

            if (_deathReturnRoutine != null)
            {
                StopCoroutine(_deathReturnRoutine);
                _deathReturnRoutine = null;
            }

            CacheAndSubscribe();
        }

        /// <summary>由 WaveManager 在「归还池中」时调用。</summary>
        internal void MarkReturned()
        {
            IsLive = false;

            if (_deathReturnRoutine != null)
            {
                StopCoroutine(_deathReturnRoutine);
                _deathReturnRoutine = null;
            }
        }

        private void HandleDied()
        {
            if (Manager == null) return;

            // 死亡瞬间通知外部（此时位置/朝向还是死亡姿态，可用于尸体、掉落、计分特效）。
            Manager.NotifyEnemyDied(gameObject);

            float delay = Manager.DeathReturnDelay;
            if (delay <= 0f)
            {
                Manager.ReturnEnemy(gameObject, true);
            }
            else
            {
                _deathReturnRoutine = StartCoroutine(ReturnAfterDelay(delay));
            }
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            // 用普通时间，保证和游戏暂停/慢动作一致。
            yield return new WaitForSeconds(delay);

            _deathReturnRoutine = null;

            if (Manager != null && IsLive)
            {
                Manager.ReturnEnemy(gameObject, true);
            }
        }

        private void OnDestroy()
        {
            if (_health != null && _subscribed)
            {
                _health.OnDied -= HandleDied;
                _subscribed = false;
            }

            if (Manager != null)
            {
                Manager.NotifyEnemyDestroyed(gameObject);
            }
        }
    }
}

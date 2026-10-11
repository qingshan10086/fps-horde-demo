using System.Collections;
using System.Collections.Generic;
using DSH.WaveSystem;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 生命值系统：管理对象的血量、受伤闪红、死亡逻辑。
/// </summary>
public class Health : MonoBehaviour
{
    [Header("死亡处理")]
    [Tooltip("是否立即销毁（无死亡动画时勾选）。注意：被对象池管理的怪物会自动忽略销毁，交给池回收。")]
    public bool isInstanceDestroy;

    [Tooltip("非立即销毁时，多久后销毁尸体（秒）。池化对象不使用这个值。")]
    public float destroyDelay = 3f;

    [Header("生命值")]
    public float maxHealth = 100f;

    public bool isDead { get; private set; }

    private float _currentHealth;

    private MeshRenderer[] _meshs;
    private Animator _animator;

    private bool _isFlashingRed = false;   // 是否正在闪红
    private Coroutine _flashCoroutine;     // 当前闪红协程的引用
    private Material[] _ownedMaterials;    // 本实例克隆出来的材质（池化复用时不泄漏）
    private Color[] _baseColors;           // 各 MeshRenderer 的原始颜色

    private PooledEnemy _pooledEnemy;      // 池化标记：有它就说明这个实例归对象池管

    // ── 事件 ────────────────────────────────────────────────────

    /// <summary>受到伤害时触发（参数为实际扣除的伤害值）。</summary>
    public event UnityAction<float> OnDamaged;

    /// <summary>血量变化时触发（当前血量, 最大血量）。</summary>
    public event UnityAction<float, float> OnHealthChanged;

    /// <summary>死亡瞬间触发一次（波次系统靠它回收怪物）。</summary>
    public event UnityAction OnDied;

    /// <summary>被复活/复用复位时触发（波次系统第二次生成同一实例时）。</summary>
    public event UnityAction OnRevived;

    // ── 只读属性 ────────────────────────────────────────────────

    public float CurrentHealth => _currentHealth;
    public float HealthPercent => maxHealth <= 0f ? 0f : Mathf.Clamp01(_currentHealth / maxHealth);

    private void Awake()
    {
        _meshs = GetComponentsInChildren<MeshRenderer>(true);
        _animator = GetComponentInChildren<Animator>();
        _pooledEnemy = GetComponent<PooledEnemy>();
        _currentHealth = maxHealth;

        CacheBaseColors();
    }

    /// <summary>缓存每个 MeshRenderer 的原始颜色，用于闪红后还原（而不是硬编码白色）。</summary>
    private void CacheBaseColors()
    {
        if (_meshs == null) return;

        _baseColors = new Color[_meshs.Length];
        for (int i = 0; i < _meshs.Length; i++)
        {
            var mesh = _meshs[i];
            _baseColors[i] = (mesh != null && mesh.sharedMaterial != null)
                ? mesh.sharedMaterial.color
                : Color.white;
        }
    }

    /// <summary>给所有 MeshRenderer 上色（首次调用会克隆材质，之后复用）。</summary>
    private void ApplyColor(Color color)
    {
        if (_meshs == null) return;

        if (_ownedMaterials == null) _ownedMaterials = new Material[_meshs.Length];

        for (int i = 0; i < _meshs.Length; i++)
        {
            var mesh = _meshs[i];
            if (mesh == null) continue;

            if (_ownedMaterials[i] == null)
            {
                // 只在第一次需要改色时克隆，避免每个实例都无脑 new Material。
                _ownedMaterials[i] = new Material(mesh.sharedMaterial != null ? mesh.sharedMaterial : mesh.material);
                mesh.material = _ownedMaterials[i];
            }

            _ownedMaterials[i].color = color;
        }
    }

    /// <summary>还原成原始颜色。baseColors == true 时用缓存的原色，否则用白色（旧行为）。</summary>
    private void RestoreColor(bool useBaseColors)
    {
        if (_meshs == null) return;

        for (int i = 0; i < _meshs.Length; i++)
        {
            var mesh = _meshs[i];
            if (mesh == null) continue;

            Color color = (useBaseColors && _baseColors != null && i < _baseColors.Length)
                ? _baseColors[i]
                : Color.white;

            if (_ownedMaterials != null && i < _ownedMaterials.Length && _ownedMaterials[i] != null)
            {
                _ownedMaterials[i].color = color;
            }
            else if (mesh.material != null)
            {
                mesh.material.color = color;
            }
        }
    }

    private void OnDestroy()
    {
        // 池化实例最终被销毁时，释放我们自己克隆出来的材质，避免内存泄漏。
        if (_ownedMaterials == null) return;
        for (int i = 0; i < _ownedMaterials.Length; i++)
        {
            if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
        }
    }

    /// <summary>
    /// 由 PooledEnemy 在自身初始化时调用。
    /// 因为 PooledEnemy 是 WaveManager 实例化之后才 AddComponent 的（晚于 Health.Awake），
    /// 所以必须在这里把「这个实例归对象池管」的事实告诉 Health，否则死亡时还是会 Destroy。
    /// </summary>
    internal void RegisterPoolOwner(PooledEnemy pooledEnemy)
    {
        _pooledEnemy = pooledEnemy;
    }

    /// <summary>由 Damageable.InflictDamage() 调用。</summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;                 // 死亡后不再受伤
        if (damage <= 0f) return;

        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);

        OnDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(OnDamage());
    }

    /// <summary>直接设置血量（治疗 / 调试用）。</summary>
    public void SetHealth(float value)
    {
        if (isDead) return;

        _currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// 复活 / 池化复用复位：血量回满、清除死亡与闪红状态。
    /// 由 WaveManager 在「从对象池取出并生成」时自动调用。
    /// </summary>
    public void Revive()
    {
        Revive(maxHealth);
    }

    public void Revive(float health)
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        _isFlashingRed = false;

        // 还原闪红时改过的材质颜色
        RestoreColor(useBaseColors: true);

        _currentHealth = Mathf.Clamp(health, 0.01f, maxHealth);
        isDead = false;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnRevived?.Invoke();
    }

    /// <summary>
    /// 受伤协程：处理受伤闪红和死亡逻辑（保留你原来的节奏：0.1 秒闪红后再判定死亡）。
    /// </summary>
    IEnumerator OnDamage()
    {
        _isFlashingRed = true;
        ApplyColor(Color.red);

        yield return _waitDamageFlash;

        if (_currentHealth > 0f)
        {
            RestoreColor(useBaseColors: true);
            _isFlashingRed = false;
        }
        else if (!isDead)
        {
            Die();
        }

        _flashCoroutine = null;
    }

    /// <summary>死亡处理：置位 isDead、发事件、决定销毁还是交给对象池。</summary>
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        _isFlashingRed = false;

        ApplyColor(Color.black);

        OnDied?.Invoke();   // ← 波次管理器在这里接手：延迟 deathReturnDelay 秒后归还对象池

        // 池化对象：绝不销毁，所有权归 WaveManager 的对象池。
        if (_pooledEnemy == null)
        {
            _pooledEnemy = GetComponent<PooledEnemy>();
        }
        if (_pooledEnemy != null) return;

        if (isInstanceDestroy)
        {
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
    }

    private static readonly WaitForSeconds _waitDamageFlash = new WaitForSeconds(0.1f);
}

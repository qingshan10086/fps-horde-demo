using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


/// <summary>
/// 敌人基类
///
/// 波次系统接入说明：
///   1. 本类实现 IResetable.OnReset()，由 WaveManager 在「从对象池取出、准备复用」时
///      通过反射自动调用，用于把状态机切回 Idle；
///   2. OnEnable 里面向池化场景做了两件事：重新获取玩家目标 + 回到 Idle 状态
///      （池化实例重新激活不会重跑 Start）；
///   3. 复活后必须离开 Die 状态，否则 EnemyState.Exit 留下的动画 bool 会让敌人卡在死亡姿势。
/// </summary>
public class EnemyBase : MonoBehaviour, IResetable
{
    public float idleTime=3f;//站立时间

    public Transform target;//追踪玩家的位置
    public BoxCollider meleeArea;//攻击范围
    public GameObject bullet;//子弹



    public Rigidbody rb;//刚体组件
    public NavMeshAgent _naveMeshAgent;//自动寻路ai
    public Health _health;
    public Animator anim;//动画管理器组件


    protected float _targetRadius;//攻击半径
    protected float _targetRange;//攻击射线长度



    public EnemyStateMachine stateMachine { get; private set; }//声明敌人状态机


    /// <summary>战斗开始后使用的状态（Idle）。由子类在 OnEnable() 里赋值。</summary>
    protected EnemyState resetState;

    /// <summary>第一次启用标记：区分「首次初始化」与「池化复用」。</summary>
    private bool _initialized;


    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _naveMeshAgent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        _health = GetComponent<Health>();

        stateMachine = new EnemyStateMachine();//获取敌人状态机

    }

    /// <summary>
    /// 子类在 Awake 里 new 完所有状态后必须调用一次：记录初始状态并完成状态机初始化。
    /// 这样基类才知道「复位时该切回哪个状态」。
    /// </summary>
    protected void InitStateMachine(EnemyState initialState)
    {
        resetState = initialState;
        _initialized = true;

        if (stateMachine != null && initialState != null)
        {
            stateMachine.Initialize(initialState);
        }
    }

    protected virtual void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    /// <summary>
    /// 池化实例被 WaveManager 重新激活时触发。
    /// 重新激活不会重跑 Start，所以「找玩家」必须放在这里，否则复用的怪物 target 是空的，
    /// 状态里的 enemy.target.position 会直接抛 NullReferenceException。
    /// </summary>
    protected virtual void OnEnable()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (!_initialized) return;
        if (stateMachine == null || resetState == null) return;

        // 回到 Idle：顺带清掉上一次 Die 状态留下的动画 bool
        stateMachine.ChangeState(resetState);
    }

    protected virtual void Update()
    {
        if (stateMachine == null || stateMachine.currentState == null) return;

        stateMachine.currentState.Updata();

    }


    /// <summary>
    /// 由 WaveManager 在对象池复用前调用（通过 IResetable 接口反射查找）。
    /// 复位到「刚生成」的状态：状态机回 Idle、速度清零、动画参数复位。
    /// </summary>
    public virtual void OnReset()
    {
        FreeVelocity();

        if (anim != null)
        {
            // EnemyState.Exit 会把动画 bool 置 false，但若上次死在某个状态里可能残留，
            // 这里统一清一遍，避免复用后角色抽搐或卡在死亡姿势。
            anim.SetBool("Idle", false);
            anim.SetBool("Walk", false);
            anim.SetBool("Attack", false);
            anim.SetBool("Die", false);
        }

        if (_initialized && stateMachine != null && resetState != null)
        {
            stateMachine.ChangeState(resetState);
        }
    }


    public void FreeVelocity()//设置速度为零
    {
        if (rb == null) return;

        if (!rb.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
        }

        rb.angularVelocity = Vector3.zero;
    }


    /// <summary>agent 是否处于「可以安全调用 SetDestination / isStopped」的状态。</summary>
    protected bool CanUseAgent(NavMeshAgent agent)
    {
        return agent != null
            && agent.enabled
            && agent.isActiveAndEnabled
            && agent.isOnNavMesh;
    }

    protected void SetDestinationSafe(NavMeshAgent agent, Vector3 target)
    {
        if (!CanUseAgent(agent)) return;
        agent.isStopped = false;          // ← 这一行内部就是 Resume()，是报错来源
        agent.SetDestination(target);
    }

    /// <summary>需要「停下来」时用这个，别直接写 isStopped = true。</summary>
    protected void StopAgentSafe(NavMeshAgent agent)
    {
        if (!CanUseAgent(agent)) return;
        agent.isStopped = true;
    }
}
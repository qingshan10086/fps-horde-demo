using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoss1 : EnemyBase
{
    [Header("Boss Settings")]
    [Tooltip("是否启用视线追踪模式（面向玩家方向）")]
    public bool isLook = true;

    [Header("Missile Attack - 导弹攻击设置")]
    [Tooltip("导弹预制体，需要挂载 MissileBoss1 脚本")]
    public GameObject missileBoss1;
    [Tooltip("第一枚导弹的生成位置")]
    public Transform missilePositionA;
    [Tooltip("第二枚导弹的生成位置")]
    public Transform missilePositionB;

    [Tooltip("Boss 的主碰撞体，禁用时,用来达成无敌效果")]
    public BoxCollider boxCollider;


    #region 各种状态
    public EnemyBoss1IdleState idleState { get; private set; }
    public EnemyBoss1Attack1State attack1State { get; private set; }
    public EnemyBoss1Attack2State attack2State { get; private set; }
    public EnemyBoss1Attack3State attack3State { get; private set; }
    public EnemyBoss1DieState dieState { get; private set; }
    #endregion

    protected override void Awake()
    {
        base.Awake();

        boxCollider=GetComponent<BoxCollider>();

        idleState = new EnemyBoss1IdleState(this, base.stateMachine, "Idle", this);
        attack1State = new EnemyBoss1Attack1State(this, base.stateMachine, "Attack1", this);
        attack2State = new EnemyBoss1Attack2State(this, base.stateMachine, "Attack2", this);
        attack3State = new EnemyBoss1Attack3State(this, base.stateMachine, "Attack3", this);
        dieState = new EnemyBoss1DieState(this, base.stateMachine, "Die", this);
    }

    protected override void Start()
    {
        base.Start();
        InitStateMachine(idleState);

        if (_naveMeshAgent.enabled)
        {
            _naveMeshAgent.isStopped = true;
        }
    }

    protected override void Update()
    {
        if (_health != null && _health.isDead)
        {
            // 如果当前状态不是死亡状态，才进行切换
            if (stateMachine.currentState != dieState)
            {
                stateMachine.ChangeState(dieState);
            }
            return; // 死亡后不再执行后续更新
        }
        base.Update();

    }



    /// <summary>
    /// 面向目标（玩家）
    /// 让 Boss 平滑旋转朝向玩家
    /// </summary>
    public void FaceTarget()
    {
        // 如果没有目标，直接返回
        if (base.target == null) return;

        // 计算从 Boss 指向玩家的方向向量
        Vector3 direction = (base.target.position - base.transform.position).normalized;
        direction.y = 0; // 只在水平面上旋转，保持垂直方向不变

        // 使用 Slerp 进行平滑旋转
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        base.transform.rotation = Quaternion.Slerp(
            base.transform.rotation,
            lookRotation,
            Time.deltaTime * 5f  // 旋转速度
        );
    }

}



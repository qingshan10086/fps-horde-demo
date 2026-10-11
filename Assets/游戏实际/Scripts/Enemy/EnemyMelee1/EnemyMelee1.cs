using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMelee1 : EnemyBase
{
    #region States  声明各个状态
    public EnemyMelee1IdleState idleState { get; private set; }
    public EnemyMelee1MoveState moveState { get; private set; }
    public EnemyMelee1AttackState attackState { get; private set; }
    public EnemyMelee1DieState  dieState { get; private set; }
    #endregion


    protected override void Awake()
    {
        base.Awake();

        idleState = new EnemyMelee1IdleState(this, base.stateMachine, "Idle", this);
        moveState =new EnemyMelee1MoveState(this,base.stateMachine,"Walk",this);
        attackState = new EnemyMelee1AttackState(this, base.stateMachine, "Attack", this);
        dieState = new EnemyMelee1DieState(this, base.stateMachine, "Die", this);

    }


    protected override void Start()
    {
        base.Start();

        InitStateMachine(idleState);//初始化状态
    }


    protected override void Update()
    {
        base.Update();

        if (this._health != null && this._health.isDead && stateMachine.currentState != dieState)
        {
            // 只在「尚未进入死亡状态」时切换一次；每帧重复 ChangeState 会反复 Exit()/Enter()，
            // 而 EnemyState.Enter/Exit 会置位/清除动画 bool，导致死亡动画被反复重置。
            stateMachine.ChangeState(dieState);
        }
    }

}

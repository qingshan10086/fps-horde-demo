using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyState//敌人状态
{
    protected EnemyStateMachine stateMachine;//敌人状态机
    protected EnemyBase enemyBase;//敌人基础类，为了提高代码复用率，在基类后会有其子类
    protected Rigidbody rb;//重力组件


    private string animBoolName;//动画bool变量名字

    protected bool triggerCalled;//触发器
    protected float stateTimer;//状态持续时间

    public EnemyState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName)//构造函数初始化
    {
        this.enemyBase = _enemyBase;
        this.stateMachine = _stateMachine;
        this.animBoolName = _animBoolName;
    }

    public virtual void Updata()
    {
        stateTimer -= Time.deltaTime;
    }


    public virtual void Enter()
    {
        rb = enemyBase.rb;//方便后期用rb代替enemyBase.rb

        enemyBase.anim.SetBool(animBoolName, true);//设置动画Bool变量为真

        triggerCalled = false;//动画触发器
    }

    public virtual void Exit()
    {
        enemyBase.anim.SetBool(animBoolName, false);//设置动画Bool变量为假

    }

    public virtual void AnimationFinishTrigger()//动画完成触发器函数
    {
        triggerCalled = true;
    }

}
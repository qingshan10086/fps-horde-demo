using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMelee1IdleState : EnemyState
{
    EnemyMelee1 enemy;

    public EnemyMelee1IdleState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName,EnemyMelee1 _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemy.idleTime;//站立时间
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Updata()
    {
        base.Updata();

        enemy.FreeVelocity();


        if (stateTimer < 0)//站立时间一结束，进入移动状态
        {
            stateMachine.ChangeState(enemy.moveState);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRange1IdleState : EnemyState
{
    EnemyRange1 enemy;
    public EnemyRange1IdleState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyRange1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }


    public override void Enter()
    {
        base.Enter();
        stateTimer = enemy.idleTime;//站立时间
                                    // enemy._naveMeshAgent.isStopped = true;
        if (enemy._naveMeshAgent != null && enemy._naveMeshAgent.isOnNavMesh)
        {
           enemy. _naveMeshAgent.isStopped = true; // 推荐使用 isStopped 属性
                                             // 或者 _naveMeshAgent.Stop();
        }
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

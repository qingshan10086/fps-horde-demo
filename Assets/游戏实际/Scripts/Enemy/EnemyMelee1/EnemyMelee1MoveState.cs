using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class EnemyMelee1MoveState : EnemyState
{
    EnemyMelee1 enemy;

    private float attackRange = 2f; // 近战攻击范围

    public EnemyMelee1MoveState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyMelee1 _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        rb.isKinematic = true;  // 运动学刚体，不受力但能碰撞

    }

    public override void Exit()
    {
        base.Exit();

        
    }

    public override void Updata()
    {
        base.Updata();


        if (enemy._naveMeshAgent.enabled && !enemy._health.isDead)
        {
            // 检查是否进入攻击范围
            float distance = Vector3.Distance(enemy.transform.position, enemy.target.position);

            if (distance <= attackRange)
            {
                //进入攻击范围，切换到攻击状态
                stateMachine.ChangeState(enemy.attackState);
                return;
            }


            enemy._naveMeshAgent.SetDestination(enemy.target.position);//调用组件去追踪玩家
            enemy._naveMeshAgent.isStopped = false;//不停止移动
        }


    }
}

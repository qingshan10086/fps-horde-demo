using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoss1IdleState : EnemyState
{
    EnemyBoss1 enemy;


    public EnemyBoss1IdleState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyBoss1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemy.idleTime;//站立时间



        // === 停止移动 ===
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = true;
        }

        // === 恢复碰撞体 ===
        if (enemy.boxCollider != null)
        {
            enemy.boxCollider.enabled = true;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Updata()
    {
        base.Updata();

        enemy.FaceTarget();//面向玩家

        if (stateTimer <= 0)
        {
            SelectAttackMode();//选择攻击模式
        }
    }

    private void SelectAttackMode()//选择攻击模式
    {
        int randomAction = Random.Range(0, 5);

        // 0-3：导弹射击（60%概率）
        if (randomAction <= 2)
        {
            stateMachine.ChangeState(enemy.attack1State);
        }
        // 4：跳跃攻击（40%概率）
        else
        {
            stateMachine.ChangeState(enemy.attack2State);
        }


    }

}

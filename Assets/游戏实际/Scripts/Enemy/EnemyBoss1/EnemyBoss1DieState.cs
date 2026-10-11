using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoss1DieState : EnemyState
{
    EnemyBoss1 enemy;
    public EnemyBoss1DieState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName,EnemyBoss1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        enemy.StopAllCoroutines();

        // === 禁用导航系统 ===
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = true;  // 立即停止移动
            enemy._naveMeshAgent.enabled = false;    // 禁用 NavMeshAgent 组件
        }

        // === 禁用碰撞体 ===
        // 死亡后不再与玩家发生碰撞
        if (enemy.boxCollider != null)
        {
            enemy.boxCollider.enabled = false;
        }

        // === 禁用刚体物理 ===
        // 设置为运动学刚体，不再受物理影响
        if (enemy.rb != null)
        {
            enemy.rb.isKinematic = true;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Updata()
    {
        base.Updata();
    }

}
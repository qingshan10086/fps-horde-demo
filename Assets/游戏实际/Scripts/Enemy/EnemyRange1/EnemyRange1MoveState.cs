using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRange1MoveState : EnemyState
{
    EnemyRange1 enemy;

    private float attackRange = 8f;      // 远程攻击范围
    private float preferredDistance = 5f; // 偏好距离（保持一定距离）
    private float lastAttackTime = -999f;
    private float attackCooldown = 1.5f;//攻击冷却

    public EnemyRange1MoveState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyRange1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        // 运动学刚体
        enemy.rb.isKinematic = true;

    }

    public override void Exit()
    {
        base.Exit();

        lastAttackTime = Time.time;
    }

    public override void Updata()
    {
        base.Updata();

        if (enemy._naveMeshAgent.enabled && !enemy._health.isDead)
        {
            float distance = Vector3.Distance(enemy.transform.position, enemy.target.position);

            // 检测是否进入攻击范围
            if (distance <= attackRange && Time.time - lastAttackTime >= attackCooldown)
            {
                stateMachine.ChangeState(enemy.attackState);
                return;
            }

            // 保持在偏好距离
            if (distance < preferredDistance)
            {
                // 太近了，后退
                Vector3 retreatDirection = (enemy.transform.position - enemy.target.position).normalized;
                Vector3 retreatPosition = enemy.transform.position + retreatDirection * (preferredDistance - distance);

                if (enemy._naveMeshAgent.enabled)
                {
                    enemy._naveMeshAgent.SetDestination(retreatPosition);
                    enemy._naveMeshAgent.isStopped = false;
                }
            }
            else
            {
                // 正常追踪
                enemy._naveMeshAgent.SetDestination(enemy.target.position);
                enemy._naveMeshAgent.isStopped = false;
            }

        }
    }
}

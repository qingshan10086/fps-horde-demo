using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMelee1AttackState : EnemyState
{
    EnemyMelee1 enemy;

    private float attackDuration = 2f; // 总攻击时长
    private float damageDelay = 0.5f;    // 延迟造成伤害的时间
    private float hitboxDuration = 1f;   // 攻击判定框持续时长

    private float timer;//状态持续时间

    public EnemyMelee1AttackState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyMelee1 _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
    }

    public override void Enter()
    {
        base.Enter();

        timer = 0f;

        // 进入攻击状态时停止移动
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = true;
        }


    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Updata()
    {
        base.Updata();


        timer += Time.deltaTime;


        // 阶段1：前摇 - 等待 damageDelay 时间后开启攻击判定
        if (timer >= damageDelay )
        {
            enemy.meleeArea.enabled = true;
           
        }


        // 阶段2：攻击判定持续 - hitboxDuration 时间后关闭判定框
        if (timer >= damageDelay + hitboxDuration)
        {
            enemy.meleeArea.enabled = false;
        }

        // 阶段3：攻击结束 - 回到待机状态
        if (timer >= attackDuration)
        {
           
            stateMachine.ChangeState(enemy.moveState);
            
        }




     }
}

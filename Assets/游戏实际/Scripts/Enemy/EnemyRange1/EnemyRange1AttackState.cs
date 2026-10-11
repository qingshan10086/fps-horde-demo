using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRange1AttackState : EnemyState
{
    EnemyRange1 enemy;

    private float attackDuration = 2.5f;  // 总攻击时长
    private float shootDelay = 0.5f;       // 发射子弹延迟
    private float bulletSpeed = 20f;       // 子弹速度

    private float timer;
    private bool hasShot;

    public EnemyRange1AttackState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyRange1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        timer = 0f;
        hasShot = false;

        // 进入攻击状态时停止移动
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = true;
        }

        // 面向玩家
        FaceTarget();


    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Updata()
    {
        base.Updata();


        timer += Time.deltaTime;

        // 阶段1：前摇 - 发射子弹
        if (timer >= shootDelay && !hasShot)
        {
            Shoot();
            hasShot = true;
        }

        // 阶段2：攻击结束 - 回到移动状态
        if (timer >= attackDuration)
        {
            stateMachine.ChangeState(enemy.moveState);
        }
    }



    private void Shoot()
    {
        if (enemy.bullet == null || enemy.target == null) return;

        // 创建子弹
        GameObject instantBullet = Object.Instantiate(enemy.bullet, enemy.transform.position, enemy.transform.rotation);
        Rigidbody rigidbodyBullet = instantBullet.GetComponent<Rigidbody>();

        if (rigidbodyBullet != null)
        {
            // 计算朝向玩家的方向
            Vector3 direction = (enemy.target.position - enemy.transform.position).normalized;
            rigidbodyBullet.velocity = direction * bulletSpeed;
        }
    }

    private void FaceTarget()
    {
        if (enemy.target == null) return;

        Vector3 direction = (enemy.target.position - enemy.transform.position).normalized;
        direction.y = 0; // 只在水平面上旋转
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, lookRotation, Time.deltaTime * 5f);
    }


}

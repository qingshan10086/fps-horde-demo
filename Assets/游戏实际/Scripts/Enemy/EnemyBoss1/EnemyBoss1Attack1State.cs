using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoss1Attack1State : EnemyState
{
    EnemyBoss1 enemy;


    private float preDelay = 0.2f;           // 前摇时间：播放动画到发射第一枚导弹的间隔
    private float intervalBetweenShots = 0.3f; // 两枚导弹之间的发射间隔
    private float postDelay = 2.5f;           // 后摇时间：发射完所有导弹后的冷却时间

    private float timer;         // 计时器
    private bool firstShotFired;  // 第一枚导弹是否已发射
    private bool secondShotFired; // 第二枚导弹是否已发射

    public EnemyBoss1Attack1State(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyBoss1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        // 重置所有状态变量
        timer = 0f;
        firstShotFired = false;
        secondShotFired = false;

        // === 停止移动 ===
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


        // 累计时间
        timer += Time.deltaTime;

        // === 面向玩家 ===
        enemy.FaceTarget();

        // === 时间轴控制 ===
        // 第一阶段：前摇结束后发射第一枚导弹
        if (timer >= preDelay && !firstShotFired)
        {
            FireMissile(enemy.missilePositionA);
            firstShotFired = true;
        }

        // 第二阶段：间隔结束后发射第二枚导弹
        if (timer >= preDelay + intervalBetweenShots && !secondShotFired)
        {
            FireMissile(enemy.missilePositionB);
            secondShotFired = true;
        }

        // 第三阶段：后摇结束后回到待机状态
        if (timer >= preDelay + intervalBetweenShots + postDelay)
        {
            stateMachine.ChangeState(enemy.idleState);
        }

    }

    /// <summary>
    /// 发射导弹
    /// </summary>
    private void FireMissile(Transform spawnPoint)
    {
        // 安全检查
        if (enemy.missileBoss1 == null || spawnPoint == null || enemy.target == null)
        {
            Debug.LogWarning("导弹发射失败：缺少必要组件或目标");
            return;
        }

        // 在指定位置生成导弹
        GameObject missile = Object.Instantiate(
            enemy.missileBoss1,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // 获取导弹上的脚本，设置追踪目标
        MissileBoss1 missileScript = missile.GetComponent<MissileBoss1>();
        if (missileScript != null)
        {
            missileScript.target = enemy.target;
        }
    }


}

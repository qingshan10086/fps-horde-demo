using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;

public class EnemyBoss1Attack2State : EnemyState
{
    EnemyBoss1 enemy;

    private float jumpDuration = 1.5f;          // 跳跃持续时间（到达目标点的时间）
    private float hitboxActiveDuration = 0.5f;  // 攻击判定框激活时间
    private float cooldown = 0.5f;                // 冷却时间（回到待机前的等待）

    private float timer;          // 计时器
    private bool jumpComplete;     // 跳跃是否完成（到达目标点）
    private bool hitboxActivated;  // 攻击判定框是否已激活



    public EnemyBoss1Attack2State(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyBoss1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();

        timer = 0f;
        jumpComplete = false;
        hitboxActivated = false;

        // === 计算跳跃目标位置 ===
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 lookVec = new Vector3(horizontal, 0, vertical) * 5f;
        Vector3 jumpTarget = enemy.target.position + lookVec;

        // === 检测目标点是否在 NavMesh 上 ===
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(jumpTarget, out navHit, 2.0f, NavMesh.AllAreas))
        {
            // 目标点在 NavMesh 上，使用采样后的位置
            jumpTarget = navHit.position;
        }
        else
        {
            // 目标点不在 NavMesh 上（可能在墙外或悬崖）
            // 回退方案：朝玩家方向跳跃一段固定距离
            Vector3 directionToPlayer = (enemy.target.position - enemy.transform.position).normalized;
            directionToPlayer.y = 0;
            jumpTarget = enemy.transform.position + directionToPlayer * 3f;

            // 再次检测回退位置
            if (NavMesh.SamplePosition(jumpTarget, out navHit, 2.0f, NavMesh.AllAreas))
            {
                jumpTarget = navHit.position;
            }
            else
            {
                // 最终保底：原地跳跃
                jumpTarget = enemy.transform.position;
            }
        }

        // === 设置导航目标 ===
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = false;
            enemy._naveMeshAgent.SetDestination(jumpTarget);
        }

        // === 禁用碰撞体 ===
        if (enemy.boxCollider != null)
        {
            enemy.boxCollider.enabled = false;
        }

        // === 关闭视线追踪 ===
        enemy.isLook = false;
    }

    public override void Exit()
    {
        base.Exit();

       

        // === 停止移动 ===
        if (enemy._naveMeshAgent.enabled)
        {
            enemy._naveMeshAgent.isStopped = true;
        }

        // === 关闭攻击判定 ===
        // 确保离开此状态时攻击判定是关闭的
        if (enemy.meleeArea != null)
        {
            enemy.meleeArea.enabled = false;
        }

        // === 恢复视线追踪 ===
        enemy.isLook = true;
    }

    public override void Updata()
    {
        base.Updata();


        // 累计时间
        timer += Time.deltaTime;

        // === 时间轴控制 ===
        // 阶段1：跳跃过程中（等待到达目标点）
        if (timer >= jumpDuration && !jumpComplete)
        {
            jumpComplete = true;
            // 此时 Boss 应该已经到达目标点附近
            Debug.Log("1");
        }

        // 阶段2：落地后开启攻击判定框
        if (timer >= jumpDuration && !hitboxActivated)
        {
            if (enemy.meleeArea != null)
            {
                enemy.meleeArea.enabled = true;  // 激活攻击判定
            }
            hitboxActivated = true;

            // === 恢复碰撞体 ===
            if (enemy.boxCollider != null)
            {
                enemy.boxCollider.enabled = true;
            }
        }

        // 阶段3：关闭攻击判定框
        if (timer >= jumpDuration + hitboxActiveDuration)
        {
            if (enemy.meleeArea != null)
            {
                enemy.meleeArea.enabled = false;  // 关闭攻击判定
            }
        }

        // 阶段4：恢复期 + 冷却期结束，回到待机状态
        if (timer >= jumpDuration + hitboxActiveDuration  + cooldown)
        {
            stateMachine.ChangeState(enemy.idleState);
        }
    }
}

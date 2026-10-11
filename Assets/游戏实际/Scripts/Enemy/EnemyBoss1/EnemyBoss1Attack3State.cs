using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoss1Attack3State :EnemyState
{
    EnemyBoss1 enemy;
    public EnemyBoss1Attack3State(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyBoss1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        this.enemy = enemy;
    }

    public override void Enter()
    {
        base.Enter();
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
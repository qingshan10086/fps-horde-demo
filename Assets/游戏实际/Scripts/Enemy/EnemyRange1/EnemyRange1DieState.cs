using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRange1DieState : EnemyState
{
    EnemyRange1 enemy;
    public EnemyRange1DieState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyRange1 enemy) : base(_enemyBase, _stateMachine, _animBoolName)
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

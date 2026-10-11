using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMelee1DieState : EnemyState
{
    EnemyMelee1 enemy;

    public EnemyMelee1DieState(EnemyBase _enemyBase, EnemyStateMachine _stateMachine, string _animBoolName, EnemyMelee1 _enemy) : base(_enemyBase, _stateMachine, _animBoolName)
    {
        enemy = _enemy;
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStateMachine//敌人状态机
{
    public EnemyState currentState { get; private set; }

    public void Initialize(EnemyState _startState)
    {
        currentState = _startState;
        currentState.Enter();
    }

    public void ChangeState(EnemyState _newState)//改变状态函数
    {
        currentState.Exit();
        currentState = _newState;
        currentState.Enter();
    }
}

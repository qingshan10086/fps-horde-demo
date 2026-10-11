using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 可受伤接口组件：将外部的伤害调用转发给对象的 Health 组件
/// 作用：解耦伤害来源（子弹、近战、爆炸）和伤害处理（血量减少）
public class Damageable : MonoBehaviour
{
    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    public void InflictDamage(float damage)
    {
        _health.TakeDamage(damage);
    }
}

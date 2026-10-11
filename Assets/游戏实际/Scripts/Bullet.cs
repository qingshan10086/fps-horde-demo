using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 远程敌人子弹脚本：碰到墙壁销毁，碰到玩家造成伤害并销毁
/// </summary>
public class Bullet : MonoBehaviour
{
    public float damage = 10f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
    }
}

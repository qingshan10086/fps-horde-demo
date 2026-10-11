using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


/// <summary>
/// 子弹/投射物的基类：记录射击时的初始数据（谁打的、从哪打、往哪打、带多少惯性）
/// </summary>
public class ProjectileBase : MonoBehaviour
{
    public GameObject owner {  get; private set; }// 射击者（玩家或敌人）

    public Vector3 initialPosition {get; private set; }  // 子弹生成时的初始位置（枪口位置）

    public Vector3 initialDirection { get; private set; }// 子弹的初始飞行方向（枪口朝向）

    public Vector3 inheritedMuzzleVelocity { get; private set; } // 继承的枪口速度（用于计算子弹惯性）

    public UnityAction onShoot;

    /// <summary>
    /// 初始化子弹数据（由 WeaponController.HandleShoot() 调用）
    /// </summary>
    public void  Shoot(WeaponController weaponController)
    {
        owner=weaponController.owner;
        initialPosition=transform.position;
        initialDirection=transform.forward;

        inheritedMuzzleVelocity = weaponController.muzzleWorldVelocity;

        if(onShoot != null)
        {
            onShoot.Invoke();
        }
    }

}

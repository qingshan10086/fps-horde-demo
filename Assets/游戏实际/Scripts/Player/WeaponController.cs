using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 武器控制器：管理武器的射击逻辑、枪口特效、子弹生成
/// </summary>
public class WeaponController : MonoBehaviour
{
    public GameObject weaponRoot;//武器根节点

    public Transform weaponMuzzle;//枪口闪光位置

    public GameObject muzzleFlashPrefab;//枪口闪光预制体

    public ProjectileBase projectilePrefab; // 子弹/投射物预制体


    public float delayBetweenShots = 0.1f;//开火频率

    private float _lastShotTime=Mathf.NegativeInfinity;//上一次开火时间

    public Vector3 muzzleWorldVelocity {  get; private set; }//子弹离开枪口时相对于世界坐标系的速度向量


    public bool isWeaponActive {  get; private set; }//武器是否被激活
    public GameObject owner {  get;set; }//武器拥有者对象
    public GameObject sourcePrefab {  get; set; }//武器预制体

    /// <summary>
    /// 显示或隐藏武器
    /// </summary>
    public void ShowWeapon(bool show)
    {
        weaponRoot.SetActive(show);
        isWeaponActive = show;
    }

    /// <summary>
    /// 处理射击输入（由 PlayerWeaponManager 每帧调用）
    /// </summary>
    public bool HandleShootInputs(bool inputHeld)
    {
        if (inputHeld)
        {
            return TryShoot();
        }

        return false;
    }

    /// <summary>
    /// 尝试射击：检查是否满足射击间隔
    /// </summary>
    private bool TryShoot()
    {
        if(_lastShotTime+delayBetweenShots < Time.time)
        {
            HandleShoot();
            return true;
        }

        return false;
    }


    /// <summary>
    /// 执行射击：生成子弹和枪口闪光
    /// </summary>
    private void HandleShoot()
    {
        if(projectilePrefab != null)
        {
            Vector3 shotDirection = weaponMuzzle.forward;
            ProjectileBase newProjectile = Instantiate(projectilePrefab, weaponMuzzle.position,
                weaponMuzzle.rotation, weaponMuzzle.transform);

            newProjectile.transform.SetParent(null); //立即解除父子关系，让子弹自由飞行
            newProjectile.Shoot(this);
        }

        if(muzzleFlashPrefab != null)
        {
            GameObject muzzleFlashInstance=Instantiate(muzzleFlashPrefab,weaponMuzzle.position,weaponMuzzle.rotation,weaponMuzzle.transform);

            Destroy(muzzleFlashInstance, 2);
        }
        _lastShotTime = Time.time;
    }

}

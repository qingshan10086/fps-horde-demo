using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


/// <summary>
/// 玩家武器管理器：负责武器的添加、切换、射击输入分发
/// </summary>
public class PlayerWeaponManager : MonoBehaviour
{

    [SerializeField] private Vector3 weaponOffset = new Vector3(0.3f, -0.3f, 0.5f);//调试用，一会删
    public List<WeaponController> startingWeapons= new List<WeaponController>(); // 初始武器列表

    public Camera weaponCamera;//武器相机
    public Transform weaponParentSocket;//武器位置

    public UnityAction<WeaponController> onSwitchedToWeapon;//委托，正在切换的武器

    private WeaponController[] _weaponSlots= new WeaponController[9];
    private int _activeWeaponIndex = 0;//当前活跃的武器

    private void Start()
    {
        onSwitchedToWeapon += OnWeaponSwitched;

        foreach(WeaponController weapon in startingWeapons)
        {
            AddWeapon(weapon);
        }

        SwitchWeapon();
    }


    private void Update()
    {
        WeaponController activeWeapon=_weaponSlots[_activeWeaponIndex];

        if(activeWeapon != null)
        {
            activeWeapon.HandleShootInputs(PlayerInputHander.Instance.GetFireInputHeld());
        }
    }

    /// <summary>
    /// 添加新武器到空槽位
    /// </summary>
    public bool AddWeapon(WeaponController weaponPrefab)
    {
        for(int i=0;i< _weaponSlots.Length;i++)
        {
            if (_weaponSlots[i] == null)  // 找到空位
            {
                WeaponController weaponInstance = Instantiate(weaponPrefab, weaponParentSocket);
                weaponInstance.transform.localPosition = weaponOffset;
                weaponInstance.transform.localRotation = Quaternion.identity;
                weaponInstance.owner = gameObject;
                weaponInstance.sourcePrefab = weaponPrefab.gameObject;
                _weaponSlots[i] = weaponInstance;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 切换到第一个武器（默认武器）
    /// </summary>
    public void SwitchWeapon()
    {
        SwitchWeaponToIndex(0);
    }

    /// <summary>
    /// 切换到指定索引的武器
    /// </summary>
    public void SwitchWeaponToIndex(int newWeaponIndex)
    {
        if (newWeaponIndex >= 0 && newWeaponIndex < _weaponSlots.Length)
        {
            _activeWeaponIndex = newWeaponIndex; //记录当前索引
            WeaponController newWeapon = GetWeaponAtSlotIndex(newWeaponIndex);

            if (onSwitchedToWeapon != null)
            {
                onSwitchedToWeapon.Invoke(newWeapon);
            }
        }
    }

    public WeaponController GetWeaponAtSlotIndex(int index)
    {
        if (index >= 0 && index < _weaponSlots.Length)
        {
            return _weaponSlots[index];
        }

        return null;
    }

    /// <summary>
    /// 武器切换事件的回调：显示新武器，隐藏旧武器
    /// </summary>
    private void OnWeaponSwitched(WeaponController newWeapon)
    {
        for (int i = 0; i < _weaponSlots.Length; i++)
        {
            if (_weaponSlots[i] != null)
            {
                _weaponSlots[i].ShowWeapon(_weaponSlots[i] == newWeapon);
            }
        }
    }

}

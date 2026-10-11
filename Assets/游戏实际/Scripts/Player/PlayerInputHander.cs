using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 玩家输入处理器：统一管理键盘和鼠标输入，提供干净的接口供其他脚本调用
/// </summary>
public class PlayerInputHander : MonoBehaviour
{
    public static PlayerInputHander Instance;

    public float lookSensitivity = 1f; // 鼠标灵敏度倍率


    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;//光标锁定在屏幕中间
        Cursor.visible = false;//光标隐藏
    }


    public Vector3 GetMoveInput()//得到移动输入
    {
        Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        move = Vector3.ClampMagnitude(move, 1);
        return move;
    }


    public float GetMouseLookHorizontal()//得到鼠标水平轴输入
    {
        return GetMouseLookAxis("Mouse X");
    }
    public float GetMouseLookVertical()//得到鼠标竖直轴输入
    {
        return GetMouseLookAxis("Mouse Y");
    }
    public float GetMouseLookAxis(string mouseInputName)//得到处理后的鼠标输入
    {
        float i=Input.GetAxisRaw(mouseInputName);
        i *= lookSensitivity * Time.deltaTime;

        return i;
    }

    public bool GetFireInputHeld()//得到是否按住开火建
    {
        return Input.GetButton("Fire");
    }


}
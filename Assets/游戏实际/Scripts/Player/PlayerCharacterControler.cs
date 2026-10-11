using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家角色控制器：管理移动、视角、生命值和受伤逻辑
/// </summary>
public class PlayerCharacterControler : MonoBehaviour
{
    public static PlayerCharacterControler Instance;//玩家角色管理器实例


    public Camera playerCamera;//玩家摄像机
    public float gravityDownForce = 20f;//玩家下落速度
    public float maxSpeedOnGround = 8f;//玩家地面最大移动速度
    public float moveSharpnessOnGround = 15f;//玩家下落最大速度
    public float rotationSpeed = 500f;//鼠标灵敏度

    public float maxHealth = 200f;//玩家最大血量

    public float cameraHeightRatio = 0.9f;//相机在玩家身高中占比

    private CharacterController _characterController;//角色管理器组件
    private PlayerInputHander _inputHandler;//玩家输入处理器
    private float _targetCHaracterHeight = 1.8f;//玩家身高


    public float CurrentHealth=>_currentHealth;// 外部只读访问当前血量
    private float _currentHealth;//玩家当前血量
    private float _cameraVerticalAngle = 0f;//相机竖直移动角度


    private bool _isBoosAttack;// 是否正在被 Boss 攻击击退
    public Vector3 CharacterVelocity {  get; set; }//角色速度

    private void Awake()
    {
        Instance = this;
        _currentHealth = maxHealth;
    }

    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _inputHandler = GetComponent<PlayerInputHander>();


        _characterController.enableOverlapRecovery = true;// 开启重叠恢复，防止角色卡在墙角

        UpdateCharacterHeight();// 设置角色身高和摄像机位置
    }

    private void Update()
    {
        HandleCharacterMovement();
      
    }

    /// <summary>
    /// 更新角色身高和摄像机位置
    /// </summary>
    private void UpdateCharacterHeight()
    {
        _characterController.height = _targetCHaracterHeight;
        _characterController.center = Vector3.up * _characterController.height * 0.5f;

        playerCamera.transform.localPosition = Vector3.up * _characterController.height * 0.9f;
    }

    /// <summary>
    /// 玩家移动处理器：包含视角旋转和位移
    /// </summary>
    private void HandleCharacterMovement()
    {  
        //以下是相机转动代码
        this.transform.Rotate(new Vector3(0, _inputHandler.GetMouseLookHorizontal() * rotationSpeed, 0));

        _cameraVerticalAngle += _inputHandler.GetMouseLookVertical() * rotationSpeed;
       
        _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle, -89f, 89f); // 限制垂直视角范围，防止翻转（-89° ~ 89°）

        playerCamera.transform.localEulerAngles = new Vector3(-_cameraVerticalAngle, 0, 0);


        //以下为移动代码
        Vector3 worldSpaceMoveInput = transform.TransformVector(_inputHandler.GetMoveInput());

        if (_characterController.isGrounded)
        {
            Vector3 targetVelocity = worldSpaceMoveInput * maxSpeedOnGround;

            CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity, moveSharpnessOnGround * Time.deltaTime);

        }
        else
        {
            CharacterVelocity += Vector3.down * gravityDownForce * Time.deltaTime;
        }

        if (_isBoosAttack)
        {
            // 计算击退方向（只取水平方向，避免被推上天或入地）
            Vector3 knockbackDirection = -transform.forward;
            knockbackDirection.y = 0; // 保持水平
            knockbackDirection.Normalize();

            // 检测前方是否有障碍物
            float knockbackDistance = 3f; // 击退距离
            RaycastHit hit;
            if (Physics.Raycast(transform.position, knockbackDirection, out hit, knockbackDistance))
            {
                // 前方有障碍物，缩短击退距离到障碍物前
                knockbackDistance = Mathf.Max(0.1f, hit.distance - 0.5f); // 留0.5米缓冲
            }

            // 3. 应用击退速度
            CharacterVelocity = knockbackDirection * knockbackDistance / 0.2f; // 0.2秒内完成击退
        }

        _characterController.Move(CharacterVelocity*Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        OnHitPlayer(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        OnHitPlayer(collision.collider);
    }


    /// <summary>
    /// 统一受伤处理：根据碰撞物类型扣除血量
    /// </summary>
    private void OnHitPlayer(Collider other)
    {
        if (other.CompareTag("EnemyBullet"))
        {
            Bullet enemyBullet=other.GetComponent<Bullet>();
            _currentHealth -= enemyBullet.damage;

            StartCoroutine(OnDamage());

            if (other.GetComponent<Rigidbody>())
            {
                Destroy(other.gameObject);
            }
        }


        if (other.CompareTag("MeleeArea"))
        {
            MeleeAttacter meleeAttacter = other.GetComponent<MeleeAttacter>();
            _currentHealth -= meleeAttacter.damage;

            print("血量扣"+meleeAttacter.damage);


            _isBoosAttack = other.name == "Boss1MeleeArea";
            StartCoroutine(OnDamage());
        }
    }

    /// <summary>
    /// 受伤协程：打印当前血量，检查是否死亡，重置击退状态
    /// </summary>
    IEnumerator OnDamage()
    {
        print("当前血量为" + _currentHealth);

        if (_currentHealth < 0)
        {
            OnDie();
        }

        yield return new WaitForSeconds(0.2f);
        _isBoosAttack = false;
    }

    /// <summary>
    /// 死亡处理：重新加载关卡
    /// </summary>
    private void OnDie()
    {
        SceneManager.LoadScene("Level1");
    }
}

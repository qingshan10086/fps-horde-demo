using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


/// <summary>
/// 标准子弹脚本：处理子弹飞行、准星校正、碰撞检测和命中效果
/// </summary>
public class ProjectileStandard : MonoBehaviour
{
    public float maxLifeTime = 5f;
    public float speed = 300f;// 子弹飞行速度


    public Transform root;//子弹根部位置
    public Transform tip;//子弹头部位置
    public float radius = 0.01f;//碰撞检测半径
    public LayerMask hittableLayers = -1;//-1默认检测到所有图层

    public float damage = 20f;//子弹伤害

    //命中特效
    public GameObject impactVFX;//子弹粒子特效
    public float impactVFXLifeTime = 0.5f;//特效存在时间
    public float impactVFXSpawnOffset = 0.1f;//特性偏移量

    public float trajectoryCorrectionDistance = 5f;// 准星校正距离



    private ProjectileBase _projectileBase;// 子弹基础数据（谁打的、初始位置等）
    private Vector3 _velocity;//子弹方向
    private Vector3 _lastRootPosition;//上一次根部位置

    private bool _hasTrajectroyCorrected;//是否进行了准心校准
    private Vector3 _correctionVector;//准心校准向量
    private Vector3 _consumeCorrectionVection;//正在进行的校准向量

    private void OnEnable()
    {
        _projectileBase = GetComponent<ProjectileBase>();
        _projectileBase.onShoot += OnShoot;
        Destroy(gameObject,maxLifeTime);
    }

    /// <summary>
    /// 射击事件回调：初始化子弹速度和准星校正数据
    /// </summary>
    private void OnShoot()//子弹射击启动函数
    {
        _lastRootPosition = root.position;
        _velocity += transform.forward * speed;

        PlayerWeaponManager playerWeaponManager = _projectileBase.owner.GetComponent<PlayerWeaponManager>();

        if (playerWeaponManager != null)
        {
            _hasTrajectroyCorrected = false;

            Transform weaponCameraTransform = playerWeaponManager.weaponCamera.transform;

            Vector3 cameraToMuzzle = _projectileBase.initialPosition - weaponCameraTransform.position;

            _correctionVector = Vector3.ProjectOnPlane(-cameraToMuzzle, weaponCameraTransform.forward);
        }



    }

    private void Update()
    {
        //子弹移动
        transform.position += _velocity * Time.deltaTime;

        //子弹方向
        transform.forward = _velocity.normalized;

        //子弹修正到准心
        if (!_hasTrajectroyCorrected && _consumeCorrectionVection.sqrMagnitude < _correctionVector.sqrMagnitude)
        {
            Vector3 correctionLeft = _correctionVector - _consumeCorrectionVection;

            float distanceThisFrame = (root.position - _lastRootPosition).magnitude;

            Vector3 correctionThisFrame = (distanceThisFrame / trajectoryCorrectionDistance) * _correctionVector;
            correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
            _consumeCorrectionVection += correctionThisFrame;

            if (Mathf.Abs(_consumeCorrectionVection.sqrMagnitude - _correctionVector.sqrMagnitude) < Mathf.Epsilon)
            {
                _hasTrajectroyCorrected = true;
            }

            transform.position += correctionThisFrame;
        }



        //Hit detection
        RaycastHit closesHit=new RaycastHit();//记录最近的击中结果
        closesHit.distance = Mathf.Infinity;//初始设为最大值
        bool foundHit = false;//有效命中标记


        //SphereCastAll
        Vector3 displacementSinceLastFrame=tip.position- _lastRootPosition;

        RaycastHit[] hits = Physics.SphereCastAll(_lastRootPosition,
            radius,
            displacementSinceLastFrame.normalized,
            displacementSinceLastFrame.magnitude,
            hittableLayers,
            QueryTriggerInteraction.Collide
            );

        foreach( RaycastHit hit in hits )
        {
            if (IsHitValid(hit) && hit.distance < closesHit.distance)
            {
                closesHit = hit;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            if(closesHit.distance <= 0)
            {
                closesHit.point=root.position;
                closesHit.normal = -transform.forward;

            }

            OnHit(closesHit.point,closesHit.normal,closesHit.collider);//子弹命中逻辑
        }


    }


    /// <summary>
    /// 检查命中是否有效（排除触发器）
    /// </summary>
    private bool IsHitValid( RaycastHit hit)
    {
        if (hit.collider.isTrigger)
        {
            return false;
        }
        return true;
    }


    /// <summary>
    /// 命中处理：造成伤害、生成特效、销毁子弹
    /// </summary>
    private void OnHit(Vector3 point,Vector3 normal,Collider collider)
    {
        Damageable damageable = collider.GetComponent<Damageable>();

        if (damageable != null)
        {
            damageable.InflictDamage(damage);
        }


        if(impactVFX!=null)
        {
            GameObject impactVFXInstance = Instantiate(impactVFX,
                point + normal * impactVFXSpawnOffset,
                Quaternion.LookRotation(normal));

            if (impactVFXLifeTime >= 0)
            {
                Destroy(impactVFXInstance,impactVFXLifeTime);
            }

        }
        

        Destroy(gameObject);
    }

}

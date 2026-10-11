using UnityEngine;

/// <summary>
/// 可重置接口：对象从对象池取出、准备复用之前调用。
///
/// 由 <c>WaveManager.ResetInstanceState()</c> 通过反射查找并调用（零编译耦合），
/// 所以实现类不需要被波次系统引用。查找顺序：
///   1. 实现了名为 <c>IResetable</c> / <c>IResettable</c> 的接口的方法（本接口走这条）；
///   2. 退化为无参公开方法 <c>ResetState()</c> / <c>OnReset()</c> / <c>ResetToDefault()</c> / <c>ResetSelf()</c>。
/// 结果按类型缓存，不会每帧反射。
///
/// 实现要点（以 EnemyBase 为例）：把状态机切回 Idle、清掉速度、复位动画 bool，
/// 否则复用的怪物会保持上一次死亡瞬间的状态。
///
/// 历史说明：本接口原先定义在 MonsterPool.cs 末尾。MonsterPool 已废弃删除，
/// 接口因此独立成文件，方法名保持 OnReset() 不变，避免影响已有实现。
/// </summary>
public interface IResetable
{
    /// <summary>对象被复用前重置自身状态。</summary>
    void OnReset();
}

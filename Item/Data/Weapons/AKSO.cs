using System.Collections;
using System.Collections.Generic;
using Items.Data.Weapons;
using UnityEngine;

[CreateAssetMenu(fileName = "New AKSO", menuName = "BBBNexus/Items/Weapons/AKSO")]
public class AKSO : RangedWeaponSO
{
    [Header("--- AK专属动画参数 ---")]
    [Tooltip("开启IK的时间点（秒），相对于拿出动画开始")] public float EnableIKTime = 0.4f;
    [Tooltip("关闭IK的时间点（秒），相对于收起动画开始")] public float DisableIKTime = 0.4f;
}

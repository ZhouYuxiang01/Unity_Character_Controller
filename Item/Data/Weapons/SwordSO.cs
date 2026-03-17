using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New SwordSO", menuName = "BBBNexus/Items/Weapons/Sword")]
    public class SwordSO : MeleeWeaponSO
    {
        [Header("--- 剑的攻击配置 (Sword Attack Configurations) ---")]
        [Tooltip("第一段攻击的完整接管请求配置")]
        public ActionRequest AttackRequest1;

        [Tooltip("第二段攻击的完整接管请求配置")]
        public ActionRequest AttackRequest2;

        [Tooltip("第三段攻击的完整接管请求配置")]
        public ActionRequest AttackRequest3;

        [Header("--- 攻击音效 (Attack Sounds) ---")]
        [Tooltip("挥动时的音效")]
        public AudioClip SwingSound;

        [Tooltip("击中时的音效")]
        public AudioClip HitSound;

        [Header("--- 攻击伤害 (Damage) ---")]
        [Tooltip("攻击伤害值")]
        public float AttackDamage = 10f;

        // 根据连击索引返回对应的ActionRequest
        public ActionRequest GetAttackRequest(int comboIndex)
        {
            switch (comboIndex % 3)
            {
                case 0:
                    return AttackRequest1;
                case 1:
                    return AttackRequest2;
                case 2:
                    return AttackRequest3;
                default:
                    return AttackRequest1;
            }
        }
    }
}

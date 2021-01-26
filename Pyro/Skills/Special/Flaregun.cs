using EntityStates;
using RoR2;
using UnityEngine;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Text;
using Pyro.Controllers;

namespace Pyro.Skills
{
    class Flaregun : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.stopwatch = 0f;

            duration = baseDuration / this.attackSpeedStat;
            this.heatController = base.GetComponent<PyroHeatController>();
            float heatPercent = heatController.GetHeat();
            pelletCount = 1 + Mathf.CeilToInt(heatPercent * 8);
            heatController.ConsumeHeat(heatPercent);
            Util.PlaySound(attackSoundString, base.gameObject);

            if (base.isAuthority)
            {
                Ray aimRay = base.GetAimRay();
                ProjectileManager.instance.FireProjectile(projectilePrefab, aimRay.origin, Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * damageCoefficient * pelletCount, 0f, RollCrit(), DamageColorIndex.Default, null, -1f);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            this.stopwatch += Time.fixedDeltaTime;
            if (base.isAuthority && this.stopwatch > this.duration)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }

        public static float baseDuration = 0.6f;
        public static float damageCoefficient = 1.4f;
        public static float ignitedHeatPercentRestore = 0.05f;
        public static float ignitedDirectHitMult = 0.1f;
        public static GameObject projectilePrefab;
        public static string attackSoundString = "Play_bandit_M2_shot";

        private float duration;
        private PyroHeatController heatController;
        private float stopwatch;
        private int pelletCount;
    }
}

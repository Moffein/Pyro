using EntityStates;
using Pyro.Controllers;
using RoR2.Projectile;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pyro.Skills
{
    public class Blazeborne : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.stopwatch = 0f;

            fireDuration = baseFireDuration / this.attackSpeedStat;
            this.heatController = base.GetComponent<PyroHeatController>();
            float heatPercent = heatController.GetHeat();
            pelletCount = 1 + Mathf.CeilToInt(heatPercent * 8);
            heatController.ConsumeHeat(heatPercent);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            this.stopwatch += Time.fixedDeltaTime;
            if (this.stopwatch > this.fireDuration && pelletCount > 0)
            {
                this.stopwatch -= this.fireDuration;
                FireSeekingPellet();
            }
            if (base.isAuthority && pelletCount < 1)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        private void FireSeekingPellet()
        {

            Util.PlaySound(Blazeborne.attackSoundString, base.gameObject);

            if (base.isAuthority)
            {
                FireProjectileInfo fireProjectileInfo = default(FireProjectileInfo);
                fireProjectileInfo.position = base.inputBank.aimOrigin;
                fireProjectileInfo.rotation = Quaternion.LookRotation(Vector3.up);
                fireProjectileInfo.crit = base.RollCrit();
                fireProjectileInfo.damage = this.damageStat * Blazeborne.damageCoefficient;
                fireProjectileInfo.damageColorIndex = DamageColorIndex.Default;
                fireProjectileInfo.owner = base.gameObject;
                fireProjectileInfo.projectilePrefab = Blazeborne.projectilePrefab;
                ProjectileManager.instance.FireProjectile(fireProjectileInfo);
                pelletCount--;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }

        public static float baseFireDuration = 0.1f;
        public static float damageCoefficient = 2.4f;
        public static float ignitedHeatPercentRestore = 0.05f;
        public static float ignitedDirectHitMult = 0.1f;
        public static GameObject projectilePrefab;
        public static string attackSoundString = "Play_item_proc_firework_fire";

        private float fireDuration;
        private int pelletCount;
        private PyroHeatController heatController;
        private float stopwatch;
    }
}

using EntityStates;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using Pyro.Controllers;
using RoR2.Projectile;
using RoR2;

namespace Pyro.Skills
{
    public class PrepFlare : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = PrepFlare.baseDuration / this.attackSpeedStat;
            Util.PlaySound(PrepFlare.prepSoundString, base.gameObject);
            this.defaultCrosshairPrefab = base.characterBody.crosshairPrefab;
            base.characterBody.crosshairPrefab = PrepFlare.specialCrosshairPrefab;

            if (base.characterBody)
            {
                base.characterBody.SetAimTimer(this.duration);
            }

            //this.heatController = base.GetComponent<PyroHeatController>();
            //this.heatController.pauseDecay = true;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority && !inputBank.skill4.down)
            {
                this.outer.SetNextState(new FireFlareBurst());
                return;
            }
        }

        public override void OnExit()
        {
            base.characterBody.crosshairPrefab = this.defaultCrosshairPrefab;
            //this.heatController.pauseDecay = false;
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
        public static float baseDuration = 0.4f;
        public static string prepSoundString = "Play_bandit_M2_load";
        private float duration;
        private ChildLocator childLocator;
        public static GameObject specialCrosshairPrefab = Resources.Load<GameObject>("prefabs/crosshair/banditcrosshairrevolver");
        private GameObject defaultCrosshairPrefab;
        //private PyroHeatController heatController;
    }

    public class FireFlareBurst : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.heatController = base.gameObject.GetComponent<PyroHeatController>();
            float heatPercent = heatController.GetHeat();
            //float heatPercent = 1f;
            flareCount = 1 + Mathf.CeilToInt(heatPercent * 8);
            heatController.ConsumeHeat(heatPercent);

            Util.PlaySound(FireFlareBurst.attackSoundString, base.gameObject);

            this.duration = FireFlareBurst.baseDuration / this.attackSpeedStat;

            Ray aimRay = base.GetAimRay();

            if(base.isAuthority)
            {
                Ray newRay;
                for (int i = 0; i < flareCount; i++)
                {
                    newRay = aimRay;
                    if (i > 0 && i <= 4)
                    {
                        newRay.direction = ApplySpread(newRay, 2.5f, i);
                    }
                    else if (i > 4)
                    {
                        newRay.direction = ApplySpread(newRay, 5f, i);
                    }
                    FireFlare(newRay);
                }
            }

            /*if (base.characterBody && base.characterMotor && !base.characterMotor.isGrounded)
            {
                if (base.characterMotor.velocity.y < 0f)
                {
                    base.characterMotor.velocity.y = 0f;
                }
                base.characterMotor.ApplyForce(-aimRay.direction * FireFlareBurst.selfForce, true, false);
            }*/
        }

        private Vector3 ApplySpread(Ray aimRay, float spread, int shotCount)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, aimRay.direction);
            float x = spread;
            float z = shotCount <= 4 ? (45f + 90f * shotCount) : (90f * (shotCount - 4));
            Vector3 vector = Quaternion.Euler(0f, 0f, z) * (Quaternion.Euler(x, 0f, 0f) * Vector3.forward);
            float y = vector.y;
            vector.y = 0f;
            float angle = (Mathf.Atan2(vector.z, vector.x) * 57.29578f - 90f);
            float angle2 = Mathf.Atan2(y, vector.magnitude) * 57.29578f;
            return Quaternion.AngleAxis(angle, Vector3.up) * (Quaternion.AngleAxis(angle2, axis) * aimRay.direction);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.isAuthority && this.duration > base.fixedAge)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Pain;
        }


        private void FireFlare(Ray aimRay)
        {
            FireProjectileInfo info = new FireProjectileInfo()
            {
                projectilePrefab = projectilePrefab,
                position = aimRay.origin,
                rotation = base.characterBody.transform.rotation * Util.QuaternionSafeLookRotation(aimRay.direction),
                owner = base.characterBody.gameObject,
                damage = base.characterBody.damage * FireFlareBurst.damageCoefficient,
                force = FireFlareBurst.force,
                crit = base.RollCrit(),
                damageColorIndex = DamageColorIndex.Default,
                target = null,
                speedOverride = 90f,
                useSpeedOverride = true
            };
            ProjectileManager.instance.FireProjectile(info);
        }

        public static GameObject projectilePrefab;
        public static float baseDuration = 0.4f;
        public static float damageCoefficient = 1.4f;
        public static float force = 700f;
        public static float selfForce = 2700f;
        public static string attackSoundString = "Play_bandit_M2_shot";

        public static float ignitedHeatPercentRestore = 0.05f;
        public static float ignitedDirectHitMult = 0.1f;

        private int flareCount;
        private PyroHeatController heatController;
        private float duration;
    }
}

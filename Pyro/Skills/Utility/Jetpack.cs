using EntityStates;
using RoR2;
using Pyro.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pyro.Skills
{
    public class Jetpack : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            PyroHeatController phc = base.GetComponent<PyroHeatController>();
            float currentHeat = phc.GetHeat();
            //float currentHeat = 1f;
            if (currentHeat >= Jetpack.heatCost)
            {
                heatMult = 1f;
                phc.ConsumeHeat(Jetpack.heatCost);
            }
            else
            {
                heatMult = currentHeat / Jetpack.heatCost;
                phc.ConsumeHeat(currentHeat);
            }

            if (base.characterBody && base.characterMotor)
            {
                Ray aimRay = base.GetAimRay();
                if (base.characterMotor.velocity.y < 0f)
                {
                    base.characterMotor.velocity.y = 0f;
                }
                if (base.characterMotor.isGrounded)
                {
                    base.characterMotor.rootMotion.y += 1;
                }
                Vector3 direction = new Vector3(aimRay.direction.x, aimRay.direction.y > 0 ? aimRay.direction.y * Mathf.Lerp(Jetpack.verticalForceMin, Jetpack.verticalForceMax, heatMult) : aimRay.direction.y, aimRay.direction.z);
                base.characterMotor.ApplyForce(direction * Jetpack.selfForce, true, false);
            }
            Util.PlaySound(Jetpack.startJetpackSoundString, base.gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            trailStopwatch += base.fixedAge;
            if (this.trailStopwatch >= Jetpack.trailDelay)
            {
                EffectManager.SpawnEffect(trailPrefab, new EffectData
                {
                    origin = base.transform.position
                }, false);
                this.trailStopwatch -= Jetpack.trailDelay;
            }


            if (base.fixedAge > Jetpack.baseDuration)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }

        public static float verticalForceMin = 0.25f;
        public static float verticalForceMax = 0.75f;
        public static float selfForce = 4500f;
        public static float baseDuration = 0.8f;
        public static float heatCost = 0.4f;
        public static string startJetpackSoundString = "Play_mage_m1_impact";

        public static GameObject trailPrefab = Resources.Load<GameObject>("prefabs/effects/impacteffects/missileexplosionvfx");
        public static float trailDelay = 0.2f;
        private float trailStopwatch;

        private float heatMult;
    }
}

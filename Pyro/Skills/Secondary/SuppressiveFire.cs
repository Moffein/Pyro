using EntityStates;
using Pyro.Controllers;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pyro.Skills
{
	public class SuppressiveFire : BaseState
	{
		public override void OnEnter()
		{
			base.OnEnter();
			this.firedShot = false;
			this.heatController = base.gameObject.GetComponent<PyroHeatController>();
			Util.PlaySound(SuppressiveFire.startAttackSoundString, base.gameObject);

			if (!SuppressiveFire.flamethrowerEffectPrefab)
			{
				SuppressiveFire.flamethrowerEffectPrefab = (Instantiate(typeof(EntityStates.Mage.Weapon.Flamethrower)) as EntityStates.Mage.Weapon.Flamethrower).flamethrowerEffectPrefab;
			}

			Transform modelTransform = base.GetModelTransform();
			if (modelTransform)
			{
				this.childLocator = modelTransform.GetComponent<ChildLocator>();
				this.muzzleTransform = this.childLocator.FindChild("MuzzleRight");
			}

			if (this.childLocator)
			{
				Transform transform2 = this.childLocator.FindChild("MuzzleRight");
				if (transform2)
				{
					this.flamethrowerTransform = UnityEngine.Object.Instantiate<GameObject>(SuppressiveFire.flamethrowerEffectPrefab, transform2).transform;
				}
				if (this.flamethrowerTransform)
				{
					this.flamethrowerTransform.GetComponent<ScaleParticleSystemDuration>().newDuration = 2f;
				}
			}

			this.shotCounter = 0;
			this.flamethrowerStopwatch = 0f;
			this.selfForceStopwatch = 0f;
			this.tickDuration = SuppressiveFire.baseTickDuration / this.attackSpeedStat;

			this.flamethrowerEffectResetStopwatch = 0f;
		}

		public override void FixedUpdate()
		{
			base.FixedUpdate();
			this.flamethrowerStopwatch += Time.fixedDeltaTime;
			this.flamethrowerEffectResetStopwatch += Time.fixedDeltaTime;
			this.selfForceStopwatch += Time.fixedDeltaTime;
			this.tickDuration = SuppressiveFire.baseTickDuration / this.attackSpeedStat;

			if (base.characterBody)
			{
				base.characterBody.isSprinting = false;
			}

			if (base.isAuthority && this.selfForceStopwatch > SuppressiveFire.baseTickDuration)
			{
				this.selfForceStopwatch -= SuppressiveFire.baseTickDuration;
				if (base.characterMotor && !base.characterMotor.isGrounded)
				{
					base.characterMotor.ApplyForce(base.GetAimRay().direction * -SuppressiveFire.selfForce, false, false);
				}
			}

			if (this.flamethrowerEffectResetStopwatch > SuppressiveFire.flamethrowerEffectResetTimer)   //hacky stuff to get arti's flamethrower effect to loop
			{
				this.flamethrowerEffectResetStopwatch = 0f;
				EntityState.Destroy(this.flamethrowerTransform.gameObject);
				if (this.childLocator)
				{
					Transform transform2 = this.childLocator.FindChild("MuzzleRight");
					if (transform2)
					{
						this.flamethrowerTransform = UnityEngine.Object.Instantiate<GameObject>(SuppressiveFire.flamethrowerEffectPrefab, transform2).transform;
					}
					if (this.flamethrowerTransform)
					{
						this.flamethrowerTransform.GetComponent<ScaleParticleSystemDuration>().newDuration = 2f;
					}
				}
			}

			if (this.flamethrowerStopwatch > this.tickDuration)
			{
				while (this.flamethrowerStopwatch - this.tickDuration > 0f)
				{
					this.flamethrowerStopwatch -= this.tickDuration;
				}
				this.ShootFlame();
			}
			this.UpdateFlamethrowerEffect();
			if ((((!base.inputBank || !base.inputBank.skill2.down) && this.firedShot) || heatController.GetHeat() <= 0f) && base.isAuthority)
			{
				if (base.skillLocator)
                {
					if (heatController.GetHeat() <= 0f)
					{
						base.skillLocator.secondary.stock = 0;
					}
					else
					{
						base.skillLocator.secondary.stock = base.skillLocator.secondary.maxStock;
					}
					base.skillLocator.secondary.rechargeStopwatch = 0f;
				}
				this.outer.SetNextStateToMain();
				return;
			}
		}

		public override void OnExit()
		{
			Util.PlaySound(SuppressiveFire.endAttackSoundString, base.gameObject);
			if (this.flamethrowerTransform)
			{
				EntityState.Destroy(this.flamethrowerTransform.gameObject);
			}
			base.OnExit();
		}

		private void ShootFlame()
		{
			this.firedShot = true;
			if (base.characterBody)
			{
				base.characterBody.SetAimTimer(2f);
			}
			Ray aimRay = base.GetAimRay();
			if (base.isAuthority)
			{
				this.shotCounter = (this.shotCounter + 1) % burnFrequency;
				new BulletAttack
				{
					owner = base.gameObject,
					weapon = base.gameObject,
					origin = aimRay.origin,
					aimVector = aimRay.direction,
					minSpread = 0f,
					damage = SuppressiveFire.damageCoefficient * this.damageStat,
					force = SuppressiveFire.force,
					muzzleName = "",
					hitEffectPrefab = SuppressiveFire.impactEffectPrefab,
					isCrit = base.RollCrit(),
					radius = SuppressiveFire.radius,
					falloffModel = BulletAttack.FalloffModel.None,
					stopperMask = LayerIndex.world.mask,
					procCoefficient = SuppressiveFire.procCoefficient,
					maxDistance = SuppressiveFire.maxDistance,
					smartCollision = true,
					damageType = this.shotCounter == burnFrequency - 1 ? DamageType.IgniteOnHit : DamageType.Generic
				}.Fire();
				heatController.ConsumeHeat(SuppressiveFire.heatCostPerTick * (100f/(100f + SuppressiveFire.backupMagFuelReduction * (base.skillLocator.secondary.stock - 1))));
				base.characterBody.AddSpreadBloom(0.3f);
			}
		}

		private void UpdateFlamethrowerEffect()
		{
			if (this.flamethrowerTransform)
			{
				this.flamethrowerTransform.forward = base.GetAimRay().direction;
			}
		}

		public override InterruptPriority GetMinimumInterruptPriority()
		{
			return InterruptPriority.PrioritySkill;
		}

		public static string startAttackSoundString = "Play_mage_R_start";
		public static string endAttackSoundString = "Play_mage_R_end";
		public static GameObject flamethrowerEffectPrefab = null;
		public static GameObject impactEffectPrefab = Resources.Load<GameObject>("prefabs/effects/impacteffects/missileexplosionvfx");

		public static float selfForce = 600f;

		public static float maxDistance = 21f;
		public static float procCoefficient = 1f;
		public static float radius = 3f;
		public static float force = 0f;
		public static float damageCoefficient = 0.8f;
		public static float baseTickDuration = 0.16f;
		public static float heatCostPerTick = 0.05f;
		public static int burnFrequency = 3;
		public static float backupMagFuelReduction = 15f;

		private PyroHeatController heatController;
		private Transform flamethrowerTransform;
		private Transform muzzleTransform;
		private ChildLocator childLocator;
		private float flamethrowerStopwatch;
		private int shotCounter;
		private float tickDuration;
		private bool firedShot;
		private Vector3 selfForceDirection;
		private float selfForceStopwatch;

		private static float flamethrowerEffectResetTimer = 1.8f;
		private float flamethrowerEffectResetStopwatch;
	}
}

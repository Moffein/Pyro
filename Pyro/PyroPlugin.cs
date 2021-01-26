using BepInEx;
using EntityStates;
using EntityStates.Captain.Weapon;
using RoR2.Projectile;
using Pyro.Skills;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Skills;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Pyro.Controllers;

namespace Pyro
{
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.Moffein.Pyro", "Pyro", "0.0.1")]
    [R2API.Utils.R2APISubmoduleDependency(nameof(SurvivorAPI), nameof(PrefabAPI), nameof(LoadoutAPI), nameof(LanguageAPI), nameof(ResourcesAPI), nameof(BuffAPI))]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class PyroPlugin : BaseUnityPlugin
    {
        Color PyroColor = new Color(215f / 255f, 131f / 255f, 38f / 255f);
        GameObject PyroObject;
        string PyroColorString = "#D78326";
        private static string assetPrefix = "@MoffeinPyro";

        private void Setup()
        {
            LoadResources();
            PyroObject = CloneCommandoBody("PyroBody");
            BuildProjectiles();
            SetupStats(PyroObject);
            AddSkins(PyroObject);
            RegisterPyroSurvivor();
            AssignSkills(PyroObject);
            RegisterLanguageTokens();
        }

        private void RegisterLanguageTokens()
        {
            LanguageAPI.Add("PYRO_BODY_NAME", "Pyro");
            LanguageAPI.Add("PYRO_BODY_SUBTITLE", "Extra Crispy");
            LanguageAPI.Add("PYRO_OUTRO_FLAVOR", "..and so he left, his chest ready to burst.");
            LanguageAPI.Add("PYRO_DEFAULT_SKIN_NAME", "Default");
            LanguageAPI.Add("PYRO_DESCRIPTION", "");

            LanguageAPI.Add("PYRO_PRIMARY_NAME", "Scorch");
            LanguageAPI.Add("PYRO_PRIMARY_DESC", "<color=" + PyroColorString + ">Heat up</color> by burning nearby enemies for <style=cIsDamage>60% damage</style>.\n<style=cIsDamage>High heat</style> <color=" + PyroColorString + ">ignites</color> enemies, <style=cIsDamage>dealing damage over time</style>. ");
            //LanguageAPI.Add("PYRO_PRIMARY_DESC", "<color=" + PyroColorString + ">Ignite</color> enemies for <style=cIsDamage>60% damage</style>.");

            LanguageAPI.Add("PYRO_SECONDARY_NAME", "Heat Wave");
            LanguageAPI.Add("PYRO_SECONDARY_DESC", "<color=" + PyroColorString + ">30% heat</color>. Fire a burst of flames, <color=" + PyroColorString + ">igniting</color> enemies for <style=cIsDamage>400% damage</style> and <style=cIsUtility>reflecting projectiles</style>. Extra stocks <color=" + PyroColorString + ">reduce heat consumption by 15%</color>.");

            LanguageAPI.Add("PYRO_SECONDARY_ALT_NAME", "Suppressive Fire");
            LanguageAPI.Add("PYRO_SECONDARY_ALT_DESC", "<color=" + PyroColorString + ">5% heat</color>. Cremate enemies for <style=cIsDamage>120% damage</style>, <color=" + PyroColorString + ">igniting</color> them. Extra stocks <color=" + PyroColorString + ">reduce heat consumption by 15%</color>.");

            LanguageAPI.Add("PYRO_UTILITY_NAME", "Plan B");
            LanguageAPI.Add("PYRO_UTILITY_DESC", "<color=" + PyroColorString + ">40% heat</color>. <style=cIsUtility>Launch yourself</style> in the direction you are looking at.");

            LanguageAPI.Add("PYRO_SPECIAL_ALT_NAME", "Blazeborne");
            LanguageAPI.Add("PYRO_SPECIAL_ALT_DESC", "<color=" + PyroColorString + ">100% heat</color>. Fire up to 9 seeking flares, <color=" + PyroColorString + ">igniting</color> enemies for <style=cIsDamage>140% damage</style>. <style=cIsDamage>Direct hits</style> against <color=" + PyroColorString + ">ignited</color> enemies deal <style=cIsDamage>bonus damage</style> and <color=" + PyroColorString + ">restore heat</color> for each stack of <color=" + PyroColorString + ">ignite</color>.");
            //LanguageAPI.Add("PYRO_SPECIAL_DESC", "<style=cIsDamage>Stunning</style>. Fire 8 explosive flares that <color=" + PyroColorString + ">ignite</color> enemies for <style=cIsDamage>140% damage</style>. <style=cIsDamage>Direct hits</style> against <color=" + PyroColorString + ">ignited</color> enemies deal <style=cIsDamage>bonus damage</style> for each stack of <color=" + PyroColorString + ">ignite</color> on them.");

            LanguageAPI.Add("PYRO_SPECIAL_NAME", "Parting Shot");
            LanguageAPI.Add("PYRO_SPECIAL_DESC", "<color=" + PyroColorString + ">100% heat</color>. Fire an explosive flare, <color=" + PyroColorString + ">igniting</color> enemies for <style=cIsDamage>140%-1260% damage</style> based on the amount of <color=" + PyroColorString + ">heat used</color>. <style=cIsDamage>Direct hits</style> against <color=" + PyroColorString + ">ignited</color> enemies deal <style=cIsDamage>bonus damage</style> and <color=" + PyroColorString + ">restore heat</color> for each stack of <color=" + PyroColorString + ">ignite</color>.");

            LanguageAPI.Add("KEYWORD_PYRO_HEAT", "<style=cKeywordName>X% heat</style><style=cSub>This skill costs <color=" + PyroColorString + ">X% of your total heat</color>.</style>");
        }

        public void Awake()
        {
            Setup();

            On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
            {
                bool isPyro = false;
                if (damageInfo.attacker != null)
                {
                    CharacterBody cb = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (cb && cb.baseNameToken == "PYRO_BODY_NAME")
                    {
                        isPyro = true;
                    }
                }

                //This could be done less hackily once official custom damagetype support is in.
                bool isScorch = false;
                bool isBlazeborne = false;
                bool isFlaregun = false;
                if (isPyro)
                {
                    if (damageInfo.procCoefficient == Scorch.procCoefficient && damageInfo.damageColorIndex == DamageColorIndex.Default && damageInfo.damageType == DamageType.IgniteOnHit)
                    {
                        isScorch = true;
                        damageInfo.damageType = DamageType.Generic; //Overwrite scorch attempting to ignite.
                    }
                    else if (damageInfo.damageColorIndex == DamageColorIndex.Default && damageInfo.procCoefficient == 1f)
                    {
                        if (damageInfo.damageType == (DamageType.IgniteOnHit | DamageType.SlowOnHit))
                        {
                            isBlazeborne = true;
                            damageInfo.damageType = DamageType.IgniteOnHit; //The SlowOnHit damagetype is purely for identification purposes.
                        }
                        else if (damageInfo.damageType == (DamageType.IgniteOnHit | DamageType.AOE))
                        {
                            isFlaregun = true;
                        }
                    }
                }

                if (isBlazeborne)  //Handle Blazeborne reheating.
                {
                    int burnCount = self.body.GetBuffCount(BuffIndex.OnFire);
                    if (self.body && burnCount > 0)
                    {
                        damageInfo.damage *= 1 + burnCount * Blazeborne.ignitedDirectHitMult;

                        PyroHeatController phc = damageInfo.attacker.GetComponent<PyroHeatController>();
                        if (phc)
                        {
                            phc.RpcAddHeatServer(Blazeborne.ignitedHeatPercentRestore);
                        }
                    }
                }
                else if (isFlaregun)
                {
                    int burnCount = self.body.GetBuffCount(BuffIndex.OnFire);
                    if (self.body && burnCount > 0)
                    {
                        damageInfo.damage *= 1 + burnCount * Flaregun.ignitedDirectHitMult;

                        PyroHeatController phc = damageInfo.attacker.GetComponent<PyroHeatController>();
                        if (phc)
                        {
                            phc.RpcAddHeatServer(Flaregun.ignitedHeatPercentRestore);
                        }
                    }
                }

                orig(self, damageInfo);

                //For Scorch, manually apply a burn DoT so that it will last longer, because Scorch has a low proc coefficient.
                if (isScorch)
                {
                    DotController.InflictDot(self.gameObject, damageInfo.attacker, DotController.DotIndex.Burn, 4.5f, 1f);
                }
            };
        }

        private void AssignSkills(GameObject bodyObject)
        {
            SkillLocator skills = bodyObject.GetComponent<SkillLocator>();
            AssignPrimary(skills);
            AssignSecondary(skills);
            AssignUtility(skills);
            AssignSpecial(skills);
        }
        private void AssignPrimary(SkillLocator sk)
        {
            SkillFamily primarySkillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            primarySkillFamily.defaultVariantIndex = 0u;
            primarySkillFamily.variants = new SkillFamily.Variant[1];
            Reflection.SetFieldValue<SkillFamily>(sk.primary, "_skillFamily", primarySkillFamily);

            SkillDef primaryScorchDef = SkillDef.CreateInstance<SkillDef>();
            primaryScorchDef.activationState = new SerializableEntityStateType(typeof(Scorch));
            primaryScorchDef.activationStateMachineName = "Weapon";
            primaryScorchDef.baseMaxStock = 1;
            primaryScorchDef.baseRechargeInterval = 0f;
            primaryScorchDef.beginSkillCooldownOnSkillEnd = false;
            primaryScorchDef.canceledFromSprinting = false;
            primaryScorchDef.dontAllowPastMaxStocks = true;
            primaryScorchDef.forceSprintDuringState = false;
            primaryScorchDef.fullRestockOnAssign = true;
            primaryScorchDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill1.png");
            primaryScorchDef.interruptPriority = InterruptPriority.Any;
            primaryScorchDef.isBullets = true;
            primaryScorchDef.isCombatSkill = true;
            primaryScorchDef.keywordTokens = new string[] { };
            primaryScorchDef.mustKeyPress = false;
            primaryScorchDef.noSprint = true;
            primaryScorchDef.rechargeStock = 1;
            primaryScorchDef.requiredStock = 1;
            primaryScorchDef.shootDelay = 0f;
            primaryScorchDef.skillName = "Scorch";
            primaryScorchDef.skillNameToken = "PYRO_PRIMARY_NAME";
            primaryScorchDef.skillDescriptionToken = "PYRO_PRIMARY_DESC";
            primaryScorchDef.stockToConsume = 1;
            LoadoutAPI.AddSkill(typeof(Scorch));
            LoadoutAPI.AddSkillDef(primaryScorchDef);
            primarySkillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = primaryScorchDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(primaryScorchDef.skillNameToken, false)
            };
        }

        private void AssignSecondary(SkillLocator sk)
        {
            SkillFamily secondarySkillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            secondarySkillFamily.defaultVariantIndex = 0u;
            secondarySkillFamily.variants = new SkillFamily.Variant[1];
            Reflection.SetFieldValue<SkillFamily>(sk.secondary, "_skillFamily", secondarySkillFamily);

            SkillDef secondaryHeatWaveDef = SkillDef.CreateInstance<SkillDef>();
            secondaryHeatWaveDef.activationState = new SerializableEntityStateType(typeof(HeatWave));
            secondaryHeatWaveDef.activationStateMachineName = "Weapon";
            secondaryHeatWaveDef.baseMaxStock = 1;
            secondaryHeatWaveDef.baseRechargeInterval = 0f;
            secondaryHeatWaveDef.beginSkillCooldownOnSkillEnd = true;
            secondaryHeatWaveDef.canceledFromSprinting = false;
            secondaryHeatWaveDef.dontAllowPastMaxStocks = true;
            secondaryHeatWaveDef.forceSprintDuringState = false;
            secondaryHeatWaveDef.fullRestockOnAssign = true;
            secondaryHeatWaveDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill2.png");
            secondaryHeatWaveDef.interruptPriority = InterruptPriority.Skill;
            secondaryHeatWaveDef.isBullets = true;
            secondaryHeatWaveDef.isCombatSkill = true;
            secondaryHeatWaveDef.keywordTokens = new string[] { "KEYWORD_PYRO_HEAT" };
            secondaryHeatWaveDef.mustKeyPress = false;
            secondaryHeatWaveDef.noSprint = false;
            secondaryHeatWaveDef.rechargeStock = 1;
            secondaryHeatWaveDef.requiredStock = 1;
            secondaryHeatWaveDef.shootDelay = 0f;
            secondaryHeatWaveDef.skillName = "FireBlast";
            secondaryHeatWaveDef.skillNameToken = "PYRO_SECONDARY_NAME";
            secondaryHeatWaveDef.skillDescriptionToken = "PYRO_SECONDARY_DESC";
            secondaryHeatWaveDef.stockToConsume = 0;
            LoadoutAPI.AddSkill(typeof(HeatWave));
            LoadoutAPI.AddSkillDef(secondaryHeatWaveDef);
            secondarySkillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = secondaryHeatWaveDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(secondaryHeatWaveDef.skillNameToken, false)
            };

            SkillDef secondaryBurnDef = SkillDef.CreateInstance<SkillDef>();
            secondaryBurnDef.activationState = new SerializableEntityStateType(typeof(SuppressiveFire));
            secondaryBurnDef.activationStateMachineName = "Weapon";
            secondaryBurnDef.baseMaxStock = 1;
            secondaryBurnDef.baseRechargeInterval = 0f;
            secondaryBurnDef.beginSkillCooldownOnSkillEnd = true;
            secondaryBurnDef.canceledFromSprinting = false;
            secondaryBurnDef.dontAllowPastMaxStocks = true;
            secondaryBurnDef.forceSprintDuringState = false;
            secondaryBurnDef.fullRestockOnAssign = true;
            secondaryBurnDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill2.png");
            secondaryBurnDef.interruptPriority = InterruptPriority.Skill;
            secondaryBurnDef.isBullets = true;
            secondaryBurnDef.isCombatSkill = true;
            secondaryBurnDef.keywordTokens = new string[] { "KEYWORD_PYRO_HEAT" };
            secondaryBurnDef.mustKeyPress = false;
            secondaryBurnDef.noSprint = true;
            secondaryBurnDef.rechargeStock = 1;
            secondaryBurnDef.requiredStock = 1;
            secondaryBurnDef.shootDelay = 0f;
            secondaryBurnDef.skillName = "SuppressiveFire";
            secondaryBurnDef.skillNameToken = "PYRO_SECONDARY_ALT_NAME";
            secondaryBurnDef.skillDescriptionToken = "PYRO_SECONDARY_ALT_DESC";
            secondaryBurnDef.stockToConsume = 0;
            LoadoutAPI.AddSkillDef(secondaryBurnDef);
            Array.Resize(ref secondarySkillFamily.variants, secondarySkillFamily.variants.Length + 1);
            secondarySkillFamily.variants[secondarySkillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = secondaryBurnDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(secondaryBurnDef.skillNameToken, false)
            };
            LoadoutAPI.AddSkill(typeof(SuppressiveFire));
        }

        private void AssignUtility(SkillLocator sk)
        {
            JetpackStateMachineSetup(sk.gameObject);

            SkillFamily utilitySkillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            utilitySkillFamily.defaultVariantIndex = 0u;
            utilitySkillFamily.variants = new SkillFamily.Variant[1];
            Reflection.SetFieldValue<SkillFamily>(sk.utility, "_skillFamily", utilitySkillFamily);

            SkillDef utilityJetpackDef = SkillDef.CreateInstance<SkillDef>();
            utilityJetpackDef.activationState = new SerializableEntityStateType(typeof(Jetpack));
            utilityJetpackDef.activationStateMachineName = "Jetpack";
            utilityJetpackDef.baseMaxStock = 1;
            utilityJetpackDef.baseRechargeInterval = 5f;
            utilityJetpackDef.beginSkillCooldownOnSkillEnd = true;
            utilityJetpackDef.canceledFromSprinting = false;
            utilityJetpackDef.dontAllowPastMaxStocks = true;
            utilityJetpackDef.forceSprintDuringState = true;
            utilityJetpackDef.fullRestockOnAssign = true;
            utilityJetpackDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill3.png");
            utilityJetpackDef.interruptPriority = InterruptPriority.Skill;
            utilityJetpackDef.isBullets = false;
            utilityJetpackDef.isCombatSkill = false;
            utilityJetpackDef.keywordTokens = new string[] { "KEYWORD_PYRO_HEAT" };
            utilityJetpackDef.mustKeyPress = false;
            utilityJetpackDef.noSprint = false;
            utilityJetpackDef.rechargeStock = 1;
            utilityJetpackDef.requiredStock = 1;
            utilityJetpackDef.shootDelay = 0f;
            utilityJetpackDef.skillName = "Jetpack";
            utilityJetpackDef.skillNameToken = "PYRO_UTILITY_NAME";
            utilityJetpackDef.skillDescriptionToken = "PYRO_UTILITY_DESC";
            utilityJetpackDef.stockToConsume = 1;
            LoadoutAPI.AddSkill(typeof(Jetpack));
            LoadoutAPI.AddSkillDef(utilityJetpackDef);
            utilitySkillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = utilityJetpackDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(utilityJetpackDef.skillNameToken, false)
            };
        }

        private void JetpackStateMachineSetup(GameObject go)
        {
            EntityStateMachine jetpackMachine = go.AddComponent<EntityStateMachine>();
            jetpackMachine.customName = "Jetpack";
            jetpackMachine.initialStateType = new SerializableEntityStateType(typeof(EntityStates.BaseBodyAttachmentState));
            jetpackMachine.mainStateType = new SerializableEntityStateType(typeof(EntityStates.BaseBodyAttachmentState));
        }

        private void AssignSpecial(SkillLocator sk)
        {
            SkillFamily specialSkillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            specialSkillFamily.defaultVariantIndex = 0u;
            specialSkillFamily.variants = new SkillFamily.Variant[1];
            Reflection.SetFieldValue<SkillFamily>(sk.special, "_skillFamily", specialSkillFamily);

            SkillDef specialFlareDef = SkillDef.CreateInstance<SkillDef>();
            specialFlareDef.activationState = new SerializableEntityStateType(typeof(Flaregun));
            specialFlareDef.activationStateMachineName = "Weapon";
            specialFlareDef.baseMaxStock = 1;
            specialFlareDef.baseRechargeInterval = 10f;
            specialFlareDef.beginSkillCooldownOnSkillEnd = true;
            specialFlareDef.canceledFromSprinting = false;
            specialFlareDef.dontAllowPastMaxStocks = true;
            specialFlareDef.forceSprintDuringState = false;
            specialFlareDef.fullRestockOnAssign = true;
            specialFlareDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill4.png");
            specialFlareDef.interruptPriority = InterruptPriority.PrioritySkill;
            specialFlareDef.isBullets = false;
            specialFlareDef.isCombatSkill = true;
            specialFlareDef.keywordTokens = new string[] { "KEYWORD_PYRO_HEAT" };
            specialFlareDef.mustKeyPress = false;
            specialFlareDef.noSprint = true;
            specialFlareDef.rechargeStock = 1;
            specialFlareDef.requiredStock = 1;
            specialFlareDef.shootDelay = 0f;
            specialFlareDef.skillName = "Flaregun";
            specialFlareDef.skillNameToken = "PYRO_SPECIAL_NAME";
            specialFlareDef.skillDescriptionToken = "PYRO_SPECIAL_DESC";
            specialFlareDef.stockToConsume = 1;
            LoadoutAPI.AddSkill(typeof(Flaregun));
            LoadoutAPI.AddSkillDef(specialFlareDef);
            specialSkillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = specialFlareDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(specialFlareDef.skillNameToken, false)
            };

            SkillDef specialBlazeborneDef = SkillDef.CreateInstance<SkillDef>();
            specialBlazeborneDef.activationState = new SerializableEntityStateType(typeof(Blazeborne));
            specialBlazeborneDef.activationStateMachineName = "Weapon";
            specialBlazeborneDef.baseMaxStock = 1;
            specialBlazeborneDef.baseRechargeInterval = 10f;
            specialBlazeborneDef.beginSkillCooldownOnSkillEnd = true;
            specialBlazeborneDef.canceledFromSprinting = false;
            specialBlazeborneDef.dontAllowPastMaxStocks = true;
            specialBlazeborneDef.forceSprintDuringState = false;
            specialBlazeborneDef.fullRestockOnAssign = true;
            specialBlazeborneDef.icon = Resources.Load<Sprite>(assetPrefix + ":skill4.png");
            specialBlazeborneDef.interruptPriority = InterruptPriority.PrioritySkill;
            specialBlazeborneDef.isBullets = false;
            specialBlazeborneDef.isCombatSkill = true;
            specialBlazeborneDef.keywordTokens = new string[] { "KEYWORD_PYRO_HEAT" };
            specialBlazeborneDef.mustKeyPress = false;
            specialBlazeborneDef.noSprint = true;
            specialBlazeborneDef.rechargeStock = 1;
            specialBlazeborneDef.requiredStock = 1;
            specialBlazeborneDef.shootDelay = 0f;
            specialBlazeborneDef.skillName = "Blazeborne";
            specialBlazeborneDef.skillNameToken = "PYRO_SPECIAL_ALT_NAME";
            specialBlazeborneDef.skillDescriptionToken = "PYRO_SPECIAL_ALT_DESC";
            specialBlazeborneDef.stockToConsume = 1;
            LoadoutAPI.AddSkill(typeof(Blazeborne));
            LoadoutAPI.AddSkillDef(specialBlazeborneDef);
            Array.Resize(ref specialSkillFamily.variants, specialSkillFamily.variants.Length + 1);
            specialSkillFamily.variants[specialSkillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = specialBlazeborneDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(specialBlazeborneDef.skillNameToken, false)
            };

            //Unused
            LoadoutAPI.AddSkill(typeof(PrepFlare));
            LoadoutAPI.AddSkill(typeof(FireFlareBurst));
        }

        private void BuildProjectiles()
        {
            BuildFlareProjectile();
            BuildBlazeborneProjectile();
        }

        private void BuildFlareProjectile()
        {
            GameObject flareProjectile = Resources.Load<GameObject>("prefabs/projectiles/magefireboltbasic").InstantiateClone("SS2PyroFlare", true);
            ProjectileCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(flareProjectile);
            };

            ProjectileSimple ps = flareProjectile.GetComponent<ProjectileSimple>();
            ps.lifetime = 18f;

            ProjectileImpactExplosion pie = flareProjectile.GetComponent<ProjectileImpactExplosion>();
            pie.blastRadius = 8f;
            pie.blastDamageCoefficient = 1f;
            pie.blastProcCoefficient = 1f;
            pie.timerAfterImpact = false;
            pie.falloffModel = BlastAttack.FalloffModel.None;
            pie.lifetime = 18f;
            pie.destroyOnEnemy = true;
            pie.destroyOnWorld = true;

            ProjectileDamage pd = flareProjectile.GetComponent<ProjectileDamage>();
            pd.damage = 0f;
            pd.damageColorIndex = DamageColorIndex.Default;
            pd.damageType = DamageType.IgniteOnHit;
            Flaregun.projectilePrefab = flareProjectile;
            FireFlareBurst.projectilePrefab = flareProjectile;  //unused skill

            GameObject reflectProjectile = Resources.Load<GameObject>("prefabs/projectiles/magefireboltbasic").InstantiateClone("SS2PyroFlareReflect", true);
            ProjectileCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(reflectProjectile);
            };
            ProjectileImpactExplosion pie2 = reflectProjectile.GetComponent<ProjectileImpactExplosion>();
            pie2.blastRadius = 8f;
            pie2.blastDamageCoefficient = 1f;
            pie2.blastProcCoefficient = 1f;
            pie2.timerAfterImpact = false;
            pie2.falloffModel = BlastAttack.FalloffModel.None;
            pie2.lifetime = 18f;
            pie2.destroyOnEnemy = true;
            pie2.destroyOnWorld = true;

            ProjectileDamage pd2 = reflectProjectile.GetComponent<ProjectileDamage>();
            pd2.damage = 0f;
            pd2.damageColorIndex = DamageColorIndex.Default;
            pd2.damageType = DamageType.IgniteOnHit;

            HeatWave.reflectProjectilePrefab = reflectProjectile;
        }

        private void BuildBlazeborneProjectile()
        {
            GameObject projectile = Resources.Load<GameObject>("prefabs/projectiles/EngiHarpoon").InstantiateClone("SS2PyroBlazeborneMissle", true);
            ProjectileCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(projectile);
            };

            ProjectileDamage pd = projectile.GetComponent<ProjectileDamage>();
            pd.damage = 0f;
            pd.damageColorIndex = DamageColorIndex.Default;
            pd.damageType = (DamageType.IgniteOnHit | DamageType.SlowOnHit);    //damagetype is for identification purposes. refactor once custom damagetype support exists

            Blazeborne.projectilePrefab = projectile;
        }

        private void RegisterPyroSurvivor()
        {
            GameObject tempDisplay = PyroObject.GetComponent<ModelLocator>().modelTransform.gameObject;
            SurvivorDef item = new SurvivorDef
            {
                name = "Pyro",
                bodyPrefab = PyroObject,
                descriptionToken = "PYRO_DESCRPTION",
                displayPrefab = tempDisplay,
                primaryColor = PyroColor,
                unlockableName = "",
                outroFlavorToken = "PYRO_OUTRO_FLAVOR"
            };
            SurvivorAPI.AddSurvivor(item);
        }

        private GameObject CloneCommandoBody(string bodyname)
        {
            GameObject go = Resources.Load<GameObject>("prefabs/characterbodies/CommandoBody").InstantiateClone(bodyname, true);
            BodyCatalog.getAdditionalEntries += delegate (List<GameObject> list)
            {
                list.Add(go);
            };
            return go;
        }

        private void SetupStats(GameObject bodyObject)
        {
            Controllers.PyroHeatController.heatGauge = Resources.Load<Texture2D>(assetPrefix + ":heatgauge.png");
            Controllers.PyroHeatController.heatGauge_Heated = Resources.Load<Texture2D>(assetPrefix + ":heatgauge_heated.png");
            Controllers.PyroHeatController.heatBar = Resources.Load<Texture2D>(assetPrefix + ":heatbar.png");
            Controllers.PyroHeatController.heatBar_Heated = Resources.Load<Texture2D>(assetPrefix + ":heatbar_heated.png");

            bodyObject.AddComponent<Controllers.PyroHeatController>();
            CharacterBody cb = bodyObject.GetComponent<CharacterBody>();
            cb.bodyFlags = CharacterBody.BodyFlags.ImmuneToExecutes;
            cb.baseNameToken = "PYRO_BODY_NAME";
            cb.subtitleNameToken = "PYRO_BODY_SUBTITLE";
            cb.crosshairPrefab = Resources.Load<GameObject>("prefabs/crosshair/banditcrosshair");
            cb.baseMaxHealth = 100f;
            cb.baseRegen = 1f;
            cb.baseMaxShield = 0f;
            cb.baseMoveSpeed = 7f;
            cb.baseAcceleration = 80f;
            cb.baseJumpPower = 15f;
            cb.baseDamage = 11f;
            cb.baseAttackSpeed = 1f;
            cb.baseCrit = 1f;
            cb.baseArmor = 0f;
            cb.baseJumpCount = 1;
            cb.tag = "Player";

            cb.autoCalculateLevelStats = true;
            cb.levelMaxHealth = cb.baseMaxHealth * 0.3f;
            cb.levelRegen = cb.baseRegen * 0.2f;
            cb.levelMaxShield = 0f;
            cb.levelMoveSpeed = 0f;
            cb.levelJumpPower = 0f;
            cb.levelDamage = cb.baseDamage * 0.2f;
            cb.levelAttackSpeed = 0f;
            cb.levelCrit = 0f;
            cb.levelArmor = 0f;
        }

        private void AddSkins(GameObject bodyObject)    //credits to rob
        {
            GameObject bodyPrefab = bodyObject;
            GameObject model = bodyPrefab.GetComponentInChildren<ModelLocator>().modelTransform.gameObject;
            CharacterModel characterModel = model.GetComponent<CharacterModel>();

            ModelSkinController skinController = null;
            if (model.GetComponent<ModelSkinController>())
                skinController = model.GetComponent<ModelSkinController>();
            else
                skinController = model.AddComponent<ModelSkinController>();

            SkinnedMeshRenderer mainRenderer = Reflection.GetFieldValue<SkinnedMeshRenderer>(characterModel, "mainSkinnedMeshRenderer");
            if (mainRenderer == null)
            {
                CharacterModel.RendererInfo[] bRI = Reflection.GetFieldValue<CharacterModel.RendererInfo[]>(characterModel, "baseRendererInfos");
                if (bRI != null)
                {
                    foreach (CharacterModel.RendererInfo rendererInfo in bRI)
                    {
                        if (rendererInfo.renderer is SkinnedMeshRenderer)
                        {
                            mainRenderer = (SkinnedMeshRenderer)rendererInfo.renderer;
                            break;
                        }
                    }
                    if (mainRenderer != null)
                    {
                        characterModel.SetFieldValue<SkinnedMeshRenderer>("mainSkinnedMeshRenderer", mainRenderer);
                    }
                }
            }

            LoadoutAPI.SkinDefInfo skinDefInfo = default(LoadoutAPI.SkinDefInfo);
            skinDefInfo.BaseSkins = Array.Empty<SkinDef>();
            skinDefInfo.GameObjectActivations = Array.Empty<SkinDef.GameObjectActivation>();
            skinDefInfo.Icon = LoadoutAPI.CreateSkinIcon(Color.white, Color.white, Color.white, Color.white);
            skinDefInfo.MeshReplacements = new SkinDef.MeshReplacement[]
            {
                new SkinDef.MeshReplacement
                {
                    renderer = mainRenderer,
                    mesh = mainRenderer.sharedMesh
                }
            };
            skinDefInfo.Name = "PYRO_DEFAULT_SKIN_NAME";
            skinDefInfo.NameToken = "PYRO_DEFAULT_SKIN_NAME";
            skinDefInfo.RendererInfos = characterModel.baseRendererInfos;
            skinDefInfo.RootObject = model;
            skinDefInfo.UnlockableName = "";
            skinDefInfo.MinionSkinReplacements = new SkinDef.MinionSkinReplacement[0];
            skinDefInfo.ProjectileGhostReplacements = new SkinDef.ProjectileGhostReplacement[0];

            SkinDef defaultSkin = LoadoutAPI.CreateNewSkinDef(skinDefInfo);

            skinController.skins = new SkinDef[1]
            {
                defaultSkin,
            };
        }

        public static void LoadResources()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Pyro.pyrobundle"))
            {
                var bundle = AssetBundle.LoadFromStream(stream);
                var provider = new R2API.AssetBundleResourcesProvider(assetPrefix, bundle);
                R2API.ResourcesAPI.AddProvider(provider);
            }
        }
    }
}

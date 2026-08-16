using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Infrastructure.ResourceNeed
{
    [Serializable]
    public class CategoryCurveSetting
    {
        [SerializeField] private ResourceCategory category;
        [SerializeField] private AnimationCurve needCurve;
        [SerializeField, Min(0f)] private float minWeight = 0.5f;
        [SerializeField, Min(0f)] private float maxWeight = 2.0f;
        [SerializeField, Min(0f)] private float defaultWeight = 1.0f;

        public ResourceCategory Category => category;
        public AnimationCurve NeedCurve => needCurve;
        public float MinWeight => minWeight;
        public float MaxWeight => maxWeight;
        public float DefaultWeight => defaultWeight;

        public CategoryCurveSetting()
        {
            needCurve = CreateStandardCurve();
        }

        public CategoryCurveSetting(ResourceCategory category, AnimationCurve curve, float minWeight = 0.5f, float maxWeight = 2.0f, float defaultWeight = 1.0f)
        {
            this.category = category;
            this.needCurve = curve ?? CreateStandardCurve();
            this.minWeight = minWeight;
            this.maxWeight = maxWeight;
            this.defaultWeight = defaultWeight;
        }

        public static AnimationCurve CreateStandardCurve()
        {
            return new AnimationCurve(
                new Keyframe(0.0f, 2.0f),
                new Keyframe(0.5f, 1.0f),
                new Keyframe(1.0f, 0.5f)
            );
        }

        public NeedWeightCategoryConfig ToDomainCategoryConfig(int sampleCount = 21)
        {
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                samples[i] = needCurve != null ? needCurve.Evaluate(t) : Mathf.Lerp(2.0f, 0.5f, t);
            }
            var evaluator = new SampledPiecewiseCurveEvaluator(samples);
            return new NeedWeightCategoryConfig(category, evaluator, minWeight, maxWeight, defaultWeight);
        }
    }

    [CreateAssetMenu(fileName = "PlayerNeedWeightSettings", menuName = "Shinzui/ResourceNeed/PlayerNeedWeightSettings")]
    public class PlayerNeedWeightSettingsSO : ScriptableObject
    {
        [Header("Target Amounts (充足目標量)")]
        [SerializeField, Min(1)] private int targetHealingItems = 3;
        [SerializeField, Min(1)] private int targetBatteries = 4;
        [SerializeField, Min(1)] private int targetWeapons = 2;
        [SerializeField, Min(1)] private int targetUtilityItems = 5;
        [SerializeField, Min(1)] private int targetRareItems = 2;
        [SerializeField, Min(1)] private int defaultTargetReserveAmmoPerWeapon = 30;

        [Header("Healing Ratio Coefficients")]
        [SerializeField, Range(0f, 1f)] private float healingHpWeight = 0.6f;
        [SerializeField, Range(0f, 1f)] private float healingItemWeight = 0.4f;

        [Header("Light Ratio Coefficients")]
        [SerializeField, Range(0f, 1f)] private float lightChargeWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float lightBatteryWeight = 0.5f;

        [Header("Ammo Ratio Coefficients")]
        [SerializeField, Range(0f, 1f)] private float ammoLoadedWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float ammoReserveWeight = 0.5f;
        [SerializeField, Range(0f, 1f)] private float noWeaponDefaultAmmoRatio = 1.0f;

        [Header("Category Curve & Clamp Settings")]
        [SerializeField] private List<CategoryCurveSetting> categoryCurveSettings = new();

        public PlayerNeedWeightEvaluationConfig ToDomainConfig()
        {
            var dict = new Dictionary<ResourceCategory, NeedWeightCategoryConfig>();
            if (categoryCurveSettings != null)
            {
                foreach (var setting in categoryCurveSettings)
                {
                    if (setting != null)
                    {
                        dict[setting.Category] = setting.ToDomainCategoryConfig();
                    }
                }
            }

            // 未設定のカテゴリにデフォルト補完
            foreach (ResourceCategory cat in Enum.GetValues(typeof(ResourceCategory)))
            {
                if (!dict.ContainsKey(cat))
                {
                    dict[cat] = new NeedWeightCategoryConfig(cat);
                }
            }

            return new PlayerNeedWeightEvaluationConfig(
                targetHealingItems,
                targetBatteries,
                targetWeapons,
                targetUtilityItems,
                targetRareItems,
                defaultTargetReserveAmmoPerWeapon,
                healingHpWeight,
                healingItemWeight,
                lightChargeWeight,
                lightBatteryWeight,
                ammoLoadedWeight,
                ammoReserveWeight,
                noWeaponDefaultAmmoRatio,
                dict
            );
        }

        public static PlayerNeedWeightSettingsSO CreateDefault()
        {
            var so = CreateInstance<PlayerNeedWeightSettingsSO>();
            so.targetHealingItems = 3;
            so.targetBatteries = 4;
            so.targetWeapons = 2;
            so.targetUtilityItems = 5;
            so.targetRareItems = 2;
            so.defaultTargetReserveAmmoPerWeapon = 30;
            so.healingHpWeight = 0.6f;
            so.healingItemWeight = 0.4f;
            so.lightChargeWeight = 0.5f;
            so.lightBatteryWeight = 0.5f;
            so.ammoLoadedWeight = 0.5f;
            so.ammoReserveWeight = 0.5f;
            so.noWeaponDefaultAmmoRatio = 1.0f;

            so.categoryCurveSettings = new List<CategoryCurveSetting>
            {
                new(ResourceCategory.Healing, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
                new(ResourceCategory.Ammo, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
                new(ResourceCategory.LightResource, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
                new(ResourceCategory.Weapon, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
                new(ResourceCategory.Utility, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
                new(ResourceCategory.Rare, CategoryCurveSetting.CreateStandardCurve(), 0.5f, 2.0f, 1.0f),
            };

            return so;
        }
    }
}

#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using PartyRacers.AI;
using PartyRacers.UI.Settings;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.Tests
{
    public sealed class ElectricTrapEditModeTests
    {
        private const string Root = "Assets/_Projeto/Prefabs/PowerUps/EletricTrap";
        private const string TrapPath = Root + "/Prefabs/ElectricTrap.prefab";
        private const string DefinitionPath = Root + "/Config/Resources/PowerDefinitions/Power_ArmadilhaEletrica.asset";

        [Test]
        public void Prefab_IsRuntimeReadyAndSafeWhileEquipped()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrapPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject owner = new GameObject("ElectricTrap_EditModeOwner");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                ElectricTrapPower trap = instance.GetComponent<ElectricTrapPower>();
                Rigidbody body = instance.GetComponent<Rigidbody>();
                Collider physical = instance.GetComponent<BoxCollider>();
                SphereCollider trigger = instance.transform.Find("ActivationTrigger")?.GetComponent<SphereCollider>();

                Assert.That(trap, Is.Not.Null);
                trap.SetEquipped(owner, null);

                Assert.That(body, Is.Not.Null);
                Assert.That(physical, Is.Not.Null);
                Assert.That(trigger, Is.Not.Null);
                Assert.That(trigger.isTrigger, Is.True);
                Assert.That(trigger.enabled, Is.False);
                Assert.That(physical.enabled, Is.False);
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.detectCollisions, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Prefab_UsesOfficialAtlasAndHasNoTimedExpiry()
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/EletricTrap.jpg");
            Material bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_ElectricTrapBody.mat");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrapPath);
            Renderer modelRenderer = prefab != null
                ? prefab.transform.Find("VisualRoot/ElectricTrap_Model")?.GetComponentInChildren<Renderer>(true)
                : null;

            Assert.That(atlas, Is.Not.Null);
            Assert.That(bodyMaterial, Is.Not.Null);
            Assert.That(bodyMaterial.GetTexture("_BaseMap"), Is.SameAs(atlas));
            Assert.That(modelRenderer, Is.Not.Null);
            Assert.That(modelRenderer.sharedMaterial, Is.SameAs(bodyMaterial));

            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(ElectricTrapPower).GetField("maximumLifetime", fields), Is.Null);
            Assert.That(typeof(ElectricTrapPower).GetField("lifetime", fields), Is.Null);
        }

        [Test]
        public void Prefab_HasMandatoryBalanceAndVisualReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrapPath);
            ElectricTrapPower trap = prefab != null ? prefab.GetComponent<ElectricTrapPower>() : null;
            Assert.That(trap, Is.Not.Null);
            Assert.That(trap.SpeedMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(trap.ShockDuration, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(trap.ArmingDelay, Is.EqualTo(0.5f).Within(0.0001f));

            SerializedObject serialized = new SerializedObject(trap);
            Assert.That(serialized.FindProperty("impactVfxPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("auraVfxPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("armedVfx").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("warningLight").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("warningRenderer").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void HudDefinition_UsesMineIconsAndElectricTrapType()
        {
            PowerDefinition definition = AssetDatabase.LoadAssetAtPath<PowerDefinition>(DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.tipo, Is.EqualTo(KartPowerType.ElectricTrap));
            Assert.That(definition.nomeExibido, Is.EqualTo("ARMADILHA ELÉTRICA"));
            Assert.That(definition.iconeColorido, Is.Not.Null);
            Assert.That(definition.iconeMono, Is.Not.Null);
            Assert.That(definition.iconeCinza, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(definition.iconeColorido), Does.EndWith("Power_Mine_Color.png"));
        }

        [Test]
        public void DistributionAndBotFlow_IncludeElectricTrap()
        {
            GameObject itemBoxObject = new GameObject("ElectricTrap_EditModeItemBox");
            try
            {
                ItemBox itemBox = itemBoxObject.AddComponent<ItemBox>();
                SerializedProperty powers = new SerializedObject(itemBox).FindProperty("availablePowers");
                bool includesTrap = false;
                for (int i = 0; i < powers.arraySize; i++)
                {
                    if ((KartPowerType)powers.GetArrayElementAtIndex(i).enumValueIndex == KartPowerType.ElectricTrap)
                    {
                        includesTrap = true;
                        break;
                    }
                }

                Assert.That(includesTrap, Is.True);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.That(typeof(BotPowerController).GetField("trapRearDetectionDistance", fields), Is.Not.Null);
                Assert.That(typeof(BotPowerController).GetField("trapMaxHoldSeconds", fields), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(itemBoxObject);
            }
        }

        [Test]
        public void VfxWrappers_UseLocalRuntimeMaterials()
        {
            string[] wrapperPaths =
            {
                Root + "/VFX/ElectricTrap_Impact.prefab",
                Root + "/VFX/ElectricTrap_TargetAura.prefab",
                Root + "/VFX/ElectricTrap_Armed.prefab"
            };
            const string localMaterialRoot = "Assets/_Projeto/Prefabs/PowerUps/EletricTrap/Materials/HovlRuntime/";
            int materialSlots = 0;

            for (int i = 0; i < wrapperPaths.Length; i++)
            {
                GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPaths[i]);
                Assert.That(wrapper, Is.Not.Null);
                Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] materials = renderers[r].sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        materialSlots++;
                        Assert.That(AssetDatabase.GetAssetPath(materials[m]), Does.StartWith(localMaterialRoot));
                    }
                }
            }

            Assert.That(materialSlots, Is.GreaterThan(0));
        }

        [TestCase("Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab")]
        [TestCase("Assets/_Projeto/Prefabs/Cars/PlayerKart_Network.prefab")]
        public void KartPrefab_IsWiredToFinalTrap(string kartPath)
        {
            GameObject kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(kartPath);
            KartPowerUser powerUser = kartPrefab != null ? kartPrefab.GetComponent<KartPowerUser>() : null;
            Assert.That(powerUser, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(powerUser);
            GameObject trapPrefab = serialized.FindProperty("electricTrapPrefab").objectReferenceValue as GameObject;
            Assert.That(trapPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(trapPrefab), Is.EqualTo(TrapPath));

            // Referências dos poderes existentes continuam presentes.
            Assert.That(serialized.FindProperty("rocketProjectilePrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("ufoProjectilePrefab").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void SpeedModifier_IsExactlyHalfAndDoesNotStackOnSameSource()
        {
            GameObject kartObject = new GameObject("ElectricTrap_EditModeKart");
            GameObject source = new GameObject("ElectricTrap_EditModeSource");
            try
            {
                kartObject.AddComponent<Rigidbody>();
                KartController kart = kartObject.AddComponent<KartController>();
                float baseSpeed = kart.MaxForwardSpeedKmh;

                kart.SetSpeedLimitMultiplier(source, 0.5f);
                kart.SetSpeedLimitMultiplier(source, 0.5f);

                Assert.That(kart.SpeedLimitMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(kart.CurrentMaxForwardSpeedKmh, Is.EqualTo(baseSpeed * 0.5f).Within(0.001f));

                kart.RemoveSpeedLimitMultiplier(source);
                Assert.That(kart.SpeedLimitMultiplier, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(kart.CurrentMaxForwardSpeedKmh, Is.EqualTo(baseSpeed).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(kartObject);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void RepeatedShock_ReusesComponentAndRestartsWithoutStacking()
        {
            GameObject kartObject = new GameObject("ElectricTrap_ReapplyKart");
            try
            {
                kartObject.AddComponent<Rigidbody>();
                KartController kart = kartObject.AddComponent<KartController>();

                KartElectricShockEffect first = KartElectricShockEffect.ApplyTo(kartObject, 3f, 0.5f, null);
                KartElectricShockEffect second = KartElectricShockEffect.ApplyTo(kartObject, 3f, 0.5f, null);

                Assert.That(first, Is.SameAs(second));
                Assert.That(kartObject.GetComponents<KartElectricShockEffect>().Length, Is.EqualTo(1));
                Assert.That(kart.SpeedLimitMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(second.RemainingSeconds, Is.GreaterThan(2.9f));

                second.Cancel();
                Assert.That(kart.SpeedLimitMultiplier, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(kartObject);
            }
        }

        [Test]
        public void ExistingEnumValuesRemainStable()
        {
            Assert.That((int)KartPowerType.None, Is.EqualTo(0));
            Assert.That((int)KartPowerType.SwapPosition, Is.EqualTo(1));
            Assert.That((int)KartPowerType.Rocket, Is.EqualTo(2));
            Assert.That((int)KartPowerType.Shield, Is.EqualTo(3));
            Assert.That((int)KartPowerType.ElectricTrap, Is.EqualTo(4));
        }
    }
}
#endif

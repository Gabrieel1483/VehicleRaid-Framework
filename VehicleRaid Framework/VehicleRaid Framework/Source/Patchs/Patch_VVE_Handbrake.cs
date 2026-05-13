using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using Verse.Sound;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace VehicleRaidFramework
{





    [StaticConstructorOnStartup]
    public static class Patch_VVE_Handbrake
    {

        static Patch_VVE_Handbrake()
        {
            try
            {
                ApplyPatches();
            }
            catch (Exception ex)
            {
}
        }

        private static void ApplyPatches()
        {
            var harmony = new Harmony("com.vrf.patches.vve.handbrake");

            Type compType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    compType = asm.GetType("VanillaVehiclesExpanded.CompVehicleMovementController");
                    if (compType != null) break;
                }
                catch { }
            }

            if (compType == null)
            {
return;
            }

            MethodInfo slowdownMethod = compType.GetMethod("Slowdown",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (slowdownMethod != null)
            {
                harmony.Patch(slowdownMethod,
                    prefix: new HarmonyMethod(typeof(Patch_VVE_Handbrake), nameof(Slowdown_Prefix)),
                    postfix: new HarmonyMethod(typeof(Patch_VVE_Handbrake), nameof(Slowdown_Postfix)));
            }
            else
            {
}
        }






        public static void Slowdown_Prefix(object __instance, out bool __state)
        {
            __state = false;
            try
            {

                var vehicleProp = __instance.GetType().GetProperty("Vehicle",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (vehicleProp == null) return;

                var vehicle = vehicleProp.GetValue(__instance) as VehiclePawn;
                if (vehicle == null) return;

                if (vehicle.Faction != null && !vehicle.Faction.IsPlayer)
                {
                    __state = true;

                    Type settingsType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            settingsType = asm.GetType("VanillaVehiclesExpanded.VanillaVehiclesExpandedSettings");
                            if (settingsType != null) break;
                        }
                        catch { }
                    }

                    if (settingsType != null)
                    {
                        var field = settingsType.GetField("handbrakeDealsDamage",
                            BindingFlags.Public | BindingFlags.Static);
                        if (field != null)
                        {
                            bool currentVal = (bool)field.GetValue(null);
                            if (currentVal)
                            {

                                field.SetValue(null, false);
                            }
                        }
                    }
                }
            }
            catch { }
        }




        public static void Slowdown_Postfix(object __instance, bool __state)
        {
            if (!__state) return;

            try
            {
                Type settingsType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        settingsType = asm.GetType("VanillaVehiclesExpanded.VanillaVehiclesExpandedSettings");
                        if (settingsType != null) break;
                    }
                    catch { }
                }

                if (settingsType != null)
                {
                    var field = settingsType.GetField("handbrakeDealsDamage",
                        BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                    {

                        field.SetValue(null, true);
                    }
                }

                var isScreechingField = __instance.GetType().GetField("isScreeching",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var sustainerField = __instance.GetType().GetField("screechingSustainer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (isScreechingField != null)
                    isScreechingField.SetValue(__instance, false);

                if (sustainerField != null)
                {
                    var sustainer = sustainerField.GetValue(__instance) as Sustainer;
                    if (sustainer != null && !sustainer.Ended)
                    {
                        sustainer.End();
                    }
                    sustainerField.SetValue(__instance, null);
                }
            }
            catch { }
        }
    }
}




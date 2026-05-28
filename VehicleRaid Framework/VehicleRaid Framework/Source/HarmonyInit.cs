using HarmonyLib;
using Verse;

namespace VehicleRaidFramework
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("VRF.VehicleRaidFramework");
            harmony.PatchAll();

            if (VRF_Mod.Settings != null && VRF_Mod.Settings.autoLoadPresets)
                VRF_PresetIO.AutoLoadAllPresets(VRF_Mod.Settings);
        }
    }
}




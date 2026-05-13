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
}
    }
}




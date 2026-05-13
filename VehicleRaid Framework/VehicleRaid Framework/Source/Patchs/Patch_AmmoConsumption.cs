using HarmonyLib;
using Vehicles;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{





    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.ConsumeChamberedShot))]
    public static class Patch_NPCBreacherAmmo
    {
        [HarmonyPrefix]
        public static bool Prefix(VehicleTurret __instance)
        {
            VehiclePawn vehicle = __instance.vehicle;

            if (vehicle == null || vehicle.Faction == null || vehicle.Faction.IsPlayer)
                return true;

            Thing target = __instance.targetInfo.Thing;
            if (target is Building)
            {

                return false;
            }

            return true;
        }
    }
}




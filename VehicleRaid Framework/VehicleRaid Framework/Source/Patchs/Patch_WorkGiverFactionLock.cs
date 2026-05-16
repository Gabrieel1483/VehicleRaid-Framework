using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Vehicles;

namespace VehicleRaidFramework
{




    [HarmonyPatch(typeof(WorkGiver_Scanner), "HasJobOnThing")]
    public static class Patch_WorkGiverFactionLock
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, Thing t, ref bool __result)
        {

            if (t is VehiclePawn vehicle && vehicle.Faction != pawn.Faction)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}




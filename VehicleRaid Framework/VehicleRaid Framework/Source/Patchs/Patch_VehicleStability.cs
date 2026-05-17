using HarmonyLib;
using Vehicles;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{





    [HarmonyPatch(typeof(VehiclePathFollower), "CostToMoveIntoCell")]
    public static class Patch_CostToMoveIntoCell
    {
        [HarmonyPrefix]
        public static bool Prefix(VehiclePawn vehicle, ref float __result)
        {



            if (vehicle == null || !vehicle.Spawned || vehicle.Map == null || vehicle.jobs?.curDriver == null)
            {
                __result = 1f;
                return false;
            }

            return true;
        }
    }



    [HarmonyPatch(typeof(VehiclePawn), "CalculateImpactDamage")]
    public static class Patch_CalculateImpactDamage
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, VehiclePawn vehicle, ref (float pawnDamage, float vehicleDamage) __result)
        {
            if (pawn?.Faction == null || vehicle?.Faction == null) return true;

            if (pawn.Faction == vehicle.Faction || !pawn.Faction.HostileTo(vehicle.Faction))
            {
                __result = (0f, 0f);
                return false;
            }

            if (vehicle.Faction.RelationKindWith(pawn.Faction) == FactionRelationKind.Neutral)
            {




                return true; 
            }

            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, VehiclePawn vehicle, ref (float pawnDamage, float vehicleDamage) __result)
        {
            if (pawn?.Faction == null || vehicle?.Faction == null) return;

            if (pawn.Faction != vehicle.Faction && vehicle.Faction.RelationKindWith(pawn.Faction) == FactionRelationKind.Neutral)
            {
                __result.pawnDamage *= 0.1f;
                __result.vehicleDamage *= 0.1f;
            }
        }
    }
}




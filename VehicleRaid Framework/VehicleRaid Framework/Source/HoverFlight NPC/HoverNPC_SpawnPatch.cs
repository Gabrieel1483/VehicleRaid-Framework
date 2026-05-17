using HarmonyLib;
using Vehicles;
using Verse;
using RimWorld;
using VehicleRaid;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(VehicleRaidUtility), nameof(VehicleRaidUtility.SpawnArmoredDivision))]
    public static class HoverNPC_SpawnArmoredDivision_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(System.Collections.Generic.List<Pawn> __result)
        {
            if (__result == null || __result.Count == 0) return;

            foreach (Pawn pawn in __result)
            {
                if (!(pawn is VehiclePawn vehicle)) continue;
                if (vehicle.Faction == null || vehicle.Faction.IsPlayer) continue;
                if (!vehicle.Spawned || vehicle.Map == null) continue;

                CompVehicleHover hoverComp = vehicle.GetComp<CompVehicleHover>();
                if (hoverComp == null) continue;
                if (hoverComp.State != HoverState.Grounded) continue;

                hoverComp.ActivateHoverNPC();
            }
        }
    }
}

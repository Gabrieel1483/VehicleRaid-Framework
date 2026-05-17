using HarmonyLib;
using Vehicles;
using Verse;
using RimWorld;
using System.Reflection;

namespace VehicleRaid
{
    [HarmonyPatch(typeof(VehicleTurret), "TurretTargetValid", MethodType.Getter)]
    public static class VehicleHover_TurretTargetValid_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VehicleTurret __instance, ref bool __result)
        {
            if (!__result) return;

            VehiclePawn vehicle = __instance.vehicle;
            if (vehicle == null) return;

            CompVehicleHover hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || !hoverComp.IsAirborne) return;

            LocalTargetInfo target = __instance.targetInfo;
            if (!target.IsValid) return;

            Map map = vehicle.Map;
            if (map == null) return;

            IntVec3 targetCell;
            if (target.HasThing)
            {
                if (target.Thing == null || target.Thing.Destroyed || !target.Thing.Spawned) return;
                targetCell = target.Thing.Position;
            }
            else
            {
                targetCell = target.Cell;
            }

            if (!targetCell.InBounds(map)) return;

            RoofDef roof = map.roofGrid.RoofAt(targetCell);
            if (roof == null) return;

            if (HoverRoofUtil.IsBlockingRoof(roof))
            {
                __result = false;
                __instance.SetTarget(LocalTargetInfo.Invalid);
            }
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "ScanForTarget")]
    public static class VehicleHover_ScanForTarget_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VehicleTurret __instance)
        {
            VehiclePawn vehicle = __instance.vehicle;
            if (vehicle == null) return true;

            CompVehicleHover hoverComp = vehicle.GetComp<CompVehicleHover>();
            if (hoverComp == null || !hoverComp.IsAirborne) return true;

            Map map = vehicle.Map;
            if (map == null) return true;

            LocalTargetInfo current = __instance.targetInfo;
            if (current.IsValid && current.HasThing && current.Thing != null && current.Thing.Spawned)
            {
                RoofDef roof = map.roofGrid.RoofAt(current.Thing.Position);
                if (roof != null && HoverRoofUtil.IsBlockingRoof(roof))
                {
                    __instance.SetTarget(LocalTargetInfo.Invalid);
                    return false;
                }
            }

            return true;
        }
    }

    internal static class HoverRoofUtil
    {
        public static bool IsBlockingRoof(RoofDef roof)
        {
            if (roof == RoofDefOf.RoofConstructed) return false;
            if (roof == RoofDefOf.RoofRockThin) return false;
            return true;
        }
    }
}

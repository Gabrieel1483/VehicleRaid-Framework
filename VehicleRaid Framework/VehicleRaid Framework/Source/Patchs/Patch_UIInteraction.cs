using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using System.Linq;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(ITab_Vehicle_Cargo), "AllowDropping", MethodType.Getter)]
    public static class Patch_BlockCargoManipulation_Visibility
    {
        [HarmonyPostfix]
        public static void Postfix(ITab_Vehicle_Cargo __instance, ref bool __result)
        {
            if (DebugSettings.godMode) return;

            Pawn selPawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
            if (selPawn is VehiclePawn vehicle && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ITab_Vehicle_Cargo), "InterfaceDrop")]
    public static class Patch_BlockCargoManipulation_Drop
    {
        [HarmonyPrefix]
        public static bool Prefix(ITab_Vehicle_Cargo __instance)
        {
            if (DebugSettings.godMode) return true;

            Pawn selPawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
            if (selPawn is VehiclePawn vehicle && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ITab_Vehicle_Cargo), "InterfaceDropAll")]
    public static class Patch_BlockCargoManipulation_DropAll
    {
        [HarmonyPrefix]
        public static bool Prefix(ITab_Vehicle_Cargo __instance)
        {
            if (DebugSettings.godMode) return true;

            Pawn selPawn = Traverse.Create(__instance).Property("SelPawn").GetValue<Pawn>();
            if (selPawn is VehiclePawn vehicle && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(VehicleTabHelper_Passenger), "HandleDragEvent")]
    public static class Patch_BlockPassengerManipulation
    {

        private static Traverse DraggedPawnTraverse => Traverse.Create(typeof(VehicleTabHelper_Passenger)).Field("draggedPawn");

        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (DebugSettings.godMode) return true;

            Pawn dragged = DraggedPawnTraverse.GetValue<Pawn>();
            if (dragged == null) return true;

            if (dragged.ParentHolder is VehicleRoleHandler handler && handler.vehicle != null)
            {
                if (handler.vehicle.Faction != null && !handler.vehicle.Faction.IsPlayer)
                {

                    DraggedPawnTraverse.SetValue(null);
                    return false;
                }
            }

            else if (dragged.ParentHolder is Pawn_InventoryTracker inventory && inventory.pawn is VehiclePawn vehicle)
            {
                if (vehicle.Faction != null && !vehicle.Faction.IsPlayer)
                {
                    DraggedPawnTraverse.SetValue(null);
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), "DoInspectPaneButtons")]
    public static class Patch_BlockVehicleButtons
    {
        [HarmonyPrefix]
        public static bool Prefix(VehiclePawn __instance, ref float __result)
        {
            if (DebugSettings.godMode) return true;

            if (__instance.Faction != null && !__instance.Faction.IsPlayer)
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "CanTakeOrder", MethodType.Getter)]
    public static class Patch_BlockNPCVehicleOrders
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result && __instance is VehiclePawn vehicle && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                if (!DebugSettings.godMode)
                {
                    __result = false;
                }
            }
        }
    }
}




using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using System.Threading;
using System;
using Verse.AI;
using SmashTools;
using SmashTools.Performance;

namespace VehicleRaidFramework
{



    [HarmonyPatch(typeof(Vehicles.VehiclePathFollower), "GeneratePath")]
    public static class Patch_VehiclePathFollower_SuppressMessage
    {
        public static bool Prefix(Vehicles.VehiclePathFollower __instance, CancellationToken token)
        {
            var traverse = Traverse.Create(__instance);
            VehiclePawn vehicle = traverse.Field("vehicle").GetValue<VehiclePawn>();

            if (vehicle != null && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                VehiclePath path = traverse.Method("FindPath", token).GetValue<VehiclePath>();

                if (path == null || !path.Found)
                {
                    
                    __instance.PatherFailed();
                }
                else
                {
                    VehiclePath curPath = __instance.curPath;
                    if (curPath != null)
                    {
                        if (UnityData.IsInMainThread)
                            curPath.Dispose();
                        else
                            UnityThread.ExecuteOnMainThread(new Action(curPath.Dispose));
                    }
                    __instance.curPath = path;
                    traverse.Property("RequestStatus").SetValue(VehiclePathFollower.PathRequestStatus.None);
                }
                return false;
            }
            
            return true;
        }
    }
    [HarmonyPatch(typeof(Vehicles.VehiclePathFinder), "FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(Vehicles.VehiclePawn), typeof(CancellationToken), typeof(PathEndMode) })]
    public static class Patch_VehiclePathFinder_SuppressMessage
    {
        public static bool Prefix(Vehicles.VehiclePathFinder __instance, ref Vehicles.VehiclePath __result, IntVec3 start, LocalTargetInfo dest, Vehicles.VehiclePawn vehicle, CancellationToken token, PathEndMode peMode)
        {

            if (vehicle != null && vehicle.Faction != null && !vehicle.Faction.IsPlayer)
            {
                if (!vehicle.DrivableRectOnCell(dest.Cell, Ext_Vehicles.DestinationHitboxReq.AnyRotation))
                {
                    __result = VehiclePath.NotFound;

                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.CanDraft))]
    public static class Patch_VehiclePawn_CanDraft_SuppressMessage
    {
        public static bool Prefix(VehiclePawn __instance, ref AcceptanceReport __result)
        {
            if (__instance.Faction != null && !__instance.Faction.IsPlayer)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}




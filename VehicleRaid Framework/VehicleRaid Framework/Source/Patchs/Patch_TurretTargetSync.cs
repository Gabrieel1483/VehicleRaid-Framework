using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Vehicles;
using RimWorld;
using SmashTools;

namespace VehicleRaidFramework
{







    [HarmonyPatch(typeof(VehicleTurret), "ScanForTarget")]
    public static class Patch_ScanForTarget_BreachSync
    {
        static bool Prefix(VehicleTurret __instance)
        {
            VehiclePawn vehicle = __instance.vehicle;
            if (vehicle == null || vehicle.Faction == null || vehicle.Faction.IsPlayer)
                return true;

            BreachingTargetData breachData = vehicle.mindState?.breachingTarget;
            if (breachData != null && breachData.target != null 
                && !breachData.target.Destroyed && breachData.target.Spawned)
            {


                LocalTargetInfo currentTarget = __instance.targetInfo;
                if (currentTarget.IsValid && currentTarget.Thing == breachData.target)
                {

                    return false;
                }

                __instance.SetTarget(new LocalTargetInfo(breachData.target));
                return false;
            }

            return true;
        }
    }
}


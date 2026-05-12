using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Vehicles;
using Verse;
using RimWorld;
using UnityEngine;

namespace VehicleRaidFramework
{
    public static class Patch_VehicleNPCOnOff
    {
        public static void UpdateVehiclePower(VehiclePawn vehicle)
        {
            if (vehicle == null || vehicle.ignition == null || vehicle.Faction == null || vehicle.Faction.IsPlayer) return;

            bool shouldBeOn = ShouldVehicleBeOn(vehicle);

            if (vehicle.ignition.Drafted != shouldBeOn)
            {
                vehicle.ignition.Drafted = shouldBeOn;
            }
        }

        public static bool ShouldVehicleBeOn(VehiclePawn vehicle)
        {
            if (vehicle.Dead || vehicle.Destroyed || !vehicle.Spawned) return false;

            if (!CrewManager.HasOperationalDriver(vehicle)) return false;

            var fuelComp = vehicle.GetComp<CompFueledTravel>();
            if (fuelComp != null && fuelComp.Fuel <= 0) return false;

            if (!CrewManager.HasFunctionalEngine(vehicle)) return false;

            return true;
        }
    }
}

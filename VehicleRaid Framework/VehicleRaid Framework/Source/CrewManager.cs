using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using RimWorld;
using Vehicles;
using HarmonyLib;

namespace VehicleRaidFramework
{
    public static class CrewManager
    {
        public static void ReassignCrew(VehiclePawn vehicle)
        {
            if (vehicle.Faction == null || vehicle.Faction.IsPlayer) return;

            var handlers = vehicle.handlers;
            if (handlers == null || handlers.Count == 0) return;

            List<Pawn> consciousPawns = new List<Pawn>();
            List<Pawn> downedPawns = new List<Pawn>();

            foreach (var h in handlers)
            {
                foreach (Pawn p in h.thingOwner)
                {
                    if (p == null || p.Dead) continue;
                    if (p.Downed) downedPawns.Add(p);
                    else consciousPawns.Add(p);
                }
            }

            foreach (var h in handlers)
            {
                h.thingOwner.Clear();
            }

            var movementHandlers = handlers.Where(h => h.role != null && (h.role.HandlingTypes & HandlingType.Movement) != 0).ToList();
            var turretHandlers = handlers.Where(h => h.role != null && (h.role.HandlingTypes & HandlingType.Turret) != 0).ToList();
            var otherHandlers = handlers.Where(h => !movementHandlers.Contains(h) && !turretHandlers.Contains(h)).ToList();

            DistributePawns(consciousPawns, movementHandlers);
            DistributePawns(consciousPawns, turretHandlers);
            DistributePawns(consciousPawns, otherHandlers);

            DistributePawns(downedPawns, otherHandlers);
            DistributePawns(downedPawns, turretHandlers);
            DistributePawns(downedPawns, movementHandlers);

            CheckRetreat(vehicle);
        }

        private static void DistributePawns(List<Pawn> pawns, List<VehicleRoleHandler> targetHandlers)
        {
            foreach (var h in targetHandlers)
            {
                while (pawns.Count > 0 && h.thingOwner.Count < h.role.Slots)
                {
                    Pawn p = pawns[0];
                    h.thingOwner.TryAdd(p);
                    pawns.RemoveAt(0);
                }
            }
        }

        public static void CheckRetreat(VehiclePawn vehicle)
        {
            if (vehicle.Faction == null || vehicle.Faction.IsPlayer) return;

            if (vehicle.mindState.duty?.def.defName == "VRF_VehicleExitMap" || vehicle.mindState.duty?.def == DutyDefOf.ExitMapBest) return;

            int totalConscious = vehicle.AllPawnsAboard.Count(p => !p.Dead && !p.Downed);
            if (totalConscious == 1 && HasOperationalDriver(vehicle))
            {
                bool isDesignedForMore = vehicle.handlers.Any(h => h.role != null && (h.role.HandlingTypes & HandlingType.Movement) == 0 && h.role.Slots > 0);
                if (isDesignedForMore)
                {
                    TriggerRetreat(vehicle, "MessageVRF_VehicleRetreating");
                    return;
                }
            }

            if (IsOutOfAmmo(vehicle))
            {
                TriggerRetreat(vehicle, "MessageVRF_VehicleRetreatingNoAmmo");
                return;
            }
        }

        private static void TriggerRetreat(VehiclePawn vehicle, string messageKey)
        {
            vehicle.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamedSilentFail("VRF_VehicleExitMap") ?? DutyDefOf.ExitMapBest);
            Messages.Message(messageKey.Translate(vehicle.LabelShort), vehicle, MessageTypeDefOf.NeutralEvent);
        }







        public static bool IsOutOfAmmo(VehiclePawn vehicle)
        {
            CompVehicleTurrets turretComp = vehicle.CompVehicleTurrets;
            if (turretComp == null) return false;

            bool hasAnyAmmoTurret = false;

            foreach (VehicleTurret turret in turretComp.Turrets)
            {

                if (turret.def.ammunition == null) continue;

                hasAnyAmmoTurret = true;

                if (turret.shellCount > 0) return false;

                foreach (Thing item in vehicle.inventory.innerContainer)
                {
                    if (turret.def.ammunition.Allows(item.def))
                    {


                        if (item.stackCount >= turret.def.chargePerAmmoCount)
                        {
                            return false;
                        }
                    }
                }
            }


            return hasAnyAmmoTurret;
        }

        private static Pawn FindCandidateInHandlers(IEnumerable<VehicleRoleHandler> handlers)
        {
            foreach (var h in handlers)
            {
                foreach (Pawn p in h.thingOwner)
                {
                    if (p != null && !p.Dead && !p.Downed) return p;
                }
            }
            return null;
        }

        private static void SwapSeat(VehiclePawn vehicle, Pawn pawn, VehicleRoleHandler targetHandler)
        {
            foreach (var h in vehicle.handlers)
            {
                if (h.thingOwner.Contains(pawn))
                {
                    h.thingOwner.Remove(pawn);
                    break;
                }
            }
            targetHandler.thingOwner.TryAdd(pawn);
        }

        public static bool CanMove(VehiclePawn vehicle)
        {

            if (!vehicle.CanMove) return false;

            var fuelComp = vehicle.GetComp<CompFueledTravel>();
            if (fuelComp != null && fuelComp.Fuel <= 0 && !HasFuelInInventory(vehicle)) return false;

            if (!HasOperationalDriver(vehicle)) return false;

            return true;
        }

        public static bool HasOperationalDriver(VehiclePawn vehicle)
        {
            if (vehicle.handlers == null) return false;
            foreach (var h in vehicle.handlers)
            {
                if (h.role != null && (h.role.HandlingTypes & HandlingType.Movement) != 0)
                {

                    foreach (var thing in h.thingOwner)
                    {
                        if (thing is Pawn p && !p.Dead && !p.Downed) return true;
                    }
                }
            }
            return false;
        }

        private static bool HasFuelInInventory(VehiclePawn vehicle)
        {
            return CompFueledTravel.AllFuelFromInventory(vehicle).Any();
        }

        public static void CheckAbandonment(VehiclePawn vehicle)
        {

            if (!vehicle.AllPawnsAboard.Any()) return;

            if (!CanMove(vehicle))
            {
                if (IsCriticallyFailing(vehicle))
                {
                    AbandonVehicle(vehicle);
                }
            }
        }

        private static bool IsCriticallyFailing(VehiclePawn vehicle)
        {

            if (!vehicle.CanMove) return true;

            if (!HasOperationalDriver(vehicle)) return true;

            var fuelComp = vehicle.GetComp<CompFueledTravel>();
            if (fuelComp != null && fuelComp.Fuel <= 0 && !HasFuelInInventory(vehicle))
            {
                return true; 
            }

            return false;
        }

        public static bool HasFunctionalEngine(VehiclePawn vehicle)
        {
            if (vehicle.statHandler?.components == null) return true;
            
            foreach (var part in vehicle.statHandler.components)
            {
                if (part.Health <= 0 && part.props?.tags != null && (part.props.tags.Contains("engine") || part.props.tags.Contains("fuel_tank") || part.props.tags.Contains("transmission")))
                {
                    return false;
                }
            }
            return true;
        }

        public static void AbandonVehicle(VehiclePawn vehicle)
        {
            if (!vehicle.Spawned || vehicle.Dead) return;

            var crew = vehicle.AllPawnsAboard.ToList();
            if (!crew.Any()) return;

            Messages.Message("MessageVRF_VehicleAbandoned".Translate(vehicle.LabelShort), vehicle, MessageTypeDefOf.NegativeEvent);

            for (int i = crew.Count - 1; i >= 0; i--)
            {
                Pawn p = crew[i];
                vehicle.DisembarkPawn(p);

                if (p.Dead || p.Downed || !p.Spawned) continue;
                p.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                p.jobs.StopAll();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_CrewCasualty
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance.ParentHolder is VehicleRoleHandler handler)
            {
                CrewManager.ReassignCrew(handler.vehicle);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_CrewDowned
    {
        public static void Postfix(Pawn_HealthTracker __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn?.ParentHolder is VehicleRoleHandler handler)
            {
                CrewManager.ReassignCrew(handler.vehicle);
            }
        }
    }
}


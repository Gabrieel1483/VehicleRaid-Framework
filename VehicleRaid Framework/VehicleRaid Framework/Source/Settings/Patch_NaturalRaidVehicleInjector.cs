using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Vehicles;
using VehicleRaid;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(IncidentWorker_Raid), "TryExecuteWorker")]
    public static class Patch_NaturalRaidVehicleInjector
    {
        [HarmonyPostfix]
        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (!__result) return;
            if (parms == null || parms.faction == null) return;

            Map map = parms.target as Map;
            if (map == null) return;

            if (parms.raidStrategy?.GetModExtension<VehicleRaidExtension>() != null) return;

            var factionConfig = VRF_Mod.Settings?.GetFactionConfig(parms.faction.def.defName);
            if (factionConfig == null) return;

            var enabledEntries = factionConfig.vehicleEntries.Where(e => e.enabled).ToList();
            if (enabledEntries.Count == 0) return;

            List<(PawnKindDef kind, VRF_NaturalRaidVehicleEntry entry)> eligible = new List<(PawnKindDef, VRF_NaturalRaidVehicleEntry)>();
            foreach (var e in enabledEntries)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(e.vehicleKindDefName);
                if (kind == null || !(kind.race is VehicleDef)) continue;

                float minPoints = e.minRaidPoints > 0f ? e.minRaidPoints : (e.combatPowerOverride > 0f ? e.combatPowerOverride : kind.combatPower);
                if (parms.points < minPoints) continue;

                eligible.Add((kind, e));
            }

            if (eligible.Count == 0) return;

            var chosen = eligible.RandomElement();
            PawnKindDef chosenKind = chosen.kind;
            VRF_NaturalRaidVehicleEntry chosenEntry = chosen.entry;
            VehicleDef chosenVDef = chosenKind.race as VehicleDef;

            float originalCombatPower = chosenKind.combatPower;
            float effectiveCombatPower = chosenEntry.combatPowerOverride > 0f ? chosenEntry.combatPowerOverride : originalCombatPower;

            bool addedHoverComp = false;
            CompProperties_VehicleHover injectedHoverProps = null;

            if (chosenEntry.helicopterMode && chosenVDef != null && chosenVDef.type == VehicleType.Air)
            {
                bool alreadyHasHover = chosenVDef.comps.Any(c => c is CompProperties_VehicleHover);
                if (!alreadyHasHover)
                {
                    CompProperties_VehicleHover referenceProps = null;
                    VehicleDef mosquitoDef = DefDatabase<VehicleDef>.GetNamedSilentFail("VVE_MosquitoNPC");
                    if (mosquitoDef != null)
                        referenceProps = mosquitoDef.comps.OfType<CompProperties_VehicleHover>().FirstOrDefault();

                    if (referenceProps != null)
                    {
                        injectedHoverProps = new CompProperties_VehicleHover
                        {
                            maxTicks                  = referenceProps.maxTicks,
                            maxTicksVertical          = referenceProps.maxTicksVertical,
                            maxTicksPropeller         = referenceProps.maxTicksPropeller,
                            hoverAltitude             = referenceProps.hoverAltitude,
                            hoverShadowOffset         = referenceProps.hoverShadowOffset,
                            hoverBobAmount            = referenceProps.hoverBobAmount,
                            hoverBobSpeed             = referenceProps.hoverBobSpeed,
                            hoverMoveSpeed            = chosenEntry.helicopterMoveSpeed,
                            angularVelocityPropeller  = referenceProps.angularVelocityPropeller,
                            rotationCurve             = referenceProps.rotationCurve,
                            rotationVerticalCurve     = referenceProps.rotationVerticalCurve,
                            zPositionVerticalCurve    = referenceProps.zPositionVerticalCurve,
                            xPositionVerticalCurve    = referenceProps.xPositionVerticalCurve,
                            shadowAlphaPropellerCurve = referenceProps.shadowAlphaPropellerCurve,
                            fleckDataVertical         = referenceProps.fleckDataVertical,
                            fleckDataPropeller        = referenceProps.fleckDataPropeller
                        };
                    }
                    else
                    {
                        injectedHoverProps = new CompProperties_VehicleHover
                        {
                            maxTicks          = 600,
                            maxTicksVertical  = 400,
                            maxTicksPropeller = 800,
                            hoverAltitude     = 4f,
                            hoverShadowOffset = 1.5f,
                            hoverBobAmount    = 0.22f,
                            hoverBobSpeed     = 2.0f,
                            hoverMoveSpeed    = chosenEntry.helicopterMoveSpeed,
                            angularVelocityPropeller = new SmashTools.BezierCurve(new System.Collections.Generic.List<CurvePoint>
                            {
                                new CurvePoint(0f, 0f), new CurvePoint(0.3f, 0f),
                                new CurvePoint(0.5f, 30f), new CurvePoint(1f, 59f)
                            })
                        };
                    }

                    chosenVDef.comps.Add(injectedHoverProps);
                    addedHoverComp = true;
                }
                else
                {
                    var existing = chosenVDef.comps.OfType<CompProperties_VehicleHover>().First();
                    existing.hoverMoveSpeed = chosenEntry.helicopterMoveSpeed;
                }

                if (!VehicleMod.settings.vehicles.vehicleStats.ContainsKey(chosenVDef.defName))
                    VehicleMod.settings.vehicles.vehicleStats[chosenVDef.defName] = new Dictionary<string, float>();
                VehicleMod.settings.vehicles.vehicleStats[chosenVDef.defName][VehicleStatDefOf.MoveSpeed.defName] = 4.5f;
            }

            VehicleRaidExtension syntheticExt = new VehicleRaidExtension();
            syntheticExt.vehicleOptions = new List<VehicleRaidOption>
            {
                new VehicleRaidOption
                {
                    kindDef = chosenKind,
                    weight = 1f,
                    forceCount = 0,
                    spawnVehicle = true,
                    pawnFollowVehicle = true,
                    colorConfig = new VehicleColorConfig { mode = VehicleColorMode.Faction }
                }
            };
            syntheticExt.infantryPointsFraction = 0f;
            syntheticExt.spawnInfantry = false;
            syntheticExt.stayTicks = 35000;

            IncidentParms vehicleParms = new IncidentParms
            {
                target = parms.target,
                faction = parms.faction,
                points = parms.points,
                raidStrategy = parms.raidStrategy,
                raidArrivalMode = parms.raidArrivalMode
            };

            chosenKind.combatPower = effectiveCombatPower;
            List<Pawn> vehiclePawns;
            try
            {
                vehiclePawns = VehicleRaidUtility.SpawnArmoredDivision(map, parms.faction, vehicleParms, syntheticExt);
            }
            finally
            {
                chosenKind.combatPower = originalCombatPower;
                if (addedHoverComp && injectedHoverProps != null)
                    chosenVDef.comps.Remove(injectedHoverProps);
            }

            if (vehiclePawns.NullOrEmpty()) return;

            foreach (Pawn p in vehiclePawns)
            {
                if (!(p is VehiclePawn vehicle)) continue;

                CompFueledTravel fuelComp = vehicle.GetComp<CompFueledTravel>();
                if (fuelComp != null)
                {
                    float targetFuel = fuelComp.FuelCapacity * (chosenEntry.fuelPercent / 100f);
                    fuelComp.ConsumeFuel(fuelComp.Fuel);
                    if (targetFuel > 0f)
                        fuelComp.Refuel(targetFuel);
                }

                if (vehicle.CompVehicleTurrets != null)
                {
                    float cargoCapacity = vehicle.VehicleDef.GetStatValueAbstract(VehicleStatDefOf.CargoCapacity);
                    foreach (VehicleTurret turret in vehicle.CompVehicleTurrets.Turrets)
                    {
                        if (turret?.def?.ammunition == null) continue;
                        ThingDef ammoDef = turret.def.ammunition.AllowedThingDefs.FirstOrDefault();
                        if (ammoDef == null) continue;

                        var tEntry = chosenEntry.GetOrCreateTurretAmmo(turret.def.defName);
                        if (tEntry.ammoPercent <= 0f) continue;

                        float ammoMass = ammoDef.GetStatValueAbstract(StatDefOf.Mass);
                        if (ammoMass <= 0f) ammoMass = 0.1f;
                        float targetKg = cargoCapacity * (tEntry.ammoPercent / 100f);
                        int count = Mathf.FloorToInt(targetKg / ammoMass);
                        if (count > 0)
                        {
                            Thing ammo = ThingMaker.MakeThing(ammoDef);
                            ammo.stackCount = count;
                            vehicle.inventory.innerContainer.TryAdd(ammo);
                        }
                    }
                }

                EnforceCargoSafetyLimit(vehicle);

                if (chosenEntry.helicopterMode && vehicle.VehicleDef.type == VehicleType.Air)
                {
                    var hoverComp = vehicle.GetComp<CompVehicleHover>();
                    if (hoverComp != null)
                        hoverComp.ActivateHoverNPC();
                }
            }

            Lord existingLord = null;
            if (!chosenEntry.helicopterMode || chosenVDef?.type != VehicleType.Air)
            {
                existingLord = map.lordManager.lords
                    .Where(l => l.faction == parms.faction && !(l.LordJob is LordJob_VehicleRaid))
                    .OrderByDescending(l => l.ownedPawns.Count)
                    .FirstOrDefault();
            }

            if (existingLord != null)
            {
                foreach (Pawn p in vehiclePawns)
                    existingLord.AddPawn(p);
            }
            else
            {
                LordJob_VehicleRaid vehicleLord = new LordJob_VehicleRaid(parms.faction, syntheticExt.stayTicks);
                LordMaker.MakeNewLord(parms.faction, vehicleLord, map, vehiclePawns);
            }
        }
        private static void EnforceCargoSafetyLimit(VehiclePawn vehicle)
        {
            float cargoCapacity = vehicle.VehicleDef.GetStatValueAbstract(VehicleStatDefOf.CargoCapacity);
            if (cargoCapacity <= 0f) return;

            float limit = cargoCapacity * 0.99f;

            float currentMass = MassUtility.GearAndInventoryMass(vehicle);
            if (currentMass <= limit) return;

            var ammoItems = vehicle.inventory.innerContainer
                .Where(t => t.def?.projectile != null || (t.def?.projectileWhenLoaded != null))
                .OrderBy(t => t.def.GetStatValueAbstract(StatDefOf.Mass))
                .ToList();

            foreach (Thing ammo in ammoItems)
            {
                if (currentMass <= limit) break;

                float massPerUnit = ammo.def.GetStatValueAbstract(StatDefOf.Mass);
                if (massPerUnit <= 0f) continue;

                float excess = currentMass - limit;
                int unitsToRemove = Mathf.CeilToInt(excess / massPerUnit);
                unitsToRemove = Mathf.Min(unitsToRemove, ammo.stackCount);

                ammo.stackCount -= unitsToRemove;
                currentMass -= massPerUnit * unitsToRemove;

                if (ammo.stackCount <= 0)
                    ammo.Destroy();
            }
        }
    }
}

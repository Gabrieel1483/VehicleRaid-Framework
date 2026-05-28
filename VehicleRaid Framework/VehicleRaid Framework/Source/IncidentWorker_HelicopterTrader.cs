using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;
using UnityEngine;

namespace VehicleRaidFramework
{
    public class IncidentWorker_HelicopterTrader : IncidentWorker
    {
        public override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            return TryFindTraderFaction(out Faction faction, map);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Faction faction = parms.faction;
            if (faction == null && !TryFindTraderFaction(out faction, map)) return false;

            var heliExt = def.GetModExtension<HelicopterIncidentExtension>();

            TraderKindDef traderKind = parms.traderKind;
            if (traderKind == null)
            {
                if (heliExt?.traderKind != null)
                {
                    if (!TraderKindAllowed(heliExt.traderKind, map, faction)) return false;
                    traderKind = heliExt.traderKind;
                }
                else
                {
                    if (!faction.def.caravanTraderKinds.Where(t => TraderKindAllowed(t, map, faction))
                        .TryRandomElementByWeight(t => t.CalculatedCommonality, out traderKind))
                        return false;
                }
            }
            else
            {
                if (!TraderKindAllowed(traderKind, map, faction)) return false;
            }
            VehicleMerchantExtension ext = faction.def.GetModExtension<VehicleMerchantExtension>();
            TraderVehicleMapper mapper = ext?.GetMapperFor(traderKind);

            PawnKindDef traderPawnKind = faction.def.pawnGroupMakers?.SelectMany(x => x.options).Select(x => x.kind).FirstOrDefault(x => x.trader) 
                ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(x => x.trader) 
                ?? faction.def.basicMemberKind;

            Pawn trader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(traderPawnKind, faction, PawnGenerationContext.NonPlayer, map.Tile));
            trader.mindState.wantsToTradeWithColony = true;
            if (trader.trader == null) trader.trader = new RimWorld.Pawn_TraderTracker(trader);
            trader.trader.traderKind = traderKind;

            PawnKindDef vehicleKind = heliExt?.vehicleKind;

            if (vehicleKind == null)
            {
                vehicleKind = mapper?.principalVehicle?.kindDef;
            }

            if (vehicleKind == null) return false;

            VehiclePawn heli = VehicleSpawner.GenerateVehicle(vehicleKind.race as VehicleDef, faction);
            if (heli == null) return false;
            heli.kindDef = vehicleKind;

            if (heliExt?.colorConfig != null)
                VehicleRaidUtility.ApplyColorConfig(heli, heliExt.colorConfig, faction);

            bool traderAdded = false;
            foreach (var r in heli.VehicleDef.properties.roles)
            {
                if (heli.GetHandler(r.key).AreSlotsAvailable)
                {
                    heli.TryAddPawn(trader, heli.GetHandler(r.key));
                    traderAdded = true;
                    break;
                }
            }

            if (!traderAdded)
            {
                Log.Warning("VRF: Could not find a free seat for trader, forcing entry.");
                heli.GetHandler(heli.VehicleDef.properties.roles.First().key).thingOwner.TryAdd(trader);
            }

            FillCrew(heli, faction, map);

            List<Thing> stock = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = traderKind, tile = map.Tile, makingFaction = faction });
            foreach (Thing t in stock) heli.inventory.innerContainer.TryAdd(t);

            if (!TryFindLandingSpot(map, heli, out IntVec3 landingSpot)) return false;

            CompVehicleLauncher launcher = heli.CompVehicleLauncher;
            if (launcher != null)
            {
                launcher.inFlight = true;
                VehicleSkyfaller_Arriving skyfaller = (VehicleSkyfaller_Arriving)VehicleSkyfallerMaker.MakeSkyfaller(launcher.Props.skyfallerIncoming, heli);
                GenSpawn.Spawn(skyfaller, landingSpot, map);
            }
            else
            {
                GenSpawn.Spawn(heli, landingSpot, map);
            }

            List<Pawn> lordMembers = new List<Pawn> { trader };
            foreach (Pawn crew in heli.AllPawnsAboard) if (crew != trader) lordMembers.Add(crew);
            lordMembers.Add(heli);

            LordJob_HelicopterTrade lordJob = new LordJob_HelicopterTrade(faction, landingSpot, new List<VehiclePawn> { heli });
            LordMaker.MakeNewLord(faction, lordJob, map, lordMembers);

            string label = "LetterLabelTraderCaravanArrival".Translate(faction.Name, traderKind.label).CapitalizeFirst();
            string text = "LetterTraderCaravanArrival".Translate(faction.Name, traderKind.label).CapitalizeFirst();
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, lordMembers);

            return true;
        }

        private bool TryFindTraderFaction(out Faction faction, Map map)
        {
            var heliExt = def.GetModExtension<HelicopterIncidentExtension>();
            return Find.FactionManager.AllFactions.Where(x =>
                !x.IsPlayer &&
                !x.HostileTo(Faction.OfPlayer) &&
                (heliExt?.factionDef == null || x.def == heliExt.factionDef) &&
                x.def.caravanTraderKinds.Any(t => TraderKindAllowed(t, map, x))
            ).TryRandomElement(out faction);
        }

        private bool TraderKindAllowed(TraderKindDef traderKind, Map map, Faction faction)
        {
            if (traderKind.faction != null && faction.def != traderKind.faction)
                return false;

            if (ModsConfig.IdeologyActive && faction.ideos != null && traderKind.category == "Slaver")
            {
                foreach (Ideo ideo in faction.ideos.AllIdeos)
                {
                    if (!ideo.IdeoApprovesOfSlavery())
                        return false;
                }
            }

            if (traderKind.permitRequiredForTrading != null)
            {
                if (!map.mapPawns.FreeColonists.Any(p =>
                    p.royalty != null && p.royalty.HasPermit(traderKind.permitRequiredForTrading, faction)))
                    return false;
            }

            return true;
        }

        private void FillCrew(VehiclePawn v, Faction faction, Map map)
        {
            PawnKindDef kind = faction.def.basicMemberKind ?? faction.def.pawnGroupMakers.SelectMany(x => x.options).Select(x => x.kind).FirstOrDefault(x => x.RaceProps.Humanlike) ?? PawnKindDefOf.AncientSoldier;
            foreach (var role in v.VehicleDef.properties.roles)
            {
                int needed = role.Slots - v.GetHandler(role.key).thingOwner.Count;
                for (int i = 0; i < needed; i++)
                {
                    Pawn crew = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile));
                    v.TryAddPawn(crew, v.GetHandler(role.key));
                }
            }
        }

        private bool TryFindLandingSpot(Map map, VehiclePawn vehicle, out IntVec3 result)
        {
            if (ModsConfig.RoyaltyActive)
            {
                List<ShipLandingArea> landingZones = ShipLandingBeaconUtility.GetLandingZones(map);
                if (landingZones != null)
                {
                    foreach (var zone in landingZones)
                    {
                        if (zone.Active)
                        {
                            if (IsSpotClear(map, vehicle, zone.CenterCell))
                            {
                                result = zone.CenterCell;
                                return true;
                            }
                        }
                    }
                }
            }

            for (int j = 0; j < 10; j++)
            {
                if (RCellFinder.TryFindRandomSpotJustOutsideColony(IntVec3.Invalid, map, out IntVec3 spot))
                {
                    if (IsSpotClear(map, vehicle, spot))
                    {
                        result = spot;
                        return true;
                    }
                }
            }

            for (int i = 0; i < 50; i++)
            {
                IntVec3 spot;
                if (CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Roofed(map) && !c.CloseToEdge(map, 15), map, 1000, out spot))
                {
                    if (IsSpotClear(map, vehicle, spot))
                    {
                        result = spot;
                        return true;
                    }
                }
            }
            result = map.Center;
            return true;
        }
        private bool IsSpotClear(Map map, VehiclePawn vehicle, IntVec3 spot)
        {
            CellRect rect = CellRect.CenteredOn(spot, vehicle.def.size.x + 4, vehicle.def.size.z + 4);
            foreach (var cell in rect)
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Roofed(map))
                {
                    return false;
                }
                Building building = cell.GetEdifice(map);
                if (building != null && building.def != ThingDefOf.ShipLandingBeacon)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

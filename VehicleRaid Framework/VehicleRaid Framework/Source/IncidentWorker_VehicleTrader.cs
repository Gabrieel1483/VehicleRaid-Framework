using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public class IncidentWorker_VehicleTrader : IncidentWorker
    {
        public override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            return TryFindTraderFaction(out Faction faction, map) && !NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, faction);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Faction faction = parms.faction;
            if (faction == null && !TryFindTraderFaction(out faction, map)) return false;
            
            TraderKindDef traderKind = parms.traderKind;
            if (traderKind == null)
            {
                traderKind = faction.def.caravanTraderKinds.RandomElementByWeight(x => x.CalculatedCommonality);
            }

            VehicleMerchantExtension ext = faction.def.GetModExtension<VehicleMerchantExtension>();
            TraderVehicleMapper mapper = ext?.GetMapperFor(traderKind);

            if (mapper == null) return false;

            List<Pawn> allPawns = new List<Pawn>();
            List<VehiclePawn> vehicles = new List<VehiclePawn>();
            
            
            PawnKindDef traderPawnKind = faction.def.pawnGroupMakers
                ?.SelectMany(x => x.options)
                .Select(x => x.kind)
                .FirstOrDefault(x => x.trader);

            if (traderPawnKind == null)
            {
                
                traderPawnKind = DefDatabase<PawnKindDef>.AllDefsListForReading
                    .FirstOrDefault(x => x.trader);
                
                if (traderPawnKind == null)
                {
                    traderPawnKind = faction.def.basicMemberKind; 
}
            }
            
            Pawn trader = PawnGenerator.GeneratePawn(new PawnGenerationRequest(traderPawnKind, faction, PawnGenerationContext.NonPlayer, map.Tile));
            trader.mindState.wantsToTradeWithColony = true;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(trader, true);
            
            if (trader.trader == null)
            {
                
                trader.trader = new RimWorld.Pawn_TraderTracker(trader);
            }
            trader.trader.traderKind = traderKind;

            allPawns.Add(trader);

            List<VehiclePawn> tradeableVehicles = new List<VehiclePawn>();

            VehiclePawn pVehicle = SpawnVehicleWithTrader(mapper.principalVehicle, faction, map, trader, allPawns);
            if (pVehicle != null) 
            {
                vehicles.Add(pVehicle);
                if (mapper.principalVehicle != null && mapper.principalVehicle.tradeCargo) tradeableVehicles.Add(pVehicle);
            }

            foreach (var opt in mapper.cargoVehicles)
            {
                int count = opt.count.RandomInRange;
                for (int i = 0; i < count; i++)
                {
                    VehiclePawn v = SpawnGenericVehicle(opt, faction, map, allPawns);
                    if (v != null) 
                    {
                        vehicles.Add(v);
                        if (opt.tradeCargo) tradeableVehicles.Add(v);
                    }
                }
            }

            foreach (var opt in mapper.escortVehicles)
            {
                int count = opt.count.RandomInRange;
                for (int i = 0; i < count; i++)
                {
                    VehiclePawn v = SpawnGenericVehicle(opt, faction, map, allPawns);
                    if (v != null) 
                    {
                        vehicles.Add(v);
                        if (opt.tradeCargo) tradeableVehicles.Add(v);
                    }
                }
            }

            if (vehicles.Count == 0) return false;

            IntVec3 entryCell = parms.spawnCenter;
            if (!entryCell.IsValid)
            {
                List<VehicleDef> allVehicleDefs = vehicles.Select(v => v.VehicleDef).ToList();
                if (!VehicleTrafficManager.TryFindGroupEntryPoint(map, allVehicleDefs, out entryCell))
                {
                    if (!RCellFinder.TryFindRandomPawnEntryCell(out entryCell, map, CellFinder.EdgeRoadChance_Neutral))
                    {
                        return false;
                    }
                }
            }

            Rot4 rot = entryCell.OnEdge(map) ? entryCell.GetBeginningOfRoadDirection(map) : Rot4.North;
            List<IntVec3> formationCells = VehicleTrafficManager.CalculateFormationCells(entryCell, map, rot, vehicles, null);

            for (int i = 0; i < vehicles.Count; i++)
            {
                GenSpawn.Spawn(vehicles[i], formationCells[i], map, rot);
                
                Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicles[i]);
            }

            List<Thing> stock = ThingSetMakerDefOf.TraderStock.root.Generate(new ThingSetMakerParams { traderDef = traderKind, tile = map.Tile, makingFaction = faction });
            List<VehiclePawn> commerceVehicles = vehicles.Where(v => v != null).ToList();
            if (commerceVehicles.Count > mapper.escortVehicles.Sum(x => x.count.max)) 
            {
                int escortCount = 0;
                foreach(var escort in mapper.escortVehicles) escortCount += escort.count.max;
                
                int tradeVehicleCount = commerceVehicles.Count - escortCount;
                if (tradeVehicleCount > 0)
                {
                    commerceVehicles = commerceVehicles.Take(tradeVehicleCount).ToList();
                }
            }

            DistributeStock(stock, commerceVehicles);

            
            IntVec3 chillSpot = IntVec3.Invalid;
            VehiclePawn representative = vehicles.FirstOrDefault();
            
            
            if (representative != null)
            {
                RCellFinder.TryFindRandomSpotJustOutsideColony(entryCell, map, representative, out chillSpot);
            }

            if (!chillSpot.IsValid || chillSpot.CloseToEdge(map, 5))
            {
                
                
                if (!TryFindVehicleChillSpot(map, vehicles.FirstOrDefault(), out chillSpot))
                {
                    chillSpot = map.Center; 
                }
            }

            
            
            List<Pawn> lordMembers = new List<Pawn>();
            lordMembers.AddRange(allPawns);  
            lordMembers.AddRange(vehicles);   
            
            LordJob_VehicleTrade lordJob = new LordJob_VehicleTrade(faction, chillSpot, tradeableVehicles);
            LordMaker.MakeNewLord(faction, lordJob, map, lordMembers);
string label = "LetterLabelTraderCaravanArrival".Translate(faction.Name, traderKind.label).CapitalizeFirst();
            string text = "LetterTraderCaravanArrival".Translate(faction.Name, traderKind.label).CapitalizeFirst();
            
            LetterDef letterDef = LetterDefOf.PositiveEvent;
            Find.LetterStack.ReceiveLetter(label, text, letterDef, lordMembers);

            return true;
        }

        private bool TryFindTraderFaction(out Faction faction, Map map)
        {
            return Find.FactionManager.AllFactions.Where(x => !x.IsPlayer && !x.HostileTo(Faction.OfPlayer) && x.def.GetModExtension<VehicleMerchantExtension>() != null).TryRandomElement(out faction);
        }

        private VehiclePawn SpawnVehicleWithTrader(VehicleMerchantOption opt, Faction faction, Map map, Pawn trader, List<Pawn> allPawns)
        {
            if (opt == null || opt.kindDef == null) return null;
            VehiclePawn v = VehicleSpawner.GenerateVehicle(opt.kindDef.race as VehicleDef, faction);
            if (v == null) return null;

            if (opt.colorConfig != null)
                VehicleRaidUtility.ApplyColorConfig(v, opt.colorConfig, faction);

            VehicleRole firstRole = v.VehicleDef.properties.roles.Find(x => x.HandlingTypes != HandlingType.None);
            if (firstRole != null)
                v.TryAddPawn(trader, v.GetHandler(firstRole.key));

            foreach (var cargo in opt.cargoItems)
            {
                int remainingAmount = cargo.count.RandomInRange;
                while (remainingAmount > 0)
                {
                    Thing t = ThingMaker.MakeThing(cargo.thingDef);
                    t.stackCount = UnityEngine.Mathf.Min(remainingAmount, t.def.stackLimit);
                    remainingAmount -= t.stackCount;
                    if (!cargo.tradeable) v.inventory.TryAddItemNotForSale(t);
                    else v.inventory.innerContainer.TryAdd(t);
                }
            }

            FillRemainingSlots(v, faction, map, allPawns, traderOccupiesFirstRole: true);
            return v;
        }

        private VehiclePawn SpawnGenericVehicle(VehicleMerchantOption opt, Faction faction, Map map, List<Pawn> allPawns)
        {
            VehiclePawn v = VehicleSpawner.GenerateVehicle(opt.kindDef.race as VehicleDef, faction);
            if (v == null) return null;

            if (opt.colorConfig != null)
                VehicleRaidUtility.ApplyColorConfig(v, opt.colorConfig, faction);

            foreach (var cargo in opt.cargoItems)
            {
                int remainingAmount = cargo.count.RandomInRange;
                while (remainingAmount > 0)
                {
                    Thing t = ThingMaker.MakeThing(cargo.thingDef);
                    t.stackCount = UnityEngine.Mathf.Min(remainingAmount, t.def.stackLimit);
                    remainingAmount -= t.stackCount;
                    if (!cargo.tradeable) v.inventory.TryAddItemNotForSale(t);
                    else v.inventory.innerContainer.TryAdd(t);
                }
            }

            FillRemainingSlots(v, faction, map, allPawns, traderOccupiesFirstRole: false);
            return v;
        }

        private void FillRemainingSlots(VehiclePawn v, Faction faction, Map map, List<Pawn> allPawns, bool traderOccupiesFirstRole)
        {
            PawnKindDef kind = faction.def.basicMemberKind ?? faction.def.pawnGroupMakers.SelectMany(x => x.options).Select(x => x.kind).FirstOrDefault(x => x.RaceProps.Humanlike);
            bool firstRole = true;
            foreach (var role in v.VehicleDef.properties.roles)
            {
                int start = (traderOccupiesFirstRole && firstRole) ? 1 : 0;
                int needed = role.Slots - v.GetHandler(role.key).thingOwner.Count - start;
                for (int i = 0; i < needed; i++)
                {
                    Pawn crew = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, map.Tile));
                    if (v.TryAddPawn(crew, v.GetHandler(role.key)))
                        allPawns.Add(crew);
                }
                firstRole = false;
            }
        }

        private void DistributeStock(List<Thing> stock, List<VehiclePawn> vehicles)
        {
            int vIdx = 0;
            foreach (Thing t in stock)
            {
                vehicles[vIdx % vehicles.Count].inventory.innerContainer.TryAdd(t);
                vIdx++;
            }
        }
        private bool TryFindVehicleChillSpot(Map map, VehiclePawn vehicle, out IntVec3 result)
        {
            
            
            for (int i = 0; i < 20; i++)
            {
                if (CellFinderLoose.TryGetRandomCellWith(c => 
                    !c.CloseToEdge(map, 10) && 
                    c.Standable(map) && 
                    (vehicle == null || vehicle.CanReachVehicle(c, PathEndMode.OnCell, Danger.None)), 
                    map, 1000, out IntVec3 candidate))
                {
                    
                    CellRect rect = CellRect.CenteredOn(candidate, 4);
                    bool clear = true;
                    foreach (var cell in rect)
                    {
                        if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetEdifice(map) != null)
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (clear)
                    {
                        result = candidate;
                        return true;
                    }
                }
            }

            result = IntVec3.Invalid;
            return false;
        }
    }
}



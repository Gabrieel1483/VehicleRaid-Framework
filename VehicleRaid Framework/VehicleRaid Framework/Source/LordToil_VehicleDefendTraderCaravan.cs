using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
using System.Linq;
using System.Collections.Generic;
using Vehicles;
using UnityEngine;

namespace VehicleRaidFramework
{
    public class LordToil_VehicleDefendTraderCaravan : LordToil_DefendPoint
    {
        private const int ExtraSpacing = 3;

        public LordToil_VehicleDefendTraderCaravan(IntVec3 spot) : base(spot) { }

        private bool traderDisembarked = false;

        public override void Init()
        {
            base.Init();
            
        }

        public override void LordToilTick()
        {

            if (traderDisembarked || Find.TickManager.TicksGame % 60 != 0) return;

            foreach (Pawn p in lord.ownedPawns)
            {
                if (p is VehiclePawn v)
                {
                    bool hasTrader = false;
                    foreach (Pawn occupant in v.AllPawnsAboard)
                    {
                        if (occupant.mindState?.wantsToTradeWithColony == true)
                        {
                            hasTrader = true;
                            break;
                        }
                    }

                    if (hasTrader)
                    {
                        if (v.mindState?.duty != null)
                        {
                            IntVec3 dest = v.mindState.duty.focus.Cell;
                            
                            if (v.VehicleDef.type == VehicleType.Air || (v.Position.InHorDistOf(dest, 4f) && !v.pather.Moving))
                            {
                                DisembarkTrader();
                                traderDisembarked = true;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        public override void UpdateAllDuties()
        {
            LordToilData_DefendPoint data = Data;
            Pawn trader = TraderCaravanUtility.FindTrader(lord);

            if (trader != null)
            {
                trader.mindState.duty = new PawnDuty(DutyDefOf.Defend, (LocalTargetInfo)data.defendPoint, data.defendRadius);
            }

            DutyDef parkingDuty = DefDatabase<DutyDef>.GetNamed("VRF_VehicleTradeParking", false);

            
            List<VehiclePawn> vehicles = new List<VehiclePawn>();
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p is VehiclePawn v) vehicles.Add(v);
            }

            
            HashSet<int> caravanVehicleIds = new HashSet<int>();
            foreach (var v in vehicles) caravanVehicleIds.Add(v.thingIDNumber);

            
            List<IntVec3> parkingSpots = CalculateParkingFormation(data.defendPoint, lord.Map, vehicles, caravanVehicleIds);

            int vIndex = 0;
            for (int index = 0; index < lord.ownedPawns.Count; ++index)
            {
                Pawn ownedPawn = lord.ownedPawns[index];

                if (ownedPawn is VehiclePawn vehicle)
                {
                    IntVec3 mySpot = (vIndex < parkingSpots.Count) ? parkingSpots[vIndex] : data.defendPoint;
                    vIndex++;

                    if (vehicle.VehicleDef.type == VehicleType.Air)
                    {
                        ownedPawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("VRF_HelicopterTakeoff"), vehicle.Position);
                    }
                    else if (parkingDuty != null)
                    {
                        ownedPawn.mindState.duty = new PawnDuty(parkingDuty, (LocalTargetInfo)mySpot);
                    }
                    else
                    {
                        ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, (LocalTargetInfo)mySpot, data.defendRadius);
                    }
                    Patch_VehicleNPCOnOff.UpdateVehiclePower(vehicle);
                    continue;
                }

                
                if (trader != null && ownedPawn != trader)
                {
                    switch (ownedPawn.GetTraderCaravanRole())
                    {
                        case TraderCaravanRole.Carrier:
                            ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Follow, (LocalTargetInfo)(Thing)trader, 5f);
                            ownedPawn.mindState.duty.locomotion = LocomotionUrgency.Walk;
                            break;
                        case TraderCaravanRole.Guard:
                            ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, (LocalTargetInfo)data.defendPoint, data.defendRadius);
                            break;
                        case TraderCaravanRole.Chattel:
                            ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Escort, (LocalTargetInfo)(Thing)trader, 5f);
                            ownedPawn.mindState.duty.locomotion = LocomotionUrgency.Walk;
                            break;
                        default:
                            ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, (LocalTargetInfo)data.defendPoint, data.defendRadius);
                            break;
                    }
                }
                else
                {
                    ownedPawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, (LocalTargetInfo)data.defendPoint, data.defendRadius);
                }
            }
        }

        private List<IntVec3> CalculateParkingFormation(IntVec3 center, Map map, List<VehiclePawn> vehicles, HashSet<int> ignoreVehicleIds)
        {
            List<IntVec3> result = new List<IntVec3>();
            List<CellRect> reservedRects = new List<CellRect>();

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehiclePawn v = vehicles[i];
                int sizeX = v.def.size.x;
                int sizeZ = v.def.size.z;
                int maxDim = Mathf.Max(sizeX, sizeZ);
                
                
                int slotSize = maxDim + ExtraSpacing * 2;

                
                int col = (i % 2 == 0) ? -1 : 1;
                int row = i / 2;

                int lateralOffset = col * (slotSize / 2 + 1);
                int depthOffset = row * (slotSize + 1);

                IntVec3 candidate = new IntVec3(
                    center.x + lateralOffset,
                    0,
                    center.z - depthOffset
                );

                IntVec3 finalSpot = FindValidSpot(candidate, map, sizeX, sizeZ, reservedRects, ignoreVehicleIds);
                result.Add(finalSpot);

                
                int halfW = Mathf.CeilToInt(sizeX / 2f);
                int halfZ = Mathf.CeilToInt(sizeZ / 2f);
                reservedRects.Add(CellRect.CenteredOn(finalSpot, halfW + ExtraSpacing, halfZ + ExtraSpacing));
}

            return result;
        }

        private IntVec3 FindValidSpot(IntVec3 candidate, Map map, int sizeX, int sizeZ, List<CellRect> reservedRects, HashSet<int> ignoreVehicleIds)
        {
            int halfW = Mathf.CeilToInt(sizeX / 2f);
            int halfZ = Mathf.CeilToInt(sizeZ / 2f);

            if (IsSpotValid(candidate, map, halfW, halfZ, reservedRects, ignoreVehicleIds))
                return candidate;

            
            for (int dist = 1; dist <= 25; dist++)
            {
                for (int dx = -dist; dx <= dist; dx++)
                {
                    for (int dz = -dist; dz <= dist; dz++)
                    {
                        if (Mathf.Abs(dx) != dist && Mathf.Abs(dz) != dist) continue;
                        IntVec3 check = new IntVec3(candidate.x + dx, 0, candidate.z + dz);
                        if (IsSpotValid(check, map, halfW, halfZ, reservedRects, ignoreVehicleIds))
                            return check;
                    }
                }
            }

            return candidate;
        }

        private bool IsSpotValid(IntVec3 spot, Map map, int halfW, int halfZ, List<CellRect> reservedRects, HashSet<int> ignoreVehicleIds)
        {
            if (!spot.InBounds(map)) return false;

            CellRect myRect = CellRect.CenteredOn(spot, halfW, halfZ);

            
            foreach (IntVec3 cell in myRect)
            {
                if (!cell.InBounds(map)) return false;
            }

            
            CellRect expanded = myRect.ExpandedBy(ExtraSpacing);
            for (int r = 0; r < reservedRects.Count; r++)
            {
                if (expanded.Overlaps(reservedRects[r])) return false;
            }

            
            foreach (IntVec3 cell in myRect)
            {
                
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain != null && terrain.passability == Traversability.Impassable) return false;

                
                Building edifice = cell.GetEdifice(map);
                if (edifice != null) return false;

                
                List<Thing> things = cell.GetThingList(map);
                for (int t = 0; t < things.Count; t++)
                {
                    if (things[t] is VehiclePawn otherV && !ignoreVehicleIds.Contains(otherV.thingIDNumber))
                        return false;
                }
            }

            return true;
        }

        private void DisembarkTrader()
        {
            
            List<Pawn> snapshot = lord.ownedPawns.ToList();
            foreach (Pawn p in snapshot)
            {
                if (!(p is VehiclePawn v)) continue;
                if (!v.Spawned || v.Map == null) continue;

                Pawn traderInVehicle = null;
                foreach (Pawn occupant in v.AllPawnsAboard)
                {
                    if (occupant.mindState?.wantsToTradeWithColony == true)
                    {
                        traderInVehicle = occupant;
                        break;
                    }
                }

                if (traderInVehicle != null)
                {
                    Map map = v.Map ?? lord.Map;
                    if (map == null)
                    {
                        Log.Error("VRF: Could not find map to disembark trader.");
                        return;
                    }

                    v.DisembarkPawn(traderInVehicle);

                    if (!lord.ownedPawns.Contains(traderInVehicle))
                    {
                        lord.AddPawn(traderInVehicle);
                    }
                    return;
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Verse;
using Vehicles;
using RimWorld;
using SmashTools;

namespace VehicleRaidFramework
{
    public class VRF_LeaderManager : MapComponent
    {
        private Dictionary<VehiclePawn, int> leaderVehicles = new Dictionary<VehiclePawn, int>();
        private HashSet<VehiclePawn> mortarVehicles = new HashSet<VehiclePawn>();

        private Dictionary<Faction, VehiclePawn> cachedBestLeaders = new Dictionary<Faction, VehiclePawn>();
        private int lastCacheTick = -1;

        public VRF_LeaderManager(Map map) : base(map) { }

        public void RegisterLeader(VehiclePawn vehicle, int priority)
        {
            if (vehicle == null) return;
            leaderVehicles[vehicle] = priority;
            ForceRefresh();
        }

        public void RegisterMortar(VehiclePawn vehicle)
        {
            if (vehicle == null) return;
            mortarVehicles.Add(vehicle);
        }

        public bool IsMortar(VehiclePawn vehicle)
        {
            if (vehicle == null) return false;
            return mortarVehicles.Contains(vehicle);
        }

        public void ForceRefresh()
        {
            lastCacheTick = -1;
            cachedBestLeaders.Clear();
        }

        public VehiclePawn GetBestLeader(Faction faction, IEnumerable<Pawn> candidates = null)
        {
            if (faction == null) return null;

            if (candidates != null)
            {
                VehiclePawn bestInCandidates = null;
                int bestPriorityInCandidates = int.MaxValue;
                VehiclePawn firstAnyVehicleInCandidates = null;

                foreach (Pawn p in candidates)
                {
                    if (p is VehiclePawn v && v.Faction == faction && !v.Dead && !v.Destroyed && v.Spawned && CrewManager.HasOperationalDriver(v))
                    {
                        if (firstAnyVehicleInCandidates == null) firstAnyVehicleInCandidates = v;

                        if (leaderVehicles.TryGetValue(v, out int priority))
                        {
                            if (priority < bestPriorityInCandidates)
                            {
                                bestPriorityInCandidates = priority;
                                bestInCandidates = v;
                            }
                        }
                    }
                }

                if (bestInCandidates != null) return bestInCandidates;
                if (firstAnyVehicleInCandidates != null) return firstAnyVehicleInCandidates;
            }


            if (candidates == null && Find.TickManager.TicksGame == lastCacheTick)
            {
                if (cachedBestLeaders.TryGetValue(faction, out var cached)) return cached;
            }

            VehiclePawn bestLeader = null;
            int bestPriority = int.MaxValue;

            foreach (var kvp in leaderVehicles)
            {
                VehiclePawn v = kvp.Key;
                if (v != null && !v.Dead && !v.Destroyed && v.Spawned && v.Faction == faction && v.Map == map && CrewManager.HasOperationalDriver(v))
                {
                    if (kvp.Value < bestPriority)
                    {
                        bestPriority = kvp.Value;
                        bestLeader = v;
                    }
                }
            }

            if (bestLeader == null)
            {
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p is VehiclePawn v && v.Faction == faction && !v.Dead && !v.Destroyed && v.Spawned && CrewManager.HasOperationalDriver(v))
                    {
                        bestLeader = v;
                        break;
                    }
                }
            }

            if (candidates == null)
            {
                lastCacheTick = Find.TickManager.TicksGame;
                cachedBestLeaders[faction] = bestLeader;
            }

            return bestLeader;
        }

        private List<VehiclePawn> tmpLeaderKeys;
        private List<int> tmpLeaderValues;

        public override void ExposeData()
        {
            base.ExposeData();


            if (Scribe.mode == LoadSaveMode.Saving)
            {
                leaderVehicles.RemoveAll(kvp => kvp.Key == null || kvp.Key.Dead || !kvp.Key.Spawned);
                mortarVehicles.RemoveWhere(v => v == null || v.Dead || !v.Spawned);
            }
            
            Scribe_Collections.Look(ref leaderVehicles, "leaderVehicles", LookMode.Reference, LookMode.Value, ref tmpLeaderKeys, ref tmpLeaderValues);
            Scribe_Collections.Look(ref mortarVehicles, "mortarVehicles", LookMode.Reference);
            
            if (leaderVehicles == null) leaderVehicles = new Dictionary<VehiclePawn, int>();
            if (mortarVehicles == null) mortarVehicles = new HashSet<VehiclePawn>();
        }
    }
}


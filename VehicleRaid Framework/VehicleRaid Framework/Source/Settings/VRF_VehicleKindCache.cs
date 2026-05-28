using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Vehicles;

namespace VehicleRaidFramework
{
    public static class VRF_VehicleKindCache
    {
        private static List<FactionDef> _raidableFactions;
        private static List<PawnKindDef> _allVehicleKinds;
        private static bool _initialized = false;

        public static List<FactionDef> RaidableFactions
        {
            get
            {
                EnsureInitialized();
                return _raidableFactions;
            }
        }

        public static List<PawnKindDef> AllVehicleKinds
        {
            get
            {
                EnsureInitialized();
                return _allVehicleKinds;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _allVehicleKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(k => k.race is VehicleDef)
                .OrderBy(k => k.label ?? k.defName)
                .ToList();

            _raidableFactions = DefDatabase<FactionDef>.AllDefsListForReading
                .Where(f => !f.isPlayer && f.humanlikeFaction && f.permanentEnemy || IsRaidableFaction(f))
                .OrderBy(f => f.label ?? f.defName)
                .ToList();
        }

        private static bool IsRaidableFaction(FactionDef f)
        {
            if (f.isPlayer) return false;
            if (!f.humanlikeFaction) return false;
            if (f.hidden) return false;
            return f.permanentEnemy || f.pawnGroupMakers != null;
        }

        public static void Invalidate()
        {
            _initialized = false;
            _raidableFactions = null;
            _allVehicleKinds = null;
        }
    }
}

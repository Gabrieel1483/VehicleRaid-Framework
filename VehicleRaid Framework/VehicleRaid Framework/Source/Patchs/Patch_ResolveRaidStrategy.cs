using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleRaidFramework
{
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "ResolveRaidStrategy")]
    public static class Patch_ResolveRaidStrategy
    {
        [HarmonyPrefix]
        public static void Prefix(IncidentParms parms)
        {
            if (parms.raidStrategy == null) return;
            if (!(parms.raidStrategy.Worker is RaidStrategyWorker_VehicleAssault)) return;

            VehicleRaidExtension ext = parms.raidStrategy.GetModExtension<VehicleRaidExtension>();
            if (ext == null) { parms.raidStrategy = null; return; }

            bool factionMatches = false;
            if (!ext.factionDefs.NullOrEmpty())
                factionMatches = parms.faction != null && ext.factionDefs.Contains(parms.faction.def);
            else if (ext.factionDef != null)
                factionMatches = parms.faction != null && parms.faction.def == ext.factionDef;

            if (!factionMatches)
                parms.raidStrategy = null;
        }
    }
}

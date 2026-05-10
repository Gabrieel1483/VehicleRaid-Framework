using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace VehicleRaidFramework
{




    [HarmonyPatch(typeof(IncidentWorker_Raid), "TryExecuteWorker")]
    public static class Patch_VehicleRaidStrategySpawning
    {
        [HarmonyPrefix]
        public static bool Prefix(IncidentParms parms, IncidentWorker_Raid __instance, ref bool __result)
        {

            if (parms.raidStrategy?.defName != "VRF_VehicleAssault") return true;

            Map map = (Map)parms.target;
            if (map == null) return true;

            VehicleRaidExtension ext = parms.raidStrategy.GetModExtension<VehicleRaidExtension>();
            if (ext == null && parms.faction?.def != null)
            {
                ext = parms.faction.def.GetModExtension<VehicleRaidExtension>();
            }

            if (ext == null) return true;

            Faction faction = parms.faction ?? Find.FactionManager.RandomEnemyFaction();
            if (faction == null) return true;

            List<Pawn> spawnedPawns = VehicleRaidUtility.SpawnArmoredDivision(map, faction, parms, ext);

            if (spawnedPawns.NullOrEmpty())
            {
                __result = false;
                return false;
            }

            LordJob_VehicleRaid lordJob = new LordJob_VehicleRaid(faction, ext.stayTicks);
            LordMaker.MakeNewLord(faction, lordJob, map, spawnedPawns);

            SendRaidLetter(parms, spawnedPawns.First(), ext, faction);

            __result = true;
            return false;
        }

        private static void SendRaidLetter(IncidentParms parms, Pawn lookAt, VehicleRaidExtension ext, Faction faction)
        {
            string label = ext.letterLabel.Translate();
            string text = ext.letterText.Translate(faction.Name);
            LetterDef letterDef = LetterDefOf.ThreatBig;

            if (!faction.HostileTo(Faction.OfPlayer))
            {
                label = "VRF_LetterLabel_AlliedSupport".Translate();
                text = "VRF_LetterText_AlliedSupport".Translate(faction.Name);
                letterDef = LetterDefOf.PositiveEvent;
            }

            Find.LetterStack.ReceiveLetter(label, text, letterDef, lookAt, faction, null, null, null);
        }
    }
}


using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Vehicles;
using UnityEngine;

namespace VehicleRaidFramework
{




    public class IncidentWorker_VehicleRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            VehicleRaidExtension ext = this.def.GetModExtension<VehicleRaidExtension>();
            if (ext == null || ext.vehicleOptions.NullOrEmpty()) return false;

            Map map = (Map)parms.target;
            if (map == null) return false;

            Faction faction = ResolveFaction(parms, ext);
            if (faction == null) return false;

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            VehicleRaidExtension ext = this.def.GetModExtension<VehicleRaidExtension>();
            if (ext == null) return false;

            Faction faction = ResolveFaction(parms, ext);
            if (faction == null) return false;

            List<Pawn> spawnedPawns = VehicleRaidUtility.SpawnArmoredDivision(map, faction, parms, ext);

            if (spawnedPawns.NullOrEmpty()) return false;

            LordJob_VehicleRaid lordJob = new LordJob_VehicleRaid(faction, ext.stayTicks);
            LordMaker.MakeNewLord(faction, lordJob, map, spawnedPawns);

            string label = ext.letterLabel.Translate();
            string text = ext.letterText.Translate(faction.Name);
            LetterDef letterDef = LetterDefOf.ThreatBig;

            if (!faction.HostileTo(Faction.OfPlayer))
            {
                label = "VRF_LetterLabel_AlliedSupport".Translate();
                if (label == "VRF_LetterLabel_AlliedSupport") label = "Allied Support";
                text = "VRF_LetterText_AlliedSupport".Translate(faction.Name);
                if (text == "VRF_LetterText_AlliedSupport") text = "An armored division from {0} has arrived to support us!";
                letterDef = LetterDefOf.PositiveEvent;
            }

            SendStandardLetter(label, text, letterDef, parms, spawnedPawns.First());
            return true;
        }

        private Faction ResolveFaction(IncidentParms parms, VehicleRaidExtension ext)
        {
            if (!ext.factionDefs.NullOrEmpty())
            {
                FactionDef chosen = ext.factionDefs.RandomElement();
                return Find.FactionManager.FirstFactionOfDef(chosen);
            }
            if (ext.factionDef != null) return Find.FactionManager.FirstFactionOfDef(ext.factionDef);
            if (parms.faction != null) return parms.faction;
            return Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
        }
    }
}


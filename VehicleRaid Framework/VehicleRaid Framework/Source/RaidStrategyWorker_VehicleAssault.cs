using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace VehicleRaidFramework
{
    public class RaidStrategyWorker_VehicleAssault : RaidStrategyWorker
    {
        public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            VehicleRaidExtension ext = GetExtension(parms);
            if (ext == null) return false;

            if (!ext.factionDefs.NullOrEmpty())
            {
                if (parms.faction == null || !ext.factionDefs.Contains(parms.faction.def)) return false;
            }
            else if (ext.factionDef != null)
            {
                if (parms.faction == null || parms.faction.def != ext.factionDef) return false;
            }


            return base.CanUseWith(parms, groupKind);
        }

        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {

            return new LordJob_VehicleRaid(parms.faction);
        }

        private bool HasVehicleDefinition(IncidentParms parms)
        {
            return GetExtension(parms) != null;
        }

        private VehicleRaidExtension GetExtension(IncidentParms parms)
        {

            if (this.def != null)
            {
                var ext = this.def.GetModExtension<VehicleRaidExtension>();
                if (ext != null) return ext;
            }

            if (parms.faction?.def != null)
            {
                return parms.faction.def.GetModExtension<VehicleRaidExtension>();
            }

            return null;
        }

        private void SendLetter(IncidentParms parms, Pawn lookAt, VehicleRaidExtension ext)
        {
            string label = ext.letterLabel.Translate();
            string text = ext.letterText.Translate(parms.faction.Name);
            LetterDef letterDef = LetterDefOf.ThreatBig;

            if (!parms.faction.HostileTo(Faction.OfPlayer))
            {
                label = "VRF_LetterLabel_AlliedSupport".Translate();
                text = "VRF_LetterText_AlliedSupport".Translate(parms.faction.Name);
                letterDef = LetterDefOf.PositiveEvent;
            }

            Find.LetterStack.ReceiveLetter(label, text, letterDef, lookAt, parms.faction, null, null, null);
        }
    }
}


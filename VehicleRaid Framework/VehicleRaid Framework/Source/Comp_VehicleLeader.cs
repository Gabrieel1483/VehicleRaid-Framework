using Verse;

namespace VehicleRaidFramework
{
    public class CompProperties_VehicleLeader : CompProperties
    {
        public CompProperties_VehicleLeader()
        {
            this.compClass = typeof(Comp_VehicleLeader);
        }
    }

    public class Comp_VehicleLeader : ThingComp
    {
        public int priority = 0;
        public bool isLeader = false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref priority, "priority", 0);
            Scribe_Values.Look(ref isLeader, "isLeader", false);
        }
    }
}




using Verse;
using UnityEngine;

namespace VehicleRaidFramework
{
    public class VRF_Mod : Mod
    {
        public static VRF_Mod Instance { get; private set; }
        public static VRF_ModSettings Settings { get; private set; }

        public VRF_Mod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<VRF_ModSettings>();
        }

        public override string SettingsCategory() => "VRF_Settings_Category".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            VRF_SettingsUI.Draw(inRect);
        }
    }
}

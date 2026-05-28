using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;
using Verse;
using RimWorld;

namespace VehicleRaidFramework
{
    public static class VRF_PresetIO
    {
        private const string PresetFolder = "NaturalRaidPresets";

        public static string ExportPath => Path.Combine(
            GenFilePaths.ConfigFolderPath, "VehicleRaidFramework", "Presets");

        public static List<string> FindAllPresetFiles()
        {
            var files = new List<string>();

            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                string folder = Path.Combine(mod.RootDir, PresetFolder);
                if (Directory.Exists(folder))
                {
                    foreach (string f in Directory.GetFiles(folder, "*.xml"))
                        files.Add(f);
                }
            }

            if (Directory.Exists(ExportPath))
            {
                foreach (string f in Directory.GetFiles(ExportPath, "*.xml"))
                    files.Add(f);
            }

            return files;
        }

        public static void AutoLoadAllPresets(VRF_ModSettings settings)
        {
            var claimed = new HashSet<string>();

            bool any = false;
            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                string folder = Path.Combine(mod.RootDir, PresetFolder);
                if (!Directory.Exists(folder)) continue;

                foreach (string f in Directory.GetFiles(folder, "*.xml"))
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(f);
                        XmlElement root = doc.DocumentElement;
                        if (root == null || root.Name != "VRF_NaturalRaidPreset") continue;

                        foreach (XmlElement factionEl in root.SelectNodes("FactionConfig"))
                        {
                            string factionName = factionEl.GetAttribute("faction");
                            if (string.IsNullOrEmpty(factionName)) continue;

                            var factionConfig = settings.GetOrCreateFactionConfig(factionName);

                            foreach (XmlElement vehicleEl in factionEl.SelectNodes("VehicleEntry"))
                            {
                                string kindName = vehicleEl.GetAttribute("kind");
                                if (string.IsNullOrEmpty(kindName)) continue;

                                string claimKey = factionName + "::" + kindName;

                                bool.TryParse(vehicleEl.GetAttribute("enabled"), out bool enabled);

                                if (claimed.Contains(claimKey)) continue;

                                var vehicleEntry = factionConfig.GetOrCreate(kindName);

                                vehicleEntry.enabled = enabled;
                                if (float.TryParse(vehicleEl.GetAttribute("combatPower"), out float cp))
                                    vehicleEntry.combatPowerOverride = cp;
                                if (float.TryParse(vehicleEl.GetAttribute("minRaidPoints"), out float mrp))
                                    vehicleEntry.minRaidPoints = mrp;
                                if (float.TryParse(vehicleEl.GetAttribute("fuelPercent"), out float fp))
                                    vehicleEntry.fuelPercent = fp;

                                foreach (XmlElement turretEl in vehicleEl.SelectNodes("TurretAmmo"))
                                {
                                    string turretKey = turretEl.GetAttribute("turret");
                                    if (string.IsNullOrEmpty(turretKey)) continue;
                                    if (float.TryParse(turretEl.GetAttribute("ammoPercent"), out float ap))
                                        vehicleEntry.GetOrCreateTurretAmmo(turretKey).ammoPercent = ap;
                                }

                                if (enabled)
                                    claimed.Add(claimKey);

                                any = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[VRF] Failed to auto-load preset '{f}': {ex.Message}");
                    }
                }
            }

            if (any)
                VRF_Mod.Instance.WriteSettings();
        }

        public static void Export(VRF_ModSettings settings, string fileName)
        {
            try
            {
                if (!Directory.Exists(ExportPath))
                    Directory.CreateDirectory(ExportPath);

                string fullPath = Path.Combine(ExportPath, fileName + ".xml");

                XmlWriterSettings xmlSettings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\n"
                };

                using (XmlWriter writer = XmlWriter.Create(fullPath, xmlSettings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("VRF_NaturalRaidPreset");

                    foreach (var factionConfig in settings.factionConfigs)
                    {
                        if (factionConfig.vehicleEntries.Count == 0) continue;

                        writer.WriteStartElement("FactionConfig");
                        writer.WriteAttributeString("faction", factionConfig.factionDefName);

                        foreach (var vehicleEntry in factionConfig.vehicleEntries)
                        {
                            writer.WriteStartElement("VehicleEntry");
                            writer.WriteAttributeString("kind", vehicleEntry.vehicleKindDefName);
                            writer.WriteAttributeString("enabled", vehicleEntry.enabled.ToString().ToLower());
                            writer.WriteAttributeString("combatPower", vehicleEntry.combatPowerOverride.ToString("F0"));
                            writer.WriteAttributeString("minRaidPoints", vehicleEntry.minRaidPoints.ToString("F0"));
                            writer.WriteAttributeString("fuelPercent", vehicleEntry.fuelPercent.ToString("F1"));

                            foreach (var turretAmmo in vehicleEntry.turretAmmo)
                            {
                                writer.WriteStartElement("TurretAmmo");
                                writer.WriteAttributeString("turret", turretAmmo.turretKey);
                                writer.WriteAttributeString("ammoPercent", turretAmmo.ammoPercent.ToString("F1"));
                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                        }

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                Messages.Message("VRF_Preset_Exported".Translate(fullPath), MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[VRF] Failed to export preset: {ex}");
                Messages.Message("VRF_Preset_ExportFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        public static bool Import(string filePath, VRF_ModSettings settings)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);

                XmlElement root = doc.DocumentElement;
                if (root == null || root.Name != "VRF_NaturalRaidPreset")
                {
                    Messages.Message("VRF_Preset_InvalidFile".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                foreach (XmlElement factionEl in root.SelectNodes("FactionConfig"))
                {
                    string factionName = factionEl.GetAttribute("faction");
                    if (string.IsNullOrEmpty(factionName)) continue;

                    var factionConfig = settings.GetOrCreateFactionConfig(factionName);

                    foreach (XmlElement vehicleEl in factionEl.SelectNodes("VehicleEntry"))
                    {
                        string kindName = vehicleEl.GetAttribute("kind");
                        if (string.IsNullOrEmpty(kindName)) continue;

                        var vehicleEntry = factionConfig.GetOrCreate(kindName);

                        if (bool.TryParse(vehicleEl.GetAttribute("enabled"), out bool enabled))
                            vehicleEntry.enabled = enabled;
                        if (float.TryParse(vehicleEl.GetAttribute("combatPower"), out float cp))
                            vehicleEntry.combatPowerOverride = cp;
                        if (float.TryParse(vehicleEl.GetAttribute("minRaidPoints"), out float mrp))
                            vehicleEntry.minRaidPoints = mrp;
                        if (float.TryParse(vehicleEl.GetAttribute("fuelPercent"), out float fp))
                            vehicleEntry.fuelPercent = fp;

                        foreach (XmlElement turretEl in vehicleEl.SelectNodes("TurretAmmo"))
                        {
                            string turretKey = turretEl.GetAttribute("turret");
                            if (string.IsNullOrEmpty(turretKey)) continue;
                            if (float.TryParse(turretEl.GetAttribute("ammoPercent"), out float ap))
                                vehicleEntry.GetOrCreateTurretAmmo(turretKey).ammoPercent = ap;
                        }
                    }
                }

                VRF_Mod.Instance.WriteSettings();
                Messages.Message("VRF_Preset_Imported".Translate(Path.GetFileNameWithoutExtension(filePath)),
                    MessageTypeDefOf.PositiveEvent, false);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[VRF] Failed to import preset: {ex}");
                Messages.Message("VRF_Preset_ImportFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
        }

        public static string GetPresetDisplayName(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }
    }
}

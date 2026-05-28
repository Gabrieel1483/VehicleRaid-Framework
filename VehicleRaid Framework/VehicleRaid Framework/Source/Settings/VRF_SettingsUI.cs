using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Vehicles;
using Vehicles.Rendering;

namespace VehicleRaidFramework
{
    public enum VRF_SettingsPage
    {
        FactionList,
        VehicleList,
        VehicleDetail
    }

    public static class VRF_SettingsUI
    {
        private static VRF_SettingsPage _page = VRF_SettingsPage.FactionList;
        private static FactionDef _selectedFaction;
        private static PawnKindDef _selectedVehicleKind;

        private static Vector2 _factionScrollPos;
        private static Vector2 _vehicleScrollPos;
        private static Vector2 _detailScrollPos;
        private static Vector2 _presetScrollPos;

        private static bool _showPresetPanel = false;
        private static string _exportFileName = "MyPreset";

        private static readonly Dictionary<string, string> _cpBuffers  = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _mrpBuffers = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _hmsBuffers = new Dictionary<string, string>();

        private const float RowHeight = 50f;
        private const float Pad = 8f;
        private const float BtnW = 110f;

        public static void Draw(Rect inRect)
        {
            float y = inRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 32f), "VRF_Settings_Title".Translate());
            Text.Font = GameFont.Small;
            y += 36f;

            if (_page != VRF_SettingsPage.FactionList)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y, BtnW, 26f), "VRF_Settings_Back".Translate()))
                {
                    if (_page == VRF_SettingsPage.VehicleDetail)
                        _page = VRF_SettingsPage.VehicleList;
                    else
                    {
                        _page = VRF_SettingsPage.FactionList;
                        _selectedFaction = null;
                    }
                }
                y += 32f;
            }

            Rect content = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);

            switch (_page)
            {
                case VRF_SettingsPage.FactionList:   DrawFactionList(content);   break;
                case VRF_SettingsPage.VehicleList:   DrawVehicleList(content);   break;
                case VRF_SettingsPage.VehicleDetail: DrawVehicleDetail(content); break;
            }
        }

        private static void DrawFactionList(Rect rect)
        {
            var factions = VRF_VehicleKindCache.RaidableFactions;

            float topBarH = 30f;
            float checkH  = 26f;
            float topTotalH = topBarH + Pad + checkH + Pad;

            Rect topBar = new Rect(rect.x, rect.y, rect.width, topBarH);

            Widgets.Label(new Rect(topBar.x, topBar.y, topBar.width - BtnW * 2f - Pad * 2f, topBarH),
                "VRF_Settings_FactionListDesc".Translate());

            if (Widgets.ButtonText(new Rect(topBar.xMax - BtnW * 2f - Pad, topBar.y + 2f, BtnW, 26f),
                "VRF_Settings_Clear".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "VRF_Settings_ClearConfirm".Translate(),
                    () =>
                    {
                        VRF_Mod.Settings.ResetAllSettings();
                        VRF_SettingsUI.ResetBuffers();
                        VRF_Mod.Instance.WriteSettings();
                    }));
            }

            if (Widgets.ButtonText(new Rect(topBar.xMax - BtnW, topBar.y + 2f, BtnW, 26f),
                "VRF_Settings_Presets".Translate()))
                _showPresetPanel = !_showPresetPanel;

            float checkY = rect.y + topBarH + Pad;
            bool prevAuto = VRF_Mod.Settings.autoLoadPresets;
            Widgets.CheckboxLabeled(new Rect(rect.x, checkY, rect.width * 0.6f, checkH),
                "VRF_Settings_AutoLoadPresets".Translate(), ref VRF_Mod.Settings.autoLoadPresets);
            if (VRF_Mod.Settings.autoLoadPresets != prevAuto)
                VRF_Mod.Instance.WriteSettings();

            if (factions.NullOrEmpty())
            {
                Widgets.Label(new Rect(rect.x, rect.y + topTotalH, rect.width, 24f), "VRF_Settings_NoFactions".Translate());
                return;
            }

            if (_showPresetPanel)
            {
                float panelW = Mathf.Min(rect.width * 0.45f, 340f);
                Rect panelRect = new Rect(rect.xMax - panelW, rect.y + topTotalH, panelW, rect.height - topTotalH);
                DrawPresetPanel(panelRect);

                Rect scrollArea = new Rect(rect.x, rect.y + topTotalH, rect.width - panelW - Pad, rect.height - topTotalH);
                Rect viewRect   = new Rect(0f, 0f, scrollArea.width - 20f, factions.Count * RowHeight);
                Widgets.BeginScrollView(scrollArea, ref _factionScrollPos, viewRect);
                DrawFactionRows(factions, viewRect);
                Widgets.EndScrollView();
            }
            else
            {
                Rect scrollArea = new Rect(rect.x, rect.y + topTotalH, rect.width, rect.height - topTotalH);
                Rect viewRect   = new Rect(0f, 0f, scrollArea.width - 20f, factions.Count * RowHeight);
                Widgets.BeginScrollView(scrollArea, ref _factionScrollPos, viewRect);
                DrawFactionRows(factions, viewRect);
                Widgets.EndScrollView();
            }
        }

        private static void DrawFactionRows(List<FactionDef> factions, Rect viewRect)
        {
            for (int i = 0; i < factions.Count; i++)
            {
                FactionDef fDef = factions[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);

                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                Rect iconR = new Rect(row.x + Pad, row.y + (row.height - 32f) * 0.5f, 32f, 32f);
                if (fDef.FactionIcon != null)
                    GUI.DrawTexture(iconR, fDef.FactionIcon, ScaleMode.ScaleToFit);

                Rect labelR = new Rect(iconR.xMax + Pad, row.y, row.width - iconR.width - Pad * 3f - BtnW, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelR, fDef.label ?? fDef.defName);
                Text.Anchor = TextAnchor.UpperLeft;

                Rect btnR = new Rect(row.xMax - BtnW - Pad, row.y + (row.height - 26f) * 0.5f, BtnW, 26f);
                if (Widgets.ButtonText(btnR, "VRF_Settings_Configure".Translate()))
                {
                    _selectedFaction = fDef;
                    _page = VRF_SettingsPage.VehicleList;
                    _vehicleScrollPos = Vector2.zero;
                }
            }
        }

        private static void DrawPresetPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.85f));
            Widgets.DrawBox(rect, 1);

            Rect inner = rect.ContractedBy(Pad);
            float y = inner.y;

            Text.Font = GameFont.Small;

            GUI.color = new Color(0.9f, 0.85f, 0.6f);
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "VRF_Settings_PresetExport".Translate());
            GUI.color = Color.white;
            y += 24f;

            Widgets.Label(new Rect(inner.x, y, inner.width, 20f), "VRF_Settings_PresetFileName".Translate());
            y += 22f;
            _exportFileName = Widgets.TextField(new Rect(inner.x, y, inner.width - BtnW - Pad, 24f), _exportFileName);
            if (Widgets.ButtonText(new Rect(inner.xMax - BtnW, y, BtnW, 24f), "VRF_Settings_Export".Translate()))
            {
                string safeName = string.IsNullOrWhiteSpace(_exportFileName) ? "MyPreset" : _exportFileName;
                safeName = string.Concat(safeName.Split(Path.GetInvalidFileNameChars()));
                VRF_PresetIO.Export(VRF_Mod.Settings, safeName);
            }
            y += 30f;

            GUI.color = new Color(0.9f, 0.85f, 0.6f);
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "VRF_Settings_PresetImport".Translate());
            GUI.color = Color.white;
            y += 24f;

            var presetFiles = VRF_PresetIO.FindAllPresetFiles();
            if (presetFiles.Count == 0)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "VRF_Settings_NoPresets".Translate());
                GUI.color = Color.white;
            }
            else
            {
                float listH = inner.yMax - y;
                Rect listArea = new Rect(inner.x, y, inner.width, listH);
                Rect listView = new Rect(0f, 0f, listArea.width - 20f, presetFiles.Count * 30f);
                Widgets.BeginScrollView(listArea, ref _presetScrollPos, listView);

                for (int i = 0; i < presetFiles.Count; i++)
                {
                    string file = presetFiles[i];
                    string name = VRF_PresetIO.GetPresetDisplayName(file);
                    Rect row = new Rect(0f, i * 30f, listView.width, 28f);

                    if (i % 2 == 0) Widgets.DrawAltRect(row);
                    Widgets.DrawHighlightIfMouseover(row);

                    Rect nameR = new Rect(row.x + Pad, row.y, row.width - BtnW - Pad * 2f, row.height);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(nameR, name);
                    Text.Anchor = TextAnchor.UpperLeft;

                    Rect loadR = new Rect(row.xMax - BtnW, row.y + 1f, BtnW, 26f);
                    if (Widgets.ButtonText(loadR, "VRF_Settings_Load".Translate()))
                        VRF_PresetIO.Import(file, VRF_Mod.Settings);
                }

                Widgets.EndScrollView();
            }
        }

        private static void DrawVehicleList(Rect rect)
        {
            if (_selectedFaction == null) { _page = VRF_SettingsPage.FactionList; return; }

            var allKinds = VRF_VehicleKindCache.AllVehicleKinds;
            var factionConfig = VRF_Mod.Settings.GetOrCreateFactionConfig(_selectedFaction.defName);

            Text.Font = GameFont.Small;
            string header = "VRF_Settings_VehicleListDesc".Translate(_selectedFaction.label ?? _selectedFaction.defName);
            float headerH = Text.CalcHeight(header, rect.width);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, headerH), header);

            if (allKinds.NullOrEmpty())
            {
                Widgets.Label(new Rect(rect.x, rect.y + headerH + 4f, rect.width, 24f), "VRF_Settings_NoVehicles".Translate());
                return;
            }

            Rect scrollArea = new Rect(rect.x, rect.y + headerH + 4f, rect.width, rect.height - headerH - 4f);
            Rect viewRect   = new Rect(0f, 0f, scrollArea.width - 20f, allKinds.Count * RowHeight);

            Widgets.BeginScrollView(scrollArea, ref _vehicleScrollPos, viewRect);

            for (int i = 0; i < allKinds.Count; i++)
            {
                PawnKindDef kind = allKinds[i];
                var entry = factionConfig.GetOrCreate(kind.defName);

                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);

                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                float cx = row.x + Pad;

                bool wasEnabled = entry.enabled;
                Widgets.Checkbox(cx, row.y + (row.height - 24f) * 0.5f, ref entry.enabled, 24f);
                if (entry.enabled != wasEnabled)
                    VRF_Mod.Instance.WriteSettings();
                cx += 24f + Pad;

                float thumbSize = row.height - 4f;
                Rect thumbR = new Rect(cx, row.y + 2f, thumbSize, thumbSize);
                DrawVehicleThumb(thumbR, kind);
                cx += thumbSize + Pad;

                float labelW = row.width - cx - BtnW - Pad * 2f;
                Rect labelR = new Rect(cx, row.y, labelW, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                float displayCp = entry.combatPowerOverride > 0f ? entry.combatPowerOverride : kind.combatPower;
                string cp = "VRF_Settings_CombatPower".Translate() + ": " + displayCp.ToString("F0");
                Widgets.Label(labelR, $"{kind.label ?? kind.defName}\n<color=#aaaaaa>{cp}</color>");
                Text.Anchor = TextAnchor.UpperLeft;

                Rect btnR = new Rect(row.xMax - BtnW - Pad, row.y + (row.height - 26f) * 0.5f, BtnW, 26f);
                if (Widgets.ButtonText(btnR, "VRF_Settings_Details".Translate()))
                {
                    _selectedVehicleKind = kind;
                    _page = VRF_SettingsPage.VehicleDetail;
                    _detailScrollPos = Vector2.zero;
                }
            }

            Widgets.EndScrollView();
        }

        private static void DrawVehicleDetail(Rect rect)
        {
            if (_selectedVehicleKind == null) { _page = VRF_SettingsPage.VehicleList; return; }
            if (_selectedFaction == null)     { _page = VRF_SettingsPage.FactionList; return; }

            PawnKindDef kind = _selectedVehicleKind;
            VehicleDef vDef = kind.race as VehicleDef;
            var factionConfig = VRF_Mod.Settings.GetOrCreateFactionConfig(_selectedFaction.defName);
            var entry = factionConfig.GetOrCreate(kind.defName);

            if (entry.combatPowerOverride <= 0f)
                entry.combatPowerOverride = kind.combatPower;
            if (entry.minRaidPoints <= 0f)
                entry.minRaidPoints = entry.combatPowerOverride;

            string bufKey = _selectedFaction.defName + "_" + kind.defName;
            if (!_cpBuffers.TryGetValue(bufKey, out string cpBuf) || cpBuf == null)
            {
                cpBuf = ((int)entry.combatPowerOverride).ToString();
                _cpBuffers[bufKey] = cpBuf;
            }
            if (!_mrpBuffers.TryGetValue(bufKey, out string mrpBuf) || mrpBuf == null)
            {
                mrpBuf = ((int)entry.minRaidPoints).ToString();
                _mrpBuffers[bufKey] = mrpBuf;
            }

            float previewSize = Mathf.Min(rect.width * 0.38f, 200f);
            float totalContentH = CalcDetailContentHeight(kind, vDef, entry, previewSize);
            bool needsScroll = totalContentH > rect.height;
            float viewW = needsScroll ? rect.width - 20f : rect.width;
            Rect viewRect = new Rect(0f, 0f, viewW, Mathf.Max(totalContentH, rect.height));

            Widgets.BeginScrollView(rect, ref _detailScrollPos, viewRect);

            float localInfoX = previewSize + Pad * 2f;
            float localInfoW = viewW - localInfoX;

            Rect previewBox = new Rect(0f, 0f, previewSize, previewSize);
            Widgets.DrawBoxSolid(previewBox, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            Widgets.DrawBox(previewBox, 1);
            if (vDef != null)
                DrawVehicleWithTurrets(previewBox.ContractedBy(6f), vDef);
            else
                DrawVehicleThumb(previewBox.ContractedBy(6f), kind);

            float iy = 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(localInfoX, iy, localInfoW, 32f), kind.label ?? kind.defName);
            iy += 36f;
            Text.Font = GameFont.Small;

            if (vDef != null)
            {
                Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f),
                    "VRF_Settings_TechLevel".Translate(vDef.techLevel.ToStringHuman()));
                iy += 24f;
                Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f),
                    "VRF_Settings_VehicleType".Translate(vDef.type.ToString()));
                iy += 24f;
            }

            iy += 4f;

            bool wasEnabled = entry.enabled;
            Widgets.CheckboxLabeled(new Rect(localInfoX, iy, localInfoW, 26f),
                "VRF_Settings_EnableForRaids".Translate(), ref entry.enabled);
            if (entry.enabled != wasEnabled)
                VRF_Mod.Instance.WriteSettings();
            iy += 28f;

            if (entry.enabled)
            {
                GUI.color = new Color(0.55f, 1f, 0.55f);
                Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f), "VRF_Settings_VehicleActive".Translate());
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.65f, 0.65f, 0.65f);
                Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f), "VRF_Settings_VehicleInactive".Translate());
                GUI.color = Color.white;
            }
            iy += 26f;

            iy += 6f;
            Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f), "VRF_Settings_CombatPower".Translate());
            iy += 22f;
            float prevCp = entry.combatPowerOverride;
            Widgets.TextFieldNumeric(new Rect(localInfoX, iy, Mathf.Min(localInfoW, 120f), 24f),
                ref entry.combatPowerOverride, ref cpBuf, 1f, 999999f);
            _cpBuffers[bufKey] = cpBuf;
            if (entry.combatPowerOverride != prevCp)
                VRF_Mod.Instance.WriteSettings();
            iy += 28f;

            Widgets.Label(new Rect(localInfoX, iy, localInfoW, 22f), "VRF_Settings_MinCombatPoints".Translate());
            iy += 22f;
            float prevMrp = entry.minRaidPoints;
            Widgets.TextFieldNumeric(new Rect(localInfoX, iy, Mathf.Min(localInfoW, 120f), 24f),
                ref entry.minRaidPoints, ref mrpBuf, 1f, 999999f);
            _mrpBuffers[bufKey] = mrpBuf;
            if (entry.minRaidPoints != prevMrp)
                VRF_Mod.Instance.WriteSettings();
            iy += 28f;

            float topSectionBottom = Mathf.Max(previewBox.yMax, iy) + Pad;

            if (vDef != null)
            {
                float sliderAreaH = CalcSliderAreaHeight(vDef);
                DrawResourceSliders(new Rect(0f, topSectionBottom, viewW, sliderAreaH), vDef, entry);

                if (vDef.type == VehicleType.Air)
                {
                    float flightY = topSectionBottom + sliderAreaH + Pad;
                    DrawFlightModeSection(new Rect(0f, flightY, viewW, CalcFlightModeHeight(entry)), entry, bufKey);
                }
            }

            Widgets.EndScrollView();
        }

        private static float CalcDetailContentHeight(PawnKindDef kind, VehicleDef vDef, VRF_NaturalRaidVehicleEntry entry, float previewSize)
        {
            float infoH = 36f;
            if (vDef != null) infoH += 48f;
            infoH += 4f + 28f + 26f + 6f + 22f + 28f + 22f + 28f + 28f;

            float topSectionBottom = Mathf.Max(previewSize, infoH) + Pad;
            float sliderH = vDef != null ? CalcSliderAreaHeight(vDef) : 0f;
            float flightH = (vDef != null && vDef.type == VehicleType.Air) ? CalcFlightModeHeight(entry) : 0f;
            return topSectionBottom + sliderH + flightH + Pad;
        }

        private static float CalcFlightModeHeight(VRF_NaturalRaidVehicleEntry entry)
        {
            float h = 30f + 28f;
            if (entry.helicopterMode)
                h += 28f + 22f + 28f;
            return h + Pad;
        }

        private static float CalcSliderAreaHeight(VehicleDef vDef)
        {
            const float titleH = 24f;
            const float barH   = 16f;
            const float rowH   = 54f;

            float h = titleH + barH;

            var fuelProps = vDef.GetSortedCompProperties<CompProperties_FueledTravel>();
            if (fuelProps != null && !fuelProps.ElectricPowered)
                h += rowH;

            var turretProps = vDef.CompPropsVehicleTurrets;
            if (turretProps != null)
            {
                foreach (VehicleTurret turret in turretProps.turrets)
                {
                    if (turret?.def?.ammunition == null) continue;
                    if (turret.def.ammunition.AllowedThingDefs.FirstOrDefault() == null) continue;
                    h += rowH;
                }
            }

            return h + Pad;
        }

        private static void DrawResourceSliders(Rect rect, VehicleDef vDef, VRF_NaturalRaidVehicleEntry entry)
        {
            float cargoCapacity = vDef.GetStatValueAbstract(VehicleStatDefOf.CargoCapacity);
            var fuelProps = vDef.GetSortedCompProperties<CompProperties_FueledTravel>();
            var turretProps = vDef.CompPropsVehicleTurrets;

            float maxFuelKg = 0f;
            bool hasFuel = fuelProps != null && !fuelProps.ElectricPowered;
            if (hasFuel)
            {
                float massPerUnit = fuelProps.fuelType != null
                    ? fuelProps.fuelType.GetStatValueAbstract(StatDefOf.Mass)
                    : 1f;
                maxFuelKg = fuelProps.fuelCapacity * massPerUnit;
            }

            var ammoDefs = new List<(VehicleTurret turret, ThingDef ammoDef, float ammoMass)>();
            if (turretProps != null)
            {
                foreach (VehicleTurret turret in turretProps.turrets)
                {
                    if (turret?.def?.ammunition == null) continue;
                    ThingDef ammoDef = turret.def.ammunition.AllowedThingDefs.FirstOrDefault();
                    if (ammoDef == null) continue;
                    float mass = ammoDef.GetStatValueAbstract(StatDefOf.Mass);
                    if (mass <= 0f) mass = 0.1f;
                    ammoDefs.Add((turret, ammoDef, mass));
                }
            }

            float currentFuelKg = hasFuel ? maxFuelKg * (entry.fuelPercent / 100f) : 0f;
            float currentAmmoKg = 0f;
            foreach (var (turret, ammoDef, ammoMass) in ammoDefs)
            {
                var tEntry = entry.GetOrCreateTurretAmmo(turret.def.defName);
                currentAmmoKg += (cargoCapacity > 0f ? cargoCapacity : 999f) * (tEntry.ammoPercent / 100f);
            }
            float totalUsedKg = currentFuelKg + currentAmmoKg;

            float y = rect.y;
            const float titleH = 24f;
            const float labelH = 20f;
            const float sliderH = 22f;
            const float barH    = 10f;
            const float rowGap  = 10f;

            GUI.color = new Color(0.9f, 0.85f, 0.6f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, y, rect.width, titleH),
                "VRF_Settings_CargoSection".Translate(cargoCapacity.ToString("F0")));
            GUI.color = Color.white;
            y += titleH;

            if (cargoCapacity > 0f)
            {
                float fillPct = Mathf.Clamp01(totalUsedKg / cargoCapacity);
                Color barColor = fillPct >= 1f ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.3f, 0.7f, 0.3f);
                Widgets.DrawBoxSolid(new Rect(rect.x, y, rect.width, barH), new Color(0.2f, 0.2f, 0.2f));
                Widgets.DrawBoxSolid(new Rect(rect.x, y, rect.width * fillPct, barH), barColor);
                Widgets.DrawBox(new Rect(rect.x, y, rect.width, barH), 1);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.x, y, rect.width, barH),
                    $"{totalUsedKg:F1} / {cargoCapacity:F0} kg");
                Text.Anchor = TextAnchor.UpperLeft;
                y += barH + 6f;
            }

            if (hasFuel)
            {
                float fuelKg = maxFuelKg * (entry.fuelPercent / 100f);
                string fuelName = fuelProps.fuelType?.label ?? "fuel";
                Widgets.Label(new Rect(rect.x, y, rect.width, labelH),
                    "VRF_Settings_Fuel".Translate(fuelName, entry.fuelPercent.ToString("F0"), fuelKg.ToString("F1"), maxFuelKg.ToString("F1")));
                y += labelH + 2f;

                float maxAllowedFuelPct = cargoCapacity > 0f && maxFuelKg > 0f
                    ? Mathf.Clamp01((cargoCapacity - currentAmmoKg) / maxFuelKg) * 100f
                    : 100f;
                float clampedFuelPct = Mathf.Min(entry.fuelPercent, maxAllowedFuelPct);

                float newPct = Widgets.HorizontalSlider(new Rect(rect.x, y, rect.width, sliderH),
                    clampedFuelPct, 0f, 100f, roundTo: 1f);
                newPct = Mathf.Min(newPct, maxAllowedFuelPct);
                if (Mathf.Abs(newPct - entry.fuelPercent) > 0.01f)
                {
                    entry.fuelPercent = newPct;
                    VRF_Mod.Instance.WriteSettings();
                }
                y += sliderH + rowGap;
            }

            foreach (var (turret, ammoDef, ammoMass) in ammoDefs)
            {
                var tEntry = entry.GetOrCreateTurretAmmo(turret.def.defName);
                float maxAmmoKg = cargoCapacity > 0f ? cargoCapacity : 999f;
                float ammoKg = maxAmmoKg * (tEntry.ammoPercent / 100f);
                int ammoCount = Mathf.FloorToInt(ammoKg / ammoMass);

                float otherAmmoKg = currentAmmoKg - maxAmmoKg * (tEntry.ammoPercent / 100f);
                float maxAllowedAmmoPct = cargoCapacity > 0f
                    ? Mathf.Clamp01((cargoCapacity - currentFuelKg - otherAmmoKg) / maxAmmoKg) * 100f
                    : 100f;
                float clampedAmmoPct = Mathf.Min(tEntry.ammoPercent, maxAllowedAmmoPct);

                string turretLabel = turret.def.label ?? turret.def.defName;
                Widgets.Label(new Rect(rect.x, y, rect.width, labelH),
                    "VRF_Settings_Ammo".Translate(turretLabel, ammoDef.label ?? ammoDef.defName,
                        clampedAmmoPct.ToString("F0"), ammoCount.ToString(), ammoKg.ToString("F1")));
                y += labelH + 2f;

                float newPct = Widgets.HorizontalSlider(new Rect(rect.x, y, rect.width, sliderH),
                    clampedAmmoPct, 0f, 100f, roundTo: 1f);
                newPct = Mathf.Min(newPct, maxAllowedAmmoPct);
                if (Mathf.Abs(newPct - tEntry.ammoPercent) > 0.01f)
                {
                    tEntry.ammoPercent = newPct;
                    VRF_Mod.Instance.WriteSettings();
                }
                y += sliderH + rowGap;
            }
        }

        private static void DrawFlightModeSection(Rect rect, VRF_NaturalRaidVehicleEntry entry, string bufKey)
        {
            float y = rect.y;
            const float labelH = 22f;
            const float fieldH = 24f;

            GUI.color = new Color(0.7f, 0.85f, 1f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, y, rect.width, labelH), "VRF_Settings_FlightMode".Translate());
            GUI.color = Color.white;
            y += labelH + 4f;

            bool wasHeli = entry.helicopterMode;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, rect.width, labelH),
                "VRF_Settings_HelicopterType".Translate(), ref entry.helicopterMode);
            if (entry.helicopterMode != wasHeli)
                VRF_Mod.Instance.WriteSettings();
            y += labelH + 4f;

            if (entry.helicopterMode)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, labelH), "VRF_Settings_HoverMoveSpeed".Translate());
                y += labelH + 2f;

                if (!_hmsBuffers.TryGetValue(bufKey, out string hmsBuf) || hmsBuf == null)
                {
                    hmsBuf = entry.helicopterMoveSpeed.ToString("F1");
                    _hmsBuffers[bufKey] = hmsBuf;
                }

                float prevHms = entry.helicopterMoveSpeed;
                Widgets.TextFieldNumeric(new Rect(rect.x, y, Mathf.Min(rect.width, 120f), fieldH),
                    ref entry.helicopterMoveSpeed, ref hmsBuf, 0.1f, 99f);
                _hmsBuffers[bufKey] = hmsBuf;
                if (Mathf.Abs(entry.helicopterMoveSpeed - prevHms) > 0.001f)
                    VRF_Mod.Instance.WriteSettings();
            }
        }

        private static void DrawVehicleWithTurrets(Rect rect, VehicleDef vDef)
        {
            if (Event.current.type != EventType.Repaint) return;
            VehicleGui.DrawVehicleDefOnGUI(rect, vDef);
        }

        private static void DrawTurretIcon(Rect rect, VehicleTurretDef tDef)
        {
            Texture2D tex = null;

            if (!tDef.gizmoIconTexPath.NullOrEmpty())
                tex = ContentFinder<Texture2D>.Get(tDef.gizmoIconTexPath, false);

            if (tex == null && tDef.graphicData?.Graphic != null)
                tex = tDef.graphicData.Graphic.MatNorth?.mainTexture as Texture2D;

            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
            if (tex != null)
                GUI.DrawTexture(rect.ContractedBy(2f), tex, ScaleMode.ScaleToFit);
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Tiny;
                Widgets.Label(rect, "?");
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private static void DrawVehicleThumb(Rect rect, PawnKindDef kind)
        {
            if (kind?.lifeStages == null || kind.lifeStages.Count == 0) return;
            var stage = kind.lifeStages[kind.lifeStages.Count - 1];
            if (stage?.bodyGraphicData?.Graphic == null) return;
            Texture2D tex = stage.bodyGraphicData.Graphic.MatNorth?.mainTexture as Texture2D;
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        }

        public static void ResetBuffers()
        {
            _cpBuffers.Clear();
            _mrpBuffers.Clear();
            _hmsBuffers.Clear();
            _page = VRF_SettingsPage.FactionList;
            _selectedFaction = null;
            _selectedVehicleKind = null;
        }
    }
}

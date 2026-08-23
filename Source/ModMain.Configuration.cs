using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Configuration, persistent settings and MCM integration owner.
    /// Extracted in v1.7.36-test11 without changing runtime behavior.
    /// </summary>
    public static partial class ModMain
    {
        private static bool _configLoaded;
        private static bool _mcmRegistered;
        private static bool _mcmAttempted;

        private static bool EnableItemIntelligence = true;
        private static bool QuickIntelligence = true;
        private static bool InspectorEnabled = true;
        private static bool ShowInspectorHint = true;
        private static bool ShowInterfaceIcons = true;
        private static bool ModderMode = false;
        private static bool ShowMagnumUses = true;
        private static bool ShowFutureMagnumUses = true;
        private static bool ShowRecipes = true;
        private static bool ShowSources = true;
        private static bool ShowTradeInformation = true;
        private static bool UsePreviousTradeLayout = false;
        private static bool ShowMagnumSurplus = true;
        private static bool ShowAmmoRelations = true;

        // InspectorKey is one validated runtime value shared by config, MCM, hints
        // and the browser toggle.
        private static string InspectorKeyName = "F2";
        private static KeyCode InspectorKeyCode = KeyCode.F2;

        private static string ConfigDirectory
        {
            get { return Path.GetFullPath(Path.Combine(Application.persistentDataPath, "..", "Quasimorph_ModConfigs", "ItemIntelligence")); }
        }

        private static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "config.ini"); }
        }

        private static void EnsureConfigLoaded()
        {
            if (_configLoaded)
                return;

            _configLoaded = true;
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                if (!File.Exists(ConfigPath))
                {
                    SaveConfig();
                    return;
                }

                string section = string.Empty;
                string[] lines = File.ReadAllLines(ConfigPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = (lines[i] ?? string.Empty).Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    int split = line.IndexOf('=');
                    if (split <= 0)
                        continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    bool parsed;
                    if (bool.TryParse(value, out parsed))
                    {
                        ApplyConfigValue(key, parsed);
                        continue;
                    }

                    ApplyConfigTextValue(key, value);
                }

                // test18: Item Intelligence is the item browser. Older builds exposed both
                // a master switch and a browser switch even though the master had no
                // independent player-facing feature left. Preserve an old disabled master
                // as a disabled browser once, then retire the hidden duplicate gate.
                if (!EnableItemIntelligence)
                    InspectorEnabled = false;
                EnableItemIntelligence = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Config load failed; defaults are active: " + ex.Message);
            }
        }



        private static void ApplyConfigValue(string key, bool value)
        {
            if (string.Equals(key, "EnableItemIntelligence", StringComparison.OrdinalIgnoreCase)) EnableItemIntelligence = value;
            else if (string.Equals(key, "QuickIntelligence", StringComparison.OrdinalIgnoreCase)) QuickIntelligence = value;
            else if (string.Equals(key, "InspectorEnabled", StringComparison.OrdinalIgnoreCase)) InspectorEnabled = value;
            else if (string.Equals(key, "ShowInspectorHint", StringComparison.OrdinalIgnoreCase)) SetShowInspectorHint(value);
            else if (string.Equals(key, "ShowInterfaceIcons", StringComparison.OrdinalIgnoreCase)) ShowInterfaceIcons = value;
            else if (string.Equals(key, "ModderMode", StringComparison.OrdinalIgnoreCase)) ModderMode = value;
            else if (string.Equals(key, "ShowMagnumUses", StringComparison.OrdinalIgnoreCase)) ShowMagnumUses = value;
            else if (string.Equals(key, "ShowFutureMagnumUses", StringComparison.OrdinalIgnoreCase)) ShowFutureMagnumUses = value;
            else if (string.Equals(key, "ShowRecipes", StringComparison.OrdinalIgnoreCase)) ShowRecipes = value;
            else if (string.Equals(key, "ShowSources", StringComparison.OrdinalIgnoreCase)) ShowSources = value;
            else if (string.Equals(key, "ShowTradeInformation", StringComparison.OrdinalIgnoreCase)) ShowTradeInformation = value;
            else if (string.Equals(key, "UsePreviousTradeLayout", StringComparison.OrdinalIgnoreCase)) UsePreviousTradeLayout = value;
            else if (string.Equals(key, "ShowMagnumSurplus", StringComparison.OrdinalIgnoreCase)) ShowMagnumSurplus = value;
            else if (string.Equals(key, "ShowAmmoRelations", StringComparison.OrdinalIgnoreCase)) ShowAmmoRelations = value;
        }

        private static void ApplyConfigTextValue(string key, string value)
        {
            if (string.Equals(key, "InspectorKey", StringComparison.OrdinalIgnoreCase))
                SetInspectorKey(value, "config.ini");
        }











        private static string HotkeyUi(string key)
        {
            string text = Ui(key);
            return string.IsNullOrEmpty(text)
                ? text
                : text.Replace("{KEY}", GetInspectorKeyDisplayName());
        }









        private static bool SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string text =
                    "# Quasimorph Item Intelligence " + Version + "\r\n" +
                    "# Better Item Info - Uses & Sources\r\n\r\n" +
                    "[General]\r\n" +
                    "EnableItemIntelligence=" + EnableItemIntelligence + "\r\n\r\n" +
                    "[Tooltip]\r\n" +
                    "QuickIntelligence=" + QuickIntelligence + "\r\n" +
                    "ShowInspectorHint=" + ShowInspectorHint + "\r\n\r\n" +
                    "[Inspector]\r\n" +
                    "InspectorEnabled=" + InspectorEnabled + "\r\n" +
                    "InspectorKey=" + InspectorKeyName + "\r\n" +
                    "ShowInterfaceIcons=" + ShowInterfaceIcons + "\r\n" +
                    "ModderMode=" + ModderMode + "\r\n\r\n" +
                    "[Information]\r\n" +
                    "ShowMagnumUses=" + ShowMagnumUses + "\r\n" +
                    "ShowFutureMagnumUses=" + ShowFutureMagnumUses + "\r\n" +
                    "ShowRecipes=" + ShowRecipes + "\r\n" +
                    "ShowSources=" + ShowSources + "\r\n" +
                    "ShowTradeInformation=" + ShowTradeInformation + "\r\n" +
                    "UsePreviousTradeLayout=" + UsePreviousTradeLayout + "\r\n" +
                    "ShowMagnumSurplus=" + ShowMagnumSurplus + "\r\n" +
                    "ShowAmmoRelations=" + ShowAmmoRelations + "\r\n";
                File.WriteAllText(ConfigPath, text);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Config save failed: " + ex.Message);
                return false;
            }
        }

        private static void TryRegisterMcm()
        {
            if (_mcmRegistered)
                return;

            try
            {
                Type apiType = null;
                Type interfaceType = null;
                Type configValueType = null;
                Type dropdownConfigType = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly asm = assemblies[i];
                    if (asm == null) continue;
                    apiType = asm.GetType("ModConfigMenu.ModConfigMenuAPI", false);
                    if (apiType == null) continue;
                    interfaceType = asm.GetType("ModConfigMenu.Contracts.IConfigValue", false);
                    configValueType = asm.GetType("ModConfigMenu.Objects.ConfigValue", false);
                    dropdownConfigType = asm.GetType("ModConfigMenu.Implementations.DropdownConfig", false);
                    if (interfaceType != null && configValueType != null)
                        break;
                    apiType = null;
                }

                if (apiType == null || interfaceType == null || configValueType == null)
                {
                    if (!_mcmAttempted)
                        Debug.Log("[ItemIntelligence] MCM not detected; config.ini remains available.");
                    _mcmAttempted = true;
                    return;
                }

                Type listType = typeof(List<>).MakeGenericType(interfaceType);
                object list = Activator.CreateInstance(listType);
                MethodInfo add = listType.GetMethod("Add");
                if (add == null) throw new MissingMethodException("MCM IConfigValue list Add method not found.");

                // QuickIntelligence remains config-compatible for older installations,
                // but its legacy tooltip-builder hook is intentionally not installed.
                // Do not expose a switch in MCM until a safe pointer-path replacement
                // exists; showing it currently promises behavior the runtime cannot use.
                AddMcmBool(add, list, configValueType, "ShowInspectorHint", ShowInspectorHint, Ui("mcm.header.tooltip"), HotkeyUi("mcm.show_f2_hint"), HotkeyUi("mcm.show_f2_hint_tip"));
                AddMcmBool(add, list, configValueType, "InspectorEnabled", InspectorEnabled, Ui("mcm.header.inspector"), Ui("ui.enable_item_intelligence"), HotkeyUi("mcm.enable_browser_tip"));
                AddMcmStringDropdown(add, list, dropdownConfigType, configValueType, "InspectorKey", GetInspectorKeyDisplayName(), Ui("mcm.header.inspector"), "F2", HotkeyUi("mcm.inspector_hotkey_tip"), Ui("mcm.inspector_hotkey"), GetInspectorHotkeyOptions());
                AddMcmBool(add, list, configValueType, "ShowInterfaceIcons", ShowInterfaceIcons, Ui("mcm.header.inspector"), Ui("mcm.show_interface_icons"), Ui("mcm.show_interface_icons_tip"));
                AddMcmBool(add, list, configValueType, "ModderMode", ModderMode, Ui("mcm.header.inspector"), Ui("mcm.modder_mode"), Ui("mcm.modder_mode_tip"));
                AddMcmBool(add, list, configValueType, "ShowMagnumUses", ShowMagnumUses, Ui("mcm.header.information"), Ui("ui.show_magnum_uses"), Ui("ui.show_unfinished_magnum_upgrade_uses"));
                AddMcmBool(add, list, configValueType, "ShowFutureMagnumUses", ShowFutureMagnumUses, Ui("mcm.header.information"), Ui("ui.show_future_magnum_uses"), Ui("ui.include_locked_future_magnum_upgrades"));
                AddMcmBool(add, list, configValueType, "ShowRecipes", ShowRecipes, Ui("mcm.header.information"), Ui("ui.show_recipes"), Ui("ui.show_used_in_and_crafted_from_relationships"));
                AddMcmBool(add, list, configValueType, "ShowSources", ShowSources, Ui("mcm.header.information"), Ui("ui.show_sources"), Ui("ui.show_production_and_barter_source_relationships"));
                AddMcmBool(add, list, configValueType, "ShowTradeInformation", ShowTradeInformation, Ui("mcm.header.information"), Ui("ui.show_trade_information"), Ui("ui.show_barter_and_consumer_relationships"));
                AddMcmBool(add, list, configValueType, "ShowMagnumSurplus", ShowMagnumSurplus, Ui("mcm.header.information"), Ui("ui.show_magnum_surplus"), Ui("ui.show_remaining_amount_relative_to_all_unfinished"));
                AddMcmBool(add, list, configValueType, "ShowAmmoRelations", ShowAmmoRelations, Ui("mcm.header.information"), Ui("ui.show_ammo_relations"), Ui("ui.show_weapon_ammo_relationships"));

                Type delegateType = apiType.GetNestedType("ConfigStoredDelegate", BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo callbackMethod = typeof(ModMain).GetMethod("OnMcmConfigSaved", StaticFlags);
                if (delegateType == null || callbackMethod == null)
                    throw new MissingMethodException("MCM ConfigStoredDelegate API not found.");
                Delegate callback = Delegate.CreateDelegate(delegateType, callbackMethod);

                MethodInfo register = null;
                MethodInfo[] methods = apiType.GetMethods(BindingFlags.Static | BindingFlags.Public);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "RegisterModConfig", StringComparison.Ordinal)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length == 3 && p[1].ParameterType == listType && p[2].ParameterType == delegateType)
                    {
                        register = method;
                        break;
                    }
                }
                if (register == null)
                    throw new MissingMethodException("Modern MCM RegisterModConfig overload not found.");

                register.Invoke(null, new object[] { Ui("mcm.mod_name"), list, callback });
                _mcmRegistered = true;
                _mcmAttempted = true;
                Debug.Log("[ItemIntelligence] MCM registered.");
            }
            catch (Exception ex)
            {
                _mcmAttempted = true;
                Debug.LogWarning("[ItemIntelligence] MCM registration failed; config.ini remains available: " + ex);
            }
        }

        private static void AddMcmBool(MethodInfo add, object list, Type configValueType, string key, bool value, string header, string label, string tooltip)
        {
            object config = Activator.CreateInstance(configValueType, new object[] { key, value, header, value, tooltip, label });
            add.Invoke(list, new object[] { config });
        }

        private static void AddMcmStringDropdown(MethodInfo add, object list, Type dropdownConfigType, Type legacyConfigValueType, string key, string value, string header, string defaultValue, string tooltip, string label, List<string> options)
        {
            object config;

            // MCM 1.6.2+ introduced dedicated IConfigValue implementations. Prefer the
            // native DropdownConfig when present, but keep the legacy ConfigValue path
            // so Item Intelligence remains compatible with older MCM installations.
            if (dropdownConfigType != null)
            {
                List<object> boxedOptions = new List<object>();
                for (int i = 0; i < options.Count; i++)
                    boxedOptions.Add(options[i]);

                config = Activator.CreateInstance(dropdownConfigType, new object[] { key, value, header, defaultValue, tooltip, label, boxedOptions });
            }
            else
            {
                config = Activator.CreateInstance(legacyConfigValueType, new object[] { key, value, header, defaultValue, tooltip, label, options });
            }

            add.Invoke(list, new object[] { config });
        }

        private static bool OnMcmConfigSaved(Dictionary<string, object> currentConfig, out string feedbackMessage)
        {
            feedbackMessage = string.Empty;
            try
            {
                if (currentConfig == null) return true;
                ApplyMcmBool(currentConfig, "QuickIntelligence", ref QuickIntelligence);

                bool savedShowInspectorHint = ShowInspectorHint;
                ApplyMcmBool(currentConfig, "ShowInspectorHint", ref savedShowInspectorHint);
                SetShowInspectorHint(savedShowInspectorHint);

                ApplyMcmBool(currentConfig, "InspectorEnabled", ref InspectorEnabled);
                string savedInspectorKey;
                if (TryReadMcmString(currentConfig, "InspectorKey", out savedInspectorKey))
                    SetInspectorKey(savedInspectorKey, "MCM");
                ApplyMcmBool(currentConfig, "ShowInterfaceIcons", ref ShowInterfaceIcons);
                ApplyMcmBool(currentConfig, "ModderMode", ref ModderMode);
                ApplyMcmBool(currentConfig, "ShowMagnumUses", ref ShowMagnumUses);
                ApplyMcmBool(currentConfig, "ShowFutureMagnumUses", ref ShowFutureMagnumUses);
                ApplyMcmBool(currentConfig, "ShowRecipes", ref ShowRecipes);
                ApplyMcmBool(currentConfig, "ShowSources", ref ShowSources);
                ApplyMcmBool(currentConfig, "ShowTradeInformation", ref ShowTradeInformation);
                ApplyMcmBool(currentConfig, "ShowMagnumSurplus", ref ShowMagnumSurplus);
                ApplyMcmBool(currentConfig, "ShowAmmoRelations", ref ShowAmmoRelations);
                if (!InspectorEnabled) CloseInspector();

                // Defensive hard gate: the hint must never survive a disabled setting,
                // even when the MCM was opened while an item remained hovered.
                if (!ShowInspectorHint)
                    HideHoverHint();

                RefreshBrowserInterfaceIconSetting();
                if (_inspectorOpen && !string.IsNullOrEmpty(_inspectorItemId)) RenderBrowser(_inspectorItemId);
                SaveConfig();
                Debug.Log("[ItemIntelligence] MCM saved: ShowInspectorHint=" + ShowInspectorHint +
                    ", InspectorKey=" + InspectorKeyName +
                    ", InterfaceIcons=" + ShowInterfaceIcons +
                    ", ModderMode=" + ModderMode +
                    ", Information={MagnumUses=" + ShowMagnumUses +
                    ", FutureMagnumUses=" + ShowFutureMagnumUses +
                    ", Recipes=" + ShowRecipes +
                    ", Sources=" + ShowSources +
                    ", Trade=" + ShowTradeInformation +
                    ", MagnumSurplus=" + ShowMagnumSurplus +
                    ", AmmoRelations=" + ShowAmmoRelations + "}.");
                return true;
            }
            catch (Exception ex)
            {
                feedbackMessage = Ui("mcm.save_failed");
                Debug.LogError("[ItemIntelligence] MCM save failed: " + ex);
                return false;
            }
        }

        private static void ApplyMcmBool(Dictionary<string, object> config, string key, ref bool field)
        {
            object raw;
            if (!config.TryGetValue(key, out raw) || raw == null) return;

            bool value;
            if (TryReadMcmBool(raw, out value))
                field = value;
        }

        private static bool TryReadMcmString(Dictionary<string, object> config, string key, out string value)
        {
            value = string.Empty;
            object raw;
            if (config == null || !config.TryGetValue(key, out raw) || raw == null) return false;
            return TryReadMcmString(raw, out value);
        }

        private static bool TryReadMcmString(object raw, out string value)
        {
            value = string.Empty;
            if (raw == null) return false;

            string direct = raw as string;
            if (direct != null)
            {
                value = direct;
                return true;
            }

            string[] memberNames = new string[]
            {
                "Value", "CurrentValue", "StoredValue", "TempValue", "BoxedValue"
            };

            for (int i = 0; i < memberNames.Length; i++)
            {
                object nested = GetMember(raw, memberNames[i]);
                if (nested == null || object.ReferenceEquals(nested, raw))
                    continue;
                string nestedText = nested as string;
                if (nestedText != null)
                {
                    value = nestedText;
                    return true;
                }
            }

            try
            {
                value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                value = string.Empty;
                return false;
            }
        }

        private static bool TryReadMcmBool(object raw, out bool value)
        {
            value = false;
            if (raw == null) return false;

            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            string textValue = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (bool.TryParse(textValue, out value))
                return true;

            // Compatibility with MCM builds that may return a ConfigValue-like wrapper
            // instead of the boxed primitive in currentConfig.
            string[] memberNames = new string[]
            {
                "Value", "CurrentValue", "StoredValue", "TempValue", "BoxedValue"
            };

            for (int i = 0; i < memberNames.Length; i++)
            {
                object nested = GetMember(raw, memberNames[i]);
                if (nested == null || object.ReferenceEquals(nested, raw))
                    continue;

                if (nested is bool)
                {
                    value = (bool)nested;
                    return true;
                }

                string nestedText = Convert.ToString(nested, CultureInfo.InvariantCulture);
                if (bool.TryParse(nestedText, out value))
                    return true;
            }

            return false;
        }
    }
}

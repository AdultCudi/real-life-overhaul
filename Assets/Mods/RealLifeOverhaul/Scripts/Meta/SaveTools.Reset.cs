using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Entities;
using Helpers;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The save tools' part for the reset of the mod's data in this save and the repair.</summary>
    public static partial class SaveTools
    {
        private static IEnumerator ResetData()
        {
            _busy = true;
            var report = new List<string>();
            string title = "reallife_meta_resetdata_title".GetLocalization();
            MetaLog.Info("reset data: started.");

            // Nothing on disk is changed by this tool, but the player may save afterwards: so a copy first. Only a game
            // that has no save file at all goes on without one; any other failure stops here.
            string backup = Backup(out string error, out bool neverSaved);
            if (backup == null && !neverSaved)
            {
                _busy = false;
                MetaLog.Warn("reset data: stopped, no backup (" + error + "). Nothing was changed.");
                MetaUi.Message(title, error + "\n\n" + "reallife_meta_remove_nothing_changed".GetLocalization());
                yield break;
            }
            report.Add(backup != null
                ? "reallife_meta_report_backup".GetLocalization().Replace("{folder}", backup)
                : "reallife_meta_report_no_backup".GetLocalization());

            RunCleanups(report);
            yield return null;
            yield return null;
            SettlePlayer(report);

            var removed = new Removed();
            try
            {
                GameInstance save = SaveGameManager.Current;
                RemoveModifiers(save, removed);
                RemoveData(save, removed);
                foreach (Feature feature in Features.All)
                {
                    if (feature.ResetAction == null)
                    {
                        continue;
                    }
                    try
                    {
                        feature.ResetAction();
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                        report.Add("reallife_meta_report_cleanup_failed".GetLocalization().Replace("{feature}", feature.TitleKey.GetLocalization()).Replace("{error}", e.Message));
                    }
                }
                RefreshGame();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                report.Add("reallife_meta_report_error".GetLocalization().Replace("{error}", e.Message));
            }

            report.Add(string.Empty);
            report.Add("reallife_meta_report_removed".GetLocalization());
            report.Add("- " + "reallife_meta_report_modifiers".GetLocalization().Replace("{count}", removed.Modifiers.Count.ToString(CultureInfo.InvariantCulture))
                       + Names(removed.Modifiers, "reallife_happinessmodifier_"));
            report.Add("- " + "reallife_meta_report_data".GetLocalization().Replace("{count}", removed.DataKeys.Count.ToString(CultureInfo.InvariantCulture))
                       + Names(removed.DataKeys, null));
            report.Add(string.Empty);
            report.Add("reallife_meta_resetdata_next".GetLocalization());
            MetaLog.Info("reset data: done. modifiers=" + removed.Modifiers.Count + " modData=" + removed.DataKeys.Count);
            _busy = false;
            MetaUi.Message(title, string.Join("\n", report));
        }

        /// <summary>Puts back what the mod adds to a save (its contacts, its mood types) where it has gone missing, and says what it found.</summary>
        public static void Repair()
        {
            string title = "reallife_meta_repair_title".GetLocalization();
            string why = Refusal(true);
            if (why != null)
            {
                MetaUi.Message(title, why);
                return;
            }
            var report = new List<string>();
            try
            {
                var before = new HashSet<string>();
                foreach (Contact contact in SaveGameManager.Current.Contacts)
                {
                    if (contact != null && contact.id != null)
                    {
                        before.Add(contact.id);
                    }
                }
                bool modifiersBefore = ModState.ModifiersReady && ModifiersRegistered();

                foreach (Feature feature in Features.All)
                {
                    if (feature.RepairAction == null)
                    {
                        continue;
                    }
                    try
                    {
                        feature.RepairAction();
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                        report.Add("reallife_meta_report_cleanup_failed".GetLocalization().Replace("{feature}", feature.TitleKey.GetLocalization()).Replace("{error}", e.Message));
                    }
                }

                bool modifiersAfter = ModState.ModifiersReady && ModifiersRegistered();
                report.Add((modifiersAfter ? (modifiersBefore ? "reallife_meta_repair_modifiers_ok" : "reallife_meta_repair_modifiers_fixed")
                    : "reallife_meta_repair_modifiers_bad").GetLocalization());
                foreach (Feature feature in Features.All)
                {
                    foreach (string id in feature.ContactIds)
                    {
                        bool now = SaveGameManager.Current.Contacts.Exists(c => c != null && c.id == id);
                        string key = now ? (before.Contains(id) ? "reallife_meta_repair_contact_ok" : "reallife_meta_repair_contact_added") : "reallife_meta_repair_contact_off";
                        report.Add("- " + key.GetLocalization().Replace("{contact}", id.GetLocalization()));
                    }
                }
                EnsureHook();
                RefreshGame();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                report.Add("reallife_meta_report_error".GetLocalization().Replace("{error}", e.Message));
            }
            MetaLog.Info("repair: " + string.Join(" | ", report));
            MetaUi.Message(title, string.Join("\n", report));
        }

        // True when every mood type of the mod's is in the game's registry (asked of the game, not of the mod).
        private static bool ModifiersRegistered()
        {
            try
            {
                FieldInfo field = GameContract.HappinessRegistry.Field;
                return field?.GetValue(null) is Dictionary<string, HappinessModifier> registry
                       && registry.ContainsKey(ModState.Sick) && registry.ContainsKey(ModState.MoodDownBig);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

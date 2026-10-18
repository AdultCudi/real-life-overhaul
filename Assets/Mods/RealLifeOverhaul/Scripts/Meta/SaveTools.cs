using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Entities;
using Helpers;
using Localizor;
using PlayerActivity;
using UI;
using UI.Load;
using UnityEngine;
using Vehicles.DeliveryDriverJob;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The tools for what the mod leaves in a save: prepare it for removing the mod, reset the mod's data, repair, and the check
    /// after every load. What a save holds of the mod's and what is taken out is in docs/features/meta.md (save tools).
    /// Nothing runs unless a button is pressed, except the check after a load (one pass over three lists).
    /// </summary>
    public static partial class SaveTools
    {
        public const string VersionKey = "reallife:meta_release";   // the mod version that last wrote this save
        public const string DevelopmentVersionKey = "reallife:meta_version";   // the same from the development builds: removed at load, never compared

        private const float SaveWaitSeconds = 90f;   // real seconds to wait for a save being written

        private static bool _busy;

        /// <summary>True after this save was prepared for removal in this session: the mod's features are stopped until another load.</summary>
        public static bool Dormant { get; private set; }

        /// <summary>A new city load: the features are running again.</summary>
        public static void CityLoaded()
        {
            Dormant = false;
            _busy = false;
        }

        /// <summary>True for an id or key the mod made: "reallife" followed by a separator.</summary>
        private static bool IsOurs(string text)
        {
            if (text == null || text.Length < 9 || !text.StartsWith("reallife", StringComparison.Ordinal))
            {
                return false;
            }
            char next = text[8];
            return next == '_' || next == '-' || next == ':';
        }

        private static bool IsOurContact(Contact contact)
        {
            return contact != null && contact.id != null && (IsOurs(contact.id) || Features.OwnerOfContact(contact.id) != null);
        }

        /// <summary>Why a tool cannot run now (a text for the player), or null.</summary>
        private static string Refusal(bool needsFeatures)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            var uis = InstanceBehavior<UIs>.Instance;
            if (SaveGameManager.Current == null || gm == null || gm.playerController == null || uis == null || LoadScene.isLoading)
            {
                return "reallife_meta_tool_no_save".GetLocalization();
            }
            if (_busy)
            {
                return "reallife_meta_tool_running".GetLocalization();
            }
            if (Dormant)
            {
                return "reallife_meta_tool_dormant".GetLocalization();
            }
            if (needsFeatures && !ModState.Loaded)
            {
                return "reallife_meta_tool_not_loaded".GetLocalization();
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return "reallife_meta_tool_in_vehicle".GetLocalization();
            }
            // The hospital trip and custody are coroutines that cannot be cut short; sleep and activities run the game's clock.
            if (GameMembers.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
                || CasinoBoatManager.IsOnCasinoBoat || SubwaySystem.IsRiding || PoliceAttention.IsBusy)
            {
                return "reallife_meta_tool_busy".GetLocalization();
            }
            return null;
        }

        public static void AskPrepare()
        {
            string title = "reallife_meta_remove_title".GetLocalization();
            string why = Refusal(true);
            if (why == null && string.IsNullOrEmpty(SaveGameManager.Current.SaveGameName))
            {
                why = "reallife_meta_tool_never_saved".GetLocalization();
            }
            if (why == null && !CanSave())
            {
                why = "reallife_meta_tool_cannot_save".GetLocalization();
            }
            if (why != null)
            {
                MetaUi.Message(title, why);
                return;
            }
            MetaUi.Confirm(title, "reallife_meta_remove_explain".GetLocalization().Replace("{save}", SaveGameManager.Current.SaveGameName) + InConvertedShops(),
                "reallife_meta_remove_yes", () => MetaUi.Instance.Run(Prepare()));
        }

        public static void AskReset()
        {
            string title = "reallife_meta_resetdata_title".GetLocalization();
            string why = Refusal(true);
            if (why != null)
            {
                MetaUi.Message(title, why);
                return;
            }
            MetaUi.Confirm(title, "reallife_meta_resetdata_explain".GetLocalization(), "reallife_meta_resetdata_yes", () => MetaUi.Instance.Run(ResetData()));
        }

        // What the last save held of the mod's at the moment it was written, after the purge inside the save event.
        private static int _leftAtSave = -1;
        private static Removed _removedNow;

        // Through the guard and never skipped, so that one that throws cannot stop the game's other save hooks; made once, as the event compares them.
        private static Action _purgeCall, _versionCall;
        private static Action PurgeCall => _purgeCall ?? (_purgeCall = Guard.Way(PurgeOnSave));
        private static Action VersionCall => _versionCall ?? (_versionCall = Guard.Way(WriteVersion));

        // Inside the game's save event, attached last, so that nothing a feature writes there reaches the file.
        private static void PurgeOnSave()
        {
            try
            {
                Purge(_removedNow ?? new Removed());   // what an autosave took out before the tool got to it still counts in the report
                var check = new Removed();
                Purge(check);
                _leftAtSave = check.Total;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        private static IEnumerator Prepare()
        {
            _busy = true;
            var report = new List<string>();
            string title = "reallife_meta_remove_title".GetLocalization();
            GameInstance save = SaveGameManager.Current;
            MetaLog.Info("prepare for removal: started for save \"" + save.SaveGameName + "\".");

            // A save that is being written is waited for; then everything is asked again, because time has passed.
            float until = Time.unscaledTime + SaveWaitSeconds;
            while (SaveGameManager.SavingGameInProgress && Time.unscaledTime < until)
            {
                yield return null;
            }
            string stop = null;
            if (SaveGameManager.Current != save)
            {
                stop = "reallife_meta_tool_no_save".GetLocalization();
            }
            else if (SaveGameManager.SavingGameInProgress)
            {
                stop = "reallife_meta_report_save_slow".GetLocalization();
            }
            else
            {
                _busy = false;
                stop = Refusal(true) ?? (CanSave() ? null : "reallife_meta_tool_cannot_save".GetLocalization());
                _busy = true;
            }

            // (a) the backup, of the file as it is on disk now. No backup, no further step.
            string backup = null;
            if (stop == null)
            {
                backup = Backup(out stop, out bool _);
            }
            if (backup == null)
            {
                _busy = false;
                MetaLog.Warn("prepare for removal: stopped (" + stop + "). Nothing was changed.");
                MetaUi.Message(title, stop + "\n\n" + "reallife_meta_remove_nothing_changed".GetLocalization());
                yield break;
            }
            report.Add("reallife_meta_report_backup".GetLocalization().Replace("{folder}", backup));

            // (b) every feature takes its things off the street and stops.
            string vanId = RentalVanId();
            string converted = InConvertedShops();   // (asked before: the shops made offices are shops again once the features stop)
            RunCleanups(report);
            yield return null;
            yield return null;

            // The features' own hook that takes the moods out of a save goes with them, so the moods come out now and this tool's
            // hook goes on in the same frame (an autosave could run in any later one).
            var removed = new Removed();
            _removedNow = removed;
            try
            {
                RemoveModifiers(save, removed);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            GlobalEvents.onSaveGame -= PurgeCall;
            GlobalEvents.onSaveGame += PurgeCall;

            // Their own OnDestroy puts back what they changed on the game's objects, as when the game unloads the mod.
            Dormant = true;
            int destroyed = 0;
            if (MetaCity.Root != null)
            {
                UnityEngine.Object.Destroy(MetaCity.Root);
                destroyed++;
            }
            foreach (Feature feature in Features.All)
            {
                foreach (string name in feature.ObjectNames)
                {
                    GameObject found = GameObject.Find(name);
                    if (found != null)
                    {
                        UnityEngine.Object.Destroy(found);
                        destroyed++;
                    }
                }
            }
            yield return null;
            yield return null;
            yield return null;
            report.Add("reallife_meta_report_stopped".GetLocalization().Replace("{count}", destroyed.ToString(CultureInfo.InvariantCulture)));
            SettlePlayer(report);   // after the features have handed the player back

            // (c) the save in memory.
            bool saved = false;
            try
            {
                Purge(removed);
                RefreshGame();
                GlobalEvents.onSaveGame -= PurgeCall;   // last in line, after anything attached since
                GlobalEvents.onSaveGame += PurgeCall;

                // (d) the save on disk, under its own name.
                _leftAtSave = -1;
                saved = SaveGameManager.Current == save && SaveGameManager.Save(SaveGameManager.SaveType.Default, save.SaveGameName);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                report.Add("reallife_meta_report_error".GetLocalization().Replace("{error}", e.Message));
            }
            until = Time.unscaledTime + SaveWaitSeconds;
            while (saved && SaveGameManager.SavingGameInProgress && Time.unscaledTime < until)
            {
                yield return null;
            }

            AppendRemoved(report, removed);
            if (converted.Length > 0)
            {
                report.Add(converted.TrimStart('\n'));
            }
            string van = VanLeft(vanId);
            if (van != null)
            {
                report.Add(van);
            }
            if (!saved)
            {
                report.Add("reallife_meta_report_not_saved".GetLocalization().Replace("{folder}", backup));
            }
            else if (SaveGameManager.SavingGameInProgress)
            {
                report.Add("reallife_meta_report_save_slow".GetLocalization());
            }
            else
            {
                report.Add("reallife_meta_report_saved".GetLocalization()
                    .Replace("{save}", SaveGameManager.Current.SaveGameName)
                    .Replace("{left}", Math.Max(0, _leftAtSave).ToString(CultureInfo.InvariantCulture)));
            }
            report.Add(string.Empty);
            report.Add((saved ? "reallife_meta_report_next" : "reallife_meta_report_next_failed").GetLocalization());

            string text = string.Join("\n", report);
            string file = WriteReport("uninstall_report", text);
            if (file != null)
            {
                text += "\n\n" + "reallife_meta_report_file".GetLocalization().Replace("{file}", file);
            }
            MetaLog.Info("prepare for removal: " + (saved ? "done" : "NOT SAVED") + ". modifiers=" + removed.Modifiers.Count + " contacts=" + removed.Contacts.Count
                         + " messages=" + removed.Messages + " modData=" + removed.DataKeys.Count + " notifications=" + removed.Notifications
                         + " transactionsKept=" + removed.Transactions + " leftAtSave=" + _leftAtSave);
            _busy = false;
            MetaUi.Message(title, text);
        }

        // The player's businesses in shops the mod made offices (offices in a shop without the mod): a paragraph naming them, or nothing.
        private static string InConvertedShops()
        {
            try
            {
                List<string> names = Offices.Conversions.PlayersBusinesses();
                if (names.Count == 0)
                {
                    return string.Empty;
                }
                return "\n\n" + "reallife_meta_remove_converted".GetLocalization()
                    .Replace("{count}", names.Count.ToString(CultureInfo.InvariantCulture))
                    .Replace("{list}", string.Join("; ", names));
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return string.Empty;
            }
        }

        private static void AppendRemoved(List<string> report, Removed removed)
        {
            report.Add(string.Empty);
            report.Add("reallife_meta_report_removed".GetLocalization());
            report.Add("- " + "reallife_meta_report_modifiers".GetLocalization().Replace("{count}", removed.Modifiers.Count.ToString(CultureInfo.InvariantCulture))
                       + Names(removed.Modifiers, "reallife_happinessmodifier_"));
            report.Add("- " + "reallife_meta_report_contacts".GetLocalization()
                       .Replace("{count}", removed.Contacts.Count.ToString(CultureInfo.InvariantCulture))
                       .Replace("{messages}", removed.Messages.ToString(CultureInfo.InvariantCulture)) + ContactNames(removed.Contacts));
            report.Add("- " + "reallife_meta_report_data".GetLocalization().Replace("{count}", removed.DataKeys.Count.ToString(CultureInfo.InvariantCulture))
                       + DataNames(removed.DataKeys));
            report.Add("- " + "reallife_meta_report_notifications".GetLocalization().Replace("{count}", removed.Notifications.ToString(CultureInfo.InvariantCulture)));
            if (removed.BusinessTypes > 0)
            {
                report.Add("- " + "reallife_meta_report_business_types".GetLocalization().Replace("{count}", removed.BusinessTypes.ToString(CultureInfo.InvariantCulture)));
            }
            if (removed.Transactions > 0)
            {
                report.Add("- " + "reallife_meta_report_transactions".GetLocalization().Replace("{count}", removed.Transactions.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static string Names(List<string> names, string strip)
        {
            if (names.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder(": ");
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (strip != null && name.StartsWith(strip, StringComparison.Ordinal))
                {
                    name = name.Substring(strip.Length);
                }
                text.Append(i == 0 ? string.Empty : ", ").Append(name);
            }
            return text.ToString();
        }

        // The mod's data entries by the feature they belong to, in the names the guide uses, each with how many: not the raw keys.
        private static string DataNames(List<string> keys)
        {
            if (keys.Count == 0)
            {
                return string.Empty;
            }
            var titles = new List<string>(8);
            var counts = new List<int>(8);
            for (int i = 0; i < keys.Count; i++)
            {
                Feature owner = Features.DeclaredData(keys[i]);
                string title = (owner != null ? owner.TitleKey : "reallife_meta_report_data_other").GetLocalization();
                int at = titles.IndexOf(title);
                if (at < 0)
                {
                    titles.Add(title);
                    counts.Add(1);
                }
                else
                {
                    counts[at]++;
                }
            }
            var text = new StringBuilder(": ");
            for (int i = 0; i < titles.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(titles[i]).Append(" (").Append(counts[i].ToString(CultureInfo.InvariantCulture)).Append(')');
            }
            return text.ToString();
        }

        private static string ContactNames(List<string> ids)
        {
            if (ids.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder(": ");
            for (int i = 0; i < ids.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(ids[i].GetLocalization());
            }
            return text.ToString();
        }

        // The id of the van hired for a job, as Jobs remembers it in the save ("day|id"), or null.
        private static string RentalVanId()
        {
            try
            {
                Dictionary<string, string> data = SaveGameManager.Current.modData;
                if (data != null && data.TryGetValue("reallife:job_rental", out string text) && !string.IsNullOrEmpty(text))
                {
                    int bar = text.IndexOf('|');
                    string id = bar >= 0 ? text.Substring(bar + 1) : null;
                    return string.IsNullOrEmpty(id) ? null : id;
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            return null;
        }

        // Jobs takes its van back when it stops; one still in the save is said, not removed: the game clears its own job vans.
        private static string VanLeft(string vanId)
        {
            try
            {
                if (SaveGameManager.Current.VehicleInstances == null)
                {
                    return null;
                }
                foreach (VehicleInstance vehicle in SaveGameManager.Current.VehicleInstances)
                {
                    // By the id Jobs noted, or for a van hired since, by how Jobs makes one: not to be sold or given to a warehouse.
                    if (vehicle != null && ((vanId != null && vehicle.id == vanId)
                                            || (vehicle is DeliveryVehicleInstance && vehicle.zeroSellingPrice && vehicle.preventWarehouseAssign)))
                    {
                        return "- " + "reallife_meta_report_van".GetLocalization();
                    }
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            return null;
        }

        private static string WriteReport(string name, string text)
        {
            try
            {
                string path = Path.Combine(Presets.Folder, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt");
                File.WriteAllText(path, text.Replace("<b>", string.Empty).Replace("</b>", string.Empty), new UTF8Encoding(false));
                return path;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return null;
            }
        }
    }
}

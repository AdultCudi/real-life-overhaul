using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Entities;
using Helpers;
using Localizor;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.AI;
using Vehicles.DeliveryDriverJob;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The tools that deal with what the mod leaves in a save game: "prepare this save for removing the mod",
    /// "reset the mod's data in this save", "repair", and the check that runs once after every load.
    ///
    /// What the mod puts into a save (GameInstance), all of it found by reading the game's save code:
    ///   modData            a Dictionary of strings; every key of the mod's starts with "reallife:".
    ///   happinessModifiers entries whose type starts with reallife_happinessmodifier_. The game does not know
    ///                      these types without the mod, so ModState takes them out while a save is written.
    ///   Contacts           Contact objects with the mod's ids (reallife-...); a contact's messages are inside it.
    ///   notifications      the history of toasts; the mod's have a key starting with reallife_.
    ///   Transactions       the bank history; the mod's have a type starting with reallife_transaction_. These are
    ///                      money that really moved and are left alone (without the mod they show their internal name).
    ///   VehicleInstances   the van of a job in progress, which is the game's own job van and is taken back by Jobs.
    ///   BuildingRegistrations  a business of one of the tech sector's own types (Tech/Firms/TechTypes), which the game
    ///                      does not know without the mod: made the game's own web development agency again when the
    ///                      save is prepared for removal.
    /// One field of the game's own is put back to "none" when it names a contact that is removed:
    /// PlayerDefaults.contactsLastName (the contact last opened; the Contacts app lists nothing while it names one
    /// that does not exist). Nothing else of a save is touched, and nothing here runs unless the player presses one of the buttons
    /// (the check after a load is one pass over three lists).
    /// </summary>
    public static class SaveTools
    {
        public const string VersionKey = "reallife:meta_release";   // the mod version that last wrote this save
        public const string DevelopmentVersionKey = "reallife:meta_version";   // the same from the builds before the first release (0.13 to 0.46): removed at load, never compared

        private const float SaveWaitSeconds = 90f;

        private static bool _busy;

        /// <summary>True after this save was prepared for removal in this session: the mod's features are stopped until another load.</summary>
        public static bool Dormant { get; private set; }

        /// <summary>A new city load: the features are running again.</summary>
        public static void CityLoaded()
        {
            Dormant = false;
            _busy = false;
        }

        // ---------------------------------------------------------------- what is the mod's

        /// <summary>True for an id or key the mod made: "reallife" followed by a separator.</summary>
        public static bool IsOurs(string text)
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

        // ---------------------------------------------------------------- when a tool may run

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
            // The hospital trip and police custody are coroutines that cannot be cut short cleanly; sleep and
            // activities run the game's clock.
            if (FuneralHelper.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
                || CasinoBoatManager.IsOnCasinoBoat || SubwaySystem.IsRiding || PoliceAttention.IsBusy)
            {
                return "reallife_meta_tool_busy".GetLocalization();
            }
            return null;
        }

        // ---------------------------------------------------------------- the buttons

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
            MetaUi.Confirm(title, "reallife_meta_remove_explain".GetLocalization().Replace("{save}", SaveGameManager.Current.SaveGameName),
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

        // ---------------------------------------------------------------- the backup

        /// <summary>
        /// Copies the current save's files (the save, its .meta and its picture) to two places, and succeeds only if
        /// both copies are complete. One is a new folder beside the save,
        /// SaveGames/[version]/[character]/RealLifeOverhaul_backup_[time]: the game lists the files of a character
        /// folder and never looks into folders inside it, so the copy is not offered as a save. But the game
        /// deletes a character folder whole when the character, or its last save, is deleted in the load menu, so
        /// the second is outside the game's saves, in the mod's own folder under backups/[character]/[time].
        /// Returns both folders (one per line), or null with <paramref name="error"/> set;
        /// <paramref name="neverSaved"/> says the reason is that there is no save file at all. Nothing is ever
        /// deleted or overwritten.
        /// </summary>
        public static string Backup(out string error, out bool neverSaved)
        {
            error = null;
            neverSaved = false;
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || string.IsNullOrEmpty(save.SaveGameName) || string.IsNullOrEmpty(save.characterId))
                {
                    neverSaved = true;
                    error = "reallife_meta_tool_never_saved".GetLocalization();
                    return null;
                }
                SaveGameManager.JoinSaveGameThreads();   // a save still being compressed is finished first
                if (SaveGameManager.SavingGameInProgress)
                {
                    error = "reallife_meta_backup_failed".GetLocalization().Replace("{error}", "the game is still writing the save");
                    return null;
                }
                string folder = SaveGamePathHelper.GetCharacterFolderPath(save.characterId);
                string main = null;
                foreach (string extension in new[] { ".hsg", ".json" })
                {
                    string candidate = Path.Combine(folder, save.SaveGameName + extension);
                    if (File.Exists(candidate) && (main == null || File.GetLastWriteTimeUtc(candidate) > File.GetLastWriteTimeUtc(main)))
                    {
                        main = candidate;
                    }
                }
                if (main == null)
                {
                    neverSaved = true;
                    error = "reallife_meta_tool_never_saved".GetLocalization();
                    return null;
                }
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string character = save.characterId;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    character = character.Replace(c, '_');
                }
                string[] sources = { main, main + ".meta", Path.Combine(folder, save.SaveGameName + ".jpg") };
                string beside = Path.Combine(folder, "RealLifeOverhaul_backup_" + stamp);
                string apart = Path.Combine(Path.Combine(Path.Combine(Presets.Folder, "backups"), character), stamp);
                foreach (string target in new[] { beside, apart })
                {
                    string problem = CopyTo(target, sources);
                    if (problem != null)
                    {
                        error = "reallife_meta_backup_failed".GetLocalization().Replace("{error}", problem);
                        return null;
                    }
                }
                MetaLog.Info("backup: \"" + save.SaveGameName + "\" copied to " + beside + " and " + apart);
                return beside + "\n" + apart;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                error = "reallife_meta_backup_failed".GetLocalization().Replace("{error}", e.Message);
                return null;
            }
        }

        // Copies the files that exist into a folder that must be new. Returns what went wrong, or null.
        private static string CopyTo(string target, string[] sources)
        {
            if (Directory.Exists(target))
            {
                target += "_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            }
            Directory.CreateDirectory(target);
            for (int i = 0; i < sources.Length; i++)
            {
                string source = sources[i];
                if (!File.Exists(source))
                {
                    if (i == 0)
                    {
                        return "the save file has gone";
                    }
                    continue;
                }
                string copy = Path.Combine(target, Path.GetFileName(source));
                File.Copy(source, copy, false);
                long length = new FileInfo(copy).Length;
                if (length != new FileInfo(source).Length || (i == 0 && length <= 0))
                {
                    return "the copy of " + Path.GetFileName(source) + " is not complete";
                }
            }
            return null;
        }

        // The game's own test of whether saving is allowed here (it is private: read by reflection, "yes" if it cannot be read).
        private static bool CanSave()
        {
            try
            {
                MethodInfo method = typeof(SaveGameManager).GetMethod("CanSave", BindingFlags.NonPublic | BindingFlags.Static);
                return method == null || (bool)method.Invoke(null, null);
            }
            catch (Exception)
            {
                return true;
            }
        }

        // ---------------------------------------------------------------- shared steps

        // Every registered feature takes its things off the street, in order; one that fails is reported and the rest go on.
        private static void RunCleanups(List<string> report)
        {
            var ordered = new List<Feature>(Features.All);
            ordered.Sort((a, b) => a.CleanupOrder.CompareTo(b.CleanupOrder));
            foreach (Feature feature in ordered)
            {
                if (feature.CleanupAction == null)
                {
                    continue;
                }
                try
                {
                    feature.CleanupAction();
                    report.Add("reallife_meta_report_cleanup_ok".GetLocalization().Replace("{feature}", feature.TitleKey.GetLocalization()));
                }
                catch (Exception e)
                {
                    MetaLog.Error(e);
                    report.Add("reallife_meta_report_cleanup_failed".GetLocalization().Replace("{feature}", feature.TitleKey.GetLocalization()).Replace("{error}", e.Message));
                }
            }
        }

        // A player on foot out of doors who is not on walkable ground (a scene ended under them) is put on the nearest.
        private static void SettlePlayer(List<string> report)
        {
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (player == null || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || player.Character == null)
                {
                    return;
                }
                NavMeshAgent agent = player.Character.navmeshAgent;
                if (agent == null || (agent.enabled && agent.isOnNavMesh))
                {
                    return;
                }
                if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, 30f, NavMesh.AllAreas))
                {
                    if (!agent.enabled)
                    {
                        agent.enabled = true;
                    }
                    agent.Warp(hit.position);
                    report.Add("reallife_meta_report_player_moved".GetLocalization());
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        private sealed class Removed
        {
            public readonly List<string> Modifiers = new List<string>();
            public readonly List<string> Contacts = new List<string>();
            public readonly List<string> DataKeys = new List<string>();
            public int Messages;
            public int Notifications;
            public int Transactions;      // counted, never removed
            public int BusinessTypes;     // businesses of the tech sector's types made the game's own office business again

            public int Total => Modifiers.Count + Contacts.Count + DataKeys.Count + Notifications;
        }

        // Takes the mod's happiness modifiers out of the live save. They are the one thing a save must never hold without the mod.
        private static void RemoveModifiers(GameInstance save, Removed removed)
        {
            if (save.happinessModifiers != null)
            {
                for (int i = save.happinessModifiers.Count - 1; i >= 0; i--)
                {
                    HappinessModifierData modifier = save.happinessModifiers[i];
                    if (modifier != null && ModState.IsModModifier(modifier.type))
                    {
                        removed.Modifiers.Add(modifier.type);
                        save.happinessModifiers.RemoveAt(i);
                    }
                }
            }
            if (save.usedHappinessModifiers != null)
            {
                save.usedHappinessModifiers.RemoveAll(ModState.IsModModifier);
            }
        }

        private static void RemoveData(GameInstance save, Removed removed)
        {
            if (save.modData == null)
            {
                return;
            }
            var keys = new List<string>();
            foreach (string key in save.modData.Keys)
            {
                if (Features.OwnsData(key))
                {
                    keys.Add(key);
                }
            }
            foreach (string key in keys)
            {
                save.modData.Remove(key);
                removed.DataKeys.Add(key);
            }
        }

        // Everything of the mod's out of the save in memory. Other mods' keys, and everything the game owns, stay.
        private static void Purge(Removed removed)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            RemoveModifiers(save, removed);
            RemoveData(save, removed);
            removed.BusinessTypes += Tech.TechTypes.Strip(save);   // the tech sector's business types, made the game's own web development agency again

            if (save.Contacts != null)
            {
                for (int i = save.Contacts.Count - 1; i >= 0; i--)
                {
                    Contact contact = save.Contacts[i];
                    if (IsOurContact(contact))
                    {
                        removed.Contacts.Add(contact.id);
                        removed.Messages += contact.messagesQueue != null ? contact.messagesQueue.Count : 0;
                        save.Contacts.RemoveAt(i);
                    }
                }
                // The game remembers the contact last opened by its id, and its Contacts app shows an empty list
                // when that contact no longer exists: the one field of the game's own that is put back to "none".
                if (save.PlayerDefaults != null && save.PlayerDefaults.contactsLastName != null
                    && removed.Contacts.Contains(save.PlayerDefaults.contactsLastName))
                {
                    save.PlayerDefaults.contactsLastName = null;
                }
            }

            if (save.notifications != null && save.notifications.Count > 0)
            {
                int before = save.notifications.Count;
                var kept = new List<Notification>(before);
                foreach (Notification notification in save.notifications)
                {
                    if (notification == null || !IsOurs(notification.key))
                    {
                        kept.Add(notification);
                    }
                }
                if (kept.Count != before)
                {
                    save.notifications.Clear();
                    foreach (Notification notification in kept)
                    {
                        save.notifications.Enqueue(notification);
                    }
                    removed.Notifications += before - kept.Count;
                }
            }

            removed.Transactions = 0;
            if (save.Transactions != null)
            {
                foreach (Transaction transaction in save.Transactions)
                {
                    if (transaction != null && IsOurs(transaction.transactionType))
                    {
                        removed.Transactions++;
                    }
                }
            }
        }

        private static void RefreshGame()
        {
            try
            {
                HappinessHelper.UpdateHappiness();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            try
            {
                ContactsApp.onContactAdded?.Invoke();   // the phone's contact list looks again
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        // ---------------------------------------------------------------- prepare this save for removing the mod

        // What the last save held of the mod's at the moment it was written, after the purge inside the save event.
        private static int _leftAtSave = -1;
        private static Removed _removedNow;

        // Runs inside the game's save event, after every feature's own hook (it is attached last), so that nothing
        // a feature writes there reaches the file. Stays attached for the rest of the session.
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
            RunCleanups(report);
            yield return null;
            yield return null;

            // From here on no save may hold a mood of the mod's: the features' own hook, which takes them out while a
            // save is written, goes with the features. So the moods come out now, and this tool's hook is attached,
            // in the same frame in which the features are destroyed (the game's autosave could run in any later one).
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
            GlobalEvents.onSaveGame -= PurgeOnSave;
            GlobalEvents.onSaveGame += PurgeOnSave;

            // The features themselves go: their own OnDestroy puts back whatever they changed on the game's objects,
            // exactly as when the game unloads the mod.
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
                GlobalEvents.onSaveGame -= PurgeOnSave;   // last in line, after anything attached since
                GlobalEvents.onSaveGame += PurgeOnSave;

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

        // Jobs takes its hired van back when it stops. If the van is still in the save (it was not where Jobs could take
        // it), it is said, not removed: it is one of the game's own job vans, which the game clears away by itself.
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
                    // By the id Jobs noted at the last save, or (a van hired since) by how Jobs makes one: a job van
                    // that cannot be sold or given to a warehouse.
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

        // ---------------------------------------------------------------- reset the mod's data in this save

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

        // ---------------------------------------------------------------- repair

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
                report.Add((modifiersAfter ? (modifiersBefore ? "reallife_meta_repair_modifiers_ok" : "reallife_meta_repair_modifiers_fixed") : "reallife_meta_repair_modifiers_bad").GetLocalization());
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
                FieldInfo field = typeof(HappinessHelper).GetField("Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
                return field?.GetValue(null) is Dictionary<string, HappinessModifier> registry
                       && registry.ContainsKey(ModState.Sick) && registry.ContainsKey(ModState.MoodDownBig);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- after every load

        /// <summary>Later versions add a step here when they change what an earlier one wrote: (first version that no longer needs it, what it does).</summary>
        private static readonly KeyValuePair<string, Action<Dictionary<string, string>>>[] Migrations =
        {
        };

        private static void WriteVersion()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || Dormant)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                save.modData[VersionKey] = Changelog.Version;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        /// <summary>Attaches the hook that notes the mod's version in every save. The game clears its events on each load.</summary>
        public static void EnsureHook()
        {
            if (Dormant)
            {
                return;
            }
            GlobalEvents.onSaveGame -= WriteVersion;
            GlobalEvents.onSaveGame += WriteVersion;
        }

        /// <summary>
        /// Once per load, when the save's mod data has been read: looks for what another version of the mod left
        /// behind and puts it right, with a line in the log for each thing. It removes only what would break the
        /// game or the mod (mood types this version does not have); data it does not recognise is reported and kept.
        /// </summary>
        public static void CheckAfterLoad()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                Dictionary<string, string> data = save.modData;
                // A development build's number (up to 0.46) means nothing beside the released ones: such a save counts
                // as written before the first release, so every later migration runs on it and nothing is compared.
                data.Remove(DevelopmentVersionKey);
                data.TryGetValue(VersionKey, out string written);
                string now = Changelog.Version;
                int fixedCount = 0;

                if (written != null && Changelog.Compare(written, now) > 0)
                {
                    MetaLog.Info("self-check: this save was last written by mod version " + written + ", which is newer than this one (" + now + "). Data this version does not know is kept untouched.");
                }
                foreach (KeyValuePair<string, Action<Dictionary<string, string>>> migration in Migrations)
                {
                    if (written == null || Changelog.Compare(written, migration.Key) < 0)
                    {
                        migration.Value(data);
                        fixedCount++;
                        MetaLog.Info("self-check: migrated data from before " + migration.Key + ".");
                    }
                }

                // Mood types this version does not register: in the list the save was loaded with they would be unknown
                // to the game (the README: an unknown type breaks the load without the mod, and shows as nothing with it).
                if (save.happinessModifiers != null)
                {
                    for (int i = save.happinessModifiers.Count - 1; i >= 0; i--)
                    {
                        HappinessModifierData modifier = save.happinessModifiers[i];
                        if (modifier != null && ModState.IsModModifier(modifier.type) && !ModState.KnowsModifier(modifier.type))
                        {
                            save.happinessModifiers.RemoveAt(i);
                            fixedCount++;
                            MetaLog.Warn("self-check: removed the mood \"" + modifier.type + "\", which this version of the mod does not have.");
                        }
                    }
                }
                if (data.TryGetValue(ModState.ModifierStashKey, out string stash) && !string.IsNullOrEmpty(stash))
                {
                    var kept = new List<string>();
                    foreach (string entry in stash.Split(';'))
                    {
                        int equals = entry.IndexOf('=');
                        string type = equals > 0 ? entry.Substring(0, equals) : entry;
                        if (entry.Length == 0 || ModState.KnowsModifier(type))
                        {
                            kept.Add(entry);
                        }
                        else
                        {
                            fixedCount++;
                            MetaLog.Warn("self-check: dropped the stored mood \"" + type + "\", which this version of the mod does not have.");
                        }
                    }
                    data[ModState.ModifierStashKey] = string.Join(";", kept);
                }

                // Reported, never removed: a feature that has not registered its keys must not lose them here.
                int unknownKeys = 0;
                foreach (string key in data.Keys)
                {
                    if (key.StartsWith(ModState.DataPrefix, StringComparison.Ordinal) && Features.DeclaredData(key) == null)
                    {
                        unknownKeys++;
                        MetaLog.Info("self-check: the save has \"" + key + "\", which no registered feature claims (kept; \"Prepare this save for removing the mod\" removes it).");
                    }
                }
                int strayContacts = 0;
                if (save.Contacts != null)
                {
                    foreach (Contact contact in save.Contacts)
                    {
                        if (contact != null && IsOurs(contact.id) && Features.OwnerOfContact(contact.id) == null)
                        {
                            strayContacts++;
                            MetaLog.Info("self-check: the phone has the contact \"" + contact.id + "\", which no registered feature adds (kept).");
                        }
                    }
                }
                if (fixedCount > 0)
                {
                    RefreshGame();
                }
                MetaLog.Info("self-check: save last written by mod " + (written ?? "(no released version yet)") + ", running " + now + ": "
                             + fixedCount + " thing(s) put right, " + unknownKeys + " unclaimed key(s), " + strayContacts + " unclaimed contact(s).");
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}

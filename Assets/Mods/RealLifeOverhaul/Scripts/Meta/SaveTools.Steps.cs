using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Entities;
using Helpers;
using Localizor;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine.AI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The save tools' shared steps: the backup, every feature's clean-up, and the mod's things taken out of the save in memory.</summary>
    public static partial class SaveTools
    {
        /// <summary>
        /// Copies the save, its .meta and picture into two new folders, beside the save (the game never lists one there) and under
        /// the mod's backups (the game deletes a character folder whole); both must be whole. Returns both folders, one per line,
        /// or null with <paramref name="error"/> set (<paramref name="neverSaved"/>: no save file at all).
        /// </summary>
        private static string Backup(out string error, out bool neverSaved)
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
                MethodInfo method = GameContract.CanSave.Method;
                return method == null || (bool)method.Invoke(null, null);
            }
            catch (Exception)
            {
                return true;
            }
        }

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
            public int TechTypes;         // businesses of the tech sector's types made web development agencies again
            public int Academies;         // academies made recruitment agencies again
            public int Branches;          // bank branches made gift shops again
            public int Builders;          // construction companies made design studios again
            public int ShopKinds;         // ice cream parlours, tea bars and tobacconists made fast food restaurants, coffee shops and liquor stores

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
            SaveLines.Forget();   // the lines kept as they were read go with the data
            if (save.modData == null)
            {
                return;
            }
            var keys = new List<string>();
            foreach (string key in save.modData.Keys)
            {
                if (Features.OwnsData(key) && !DistrictSave.DistrictSaveDirector.Keeps(key))   // the district's records: the player's own, back with the mod
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
            removed.TechTypes += Tech.TechTypes.Strip(save);   // the tech sector's business types, made the game's own web development agency again
            removed.Academies += Careers.AcademyType.Strip(save);   // the player's academies, made the game's own recruitment agency again
            removed.Branches += Economy.BranchType.Strip(save);   // the player's bank branches, made the game's own gift shops again
            removed.Builders += Construction.ConstructionType.Strip(save);   // the player's construction companies, made the game's own design studios again
            removed.ShopKinds += Shops.ShopsGame.Strip(save);   // ice cream parlours, tea bars and tobacconists, made the game's fast food restaurants, coffee shops and liquor stores

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
                // The Contacts app lists nothing while the contact it last opened is missing: the one field of the game's that is reset.
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
    }
}

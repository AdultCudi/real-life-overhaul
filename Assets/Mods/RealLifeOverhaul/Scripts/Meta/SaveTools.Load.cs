using System;
using System.Collections.Generic;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The save tools' part for every load and save: the version noted in the save and the check after a load.</summary>
    public static partial class SaveTools
    {
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
            GlobalEvents.onSaveGame -= VersionCall;
            GlobalEvents.onSaveGame += VersionCall;
        }

        /// <summary>
        /// Once per load, after the save's mod data is read: removes what another version left that would break the game
        /// (unknown mood types), and reports, but keeps, data it does not know.
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
                // A development build's number means nothing beside the released ones: such a save counts as written before the first
                // release, so every migration runs on it and nothing is compared.
                data.Remove(DevelopmentVersionKey);
                data.TryGetValue(VersionKey, out string written);
                string now = Changelog.Version;
                int fixedCount = 0;

                if (written != null && Changelog.Compare(written, now) > 0)
                {
                    MetaLog.Info("self-check: this save was last written by mod version " + written + ", which is newer than this one (" + now + ")."
                                 + " Data this version does not know is kept untouched.");
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
                // to the game (docs/features/meta.md: an unknown type breaks the load without the mod, and shows as nothing with it).
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

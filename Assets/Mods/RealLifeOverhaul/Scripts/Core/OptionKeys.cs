using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's options kept under one id of its own (<see cref="OptionKeysRules.FixedId"/>), the same for the Workshop copy
    /// and a ModsLocal copy, and the one start on this computer that brings over what older versions kept under each copy's
    /// own id (the rules are in <see cref="OptionKeysRules"/>). COST: nothing a frame; the move reads a few hundred keys at
    /// that one start, before the first option is read, and every later start reads one key.
    /// </summary>
    public static class OptionKeys
    {
        public const string TestKey = "reallife_opt_test_kept";   // the test's button and the title of what it says

        private static readonly OptionMove Move = new OptionMove();
        private static readonly PrefsStore Store = new PrefsStore();

        /// <summary>Called by Settings.Start before the first read: every option a part of the mod reads at start is brought over.</summary>
        public static void Bring(string running)
        {
            try
            {
                if (!OptionKeysRules.Due(Store))
                {
                    return;   // brought over at an earlier start: what the player changed since stands
                }
                Move.Begin(OptionKeysRules.Sources(running, LocalFolder()));
                var ids = new List<string>(1024);
                Known(ids);
                OptionKeysRules.Move(Store, ids, Move);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);   // the options then read their defaults, as a new player's do
            }
        }

        /// <summary>
        /// Just before the page is registered: any row of it the start's own list missed is brought over too, and the move is done
        /// for good. True if a row took a value, so the options are read again.
        /// </summary>
        public static bool Finish(ModOptions page, IModLogger log)
        {
            if (!Move.Ran || Move.Finished)
            {
                return false;
            }
            int late = 0;
            try
            {
                if (page != null)
                {
                    var ids = new List<string>(page.Options.Count);
                    foreach (ModOption option in page.Options)
                    {
                        if (option is IPersistableOption persistable)
                        {
                            ids.Add(persistable.Id);
                        }
                    }
                    late = OptionKeysRules.Move(Store, ids, Move);
                }
                OptionKeysRules.Finish(Store, Move);
                UnityEngine.PlayerPrefs.Save();   // on disk now: a crash before the game quits does not bring them over twice
                log?.Info("Options are kept under " + OptionKeysRules.FixedId + ": " + Move.Total + " taken from an older version's place at this start"
                          + (Move.Total > 0 ? " (" + OptionKeysRules.From(Move) + ")" : string.Empty)
                          + (late > 0 ? ", " + late + " of them only found on the options page" : string.Empty) + ".");
            }
            catch (Exception e)
            {
                log?.Error(e);
            }
            return late > 0;
        }

        /// <summary>Test: where the options are kept, and what this start took from an older place. Works in the main menu too.</summary>
        public static void Test()
        {
            try
            {
                string text;
                if (!Move.Ran)
                {
                    text = "reallife_opt_test_kept_before".GetLocalization().Replace("{id}", OptionKeysRules.FixedId);
                }
                else if (Move.Total == 0)
                {
                    text = "reallife_opt_test_kept_none".GetLocalization().Replace("{id}", OptionKeysRules.FixedId);
                }
                else
                {
                    text = "reallife_opt_test_kept_taken".GetLocalization().Replace("{id}", OptionKeysRules.FixedId)
                        .Replace("{count}", Move.Total.ToString()).Replace("{from}", OptionKeysRules.From(Move));
                }
                Meta.OptionsNotice.Tell(TestKey, text);
            }
            catch (Exception e)
            {
                Meta.MetaLog.Error(e);
            }
        }

        // The ModsLocal copy's id as the game makes it (ModDiscoveryRegistry: Path.GetFullPath of the mod's folder).
        private static string LocalFolder()
        {
            try
            {
                return Path.GetFullPath(Path.Combine(UnityEngine.Application.persistentDataPath, "ModsLocal", OptionKeysRules.LocalFolder));
            }
            catch (Exception)
            {
                return null;   // a data path the system cannot read as one: the Workshop's id is still looked under
            }
        }

        // Every id a part of the mod reads at start: the ids each holder of options declares, found without a list to keep, so an
        // id added later is moved too (one that is not saved anywhere is simply not found).
        private static void Known(List<string> into)
        {
            Consts(typeof(Settings), into);
            Consts(typeof(JusticeSettings), into);
            Consts(typeof(Roommates.RoommateSettings), into);
            Consts(typeof(Meta.Welcome), into);
            Consts(typeof(PerfTweaks), into);
            PerfTweaks.PrefIds(into);
            Keys.PrefIds(into);
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                into.Add(slot.OptionId);
                into.Add(slot.ColourOptionId);
            }
        }

        private static void Consts(Type holder, List<string> into)
        {
            foreach (FieldInfo field in holder.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    into.Add((string)field.GetRawConstantValue());
                }
            }
        }

        // PlayerPrefs, where the game keeps mod options. A value is a whole number (every option, as the game's own controls save
        // them) or a text (the welcome's version); there is no call that says which, so a number is told by reading it twice.
        private sealed class PrefsStore : IOptionStore
        {
            public bool Has(string key) => UnityEngine.PlayerPrefs.HasKey(key);

            public void Copy(string from, string to)
            {
                int low = UnityEngine.PlayerPrefs.GetInt(from, int.MinValue), high = UnityEngine.PlayerPrefs.GetInt(from, int.MaxValue);
                if (low == high)
                {
                    UnityEngine.PlayerPrefs.SetInt(to, low);
                    return;
                }
                UnityEngine.PlayerPrefs.SetString(to, UnityEngine.PlayerPrefs.GetString(from, string.Empty));
            }

            public void Mark(string key) => UnityEngine.PlayerPrefs.SetInt(key, 1);
        }
    }
}

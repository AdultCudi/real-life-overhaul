using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The only part of RLO Texts that touches the game: where its folders are, the game's language, the player's name,
    /// the day and time as a card writes them, how fast game time runs, whether the game is quiet enough for background
    /// work, the game's own notices, and for Your own AI the game's text box with what is typed hidden, and the browser.
    /// Each read is a property or two; nothing here runs per frame.
    /// </summary>
    public static class TextsGame
    {
        public const string AppId = "1331550";   // the game's Steam app (its Workshop folder)
        public const float SlowFrameMs = 40f;    // a frame slower than this on average between two looks: not quiet

        /// <summary>The fixed folder for packs, the speed file, the server's log and its cache: "RLO Texts" in the game's own data folder.</summary>
        public static string Home => Path.Combine(Application.persistentDataPath, TextPacks.Fixed);

        /// <summary>
        /// Every place a pack may be (TextPacks.Find): the fixed folder, the folders the packs' own small mod announced,
        /// ModsLocal, and on the Workshop only RLO's own pack items, by their ids (TextPacks.WorkshopItems), switched on
        /// in the mod list or not. No other Workshop item is ever looked in.
        /// </summary>
        public static List<string> Places()
        {
            var places = new List<string>(6) { Home };
            object announced = AppDomain.CurrentDomain.GetData(TextPacks.Announced);
            places.AddRange(TextPacks.Split(announced as string));
            places.Add(Path.Combine(Application.persistentDataPath, "ModsLocal"));
            places.AddRange(TextPacks.WorkshopPacks(TextPacks.WorkshopOf(Application.dataPath, AppId), TextPacks.WorkshopItems));
            return places;
        }

        /// <summary>True while the game shows English: the models write English only, so any other language keeps the written lines.</summary>
        public static bool English
        {
            get
            {
                string locale = Localizor.LocalizorManager.LoadedLocale;
                return string.IsNullOrEmpty(locale) || locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>The player's name, as a card's player line says it.</summary>
        public static string PlayerName
        {
            get
            {
                try
                {
                    string name = Helpers.PlayerHelper.CharacterData != null ? Helpers.PlayerHelper.CharacterData.name : null;
                    return string.IsNullOrEmpty(name) ? "you" : FirstWord(name);
                }
                catch (Exception)
                {
                    return "you";
                }
            }
        }

        /// <summary>The game's day of the week and time as a card's when line: "Monday 14:05".</summary>
        public static string When()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return null;
            }
            int weekday = (int)TimeHelper.GetDayOfWeek(Mathf.Max(1, save.Day)) - (int)DayOfWeekOrdered.Monday;
            return TextWords.When(weekday, save.Hour, Mathf.FloorToInt(save.Minute) % 60);
        }

        /// <summary>The weekday (0 Monday ... 6 Sunday) and hour of the card's when line (<see cref="When"/>); false without a save.</summary>
        public static bool Clock(out int weekday, out int hour)
        {
            GameInstance save = SaveGameManager.Current;
            weekday = hour = 0;
            if (save == null)
            {
                return false;
            }
            weekday = (int)TimeHelper.GetDayOfWeek(Mathf.Max(1, save.Day)) - (int)DayOfWeekOrdered.Monday;
            hour = save.Hour;
            return true;
        }

        /// <summary>The game's day; 0 without a save.</summary>
        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;

        /// <summary>Real seconds until something due so many game minutes from now arrives, at the game's speed (at most an hour).</summary>
        public static double SecondsFor(double gameMinutes)
        {
            float perSecond = Mathf.Max(1f, PlayerPrefSettings.GameSpeed) * Mathf.Max(1f, Time.timeScale);
            return Math.Min(3600.0, Math.Max(0.0, gameMinutes) / perSecond);
        }

        /// <summary>
        /// Quiet enough for the background: no loading screen, no fast forward or time skip, no police pursuit, and frames
        /// not slow on average since the last look (frames counted between two looks, never per frame).
        /// </summary>
        public static bool Quiet(float frameMs)
        {
            if (LoadScene.isLoading || Time.timeScale > 1.001f || frameMs > SlowFrameMs)
            {
                return false;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis != null && uis.gameSpeed != null && uis.gameSpeed.isFastForwarding)
            {
                return false;
            }
            return !(PolicePursuit.Instance != null && PolicePursuit.Instance.Busy);
        }

        /// <summary>
        /// The game's own text box, empty, for a key: what is typed shows as dots (the box's field set to a password's
        /// while it is open, put back as it was when it closes either way). False, and nothing shown, without the box
        /// (the main menu). Without the contract's field (GameContract.KeyHidden off) the box still works, unhidden.
        /// </summary>
        public static bool AskSecret(string placeholderKey, string buttonKey, Action<string> done, int most)
        {
            if (!TextBox.CanType)
            {
                return false;
            }
            TMP_InputField field = KeyField();
            TMP_InputField.ContentType was = field != null ? field.contentType : TMP_InputField.ContentType.Standard;
            if (field != null)
            {
                field.contentType = TMP_InputField.ContentType.Password;
                field.ForceLabelUpdate();
            }
            _secretDone = done;
            _secretWas = was;
            _secretTyped = _secretTyped ?? Guard.Wrap<string>(SecretTyped);
            _secretClosed = _secretClosed ?? Guard.Wrap(SecretClosed);
            return TextBox.Ask(placeholderKey, buttonKey, _secretTyped, _secretClosed, string.Empty, false, most);
        }

        private static Action<string> _secretDone, _secretTyped;
        private static Action _secretClosed;
        private static TMP_InputField.ContentType _secretWas;

        private static void SecretTyped(string text)
        {
            SecretClosed();
            Action<string> done = _secretDone;
            _secretDone = null;
            done?.Invoke(text);
        }

        // The box's field as it was, and empty: a key never stays in it.
        private static void SecretClosed()
        {
            TMP_InputField field = KeyField();
            if (field != null)
            {
                field.text = string.Empty;
                field.contentType = _secretWas;
                field.ForceLabelUpdate();
            }
        }

        private static TMP_InputField KeyField()
        {
            if (GameContract.KeyHidden.Off)
            {
                return null;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            FieldInfo f = GameContract.TextInputField.Field;
            return uis != null && uis.textInputUi != null && f != null ? f.GetValue(uis.textInputUi) as TMP_InputField : null;
        }

        /// <summary>An address in the player's browser (the ChatGPT sign-in, its usage page): Unity's own call.</summary>
        public static void OpenUrl(string url)
        {
            Application.OpenURL(url);
        }

        /// <summary>One of the game's own notices.</summary>
        public static void Notice(string key, Dictionary<string, string> data, float seconds = 12f)
        {
            Notifications.Show(NotificationType.Info, key, data, seconds);
        }

        private static string FirstWord(string name)
        {
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }
    }
}

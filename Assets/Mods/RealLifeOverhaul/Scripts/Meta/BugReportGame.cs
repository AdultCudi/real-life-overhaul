using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Localizor;
using RealLifeOverhaul.GameUi;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Where "Report a bug" touches the game: the listener that keeps the mod's warnings and errors (<see cref="Listen"/>, once
    /// per line logged, a few comparisons, no allocation), the facts, the text box, the screenshot, the logs, the zip, the
    /// clipboard and the links. Nothing per frame.
    /// </summary>
    internal static class BugReportGame
    {
        /// <summary>The mod's Discord by its lasting invite: a link to the channel itself shows a player not yet in the server only "no access".</summary>
        public const string DiscordUrl = "https://discord.gg/tzCh2VXs9E";

        private const int JpgQuality = 85;   // a 1440p screen in some 400 KB: Discord takes 10 MB in a post, the logs need the room

        /// <summary>The mod's warnings and errors this session, the last <see cref="BugLog.Most"/>.</summary>
        public static readonly BugLog Log = new BugLog();

        private static bool _listening;

        /// <summary>Once, at game start (Core/RealLifeOverhaulMod): the mod's warnings and errors are kept from then on.</summary>
        public static void Listen()
        {
            if (_listening)
            {
                return;
            }
            _listening = true;
            Application.logMessageReceivedThreaded += Heard;
        }

        // From any thread, for every log line. Nothing here may log (it would come back here); a fault drops the line.
        private static void Heard(string text, string stack, LogType type)
        {
            try
            {
                if (BugReportRules.Keep((int)type, text, stack, out BugLineKind kind))
                {
                    Log.Add(kind, DateTime.UtcNow.Ticks, text, stack);
                }
            }
            catch (Exception)
            {
                // (see above: a line is lost, the game's log is not touched)
            }
        }

        /// <summary>What report.txt says, read on the main thread as the player confirms. A part that cannot be read says so.</summary>
        public static BugFacts Facts(string words)
        {
            var f = new BugFacts
            {
                Words = words,
                Written = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ModVersion = Changelog.Version,
                GameBuild = GameBuild.Running,
                MadeFor = GameBuild.MadeFor,
                Profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
            f.Platform = Read(Platform);
            f.Language = Read(() => LocalizorManager.LoadedLocale);
            f.GoingPublic = Read(() => GoingPublic.Describe().Replace("Going Public: ", string.Empty).TrimEnd('.'));
            f.Preset = Read(() => Preset(f.Changed));
            try
            {
                f.InCity = MetaCity.Root != null && City(f);
                f.PhoneOpen = f.InCity && PhoneOpen();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                f.InCity = false;
            }
            f.LinesTotal = Log.Copy(f.Lines);
            f.Contract = Read(Guard.Report);
            return f;
        }

        private static string Read(Func<string> read)
        {
            try
            {
                return read() ?? "unknown";
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return "could not be read (" + e.GetType().Name + ")";
            }
        }

        private static string Platform()
        {
            return Application.platform + ", " + SystemInfo.operatingSystem + ", " + SystemInfo.processorType + ", "
                   + SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture) + " MB memory, " + SystemInfo.graphicsDeviceName + " ("
                   + SystemInfo.graphicsMemorySize.ToString(CultureInfo.InvariantCulture) + " MB, " + SystemInfo.graphicsDeviceType + "), screen "
                   + Screen.width.ToString(CultureInfo.InvariantCulture) + " x " + Screen.height.ToString(CultureInfo.InvariantCulture);
        }

        // The preset the options match or are nearest, and every option that differs from it (from its default where no preset covers it).
        private static string Preset(List<string> changed)
        {
            Intensity? exact = Presets.Detect();
            Intensity near = exact ?? Intensity.Standard;
            if (!exact.HasValue)
            {
                int fewest = int.MaxValue;
                for (int i = 0; i <= (int)Intensity.Hardcore; i++)
                {
                    int count = Presets.CountChanges((Intensity)i, out _);
                    if (count < fewest)
                    {
                        fewest = count;
                        near = (Intensity)i;
                    }
                }
            }
            foreach (OptionInfo info in SettingsTable.All)
            {
                int now = SettingsTable.Current(info);
                if (Presets.TryValue(info, near, out int value))
                {
                    if (now != value)
                    {
                        changed.Add(info.Id + " = " + now.ToString(CultureInfo.InvariantCulture) + " (preset " + value.ToString(CultureInfo.InvariantCulture) + ")");
                    }
                }
                else if (info.HasDefault && now != info.Default)
                {
                    changed.Add(info.Id + " = " + now.ToString(CultureInfo.InvariantCulture) + " (default " + info.Default.ToString(CultureInfo.InvariantCulture) + ")");
                }
            }
            return exact.HasValue ? exact.Value.ToString() : "Custom, nearest " + near;
        }

        // The save's day, time and money: named only here, so that a game without them leaves only these lines out.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool City(BugFacts f)
        {
            if (SaveGameManager.Current == null)
            {
                return false;
            }
            f.Day = TimeHelper.CurrentDay;
            f.Hour = TimeHelper.CurrentHour;
            f.Minute = Mathf.Clamp((int)TimeHelper.CurrentMinute, 0, 59);
            f.Money = SaveGameManager.Current.Money;
            f.StaffNote = StaffPart(f.Staff);
            f.BooksNote = BooksPart(f.Books, f.Day);
            return true;
        }

        // A part of report.txt: null when read, else why not. Each reader runs in its own try, so a game without a member loses only that part.
        private static string StaffPart(List<BugStaff> into)
        {
            try
            {
                Staff(into);
                return null;
            }
            catch (Exception e)
            {
                return Unread(e);
            }
        }

        private static string BooksPart(List<BugMoney> into, int today)
        {
            try
            {
                Books(into, today);
                return null;
            }
            catch (Exception e)
            {
                return Unread(e);
            }
        }

        private static string Unread(Exception e)
        {
            MetaLog.Error(e);
            return "could not be read (" + e.GetType().Name + ")";
        }

        // The player's employees (no candidates, no other player's) by business, skill and wage: what their pay is made of.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Staff(List<BugStaff> into)
        {
            List<Entities.EmployeeInstance> all = SaveGameManager.Current.EmployeeInstances;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                Entities.EmployeeInstance e = all[i];
                if (e == null || e.IsCandidate || e.characterData == null)
                {
                    continue;
                }
                BuildingRegistration reg = e.assignedAddress != null ? Helpers.BuildingHelper.GetBuildingRegistration(e.assignedAddress) : null;
                if (reg != null && GoingPublic.Foreign(reg))
                {
                    continue;
                }
                var s = new BugStaff
                {
                    Business = reg != null ? reg.BusinessName ?? string.Empty : string.Empty,
                    Kind = reg != null ? reg.businessTypeName : null,
                    WageCents = Mathf.RoundToInt(e.hourlyWage * 100f),
                    HoursToday = e.workedHoursToday,
                    HoursWeek = e.workedHoursThisWeek,
                    RotaWeek = e.assignedWeeklyHours
                };
                if (e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null)
                {
                    s.Skill = e.characterData.skills[0].name;
                    s.Level = Mathf.RoundToInt(e.characterData.skills[0].value);
                    s.FormulaCents = Mathf.RoundToInt(Helpers.EmployeeHelper.CalculateHourlyWageForSkill(e.characterData.skills[0]) * 100f);
                }
                into.Add(s);
            }
            into.Sort((a, b) =>
            {
                int by = string.CompareOrdinal(a.Business, b.Business);
                by = by != 0 ? by : string.CompareOrdinal(a.Skill, b.Skill);
                return by != 0 ? by : b.WageCents.CompareTo(a.WageCents);
            });
        }

        // Yesterday's and today's transactions (GameBooks.Day), each with the business it was booked at, else the player's own.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Books(List<BugMoney> into, int today)
        {
            for (int day = today - 1; day <= today; day++)
            {
                List<Transaction> ofDay = Economy.GameBooks.Day(day);
                for (int i = 0; ofDay != null && i < ofDay.Count; i++)
                {
                    Transaction t = ofDay[i];
                    if (t == null)
                    {
                        continue;
                    }
                    BuildingRegistration reg = t.address != null ? Helpers.BuildingHelper.GetBuildingRegistration(t.address) : null;
                    string where = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : null;
                    if (where == null && t.transactionData != null)
                    {
                        t.transactionData.TryGetValue("businessName", out where);
                    }
                    into.Add(new BugMoney
                    {
                        Day = day,
                        Kind = t.transactionType,
                        Where = where ?? string.Empty,
                        Cents = (long)Math.Round(t.amount * 100d),
                        BalanceCents = (long)Math.Round(t.balance * 100d)
                    });
                }
            }
        }

        // The phone's full menu (an app, the Mod settings one too) is open: the screenshot shows it, and report.txt says so. Left open.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool PhoneOpen()
        {
            return UI.Smartphone.FullMenu.IsOpen;
        }

        /// <summary>True while a city is loaded: then the report has a screenshot and the game's text box is there.</summary>
        public static bool InCity => MetaCity.Root != null;

        /// <summary>The game's own text box (UIs.textInputUi) for what went wrong, which may be left empty; false when the game has none (the main menu).</summary>
        public static bool AskWords(Action<string> done)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return false;
            }
            uis.textInputUi.Show("reallife_bug_placeholder", "reallife_bug_make", text => UiClone.Safe(() => done(text)), null, string.Empty, true, BugReportRules.MostWords);
            return true;
        }

        /// <summary>True while the game's text box can be shown: a city is loaded and its interface is there.</summary>
        public static bool CanAsk
        {
            get
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.textInputUi != null;
            }
        }

        /// <summary>The screen as it is drawn, read at the end of a frame.</summary>
        public sealed class Shot
        {
            public NativeArray<byte> Pixels;   // a copy of the texture's pixels, the encoder's alone
            public GraphicsFormat Format;
            public int Width;
            public int Height;
            public long ReadMs;                // the main thread's share: the engine's read of the screen and one copy
        }

        /// <summary>The screen as the game's feedback form takes it, after WaitForEndOfFrame: pixels copied, texture let go, encoding left to the worker. Null on failure.</summary>
        public static Shot TakeShot()
        {
            var watch = Stopwatch.StartNew();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                {
                    return null;
                }
                var shot = new Shot
                {
                    Pixels = new NativeArray<byte>(texture.GetRawTextureData<byte>(), Allocator.Persistent),
                    Format = texture.graphicsFormat,
                    Width = texture.width,
                    Height = texture.height
                };
                shot.ReadMs = watch.ElapsedMilliseconds;
                return shot;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return null;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        /// <summary>On the worker: the screen as a JPG (the engine's encoder, which may run on any thread), and its pixels let go. False if it could not be written.</summary>
        public static bool WriteShot(Shot shot, string path)
        {
            NativeArray<byte> jpg = default;
            try
            {
                jpg = ImageConversion.EncodeNativeArrayToJPG(shot.Pixels, shot.Format, (uint)shot.Width, (uint)shot.Height, 0u, JpgQuality);
                File.WriteAllBytes(path, jpg.ToArray());
                return true;
            }
            finally
            {
                if (jpg.IsCreated)
                {
                    jpg.Dispose();
                }
                Drop(shot);
            }
        }

        /// <summary>The screen's pixels let go, whatever became of them.</summary>
        public static void Drop(Shot shot)
        {
            if (shot != null && shot.Pixels.IsCreated)
            {
                shot.Pixels.Dispose();
            }
        }

        /// <summary>The game's log of this session, when it is in the game's own data folder (a log written elsewhere is not read).</summary>
        public static string GameLog()
        {
            string log = Application.consoleLogPath;
            if (string.IsNullOrEmpty(log))
            {
                return null;
            }
            string folder = Path.GetDirectoryName(Path.GetFullPath(log));
            return string.Equals(folder, Path.GetFullPath(Application.persistentDataPath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ? log : null;
        }

        /// <summary>On the worker: a copy of one of the game's logs, its tail when large and the user folder taken out, read with sharing so it is never locked. False with no such file.</summary>
        public static bool CopyLog(string from, string to, long cap, string profile)
        {
            if (from == null || !File.Exists(from))
            {
                return false;
            }
            using (var read = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024))
            using (var write = new StreamWriter(to, false, new System.Text.UTF8Encoding(false), 64 * 1024))
            {
                BugReportRules.CopyTail(read, write, cap, profile);
            }
            return true;
        }

        /// <summary>On the worker: the folder zipped beside it, in its own method so a runtime without the zip classes loses only the zip; a half zip is removed, the fault thrown on.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Zip(string folder)
        {
            string zip = folder + ".zip";
            try
            {
                using (var stream = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.GetFiles(folder))
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(file), System.IO.Compression.CompressionLevel.Optimal);
                        using (Stream into = entry.Open())
                        using (var from = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            from.CopyTo(into);
                        }
                    }
                }
                return zip;
            }
            catch (Exception)
            {
                try
                {
                    File.Delete(zip);
                }
                catch (Exception)
                {
                    // (left as it is: the folder is the report)
                }
                throw;
            }
        }

        /// <summary>
        /// The summary on the clipboard, the explorer on the new zip, the Discord in the browser. On Windows the explorer is started
        /// itself: an escaped file address is read in the system's code page, which mangles accented or Chinese user folders.
        /// </summary>
        public static void HandOver(string summary, string folder, string zip)
        {
            GUIUtility.systemCopyBuffer = summary;
            if (!Explore(zip ?? folder))
            {
                Application.OpenURL(BugReportRules.FileUrl(Path.GetDirectoryName(folder)));
            }
            Application.OpenURL(DiscordUrl);
        }

        private static bool Explore(string picked)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            {
                return false;
            }
            try
            {
                using (Process.Start("explorer.exe", BugReportRules.ExplorerSelect(picked)))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                return false;
            }
        }
    }
}

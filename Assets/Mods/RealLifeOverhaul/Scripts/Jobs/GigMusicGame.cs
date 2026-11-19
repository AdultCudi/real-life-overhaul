using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player's gig music folder on the disc: where it is (RealLifeOverhaul/GigMusic in the game's data folder), the one
    /// place that reads or writes it, and the options page's button that makes it and opens it. The one Game file of the feature
    /// for the disc; the sound itself is <see cref="GigMusic"/>. Cost: nothing until a gig starts or the button is pressed.
    /// </summary>
    public static class GigMusicGame
    {
        /// <summary>persistentDataPath/RealLifeOverhaul/GigMusic; main thread only, as the engine answers it nowhere else.</summary>
        public static string Root => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", GigMusicRules.Root);

        public static readonly IGigMusicFiles Disc = new DiscFiles();

        /// <summary>The options page's button: makes the folders and the note about them if they are not there, then opens the folder.</summary>
        public static void OpenFolder()
        {
            try
            {
                string root = Root;
                string note = JobText.T("reallife_gig_music_readme",
                    "files", GigMusicRules.MostFiles.ToString(),
                    "megabytes", GigMusicRules.MostMegabytes.ToString(),
                    "types", GigMusicRules.TypesText(),
                    "folder", GigMusicRules.Name(GigSound.Busking),
                    "effectfiles", GigMusicRules.MostEffectFiles.ToString(),
                    "effectkilobytes", GigMusicRules.MostEffectKilobytes.ToString(),
                    "effectseconds", GigMusicRules.MostEffectSeconds.ToString("0"));
                if (!GigMusicRules.Prepare(Disc, root, note.Replace("\r\n", "\n").Replace("\n", "\r\n")))
                {
                    JobText.Toast(UI.Notification.NotificationType.Warning, "reallife_gig_music_no_folder", "folder", root);
                    return;
                }
                if (!Explore(root))
                {
                    Application.OpenURL(Meta.BugReportRules.FileUrl(root));
                }
            }
            catch (Exception e)
            {
                Meta.MetaLog.Error(e);
            }
        }

        // The explorer started itself on Windows: an escaped file address is read in the system's code page, which mangles accented or Chinese user folders.
        private static bool Explore(string folder)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            {
                return false;
            }
            try
            {
                using (Process.Start("explorer.exe", "\"" + folder + "\""))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Meta.MetaLog.Error(e);
                return false;
            }
        }

        private sealed class DiscFiles : IGigMusicFiles
        {
            public bool FolderExists(string folder)
            {
                try
                {
                    return Directory.Exists(folder);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public string[] FilesIn(string folder)
            {
                try
                {
                    return Directory.GetFiles(folder);
                }
                catch (Exception)
                {
                    return new string[0];
                }
            }

            public long SizeOf(string path)
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch (Exception)
                {
                    return -1L;
                }
            }

            public bool FileExists(string path)
            {
                try
                {
                    return File.Exists(path);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public bool CreateFolder(string folder)
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public bool WriteText(string path, string text)
            {
                try
                {
                    File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RealLifeOverhaul.Photos;

// The Camera and the Gallery: a photo taken this session reaches the Gallery whatever the root's
// separators (the game's Mono gives '\' where the engine's persistentDataPath has '/'), a save's id beside its photos written
// whole and mended, a folder whose save cannot be told never deleted, one locked folder never ending the look over the rest,
// a folder for each id and never one for two (an earlier build's moved to it), a photo read by its path on disk from any
// folder name, the wallpaper's last choice the one written, the world's clicks given back as the game had them (the mop's
// floor), the camera's own hold on the walk told from the game's, Flare's own picture where the Camera cannot take one, and
// the camera's ways out (the shutter's frame, a fault while opening, the HUD with no interface, a shot cut off) in the sources.
internal static partial class Program
{
    private static void PhotoFixes(string scratch)
    {
        PhotoSlashes(System.IO.Path.Combine(scratch, "slashes"));
        PhotoSaveMark(System.IO.Path.Combine(scratch, "mark"));
        PhotoSweepKept(System.IO.Path.Combine(scratch, "kept"));
        PhotoFolderNames(System.IO.Path.Combine(scratch, "names"));
        PhotoOddFolders(System.IO.Path.Combine(scratch, "odd"));
        PhotoWallpaperLast(System.IO.Path.Combine(scratch, "wallpaper"));
        PhotoWorldClicks();
        PhotoHolds();
        PhotoFlareFallback();
        PhotoWaysOut();
        PhotoWholeLoads();
        PhotoFullWords();
    }

    // ---------------------------------------------------------------- the whole photo's loads

    private static void PhotoWholeLoads()
    {
        string gallery = Read(@"Photos\GalleryApp.Viewer.cs");
        string came = Between(gallery, "private void Came(", "private void PlaceWhole()");
        string drop = Between(gallery, "private void DropWhole()", "\n        }");
        Check(Regex.IsMatch(gallery, @"load = \+\+_wholeLoad;\s*_director\.StartCoroutine\(Guard\.Routine\(PhotosGame\.Load\([^;]*Came\(t, name, opening, load\)")
              && Regex.IsMatch(came, @"if \(load != _wholeLoad \|\|[^)]*\)\s*\{\s*PhotosGame\.Destroy\(texture\);\s*return;")
              && Regex.IsMatch(came, @"if \(_wholeTexture != null && _wholeTexture != texture\)\s*\{\s*PhotosGame\.Destroy\(_wholeTexture\);") && drop.Contains("_wholeLoad++;"),
            "gallery whole loads: each load of the whole photo has its own number; an earlier load's texture (the same photo viewed again before it came) is destroyed, and the one shown before is let go");
    }

    // ---------------------------------------------------------------- the full Gallery's question

    private static void PhotoFullWords()
    {
        bool named = true;
        var wrong = new List<string>();
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(PhotoLocales, language + ".json"));
            if (words == null || !words.TryGetValue("reallife_gallery_full_no", out string keep))
            {
                named = false;
                wrong.Add(language);
                continue;
            }
            foreach (string key in new[] { "reallife_gallery_full_body", "reallife_gallery_test_full_armed" })
            {
                if (!words.TryGetValue(key, out string text) || !text.Contains(keep))
                {
                    named = false;
                    wrong.Add(language + " " + key);
                }
            }
        }
        Check(named, "gallery full words: the question and the test's notice name the button that keeps every photo as each language's button says it" + (wrong.Count > 0 ? " (" + string.Join(", ", wrong) + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- 1. a photo taken this session reaches the Gallery

    // What the game's Mono gives back for a path's folder: every '/' made '\' (its Path.CleanPath), then all before the last '\'.
    private static string MonoDirectoryName(string path)
    {
        string clean = path.Replace('/', '\\');
        int last = clean.LastIndexOf('\\');
        return last < 0 ? string.Empty : clean.Substring(0, last);
    }

    private static void PhotoSlashes(string scratch)
    {
        // The root as the engine gives persistentDataPath (forward slashes), joined as PhotosGame.Root joins it.
        string engine = scratch.Replace('\\', '/');
        string root = System.IO.Path.Combine(engine, "RealLifeOverhaul", PhotoStoreRules.RootFolder);
        const string id = "Psnh0rSw8kyjQbL4AHyJ+Q==";
        string folder = PhotoStore.Folder(root, id);                       // what PhotosDirector follows (_folder)
        string path = PhotoStore.NewPath(folder, id, new DateTime(2026, 10, 15, 9, 0, 0), false, 3, 9);
        string monoFolder = MonoDirectoryName(path);                       // what the game's Path.GetDirectoryName gives back
        Check(folder.Contains('/') && !string.Equals(monoFolder, folder, StringComparison.OrdinalIgnoreCase),
            "photos slashes: under the game's Mono the photo's folder taken back from its path is not the root's string (the reason a string comparison failed)");
        Check(PhotoStoreRules.SameFolder(monoFolder, folder) && PhotoStoreRules.SameFolder(folder, folder) && PhotoStoreRules.SameFolder(folder + "/", MonoDirectoryName(path).ToUpperInvariant()),
            "photos slashes: the photo's folder and the followed folder are one, whatever the separators, a trailing one or the letters' case");
        Check(!PhotoStoreRules.SameFolder(folder, PhotoStore.Folder(root, "another")) && !PhotoStoreRules.SameFolder(folder, null) && !PhotoStoreRules.SameFolder(folder, folder + "x"),
            "photos slashes: another save's folder, none, or a longer name is not the same folder");
        string full = System.IO.Path.GetFullPath(root);
        Check(!full.Contains('/') && PhotoStoreRules.SameFolder(full, root), "photos slashes: the root made full (as the director makes it) has one kind of separator and is the same folder");

        // The director's own path: Added is given the folder the shot took, and compares that, never the path taken apart.
        string director = Read(@"Photos\PhotosDirector.cs"), mode = Read(@"Photos\CameraMode.cs");
        string added = Between(director, "internal Texture Added(", "public IEnumerator Delete(");
        Check(added.Contains("PhotoStoreRules.SameFolder(folder, _folder)") && !added.Contains("GetDirectoryName") && mode.Contains("director.Added(folder, path, made)"),
            "photos slashes: a photo is the followed save's by the folder the shot took, compared whatever the separators");
        Check(director.Contains("_root = Path.GetFullPath(PhotosGame.Root)") && !Regex.IsMatch(PhotoCode(), @"GetDirectoryName\("),
            "photos slashes: the root is made full once, and no Photos code takes a folder back from a path");
    }

    private static string PhotoCode()
    {
        return string.Join("\n", Directory.GetFiles(System.IO.Path.Combine(PhotoScripts, "Photos"), "*.cs").Select(File.ReadAllText));
    }

    // ---------------------------------------------------------------- 2. the save's id beside its photos

    private static void PhotoSaveMark(string folder)
    {
        const string id = "live+id==";
        Directory.CreateDirectory(folder);
        string mark = System.IO.Path.Combine(folder, PhotoStoreRules.SaveFile);
        File.WriteAllText(mark, string.Empty);   // a first write cut short (a full disk, a crash)
        PhotoStore.NewPath(folder, id, new DateTime(2026, 10, 15, 9, 0, 0), true, 1, 9);
        Check(File.ReadAllText(mark) == id, "photos save mark: an empty save.txt is written again with the next photo");
        File.WriteAllText(mark, "someone");
        PhotoStore.NewPath(folder, id, new DateTime(2026, 10, 15, 9, 0, 1), true, 1, 9);
        Check(File.ReadAllText(mark) == id && !File.Exists(mark + ".part"), "photos save mark: a save.txt naming another id is written again, whole");
        DateTime before = File.GetLastWriteTimeUtc(mark);
        Check(PhotoStore.MarkSave(folder, id) && File.GetLastWriteTimeUtc(mark) == before && !PhotoStore.MarkSave(folder, null),
            "photos save mark: a save.txt that already says the id is left as it is; no id writes nothing");
        string store = Read(@"Photos\PhotoStore.cs");
        Check(Regex.IsMatch(Between(store, "public static bool MarkSave(", "public static bool WriteWhole("), @"WriteWhole\(mark, bytes, bytes\.Length\)") && !store.Contains("File.WriteAllText(mark"),
            "photos save mark: save.txt is written through WriteWhole, never left half written");
    }

    private static void PhotoSweepKept(string root)
    {
        string photos = System.IO.Path.Combine(root, "Photos"), saves = System.IO.Path.Combine(root, "SaveGames");
        Directory.CreateDirectory(saves);
        var today = new DateTime(2026, 10, 15);
        // A live save whose save.txt came out empty, marked long ago by a build that read it as gone.
        string empty = PhotoStore.Folder(photos, "LiveSave1==");
        Touch(System.IO.Path.Combine(empty, "Photo_20260101_090000_D1_09.jpg"));
        File.WriteAllText(System.IO.Path.Combine(empty, PhotoStoreRules.SaveFile), "  ");
        File.WriteAllText(System.IO.Path.Combine(empty, PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-400)));
        // A folder whose save.txt names an id whose folder is another.
        string stray = System.IO.Path.Combine(photos, "Stray");
        Touch(System.IO.Path.Combine(stray, "Photo_20260101_090000_D1_09.jpg"));
        File.WriteAllText(System.IO.Path.Combine(stray, PhotoStoreRules.SaveFile), "SomethingElse");
        File.WriteAllText(System.IO.Path.Combine(stray, PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-400)));
        bool kept = true;
        foreach (int days in new[] { 0, 30, 31, 365, 5000 })
        {
            var deleted = new List<string>();
            var marked = new List<string>();
            PhotoStore.Sweep(photos, null, id => PhotoStore.SavePresent(saves, id), today.AddDays(days), deleted, marked);
            kept &= deleted.Count == 0 && marked.Count == 0 && File.Exists(System.IO.Path.Combine(empty, "Photo_20260101_090000_D1_09.jpg")) && File.Exists(System.IO.Path.Combine(stray, "Photo_20260101_090000_D1_09.jpg"));
        }
        Check(kept, "photos sweep: a folder with an empty save.txt, or one naming an id whose folder is another, is never marked or deleted, however many days pass");

        // One locked folder never ends the look over the rest.
        string first = PhotoStore.Folder(photos, "AAAA"), last = PhotoStore.Folder(photos, "zzzz");
        foreach (string f in new[] { first, last })
        {
            Touch(System.IO.Path.Combine(f, "Photo_20260101_090000_D1_09.jpg"));
            File.WriteAllText(System.IO.Path.Combine(f, PhotoStoreRules.SaveFile), System.IO.Path.GetFileName(f));
            File.WriteAllText(System.IO.Path.Combine(f, PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-40)));
        }
        var gone = new List<string>();
        int looked;
        using (new FileStream(System.IO.Path.Combine(first, "Photo_20260101_090000_D1_09.jpg"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            looked = PhotoStore.Sweep(photos, null, id => false, today, gone, new List<string>());
        }
        Check(Directory.GetDirectories(photos).OrderBy(x => x, StringComparer.Ordinal).First() == first && gone.SequenceEqual(new[] { "zzzz" }) && !Directory.Exists(last) && Directory.Exists(first),
            "photos sweep: a folder that cannot be deleted (a photo open elsewhere) is left for next time, and the folders after it are still looked over (" + looked + " looked, " + string.Join(",", gone) + " deleted)");
    }

    // ---------------------------------------------------------------- 10. a folder for each id

    private static void PhotoFolderNames(string root)
    {
        string[] ids = { "A B", "A_B", "A.B", "A/B", "A\\B", "A:B", "AB", "Psnh0rSw8kyjQbL4AHyJ+Q==", "unsaved", "UNSAVED", "/", "\\", "Ünal Çelik", "张伟", "张三" };
        string[] names = ids.Select(PhotoStoreRules.FolderName).ToArray();
        Check(names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Length && !names.Contains(PhotoStoreRules.FolderName(null), StringComparer.OrdinalIgnoreCase),
            "photos folders: every id has a folder of its own, none the folder of a save with no id, letters' case aside (" + string.Join(" | ", names) + ")");
        Check(PhotoStoreRules.FolderName("Psnh0rSw8kyjQbL4AHyJ+Q==") == "Psnh0rSw8kyjQbL4AHyJ+Q==" && PhotoStoreRules.FolderName("AB") == "AB" && PhotoStoreRules.EarlierFolderName("AB") == null,
            "photos folders: an id of plain characters keeps its folder's name as it was (the photos already there are found where they are)");
        Check(names.All(n => n.Length <= PhotoStoreRules.NameMost && n.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0) && PhotoStoreRules.FolderName(new string('a', 81)) != PhotoStoreRules.FolderName(new string('a', 82)),
            "photos folders: every name is a folder's name, at most " + PhotoStoreRules.NameMost + " long, and two long ids alike in their first characters are two folders");
        Check(PhotoStoreRules.FolderName("A B") == PhotoStoreRules.FolderName("A B") && PhotoStoreRules.FolderName("A B").StartsWith("A_B_", StringComparison.Ordinal) && PhotoStoreRules.EarlierFolderName("A B") == "A_B",
            "photos folders: a changed id's name is the same every time, its first characters kept; the earlier build's name for it is known");

        // An earlier build's folder of a changed id is moved to its name now, only when its save.txt is that very id.
        string photos = System.IO.Path.Combine(root, "Photos");
        string earlier = System.IO.Path.Combine(photos, "A_B");
        Touch(System.IO.Path.Combine(earlier, "Photo_20260101_090000_D1_09.jpg"));
        File.WriteAllText(System.IO.Path.Combine(earlier, PhotoStoreRules.SaveFile), "A.B");
        Check(!PhotoStore.Adopt(photos, "A B") && Directory.Exists(earlier), "photos folders: an earlier folder whose save.txt names another id is not taken");
        File.WriteAllText(System.IO.Path.Combine(earlier, PhotoStoreRules.SaveFile), "A B");
        var listed = new List<PhotoEntry>();
        Check(PhotoStore.Adopt(photos, "A B") && !Directory.Exists(earlier) && PhotoStore.List(PhotoStore.Folder(photos, "A B"), listed) && listed.Count == 1,
            "photos folders: the earlier folder of the save is moved to its name now, its photos found there");
        Check(!PhotoStore.Adopt(photos, "A B") && !PhotoStore.Adopt(photos, "AB") && !PhotoStore.Adopt(photos, null), "photos folders: moved once; an id whose name did not change has nothing to move");
        string director = Read(@"Photos\PhotosDirector.cs");
        Check(Regex.IsMatch(Between(director, "private IEnumerator List(", "private void StandDown()"), @"Task\.Run\(\(\) =>\s*\{\s*adopted = PhotoStore\.Adopt\(root, id\);"),
            "photos folders: the earlier folder is moved on the worker that lists the save's photos, before it lists them");
    }

    // ---------------------------------------------------------------- 4. a photo read by its path, from any folder

    private static void PhotoOddFolders(string root)
    {
        string[] odd = { "a+b==", "a#b", "100%", "with space", "Ünal Çelik", "照片 张伟+%#" };
        bool all = true;
        var failed = new List<string>();
        foreach (string name in odd)
        {
            string folder = System.IO.Path.Combine(root, name, "RealLifeOverhaul", "Photos", PhotoStoreRules.FolderName("x+y=="));
            string path = PhotoStore.NewPath(folder, "x+y==", new DateTime(2026, 10, 15, 9, 0, 0), false, 1, 9);
            byte[] jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 0xFF, 0xD9 };
            bool ok = PhotoStore.WriteWhole(path, jpg, jpg.Length);
            byte[] back = PhotoStore.ReadPhoto(path);
            var listed = new List<PhotoEntry>();
            ok &= back != null && back.SequenceEqual(jpg) && PhotoStore.List(folder, listed) && listed.Count == 1
                  && PhotoStore.SetWallpaper(folder, System.IO.Path.GetFileName(path)) && PhotoStore.Wallpaper(folder) == System.IO.Path.GetFileName(path) && PhotoStore.Delete(folder, System.IO.Path.GetFileName(path));
            all &= ok;
            if (!ok)
            {
                failed.Add(name);
            }
        }
        Check(all, "photos odd folders: under a folder with '+', '#', '%', a space, an accent or Chinese, a photo is written, read back whole, listed, made the wallpaper and deleted" + (failed.Count > 0 ? " (failed: " + string.Join(", ", failed) + ")" : string.Empty));
        Check(PhotoStore.ReadPhoto(System.IO.Path.Combine(root, "none.jpg")) == null && PhotoStore.ReadPhoto(null) == null, "photos odd folders: a photo that is not there reads as none");
        string empty = System.IO.Path.Combine(root, "empty.jpg");
        File.WriteAllBytes(empty, Array.Empty<byte>());
        Check(PhotoStore.ReadPhoto(empty) == null, "photos odd folders: an empty file is no photo");
        string game = Read(@"Photos\PhotosGame.cs"), all2 = PhotoCode();
        string load = Between(game, "public static IEnumerator Load(", "public static void Tell(");
        Check(!Regex.IsMatch(all2, @"UnityWebRequest|new Uri\(|file://|AbsoluteUri") && Regex.IsMatch(load, @"Task\.Run\(\(\) => PhotoStore\.ReadPhoto\(path\)\)"),
            "photos odd folders: a whole photo is read on a worker by its path on disk, never through a file URL");
        Check(Regex.Matches(all2, @"\.LoadImage\(").Count == 1 && load.Contains("texture.LoadImage(bytes, true)") && load.Contains("HotSpots.Time(HotLoaded)"),
            "photos odd folders: the one decode on the main thread is the engine's, timed under its own name, its copy in memory let go");
    }

    // ---------------------------------------------------------------- 10. the wallpaper's last choice

    private static void PhotoWallpaperLast(string folder)
    {
        Directory.CreateDirectory(folder);
        var writes = new LastWins();
        int first = writes.Next(), second = writes.Next();
        Check(writes.Run(second, () => PhotoStore.SetWallpaper(folder, "B.jpg")) && !writes.Run(first, () => PhotoStore.SetWallpaper(folder, "A.jpg"))
              && File.ReadAllText(System.IO.Path.Combine(folder, PhotoStoreRules.WallpaperFile)) == "B.jpg", "photos wallpaper: an earlier change whose worker comes last writes nothing; the later choice stands");
        bool won = true;
        for (int round = 0; round < 20; round++)
        {
            var lines = new LastWins();
            var tasks = new List<Task>();
            int[] tickets = Enumerable.Range(0, 8).Select(_ => lines.Next()).ToArray();
            string written = null;
            foreach (int t in tickets.OrderBy(x => (x * 7919 + round * 104729) % 13))
            {
                int ticket = t;
                tasks.Add(Task.Run(() => lines.Run(ticket, () => { written = "W" + ticket; return true; })));
            }
            Task.WaitAll(tasks.ToArray());
            won &= written == "W" + tickets[tickets.Length - 1];
        }
        Check(won, "photos wallpaper: of many quick changes handed to workers in any order, the last asked for is the one written");
        string director = Read(@"Photos\PhotosDirector.cs");
        Check(Regex.IsMatch(Between(director, "public void SetWallpaper(string name)", "private IEnumerator LoadWallpaper("), @"int ticket = writes\.Next\(\);\s*Task\.Run\(\(\) => writes\.Run\(ticket, \(\) => PhotoStore\.SetWallpaper\(folder, name\)\)\)"),
            "photos wallpaper: the director hands each change a ticket on the main thread and writes it only while it is the latest");
    }

    // ---------------------------------------------------------------- 3. the world's clicks

    private static void PhotoWorldClicks()
    {
        bool exact = true;
        foreach (int mask in new[] { 0, 1, 0x00FFFFFF, 0x01000001, unchecked((int)0x80000000), -1, unchecked((int)0xDEADBEEF), 1 << 30 })
        {
            CameraHold.Split(mask, out float low, out float high);
            var hold = new CameraHold();
            hold.Take(HoldPart.World, low, high, 2f, 1f);
            var into = new float[CameraHold.Values];
            hold.Give(HoldPart.World, into);
            exact &= CameraHold.Join(into[0], into[1]) == mask && (int)into[2] == 2;
        }
        Check(exact, "photos world: the game's mask of the world's clicks (32 bits, the highest too) and the hover cursor are held and given back exactly");
        Check(CameraHold.WorldBack(0, 0) && !CameraHold.WorldBack(1 << 9, 0), "photos world: the mask is put back while it is still the camera's own, and a mask the game set meanwhile stands");
        string game = Read(@"Photos\PhotosGame.cs"), mode = Read(@"Photos\CameraMode.cs"), contract = Read(@"Core\GameContract.cs");
        string give = Between(game, "public static void GiveWorld(", "public static bool TakeConfirm()");
        Check(give.Contains("WorldMaskField.SetValue(null, layers)") && give.Contains("MouseController.cursorOnHover = (CursorType)cursor") && give.Contains("CameraHold.WorldBack(now, cameras)")
              && give.IndexOf("MouseController.Reset()", StringComparison.Ordinal) > give.IndexOf("return;", StringComparison.Ordinal)
              && give.Contains("PlayerHelper.IsHoldingAMop") && give.Contains("SetRestrictedObjectTypes(InteractiveObjectType.Ground)"),
            "photos world: the mask and the hover cursor are put back as held; only without the game's mask are its defaults put back, with the mop's floor again while a mop is held");
        Check(mode.Contains("_hold.Take(HoldPart.World, low, high, cursor, read ? 1f : 0f)") && mode.Contains("PhotosGame.GiveWorld(v[3] > 0.5f, CameraHold.Join(v[0], v[1]), (int)v[2], _worldMask)")
              && Regex.Matches(PhotoCode(), @"MouseController\.Reset\(\)").Count == 1,
            "photos world: the camera holds the mask before it changes it and gives it back on every way out; the game's Reset is called in the fallback alone");
        Check(contract.Contains("StaticField(PhotoHolds, Game, \"MouseController\", \"RaycastLayerMark\", \"LayerMask\")") && Regex.IsMatch(game, @"GameContract\.WorldMask\.Field"),
            "photos world: the game's mask is read through the contract (ContractCheck checks it against the game)");
    }

    // ---------------------------------------------------------------- 7. the game holding the player

    private static void PhotoHolds()
    {
        var ok = new CameraSituation { FeatureOn = true, Loaded = true, PlayerThere = true };
        CameraSituation s = ok;
        s.WalkHeld = true;
        s.OwnHold = true;
        Check(CameraRules.MustClose(s) == CameraRefusal.None, "camera holds: open, the walk held by the camera's own blocker alone does not shut it");
        s.OwnHold = false;
        Check(CameraRules.MustClose(s) == CameraRefusal.Busy && CameraRules.Refuse(s) == CameraRefusal.Busy, "camera holds: the walk held by another of the game's blockers (a scene, the map, a shift) shuts it, and refuses it");
        s = ok;
        s.Busy = true;
        s.WalkHeld = true;
        s.OwnHold = true;
        Check(CameraRules.MustClose(s) == CameraRefusal.Busy, "camera holds: a minigame or the police's attention shuts it, even while the camera holds the walk");
        s = ok;
        s.Busy = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.Busy && CameraRules.MustClose(ok) == CameraRefusal.None, "camera holds: busy, it does not open; nothing holding the player, it stays open");
        string game = Read(@"Photos\PhotosGame.cs");
        string situation = Between(game, "public static CameraSituation Situation(", "public static bool MenuOpen");
        Check(situation.Contains("s.Busy = GameManager.IsAnyMiniGameActive() || PoliceAttention.IsBusy;") && situation.Contains("s.WalkHeld = player.NavigationDisabled;")
              && situation.Contains("blockers.Count == 1 && blockers.Contains(Blocker)") && situation.Contains("GameContract.NavBlockers.Field"),
            "camera holds: the game's side reads the minigames and the police apart from the walk, and the camera's own blocker from the game's list");
    }

    // ---------------------------------------------------------------- 9. Flare's own picture

    private static void PhotoFlareFallback()
    {
        Check(CameraRules.FlareTakesOver(CameraRefusal.Vehicle, false) && CameraRules.FlareTakesOver(CameraRefusal.Transit, false) && CameraRules.FlareTakesOver(CameraRefusal.Busy, false)
              && CameraRules.FlareTakesOver(CameraRefusal.Custody, false) && CameraRules.FlareTakesOver(CameraRefusal.Skipping, false),
            "flare fallback: Flare's camera button takes its own shot in a vehicle, on the bus, while held, in custody or while time is skipped, as before the Camera");
        Check(CameraRules.FlareTakesOver(CameraRefusal.Busy, true) && CameraRules.FlareTakesOver(CameraRefusal.Custody, true) && !CameraRules.FlareTakesOver(CameraRefusal.Vehicle, true)
              && !CameraRules.FlareTakesOver(CameraRefusal.Transit, true), "flare fallback: its selfie button goes to photo mode's selfie where that works (on foot), the Camera's word in a vehicle");
        bool none = true;
        foreach (CameraRefusal r in new[] { CameraRefusal.None, CameraRefusal.Off, CameraRefusal.NotLoaded, CameraRefusal.Gone, CameraRefusal.PhotoMode, CameraRefusal.Window })
        {
            none &= !CameraRules.FlareTakesOver(r, false) && !CameraRules.FlareTakesOver(r, true);
        }
        Check(none, "flare fallback: nothing taken over with nothing to shoot, photo mode on or a window open (the Camera's word is said)");
        string director = Read(@"Photos\PhotosDirector.cs"), live = (Read(@"Social\Flare\FlareDirector.Live.cs") + Read(@"Social\Flare\FlareDirector.Cases.cs") + Read(@"Social\Flare\FlareDirector.Posting.cs")), links = Read(@"Wiring\PhotosLinks.cs");
        string open = Between(director, "public bool OpenCamera(bool selfie, bool fromFlare)", "private void Refused(");
        Check(Regex.IsMatch(open, @"if \(fromFlare && CameraRules\.FlareTakesOver\(refusal, selfie\)\)\s*\{[^}]*return false;") && open.IndexOf("FlareTakesOver", StringComparison.Ordinal) < open.IndexOf("Refused(refusal)", StringComparison.Ordinal),
            "flare fallback: the Camera answers false, with no word of its own, where Flare's picture takes over");
        Check(Regex.IsMatch(live, @"if \(FlareBridges\.OpenCamera == null \|\| !FlareBridges\.OpenCamera\(\)\)\s*\{\s*TakeShot\(\);") && links.Contains("!PhotosDirector.Instance.OpenCamera(true, true)") && links.Contains("before?.Invoke();"),
            "flare fallback: a false sends Flare's camera button to its own shot and its selfie button to photo mode's selfie");
    }

    // ---------------------------------------------------------------- 5, 6, 8, 10. the camera's ways out

    private static void PhotoWaysOut()
    {
        string mode = Read(@"Photos\CameraMode.cs"), game = Read(@"Photos\PhotosGame.cs");
        int shotAt = mode.IndexOf("private IEnumerator Shot(", StringComparison.Ordinal);
        string shot = shotAt < 0 ? string.Empty : mode.Substring(shotAt);
        string fin = shot.Substring(Math.Max(0, shot.LastIndexOf("finally", StringComparison.Ordinal)));
        Check(Regex.IsMatch(fin, @"if \(_view != null\)\s*\{\s*_view\.Busy\(false\);[^\n]*\n\s*_view\.ForShot\(false, _grid\);") && !fin.Contains("&& Active"),
            "camera ways out: the controls come back after a shot whether or not the camera is still open (shut in the shutter's frame, the next opening has them)");
        string open = Between(mode, "public CameraRefusal Open(bool selfie, bool fromFlare)", "private void Hold()");
        int active = open.IndexOf("Active = true;", StringComparison.Ordinal);
        string after = active < 0 ? string.Empty : open.Substring(active);
        Check(Regex.IsMatch(after, @"try\s*\{[\s\S]*_view\.Show\(true\);[\s\S]*_view\.ForShot\(false, _grid\);[\s\S]*Write\(\);\s*\}\s*catch \(Exception e\)\s*\{\s*Logger\?\.Error\(e\);\s*Close\(""a fault""\);"),
            "camera ways out: a fault partway through opening shuts the camera at once, everything given back; the opening puts the controls back");
        string hud = Between(game, "public static void Hud(bool shown)", "public static Camera MainCamera");
        Check(Regex.IsMatch(hud, @"else\s*\{\s*ScreenshotController\.uiIsVisible = shown;"), "camera ways out: with no interface to toggle, the game's own flag of the HUD is set back");
        string free = Between(mode, "private void FreeShot()", "public CameraRefusal Open(");
        Check(Regex.IsMatch(Between(mode, "private void OnDestroy()", "private void FreeShot()"), @"FreeShot\(\);[\s\S]*if \(!gameObject\.activeInHierarchy\)\s*\{\s*FreeShot\(\);")
              && free.Contains("work.ContinueWith(_ => PhotosGame.Drop(left))") && free.Contains("PhotosGame.Drop(left);") && mode.Contains("_pendingShot = shot;") && mode.Contains("_pendingWork = work;"),
            "camera ways out: a shot cut off with the component (destroyed, or its object switched off) has the screen's copy let go, by its worker's end if a worker has it");
    }
}

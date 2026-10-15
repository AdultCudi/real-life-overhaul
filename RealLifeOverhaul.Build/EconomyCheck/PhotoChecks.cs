using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Photos;

// The Camera and the Gallery (Scripts/Photos): a photo's name and a save's folder, the cap and the oldest offered, the look
// over deleted saves, the order and the albums from a folder of made-up photos, the photo and its small copy made from a
// made-up screen and handed to Flare where it reads them, the camera's rules (when it opens and shuts, the zoom, the aim, the
// filters and frames), the restore of everything the camera borrows as a rule (CameraHold), the sources' guarantees (the
// files and the encoding on a worker, nothing sent, the HUD and the walk given back on every way out), and every text in the
// three locales with What's new read in every one of its rows.
internal static partial class Program
{
    private static readonly string PhotoScripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    private static readonly string PhotoLocales = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));

    private static void Photos()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo_photo_checks_" + Guid.NewGuid().ToString("N"));
        try
        {
            PhotoNames();
            PhotoCap();
            PhotoSweepRules();
            PhotoFolder(System.IO.Path.Combine(scratch, "folder"));
            PhotoSweepFolders(System.IO.Path.Combine(scratch, "sweep"));
            PhotoPixelsMade();
            PhotoToFlare(System.IO.Path.Combine(scratch, "flare"));
            PhotoCameraRules();
            PhotoHold();
            PhotoSources();
            PhotoWords();
            PhotoFixes(System.IO.Path.Combine(scratch, "fixes"));   // (PhotoFixChecks.cs)
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratch))
                {
                    Directory.Delete(scratch, true);
                }
            }
            catch (IOException)
            {
            }
        }
        Console.WriteLine("  camera and gallery: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- a save's folder and a photo's name

    private static void PhotoNames()
    {
        Check(PhotoStoreRules.FolderName(null) == "unsaved" && PhotoStoreRules.FolderName(string.Empty) == "unsaved", "photos: a save with no id has the folder 'unsaved'");
        Check(PhotoStoreRules.FolderName("Psnh0rSw8kyjQbL4AHyJ+Q==") == "Psnh0rSw8kyjQbL4AHyJ+Q==", "photos: a save's id as the game writes it (Base64) is its folder's name as it is");
        Check(PhotoStoreRules.FolderName("ab/cd\\ef:g").StartsWith("ab_cd_ef_g_", StringComparison.Ordinal) && PhotoStoreRules.FolderName("ab/cd\\ef:g").Length == "ab_cd_ef_g".Length + PhotoStoreRules.HashLength,
            "photos: a character no folder's name can have is made '_', and the name marked as changed");
        string longId = new string('a', 200);
        Check(PhotoStoreRules.FolderName(longId).Length == PhotoStoreRules.NameMost, "photos: a save's folder's name is cut to " + PhotoStoreRules.NameMost + " characters");
        string odd = PhotoStoreRules.FolderName("///");
        Check(odd.StartsWith("unsaved_", StringComparison.Ordinal) && odd == PhotoStoreRules.FolderName("///") && odd != PhotoStoreRules.FolderName("\\\\\\"),
            "photos: an id of nothing but odd characters has a folder of its own, the same every time (" + odd + ")");
        Check(PhotoStoreRules.FolderName("a/b") != PhotoStoreRules.FolderName("a/b ") , "photos: two ids give two folders");

        var at = new DateTime(2026, 10, 15, 14, 30, 5);
        string photo = PhotoStoreRules.NameFor(at, false, 34, 14, 1);
        string selfie = PhotoStoreRules.NameFor(at, true, 34, 14, 2);
        Check(photo == "Photo_20261015_143005_D34_14.jpg" && selfie == "Selfie_20261015_143005_D34_14_2.jpg", "photos: a photo's name is its kind, the real moment, the game's day and hour, and a number past 1 (" + photo + ", " + selfie + ")");
        var e = new PhotoEntry();
        Check(PhotoStoreRules.Parse(selfie, e) && e.Selfie && e.Stamp == 20261015143005L && e.Number == 2 && e.Day == 34 && e.Hour == 14 && e.Name == selfie,
            "photos: a name is read back whole");
        Check(PhotoStoreRules.Parse(photo, e) && !e.Selfie && e.Number == 1 && PhotoStoreRules.Taken(e) == at, "photos: the moment it was taken is read back from the name");
        Check(PhotoStoreRules.NameFor(at, false, -3, 30, 1) == "Photo_20261015_143005_D0_23.jpg", "photos: a day below 0 is 0 and an hour past 23 is 23 in a name");
        string[] wrong =
        {
            null, "", "Photo_20261015_143005_D34_14.png", "Picture_20261015_143005_D34_14.jpg", "Photo_20261340_143005_D34_14.jpg", "Photo_20261015_253005_D34_14.jpg",
            "Photo_20261015_143005_D34_24.jpg", "Photo_20261015_143005_D34_14_1.jpg", "Photo_20261015_143005_34_14.jpg", "Photo_20261015_143005_D34_14_2_3.jpg",
            "Photo_2026101_143005_D34_14.jpg", "Photo_20261015_143005_Dx_14.jpg", "Photo_20261015_143005_D34_-1.jpg", "Selfie_.jpg"
        };
        int read = wrong.Count(n => PhotoStoreRules.Parse(n, new PhotoEntry()));
        Check(read == 0, "photos: a file that is not one of the Camera's is never read as a photo (" + read + " of " + wrong.Length + " read)");
        Check(!PhotoStoreRules.Parse(photo, null), "photos: a name read into nothing is refused");
    }

    // ---------------------------------------------------------------- the cap

    private static List<PhotoEntry> PhotoList(int count)
    {
        var list = new List<PhotoEntry>(count);
        var start = new DateTime(2026, 1, 1, 9, 0, 0);
        for (int i = 0; i < count; i++)
        {
            var e = new PhotoEntry();
            PhotoStoreRules.Parse(PhotoStoreRules.NameFor(start.AddMinutes(i), i % 3 == 0, i + 1, 9, 1), e);
            list.Add(e);
        }
        PhotoStoreRules.Order(list);
        return list;
    }

    private static void PhotoCap()
    {
        Check(PhotoStoreRules.Cap == 300 && PhotoStoreRules.OfferAtOnce == 10, "photos: a save keeps 300 photos, and 10 are offered at once when it is full");
        Check(!PhotoStoreRules.Full(0) && !PhotoStoreRules.Full(299) && PhotoStoreRules.Full(300) && PhotoStoreRules.Full(301), "photos: the Gallery is full at 300, not before");
        Check(PhotoStoreRules.ToOffer(0) == 0 && PhotoStoreRules.ToOffer(299) == 0 && PhotoStoreRules.ToOffer(300) == 10 && PhotoStoreRules.ToOffer(305) == 15,
            "photos: nothing is offered below the cap; at it, enough for ten more (and what is over it besides)");
        List<PhotoEntry> list = PhotoList(300);
        var offer = new List<PhotoEntry>();
        PhotoStoreRules.Oldest(list, 10, null, offer);
        Check(offer.Count == 10 && offer[0].Name == list[299].Name && offer[9].Name == list[290].Name, "photos: the oldest are offered, the oldest first");
        string wallpaper = list[295].Name;
        PhotoStoreRules.Oldest(list, 10, wallpaper, offer);
        Check(offer.Count == 10 && offer.All(o => o.Name != wallpaper) && offer[9].Name == list[289].Name, "photos: the wallpaper is never offered; the next oldest stands in for it");
        PhotoStoreRules.Oldest(PhotoList(3), 10, null, offer);
        Check(offer.Count == 3, "photos: no more are offered than there are");
        List<PhotoEntry> one = PhotoList(1);
        PhotoStoreRules.Oldest(one, 10, one[0].Name.ToUpperInvariant(), offer);
        Check(offer.Count == 0, "photos: the only photo, the wallpaper, is not offered (its name compared without case, as Windows names files)");
    }

    // ---------------------------------------------------------------- a deleted save, as a rule

    private static void PhotoSweepRules()
    {
        var today = new DateTime(2026, 10, 15);
        Check(PhotoStoreRules.SweepOf(true, false, null, today) == PhotoStoreRules.Sweep.Keep, "photos: a save that is there keeps its photos");
        Check(PhotoStoreRules.SweepOf(true, true, today.AddDays(-40), today) == PhotoStoreRules.Sweep.Unmark, "photos: a save that came back has its mark taken off, however old");
        Check(PhotoStoreRules.SweepOf(false, false, null, today) == PhotoStoreRules.Sweep.Mark, "photos: a save found gone is marked first, never deleted at once");
        Check(PhotoStoreRules.SweepOf(false, true, today.AddDays(-29), today) == PhotoStoreRules.Sweep.Keep, "photos: gone 29 days, its photos are kept");
        Check(PhotoStoreRules.SweepOf(false, true, today.AddDays(-30), today) == PhotoStoreRules.Sweep.Delete, "photos: gone " + PhotoStoreRules.GraceDays + " days, its photos are deleted");
        Check(PhotoStoreRules.SweepOf(false, true, today.AddDays(3), today) == PhotoStoreRules.Sweep.Mark, "photos: a mark in the future (a clock that was wrong) is counted again from today");
        Check(PhotoStoreRules.SweepOf(false, true, null, today) == PhotoStoreRules.Sweep.Mark, "photos: a mark that cannot be read is written again as today");
        Check(PhotoStoreRules.MarkDay(PhotoStoreRules.MarkText(today)) == today && PhotoStoreRules.MarkDay("yesterday") == null && PhotoStoreRules.MarkDay(null) == null,
            "photos: a mark's day is written and read back, and nonsense is no day");
    }

    // ---------------------------------------------------------------- the Gallery's order and pages, from a folder

    private static void Touch(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        File.WriteAllBytes(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
    }

    private static void PhotoFolder(string folder)
    {
        var names = new List<string>();
        var start = new DateTime(2026, 3, 1, 8, 0, 0);
        var rng = new Random(7);
        for (int i = 0; i < 47; i++)
        {
            names.Add(PhotoStoreRules.NameFor(start.AddHours(i * 5 + rng.Next(3)), i % 4 == 1, 10 + i, i % 24, 1));
        }
        names.Add(PhotoStoreRules.NameFor(start.AddHours(400), false, 99, 9, 2));   // two in one second: the later number is newer
        names.Add(PhotoStoreRules.NameFor(start.AddHours(400), true, 99, 9, 1));
        foreach (string n in names.OrderBy(_ => rng.Next()))
        {
            Touch(System.IO.Path.Combine(folder, n));
        }
        Touch(System.IO.Path.Combine(folder, "notes.jpg"));
        Touch(System.IO.Path.Combine(folder, "Photo_20260301_080000_D10_08.png"));
        Touch(System.IO.Path.Combine(folder, "Photo_broken.jpg"));
        Touch(System.IO.Path.Combine(folder, "Thumbs", "Photo_20260301_080000_D10_08.jpg"));   // a file in Thumbs is no photo
        var listed = new List<PhotoEntry>();
        Check(PhotoStore.List(folder, listed) && listed.Count == names.Count, "photos: a folder lists the Camera's photos and nothing else (" + listed.Count + " of " + names.Count + ")");
        bool ordered = true;
        for (int i = 1; i < listed.Count; i++)
        {
            ordered &= PhotoStoreRules.Newest(listed[i - 1], listed[i]) < 0;
        }
        Check(ordered && listed[0].Name == names[names.Count - 2] && listed[1].Name == names[names.Count - 1], "photos: the Gallery lists newest first, the later of one second before the earlier");
        var again = new List<PhotoEntry>();
        PhotoStore.List(folder, again);
        Check(again.Select(x => x.Name).SequenceEqual(listed.Select(x => x.Name)), "photos: the order is the same every time, whatever order the folder gives");
        Check(PhotoStore.List(System.IO.Path.Combine(folder, "none"), again) && again.Count == 0, "photos: a save with no folder yet has no photos, and that is no fault");

        var selfies = new List<PhotoEntry>();
        GalleryRules.Album(listed, GalleryAlbum.Selfies, null, selfies);
        Check(selfies.Count == listed.Count(x => x.Selfie) && selfies.All(x => x.Selfie) && selfies.Count > 0, "photos: the Selfies album of a folder has its selfies alone");
        GalleryRules.Album(listed, GalleryAlbum.All, null, selfies);
        Check(selfies.Count == listed.Count, "photos: All photos has every photo of a folder");

        var at = new DateTime(2026, 10, 15, 9, 0, 0);
        string a = PhotoStore.NewPath(folder, "Psnh0rSw8kyjQbL4AHyJ+Q==", at, false, 5, 9);
        Touch(a);
        string b = PhotoStore.NewPath(folder, "Psnh0rSw8kyjQbL4AHyJ+Q==", at, false, 5, 9);
        Check(System.IO.Path.GetFileName(a) == "Photo_20261015_090000_D5_09.jpg" && System.IO.Path.GetFileName(b) == "Photo_20261015_090000_D5_09_2.jpg", "photos: a second photo in the same second is numbered, never written over the first");
        string idFile = System.IO.Path.Combine(folder, PhotoStoreRules.SaveFile);
        Check(File.Exists(idFile) && File.ReadAllText(idFile) == "Psnh0rSw8kyjQbL4AHyJ+Q==", "photos: the save's id is written beside its photos with the first one");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        Check(PhotoStore.WriteWhole(b, bytes, 3) && File.ReadAllBytes(b).Length == 3 && !File.Exists(b + ".part"), "photos: a photo is written whole under its own name, nothing half left");

        string thumb = PhotoStore.ThumbOf(a);
        Touch(thumb);
        Check(PhotoStore.Delete(folder, System.IO.Path.GetFileName(a)) && !File.Exists(a) && !File.Exists(thumb), "photos: a photo is deleted with its small copy");
        Check(PhotoStore.Delete(folder, System.IO.Path.GetFileName(a)), "photos: deleting a photo that is gone is no fault");
        Check(!PhotoStore.Delete(folder, "..\\" + System.IO.Path.GetFileName(b)) && !PhotoStore.Delete(folder, null) && File.Exists(b), "photos: a name with a path in it deletes nothing");

        Check(PhotoStore.Wallpaper(folder) == null, "photos: a save with no wallpaper chosen has none");
        Check(PhotoStore.SetWallpaper(folder, System.IO.Path.GetFileName(b)) && PhotoStore.Wallpaper(folder) == System.IO.Path.GetFileName(b), "photos: the wallpaper's photo is written down and read back");
        File.Delete(b);
        Check(PhotoStore.Wallpaper(folder) == null, "photos: a wallpaper whose photo was deleted is no wallpaper");
        Check(PhotoStore.SetWallpaper(folder, null) && !File.Exists(System.IO.Path.Combine(folder, PhotoStoreRules.WallpaperFile)), "photos: the phone's own wallpaper again leaves nothing written");
    }

    // ---------------------------------------------------------------- deleted saves, over folders

    private static void PhotoSweepFolders(string root)
    {
        string photos = System.IO.Path.Combine(root, "Photos"), saves = System.IO.Path.Combine(root, "SaveGames");
        var today = new DateTime(2026, 10, 15);
        Touch(System.IO.Path.Combine(saves, "EA 0.10", "keep+id==", "Main.hsg"));
        Touch(System.IO.Path.Combine(saves, "EA 0.11", "json_id", "Main.json"));
        Directory.CreateDirectory(System.IO.Path.Combine(saves, "EA 0.11", "empty_id"));
        string[] ids = { "keep+id==", "json_id", "gone_new", "gone_old", "gone_soon", "playing", "empty_id" };
        foreach (string id in ids)
        {
            string folder = PhotoStore.Folder(photos, id);
            Touch(System.IO.Path.Combine(folder, "Photo_20260101_090000_D1_09.jpg"));
            File.WriteAllText(System.IO.Path.Combine(folder, PhotoStoreRules.SaveFile), id);
        }
        File.WriteAllText(System.IO.Path.Combine(PhotoStore.Folder(photos, "gone_old"), PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-31)));
        File.WriteAllText(System.IO.Path.Combine(PhotoStore.Folder(photos, "gone_soon"), PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-5)));
        File.WriteAllText(System.IO.Path.Combine(PhotoStore.Folder(photos, "keep+id=="), PhotoStoreRules.GoneFile), PhotoStoreRules.MarkText(today.AddDays(-50)));
        Touch(System.IO.Path.Combine(photos, "someone_elses", "a.jpg"));   // no save.txt: not the Camera's, never touched
        Check(PhotoStore.SavePresent(saves, "keep+id==") && PhotoStore.SavePresent(saves, "json_id") && !PhotoStore.SavePresent(saves, "gone_new") && !PhotoStore.SavePresent(saves, "empty_id"),
            "photos: a save is there when a folder of its id has a save file in any of the game's version folders; an empty folder is no save");
        Check(PhotoStore.SavePresent(System.IO.Path.Combine(root, "nowhere"), "anything") && !PhotoStore.SavePresent(saves, null),
            "photos: with no folder of saves at all nothing is judged gone, and no id is no save");
        var deleted = new List<string>();
        var marked = new List<string>();
        int looked = PhotoStore.Sweep(photos, "playing", id => PhotoStore.SavePresent(saves, id), today, deleted, marked);
        Check(looked == 6, "photos: every save's photos the Camera wrote are looked at, the played save's and others' folders left out (" + looked + ")");
        Check(deleted.SequenceEqual(new[] { PhotoStoreRules.FolderName("gone_old") }) && !Directory.Exists(PhotoStore.Folder(photos, "gone_old")), "photos: only a save gone for " + PhotoStoreRules.GraceDays + " days has its photos deleted");
        Check(marked.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { PhotoStoreRules.FolderName("empty_id"), PhotoStoreRules.FolderName("gone_new") }.OrderBy(x => x, StringComparer.Ordinal)) && File.Exists(System.IO.Path.Combine(PhotoStore.Folder(photos, "gone_new"), PhotoStoreRules.GoneFile)),
            "photos: a save found gone is marked with today");
        Check(File.Exists(System.IO.Path.Combine(PhotoStore.Folder(photos, "gone_soon"), "Photo_20260101_090000_D1_09.jpg")) && !File.Exists(System.IO.Path.Combine(PhotoStore.Folder(photos, "keep+id=="), PhotoStoreRules.GoneFile)),
            "photos: a save gone 5 days keeps its photos, and a save that came back loses its mark");
        Check(Directory.Exists(PhotoStore.Folder(photos, "playing")) && !File.Exists(System.IO.Path.Combine(PhotoStore.Folder(photos, "playing"), PhotoStoreRules.GoneFile)) && File.Exists(System.IO.Path.Combine(photos, "someone_elses", "a.jpg")),
            "photos: the save being played and a folder the Camera did not write are never touched");
        Check(PhotoStore.Sweep(System.IO.Path.Combine(root, "no_photos"), null, id => false, today, deleted, marked) == 0, "photos: with no photos at all there is nothing to look at");
    }

    // ---------------------------------------------------------------- the pixels

    private static void PhotoPixelsMade()
    {
        PhotoPixels.Fit(3840, 2160, 1920, 1080, out int w, out int h);
        Check(w == 1920 && h == 1080, "photos: a 4K screen makes a 1920 x 1080 photo");
        PhotoPixels.Fit(2560, 1080, 1920, 1080, out w, out h);
        Check(w == 1920 && h == 810, "photos: an ultra-wide screen is fitted by its width, its shape kept (" + w + " x " + h + ")");
        PhotoPixels.Fit(1600, 900, 1920, 1080, out w, out h);
        Check(w == 1600 && h == 900, "photos: a smaller screen is never made larger");
        PhotoPixels.Fit(1080, 2400, 1920, 1080, out w, out h);
        Check(w == 486 && h == 1080, "photos: a tall screen is fitted by its height (" + w + " x " + h + ")");
        PhotoPixels.Fit(0, 10, 1920, 1080, out w, out h);
        Check(w == 0 && h == 0, "photos: a screen of no size makes nothing");
        PhotoPixels.SmallSize(1920, 1080, out w, out h);
        Check(w == PhotoThumbs.Width && h == 144, "photos: the small copy of a 1080-line photo is 256 x 144 (" + w + " x " + h + ")");

        // 4 x 2 RGBA, made 2 x 1: each pixel the rounded average of its 2 x 2 block
        byte[] src =
        {
            10, 20, 30, 255, 11, 21, 31, 255, 100, 0, 0, 255, 0, 100, 0, 255,
            12, 22, 32, 255, 13, 23, 33, 255, 0, 0, 100, 255, 1, 1, 1, 255
        };
        var dst = new byte[6];
        Check(PhotoPixels.Shrink(src, 4, 2, 4, false, dst, 2, 1) && dst.SequenceEqual(new byte[] { 12, 22, 32, 25, 25, 25 }), "photos: a pixel of the photo is the rounded average of the screen's block (" + string.Join(",", dst) + ")");
        Check(PhotoPixels.Shrink(src, 4, 2, 4, true, dst, 2, 1) && dst[0] == 32 && dst[2] == 12, "photos: a screen read blue first is turned round to red first");
        var same = new byte[4 * 2 * 3];
        Check(PhotoPixels.Shrink(src, 4, 2, 4, false, same, 4, 2) && same[0] == 10 && same[3] == 11 && same[6] == 100 && same[23] == 1, "photos: a screen as small as the photo is copied as it is, without its alpha");
        var rgb = new byte[] { 1, 2, 3, 5, 6, 7 };
        var one = new byte[3];
        Check(PhotoPixels.Shrink(rgb, 2, 1, 3, false, one, 1, 1) && one[0] == 3 && one[1] == 4 && one[2] == 5, "photos: three bytes a pixel are read too");
        Check(!PhotoPixels.Shrink(src, 4, 2, 4, false, new byte[100], 8, 2) && !PhotoPixels.Shrink(src, 4, 2, 2, false, dst, 2, 1) && !PhotoPixels.Shrink(src, 4, 3, 4, false, dst, 2, 1)
              && !PhotoPixels.Shrink(src, 4, 2, 4, false, new byte[5], 2, 1), "photos: a larger photo than the screen, a pixel of two bytes, a screen shorter than said, or no room are refused");
        PhotoPixelsTimed();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void PhotoPixelsTimed()
    {
        var screen = new byte[3840 * 2160 * 4];
        for (int i = 0; i < screen.Length; i++)
        {
            screen[i] = (byte)(i * 31);
        }
        var photo = new byte[1920 * 1080 * 3];
        PhotoPixels.Shrink(screen, 3840, 2160, 4, true, photo, 1920, 1080);   // (warmed)
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        bool made = PhotoPixels.Shrink(screen, 3840, 2160, 4, true, photo, 1920, 1080);
        double ms = watch.Elapsed.TotalMilliseconds;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(made && ms < 600.0, "photos: a 4K screen is made a 1080-line photo in " + ms.ToString("0") + " ms on a worker, wanted under 600 (the main thread waits for none of it)");
        Check(bytes == 0, "photos: making the photo allocates nothing (" + bytes + " bytes)");
    }

    // ---------------------------------------------------------------- the hand-over to Flare

    private static void PhotoToFlare(string folder)
    {
        // A made-up screen of 640 x 360, blue first, as the engine may read it.
        int sw = 640, sh = 360;
        var screen = new byte[sw * sh * 4];
        for (int y = 0; y < sh; y++)
        {
            for (int x = 0; x < sw; x++)
            {
                int at = (y * sw + x) * 4;
                screen[at] = (byte)x;           // blue
                screen[at + 1] = (byte)y;       // green
                screen[at + 2] = 200;           // red
                screen[at + 3] = 255;
            }
        }
        PhotoPixels.Fit(sw, sh, PhotoPixels.PhotoWidth, PhotoPixels.PhotoHeight, out int w, out int h);
        var photo = new byte[w * h * 3];
        PhotoPixels.Shrink(screen, sw, sh, 4, true, photo, w, h);
        PhotoPixels.SmallSize(w, h, out int tw, out int th);
        var small = new byte[tw * th * 3];
        PhotoPixels.Shrink(photo, w, h, 3, false, small, tw, th);
        string path = PhotoStore.NewPath(folder, "id", new DateTime(2026, 10, 15, 12, 0, 0), true, 3, 12);
        Touch(path);
        string kept = PhotoStore.ThumbOf(path);
        PhotoThumbs.Write(kept, small, tw, th);
        Check(kept == PhotoThumbs.FileOf(path) && kept == System.IO.Path.Combine(folder, "Thumbs", System.IO.Path.GetFileName(path) + ".thumb"),
            "photos to Flare: the small copy is written where Flare reads it (PhotoThumbs.FileOf: Thumbs beside the photo)");
        byte[] back = PhotoThumbs.Read(kept, out int rw, out int rh);
        Check(back != null && rw == PhotoThumbs.Width && rh == 144 && back.SequenceEqual(small), "photos to Flare: it is Flare's format, 256 wide, read back as written (" + rw + " x " + rh + ")");
        Check(back != null && back[0] == 200 && back[2] < 10, "photos to Flare: its rows are red first, as Flare's small copies are");
        byte[] head = File.ReadAllBytes(kept).Take(12).ToArray();
        Check(head[0] == 'R' && head[1] == 'L' && head[2] == 'O' && head[3] == 'T' && BitConverter.ToInt32(head, 4) == 256 && BitConverter.ToInt32(head, 8) == 144,
            "photos to Flare: the file starts RLOT, then the width and the height as little-endian 32-bit integers");

        string flare = Read(@"Social\Flare\FlareDirector.Camera.cs"), live = Read(@"Social\Flare\FlareDirector.Live.cs"), links = Read(@"Wiring\PhotosLinks.cs");
        string gallery = GalleryText(), mode = Read(@"Photos\CameraMode.cs");
        Check(flare.Contains("public bool SharePhoto(string photoPath, string smallCopyPath = null)"), "photos to Flare: Flare's hook is there as the Camera calls it");
        Check(links.Contains("FlareDirector.Instance.SharePhoto(photo, small)") && Regex.IsMatch(gallery, @"ShareToFlare\(path, PhotoThumbs\.FileOf\(path\)\)")
              && Regex.IsMatch(mode, @"ShareToFlare\(path, PhotoThumbs\.FileOf\(path\)\)"), "photos to Flare: the Gallery and the camera hand over the photo's full path and its small copy's");
        Check(Regex.IsMatch(Between(live, "public void CameraPressed()", "public void SelfiePressed()"), @"FlareBridges\.OpenCamera\(\)") && links.Contains("FlareBridges.OpenCamera = () =>"),
            "photos to Flare: Flare's camera button opens the Camera");
        Check(links.Contains("_selfieBefore = FlareBridges.TakeSelfie") && links.Contains("FlareBridges.TakeSelfie = _selfieBefore"), "photos to Flare: Flare's selfie button is wrapped, and given back as it was");
    }

    // The Gallery app's code, all its files (GalleryApp.cs, .Grid.cs, .Viewer.cs).
    private static string GalleryText()
    {
        return Read(@"Photos\GalleryApp.cs") + "\n" + Read(@"Photos\GalleryApp.Grid.cs") + "\n" + Read(@"Photos\GalleryApp.Viewer.cs");
    }

    private static string Read(string relative)
    {
        string path = System.IO.Path.Combine(PhotoScripts, relative);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    // ---------------------------------------------------------------- the camera's rules

    private static void PhotoCameraRules()
    {
        var ok = new CameraSituation { FeatureOn = true, Loaded = true, PlayerThere = true };
        Check(CameraRules.Refuse(ok) == CameraRefusal.None, "camera: on foot with nothing open, it opens");
        CameraSituation s = ok;
        s.FeatureOn = false;
        Check(CameraRules.Refuse(s) == CameraRefusal.Off, "camera: off, it does not open");
        s = ok;
        s.InVehicle = true;
        s.Window = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.Vehicle, "camera: in a vehicle it does not open, and that is the reason given first");
        s = ok;
        s.OnTransit = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.Transit, "camera: on the bus, the subway or in a taxi it does not open");
        s = ok;
        s.WalkHeld = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.Busy, "camera: the player's walk held, it does not open");
        s.OwnHold = true;
        Check(CameraRules.MustClose(s) == CameraRefusal.None, "camera: open, its own hold on the walk does not shut it");
        s = ok;
        s.Window = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.Window && CameraRules.MustClose(s) == CameraRefusal.Window, "camera: a window open, it does not open, and one opening shuts it");
        s = ok;
        s.Loaded = false;
        s.InVehicle = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.NotLoaded && CameraRules.MustClose(s) == CameraRefusal.NotLoaded, "camera: a city loading shuts it before anything else");
        s = ok;
        s.Custody = true;
        Check(CameraRules.MustClose(s) == CameraRefusal.Custody, "camera: held by the police, it shuts");
        s = ok;
        s.Skipping = true;
        Check(CameraRules.MustClose(s) == CameraRefusal.Skipping, "camera: time skipped, it shuts");
        s = ok;
        s.PhotoMode = true;
        Check(CameraRules.Refuse(s) == CameraRefusal.PhotoMode, "camera: in photo mode it does not open");
        s = ok;
        s.PlayerThere = false;
        Check(CameraRules.MustClose(s) == CameraRefusal.Gone, "camera: the player gone (a scene change), it shuts");
        bool keys = true;
        foreach (CameraRefusal r in Enum.GetValues(typeof(CameraRefusal)))
        {
            keys &= (r == CameraRefusal.None) == (CameraRules.RefusalKey(r) == null);
        }
        Check(keys, "camera: every refusal but none has its sentence");

        Check(CameraRules.Zoom(1f, 0f) == 1f && CameraRules.Zoom(1f, -5f) == 1f && CameraRules.Zoom(4.9f, 5f) == 5f && CameraRules.Zoom(float.NaN, 1f) == CameraRules.ZoomNotch,
            "camera: the zoom stays between 1x and 5x, and nonsense starts at 1x");
        Check(Math.Abs(CameraRules.Zoom(1f, 2f) - 1.3225f) < 0.001f, "camera: two notches of the wheel zoom 1.15 times each");
        Check(Math.Abs(CameraRules.FieldOfView(1f) - 60f) < 0.01f && Math.Abs(Math.Tan(CameraRules.FieldOfView(2f) * Math.PI / 360.0) * 2 - Math.Tan(Math.PI / 6.0)) < 0.0001
              && CameraRules.FieldOfView(9f) == CameraRules.FieldOfView(5f), "camera: at 2x the picture is twice as large (the field of view's tangent halved); past 5x nothing more");
        Check(CameraRules.ZoomWords(1f) == "1.0x" && CameraRules.ZoomWords(2.345f) == "2.3x" && CameraRules.ZoomWords(7f) == "5.0x", "camera: the zoom is written 1.0x to 5.0x");
        float yaw = 170f, pitch = 0f;
        CameraRules.Aim(ref yaw, ref pitch, 1000f, 10000f, 1080f, 1f);
        Check(yaw > -180f && yaw <= 180f && Math.Abs(yaw - CameraRules.Wrap(170f + 90f)) < 0.01f && pitch == CameraRules.PitchMost, "camera: a drag turns the camera round past south, and looks up no further than its bound");
        float y2 = 0f, p2 = 0f, y3 = 0f, p3 = 0f;
        CameraRules.Aim(ref y2, ref p2, 100f, -100f, 1080f, 1f);
        CameraRules.Aim(ref y3, ref p3, 100f, -100f, 2160f, 4f);
        Check(y2 > 0f && p2 < 0f && Math.Abs(y3 * 8f - y2) < 0.01f, "camera: right and down look right and down; the same drag turns less on a taller screen and zoomed in");
        Check(Math.Abs(CameraRules.Seated(100f, 0f) - 80f) < 0.01f && Math.Abs(CameraRules.Seated(-150f, 170f) - CameraRules.Wrap(170f + 40f)) < 0.01f
              && Math.Abs(CameraRules.Seated(60f, 0f) - 60f) < 0.01f, "camera: seated, the camera turns 80 degrees either way of the seat and no further");
        Check(CameraRules.Wrap(540f) == 180f && CameraRules.Wrap(-190f) == 170f && CameraRules.Wrap(float.NaN) == 0f, "camera: an angle is kept between -180 and 180");
        Check(Math.Abs(CameraRules.FilmBar(16f / 9f) - 0.128f) < 0.001f && CameraRules.FilmBar(2.39f) == 0f && CameraRules.FilmBar(3f) == 0f && CameraRules.FilmBar(0f) == 0f,
            "camera: the film frame's bars leave 2.39 to 1 on a 16 by 9 screen, and none on a screen as wide or wider");
        Check(CameraRules.Filters.Length == 5 && !CameraRules.Graded(0) && CameraRules.Graded(3) && !CameraRules.Graded(9) && CameraRules.ClampFilter(9) == 0 && CameraRules.ClampFilter(-1) == 0,
            "camera: five filters, the first natural, and a filter that is not there is the natural one");
        Check(CameraRules.Filters[3].Saturation == -100f && CameraRules.FrameKeys.Length == 3 && CameraRules.ClampFrame(2) == CameraFrame.Film && CameraRules.ClampFrame(7) == CameraFrame.None,
            "camera: black and white has no colour at all; three frames, and a frame that is not there is none");
        Check(CameraRules.GridLines.Length == 2 && Math.Abs(CameraRules.GridLines[0] - 1f / 3f) < 1e-6 && Math.Abs(CameraRules.GridLines[1] - 2f / 3f) < 1e-6, "camera: the grid is the rule of thirds");
    }

    // ---------------------------------------------------------------- the restore, as a rule

    private static void PhotoHold()
    {
        var hold = new CameraHold();
        var into = new float[CameraHold.Values];
        Check(!hold.Any && !hold.Give(HoldPart.Place, into), "camera hold: nothing held, nothing to give back (a camera shut twice puts back nothing)");
        for (int i = 0; i < (int)HoldPart.Count; i++)
        {
            hold.Take((HoldPart)i, i, i + 0.5f, -i, i * 2f, 0.25f, 7f, i, -0.5f);
        }
        Check(hold.Any && !hold.Take(HoldPart.Place, 99f) && hold.Value(HoldPart.Place, 0) == (int)HoldPart.Place, "camera hold: a part taken again keeps what it first was (the selfie and back, a second shot)");
        bool exact = true;
        int given = 0;
        var order = new List<HoldPart>();
        foreach (HoldPart part in CameraHold.GiveOrder)
        {
            if (hold.Give(part, into))
            {
                given++;
                order.Add(part);
                int i = (int)part;
                exact &= into[0] == i && into[1] == i + 0.5f && into[2] == -i && into[3] == i * 2f && into[4] == 0.25f && into[5] == 7f && into[6] == i && into[7] == -0.5f;
            }
        }
        Check(given == (int)HoldPart.Count && exact, "camera hold: every part is given back once with exactly the values it was taken with");
        Check(!hold.Any && CameraHold.GiveOrder.All(p => !hold.Give(p, into)), "camera hold: after it is given back nothing is held, and a second way out gives nothing");
        Check(order.First() == HoldPart.Selfie && order.Last() == HoldPart.Walk && order.IndexOf(HoldPart.Facing) < order.IndexOf(HoldPart.Place)
              && order.IndexOf(HoldPart.Lens) < order.IndexOf(HoldPart.Brain) && order.IndexOf(HoldPart.Hud) < order.IndexOf(HoldPart.Walk),
            "camera hold: the selfie's pose first, the body before the camera, the camera before its brain, the player's walk last");
        Check(CameraHold.GiveOrder.Distinct().Count() == (int)HoldPart.Count, "camera hold: every part is in the order once");
        var held = new Quaternion4(0.1f, -0.7f, 0.2f, 0.68f);
        hold.Take(HoldPart.Facing, held.X, held.Y, held.Z, held.W);
        hold.Take(HoldPart.Facing, 0f, 1f, 0f, 0f);   // the rear camera turns the body: the first facing stays
        hold.Give(HoldPart.Facing, into);
        Check(into[0] == held.X && into[1] == held.Y && into[2] == held.Z && into[3] == held.W, "camera hold: the body's facing is put back as it was before the camera turned it, however often it turned");
        hold.Take(HoldPart.Grade);
        hold.Forget(HoldPart.Grade);
        Check(!hold.Held(HoldPart.Grade) && !hold.Give(HoldPart.Grade, into) && !hold.Take(HoldPart.Count) && !hold.Held((HoldPart)(-1)), "camera hold: a part let go is not given back; a part that is no part is never held");
    }

    private struct Quaternion4
    {
        public readonly float X, Y, Z, W;

        public Quaternion4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    // ---------------------------------------------------------------- the sources

    private static void PhotoSources()
    {
        string folder = System.IO.Path.Combine(PhotoScripts, "Photos");
        string all = string.Join("\n", Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        string game = Read(@"Photos\PhotosGame.cs"), director = Read(@"Photos\PhotosDirector.cs"), mode = Read(@"Photos\CameraMode.cs"), gallery = GalleryText();
        var sends = new Regex(@"\b(WebClient|HttpClient|HttpWebRequest|WWWForm|Socket|TcpClient|UdpClient|SmtpClient|SharedCity|GoingPublicLink)\b|UnityWebRequest\.(Post|Put|Get)\b|https?://", RegexOptions.IgnoreCase);
        Match send = sends.Match(all);
        Check(!send.Success, "photos: nothing in the Camera or the Gallery sends anything over the network" + (send.Success ? " (found '" + send.Value + "')" : string.Empty));
        Check(Regex.Matches(game, @"PhotoStore\.ReadPhoto\(path\)").Count == 1 && Regex.Matches(all, @"PhotoStore\.ReadPhoto\(").Count == 1,
            "photos: the one loader the Gallery uses reads a file of this computer, in the game file alone");
        Check(Regex.Matches(all, @"ScreenCapture\.CaptureScreenshotAsTexture\(\)").Count == 1 && game.Contains("HotSpots.Time(HotRead)") && game.Contains("watch.Elapsed.TotalMilliseconds"),
            "photos: the screen is read in one place, timed under a name of its own and measured");
        Check(Regex.IsMatch(mode, @"Task\.Run\(\(\) =>\s*\{\s*path = PhotoStore\.NewPath\([^;]*;\s*return PhotosGame\.Develop\(given, path\);") && game.Contains("ImageConversion.EncodeNativeArrayToJPG"),
            "photos: the photo is made, encoded and written on a worker");
        Check(!Regex.IsMatch(all, @"\.EncodeToJPG\(|\.EncodeToPNG\(") && Regex.Matches(all, @"File\.ReadAllBytes\(").Count == Regex.Matches(Read(@"Photos\PhotoStore.cs"), @"File\.ReadAllBytes\(").Count
              && Regex.Matches(all, @"\.LoadImage\(").Count == 1, "photos: no photo is encoded on the main thread; a file is read only by the store (a worker's); one decode, the whole photo's on a click");
        Check(Regex.Matches(director, @"Task\.Run\(").Count >= 5 && Regex.Matches(gallery, @"Task\.Run\(").Count == 1 && !Regex.IsMatch(director + gallery, @"Directory\.GetFiles|File\.Delete|File\.WriteAllText"),
            "photos: listing, deleting, the wallpaper's line and the look over deleted saves are a worker's, never the main thread's");
        Check(!Regex.IsMatch(all, @"modData") && !Regex.IsMatch(all, @"SaveHook"), "photos: nothing is written into the save");
        Check(Regex.Matches(mode, @"case HoldPart\.\w+:").Count == (int)HoldPart.Count - 1, "photos: the camera puts back every part it can hold but the selfie's, which puts back its own (" + Regex.Matches(mode, @"case HoldPart\.\w+:").Count + ")");
        Check(Regex.Matches(mode, @"_hold\.Take\(HoldPart\.(\w+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Count() == (int)HoldPart.Count, "photos: the camera holds each part before it changes it");
        string close = Between(mode, "public void Close(string why)", "private void GiveBack()");
        Check(close.Contains("_selfie.End()") && close.IndexOf("_selfie.End()", StringComparison.Ordinal) < close.IndexOf("GiveBack()", StringComparison.Ordinal) && close.Contains("Active = false"),
            "photos: on every way out the selfie's pose ends first, then everything else is given back");
        string update = Between(mode, "private void OnUpdate()", "private static readonly int HotLateUpdate");
        Check(update.Contains("CameraRules.MustClose(PhotosGame.Situation(true))") && update.Contains("TakeCancel()") && mode.Contains("[DefaultExecutionOrder(-31000)]"),
            "photos: the camera shuts when the moment says so or on the cancel key, which it takes before the game's own handling");
        string cam = Read(@"Camera\ThirdPersonCamera.cs"), nav = Read(@"Wiring\NavLinks.cs");
        Check(cam.Contains("Leisure.PhotoMode.Active || Photos.CameraMode.Active || AnyWindow(true)") && nav.Contains("Photos.CameraMode.Active"),
            "photos: the mod's other cameras, its HUD and its activities stand aside while the camera is open");
        Check(Regex.IsMatch(game, @"\(NavigationBlocker\)7319") && Regex.Matches(PhotoAllScripts(), @"\(NavigationBlocker\)7319").Count == 1, "photos: the camera's hold on the walk is a value of its own, nobody else's");
    }

    private static string _allScripts;

    private static string PhotoAllScripts()
    {
        return _allScripts ?? (_allScripts = string.Join("\n", Directory.GetFiles(PhotoScripts, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)));
    }

    // ---------------------------------------------------------------- the words

    private static void PhotoWords()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(PhotoLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(PhotoLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(PhotoLocales, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "photos words: the three locales are where the checks look for them (" + PhotoLocales + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        string sources = string.Join("\n", Directory.GetFiles(System.IO.Path.Combine(PhotoScripts, "Photos"), "*.cs").Select(File.ReadAllText)) + Read(@"Wiring\PhotosLinks.cs");
        var keys = new SortedSet<string>(Regex.Matches(sources, @"""(reallife_(?:phonecam|gallery|guide_photos|meta_title_photos)[a-z0-9_]*)""").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        foreach (string k in new[] { "reallife_opt_tip_phone_camera", "reallife_opt_tip_phone_camera_grid", "reallife_opt_sec_photos", "reallife_guide_photos" })
        {
            keys.Add(k);
        }
        var holes = new Regex(@"\{[a-z_]+\}");
        var missing = new List<string>();
        foreach (string key in keys)
        {
            if (!en.TryGetValue(key, out string e) || !pt.TryGetValue(key, out string p) || !zh.TryGetValue(key, out string z) || e.Length == 0 || p.Length == 0 || z.Length == 0)
            {
                missing.Add(key);
                continue;
            }
            string want = Holes(holes, e);
            if (Holes(holes, p) != want || Holes(holes, z) != want)
            {
                missing.Add(key + " (its {names} differ)");
            }
        }
        Check(keys.Count >= 100 && missing.Count == 0, "photos words: every text is in en.json, pt.json and zh-cn.json with the same {names} (" + keys.Count + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing.Take(12)) : string.Empty) + ")");
        if (missing.Count > 0)
        {
            return;
        }
        string[] notices = keys.Where(k => k.Contains("_refuse_") || k.Contains("_note_") || k.StartsWith("reallife_gallery_test_", StringComparison.Ordinal) || k == "reallife_phonecam_wait"
                                           || k == "reallife_phonecam_full_none" || k == "reallife_gallery_full_kept" || k == "reallife_phonecam_test_armed").ToArray();
        string longest = notices.OrderByDescending(k => en[k].Length).First();
        Check(notices.Length >= 20 && notices.All(k => en[k].Length <= 240), "photos words: every notification fits the game's toast (240 characters; the longest " + longest + ", " + en[longest].Length + ")");
        var dash = new Regex(@"[–—]| - |!");
        string bad = keys.FirstOrDefault(k => dash.IsMatch(en[k]) || dash.IsMatch(pt[k]));
        Check(bad == null, "photos words: no dash and no exclamation mark in English or Portuguese" + (bad != null ? " (" + bad + ")" : string.Empty));
        string guide = en["reallife_guide_photos"];
        string facts = Read(@"Photos\PhotosGuide.cs");
        var named = Regex.Matches(guide, @"\{(photos_[a-z]+)\}").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        Check(named.Count >= 6 && named.All(f => facts.Contains(".Fact(\"" + f + "\"")) && Regex.Matches(pt["reallife_guide_photos"] + zh["reallife_guide_photos"], @"\{photos_[a-z]+\}").Count >= named.Count * 2,
            "photos words: every figure the Guide's page prints is filled from the rules, in each language (" + string.Join(", ", named) + ")");
        Check(guide.Contains("# Where it works") && guide.Contains("in a taxi") && guide.Contains("indoors") && guide.Contains("{photos_grace}") && guide.Contains("{photos_cap}") && guide.Contains("# Going Public"),
            "photos words: the Guide says where it works, the cap, the deleted saves' days and what goes to the other players");
        // What's new: the line is looked for in every row, so that it is found before a release and after it numbers the row.
        Check(WhatsNewHas(en, "A Camera and a Gallery on the phone") && WhatsNewHas(pt, "Uma Câmera e uma Galeria no celular") && WhatsNewHas(zh, "手机新增相机和图库"),
            "photos words: What's new has the Camera and Gallery's line in each of the three languages (any of its rows)");
    }

    // Whether any row of What's new (reallife_changes_next or a numbered version's) has the words.
    private static bool WhatsNewHas(Dictionary<string, string> words, string line)
    {
        foreach (KeyValuePair<string, string> row in words)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(line))
            {
                return true;
            }
        }
        return false;
    }
}

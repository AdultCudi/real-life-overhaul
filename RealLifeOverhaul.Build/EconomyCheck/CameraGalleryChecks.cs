using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Photos;
using RealLifeOverhaul.Ui;

// The Camera and the Gallery rebuilt (4 Nov 2026, living/camera-gallery): the sidecar's new lines (an old photo loads, a new one round
// trips every field), the minute and how it is shown, the camera's tools as rules (zoom per lens, exposure, timer, guide, level, shape,
// the portrait's blur), the pixel work on the worker (cut, frame, grade, turn), what the lens saw, the Gallery's albums, search and
// filters over 500 photos, the Flare picker offering every photo, a deleted photo's three files, the Gallery's open cost with 500
// photos, nothing running on a frame while the camera is shut, and Esc and Close in every state.
internal static partial class Program
{
    private static void CameraGalleryChecks()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo_camgal_" + Guid.NewGuid().ToString("N"));
        try
        {
            CgSidecar();
            CgTime();
            CgCameraRules();
            CgPixels();
            CgSeen();
            CgFind();
            CgDelete(System.IO.Path.Combine(scratch, "delete"));
            CgOpenCost();
            CgSources();
            CgEscapes();
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
        Console.WriteLine("  camera and gallery rebuilt: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the sidecar

    private static void CgSidecar()
    {
        // A sidecar of the first version: the lines it had, none of the new ones.
        string old = "v=1\nplace=Broadway\ndistrict=Midtown\nfilter=1\nframe=2\nhour=19\nday=34\nc=18\nweather=rain\n";
        bool read = PhotoSidecarRules.Parse(old, out PhotoSidecar o);
        Check(read && o.Place == "Broadway" && o.Hour == 19 && o.Day == 34 && o.Frame == 2 && !o.HasMinute && !o.HasPeople && o.Aspect == 0 && o.Kind == 0 && o.Zoom == 0 && !o.Boost
              && o.Business == null && o.Original == null && o.Tags == null, "photos rebuilt: an old photo's sidecar loads as it did, every new field not known");
        var entry = new PhotoEntry { Name = "Photo_20261015_190000_D34_19.jpg", Day = 34, Hour = 19 };
        entry.Take(o);
        Check(entry.HasSide && entry.Minute == -1 && entry.Side.Weather == "rain", "photos rebuilt: an old photo keeps the hour and has no minute");
        // A new one: every new field out and back.
        var n = new PhotoSidecar
        {
            Place = "7th Avenue", District = "Midtown", Filter = 8, Frame = 3, Hour = 21, Day = 40, Celsius = 12, Weather = "mist", Minute = 47, HasMinute = true, Aspect = 4, Kind = 2, Zoom = 24,
            Exposure = -15, Timer = 10, Boost = true, Business = "Bloom Cafe", BusinessPlace = "7th_avenue~12", People = 6, HasPeople = true, Tags = "portrait,night,business,crowd,boost",
            Original = "Photo_20261104_120000_D40_21.jpg", Edits = "filter,crop"
        };
        string text = PhotoSidecarRules.Format(n);
        bool back = PhotoSidecarRules.Parse(text, out PhotoSidecar r);
        Check(back && r.Place == n.Place && r.District == n.District && r.Filter == 8 && r.Frame == 3 && r.Hour == 21 && r.Day == 40 && r.Celsius == 12 && r.Weather == "mist"
              && r.Minute == 47 && r.HasMinute && r.Aspect == 4 && r.Kind == 2 && r.Zoom == 24 && r.Exposure == -15 && r.Timer == 10 && r.Boost && r.Business == "Bloom Cafe"
              && r.BusinessPlace == "7th_avenue~12" && r.People == 6 && r.HasPeople && r.Tags == n.Tags && r.Original == n.Original && r.Edits == "filter,crop",
            "photos rebuilt: a new photo round trips the minute, the shape, the mode, the zoom, the exposure, the timer, the night boost, the business, the people, the tags and the edit");
        string[] order = { "weather=", "minute=", "aspect=", "kind=", "zoom=", "ev=", "timer=", "boost=", "business=", "bplace=", "people=", "tags=", "original=", "edits=" };
        int at = 0;
        bool endOrder = true;
        foreach (string key in order)
        {
            int i = text.IndexOf(key, at, StringComparison.Ordinal);
            endOrder &= i >= at;
            at = Math.Max(at, i);
        }
        Check(endOrder && text.IndexOf("hour=", StringComparison.Ordinal) < text.IndexOf("minute=", StringComparison.Ordinal),
            "photos rebuilt: the new fields are lines at the end of the sidecar, after the first version's, in a fixed order");
        // A later reader's line this version does not know is ignored, and a sidecar with a bad minute stays within an hour.
        PhotoSidecarRules.Parse(text + "future=1\nminute=99\n", out PhotoSidecar clamp);
        Check(clamp.Minute == 59 && clamp.Place == "7th Avenue", "photos rebuilt: an unknown line is ignored and a minute is kept within the hour");
        // The file on disk: written whole, read back, and the entry of a listing takes it.
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo_cg_side_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string photo = System.IO.Path.Combine(folder, PhotoStoreRules.NameFor(new DateTime(2026, 11, 4, 12, 0, 0), false, 40, 21, 1));
            File.WriteAllBytes(photo, new byte[] { 1, 2, 3 });
            Check(PhotoSidecarFile.Write(photo, n), "photos rebuilt: the sidecar is written beside the photo");
            var listed = new List<PhotoEntry>();
            PhotoStore.List(folder, listed);
            int read2 = PhotoStore.ReadSides(folder, listed);
            Check(listed.Count == 1 && read2 == 1 && listed[0].Minute == 47 && listed[0].Side.Business == "Bloom Cafe", "photos rebuilt: listing a folder reads each photo's sidecar once, on the worker that listed it");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
        // Edits: only what the edit changed is named.
        Check(PhotoSeenRules.Edits(true, false, 0, false) == "filter" && PhotoSeenRules.Edits(false, true, 5, true) == "frame,rotate,crop" && PhotoSeenRules.Edits(false, false, 4, false) == null,
            "photos rebuilt: an edit names only what it changed (a turn of four quarters is none)");
    }

    // ---------------------------------------------------------------- the minute

    private static void CgTime()
    {
        Check(PhotoTimeRules.Clock(19, 5, false, "{hour}h") == "19:05" && PhotoTimeRules.Clock(7, 0, false, "{hour}h") == "07:00" && PhotoTimeRules.Clock(0, 59, false, "{hour}h") == "00:59",
            "photo time: the minute is shown as the game's clock writes it (19:05)");
        Check(PhotoTimeRules.Clock(19, 5, true, "{hour}h") == "07:05 PM" && PhotoTimeRules.Clock(0, 9, true, "{hour}h") == "12:09 AM" && PhotoTimeRules.Clock(12, 30, true, "{hour}h") == "12:30 PM",
            "photo time: on the 12 hour clock the minute shows with AM or PM");
        Check(PhotoTimeRules.Clock(19, -1, false, "{hour}h") == "19h" && PhotoTimeRules.Clock(19, -1, true, "{hour}h") == "07 PM" && PhotoTimeRules.Clock(19, -1, false, "{hour}时") == "19时",
            "photo time: a photo from before the camera kept the minute shows the hour alone, never :00");
        var x = new GalleryTexts { T = key => key == "reallife_gallery_taken" ? "Day {day}, {hour}" : key == "reallife_gallery_hour_only" ? "{hour}h" : key, Real = p => "2026-11-04 12:10" };
        var withMinute = new PhotoEntry { Day = 41, Hour = 19, Minute = 5 };
        var without = new PhotoEntry { Day = 41, Hour = 19, Minute = -1 };
        Check(GalleryAlbumFill.Taken(withMinute, x) == "Day 41, 19:05 (2026-11-04 12:10)" && GalleryAlbumFill.Taken(without, x) == "Day 41, 19h (2026-11-04 12:10)",
            "photo time: the Gallery's fact shows the minute of a new photo and the hour alone of an old one");
    }

    // ---------------------------------------------------------------- the camera's tools

    private static void CgCameraRules()
    {
        Check(CameraRules.ZoomLeastOf(CameraKind.Wide) == 0.4f && CameraRules.ZoomLeastOf(CameraKind.Photo) == 1f && CameraRules.ZoomMostOf(CameraKind.Photo) == 5f
              && CameraRules.ZoomMostOf(CameraKind.Wide) == 1f && CameraRules.ZoomMostOf(CameraKind.Portrait) == 2.5f && CameraRules.ZoomStartOf(CameraKind.Wide) == 0.4f,
            "camera: each lens has its own range (wide 0.4x to 1x, photo 1x to 5x, portrait 1x to 2.5x) and the wide lens starts at its widest");
        Check(CameraRules.Zoom(1f, -5f, 0.4f, 1f) >= 0.4f && CameraRules.Zoom(1f, 9f, 0.4f, 1f) == 1f && CameraRules.Zoom(0.1f, 0f, 0.4f, 1f) == 0.4f && CameraRules.Zoom(float.NaN, 1f, 1f, 5f) > 1f,
            "camera: the zoom is kept in its lens's range, whatever the wheel or the buttons ask");
        Check(CameraRules.FieldOfView(0.4f) > 100f && CameraRules.FieldOfView(0.4f) < 112f && CameraRules.FieldOfView(0.1f) == CameraRules.FieldOfView(0.4f) && CameraRules.ZoomWords(0.4f) == "0.4x",
            "camera: the wide lens sees about 107 degrees and is written 0.4x");
        Check(CameraRules.LensAt(CameraKind.Photo, 2f) == 1 && CameraRules.LensAt(CameraKind.Photo, 2.4f) == -1 && CameraRules.Lenses(CameraKind.Wide).Length == 3, "camera: a zoom on a lens button lights it, one between does not");
        Check(CameraRules.Exposure(0, 1) == 5 && CameraRules.Exposure(20, 1) == 20 && CameraRules.Exposure(-20, -1) == -20 && CameraRules.ExposureWords(5) == "+0.5" && CameraRules.ExposureWords(-15) == "-1.5"
              && CameraRules.ExposureWords(0) == "0.0", "camera: the exposure steps by half a stop between minus two and plus two");
        Check(CameraRules.Timer(0) == 3 && CameraRules.Timer(3) == 10 && CameraRules.Timer(10) == 0, "camera: the timer goes off, 3 seconds, 10 seconds, off");
        Check(CameraRules.Counting(3, 0f) == 3 && CameraRules.Counting(3, 0.5f) == 3 && CameraRules.Counting(3, 1.2f) == 2 && CameraRules.Counting(3, 2.99f) == 1 && CameraRules.Counting(3, 3f) == 0 && CameraRules.Counting(10, 4f) == 6
              && CameraRules.Counting(99, 0f) == 10, "camera: the timer counts whole seconds down to nought, never past ten");
        Check(CameraRules.Guide(CameraGuide.Off) == CameraGuide.Thirds && CameraRules.Guide(CameraGuide.Thirds) == CameraGuide.Square && CameraRules.Guide(CameraGuide.Square) == CameraGuide.Off, "camera: the guide goes off, thirds, squares, off");
        var vx = new float[CameraRules.GuideLinesMost];
        var hy = new float[CameraRules.GuideLinesMost];
        CameraRules.GuideLines(CameraGuide.Thirds, 1920f, 1080f, vx, hy, out int nv, out int nh);
        bool thirds = nv == 2 && nh == 2 && Math.Abs(vx[0] - 640f) < 0.01f && Math.Abs(hy[1] - 720f) < 0.01f;
        CameraRules.GuideLines(CameraGuide.Square, 1920f, 1080f, vx, hy, out nv, out nh);
        bool squares = nh == 2 && nv >= 4 && nv <= CameraRules.GuideLinesMost && Math.Abs(vx[1] - vx[0] - 720f) < 0.01f && Math.Abs((vx[0] + vx[1]) / 2f - 960f) < 0.01f;
        CameraRules.GuideLines(CameraGuide.Off, 1920f, 1080f, vx, hy, out nv, out nh);
        Check(thirds && squares && nv == 0 && nh == 0, "camera: the thirds are at a third and two thirds; the squares are equal squares laid out from the middle; off has none");
        CameraRules.CropRect(3840, 2160, CameraAspect.FourThree, out int cx, out int cy, out int cw, out int ch);
        CameraRules.CropRect(3840, 2160, CameraAspect.SixteenNine, out int fx, out int fy, out int fw, out int fh);
        CameraRules.CropRect(3840, 2160, CameraAspect.Story, out int sx, out int sy, out int sw, out int sh);
        CameraRules.CropRect(2560, 1080, CameraAspect.SixteenNine, out int ux, out int uy, out int uw, out int uh);
        Check(cw == 2880 && ch == 2160 && cx == 480 && cy == 0 && fw == 3840 && fh == 2160 && fx == 0 && sw == 1215 && sh == 2160 && sx == 1312 && uw == 1920 && uh == 1080 && ux == 320,
            "camera: each shape is the largest rectangle of its ratio in the middle of the screen (16:9 on a 16:9 screen is all of it, on an ultrawide it is cut at the sides)");
        PhotoPixels.Box(CameraAspect.SixteenNine, out int bw, out int bh);
        PhotoPixels.Box(CameraAspect.Story, out int tw, out int th);
        PhotoPixels.Fit(cw, ch, bw, bh, out int w43, out int h43);
        PhotoPixels.Fit(sw, sh, tw, th, out int w916, out int h916);
        Check(w43 == 1440 && h43 == 1080 && bw == 1920 && bh == 1080 && tw == 1080 && th == 1920 && w916 == 1080 && h916 == 1920,
            "camera: every shape of photo fits the 1080p size class (4:3 is 1440 by 1080, 9:16 is 1080 by 1920)");
        Check(Math.Abs(CameraRules.HorizonAt(0f, 60f) - 0.5f) < 1e-5f && CameraRules.HorizonAt(10f, 60f) > 0.5f && CameraRules.HorizonAt(-10f, 60f) < 0.5f && CameraRules.HorizonAt(10f, 30f) > CameraRules.HorizonAt(10f, 60f)
              && CameraRules.Level(0.5f) && CameraRules.Level(-1f) && !CameraRules.Level(2f), "camera: the horizon is at the middle at level, moves down when looking up, further when zoomed; level within a degree");
        CameraRules.Blur(5f, out float nearFar, out float farFull);
        CameraRules.Blur(float.NaN, out float farNone, out _);
        CameraRules.Blur(0.01f, out float closest, out _);
        Check(Math.Abs(nearFar - 6.2f) < 0.001f && Math.Abs(farFull - 12f) < 0.001f && farNone > 60f && Math.Abs(closest - (CameraRules.FocusNear + CameraRules.FocusSharp)) < 0.001f,
            "camera: the portrait keeps the subject and a metre or so behind it sharp, blurs fully seven metres behind it, and never blurs the near");
        Check(CameraRules.FocusDue(true, 1f, 0.9f) == false && CameraRules.FocusDue(true, 1.3f, 1f) && !CameraRules.FocusDue(false, 9f, 0f), "camera: the portrait looks for its subject at most five times a second, and only after the aim changed");
        Check(CameraRules.Filters.Length == 11 && CameraRules.Filters.Select(f => f.NameKey).Distinct().Count() == 11 && Enum.GetValues(typeof(CameraKind)).Length == 4 && Enum.GetValues(typeof(CameraAspect)).Length == 5,
            "camera: eleven filters with a name each, four kinds of shot, four shapes and one not known");
        // The filters an old photo names are still the first six, and an old frame number still means what it looked like.
        Check(CameraRules.Filters[1].NameKey == "reallife_phonecam_filter_warm" && CameraRules.Filters[5].NameKey == "reallife_phonecam_filter_fade" && (int)CameraFrame.Polaroid == 1 && (int)CameraFrame.Film == 2,
            "camera: a photo of the old camera keeps its filter and its frame (the old Border is the Polaroid, still frame 1)");
    }

    // ---------------------------------------------------------------- the pixels

    private static byte[] CgPicture(int w, int h)
    {
        var rgb = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int at = (y * w + x) * 3;
                rgb[at] = (byte)(x * 255 / Math.Max(1, w - 1));
                rgb[at + 1] = (byte)(y * 255 / Math.Max(1, h - 1));
                rgb[at + 2] = 90;
            }
        }
        return rgb;
    }

    private static void CgPixels()
    {
        // A cut: the middle of a screen only.
        const int sw = 64, sh = 32;
        var screen = new byte[sw * sh * 4];
        for (int y = 0; y < sh; y++)
        {
            for (int x = 0; x < sw; x++)
            {
                int at = (y * sw + x) * 4;
                screen[at] = (byte)(x < 16 || x >= 48 ? 255 : 0);   // red at the sides only
                screen[at + 3] = 255;
            }
        }
        CameraRules.CropRect(sw, sh, CameraAspect.Square, out int cx, out int cy, out int cw, out int ch);
        var into = new byte[cw * ch * 3];
        bool cut = PhotoPixels.Shrink(screen, sw, sh, 4, false, into, cw, ch, cx, cy, cw, ch);
        bool noRed = true;
        for (int i = 0; i < into.Length; i += 3)
        {
            noRed &= into[i] == 0;
        }
        Check(cut && cw == 32 && ch == 32 && noRed && !PhotoPixels.Shrink(screen, sw, sh, 4, false, into, cw, ch, 40, 0, 32, 32) && !PhotoPixels.Shrink(screen, sw, sh, 4, false, into, cw, ch, -1, 0, 32, 32),
            "pixels: the cut keeps only the middle square of a screen (none of its red sides), and a cut outside the screen writes nothing");
        // Frames burned in: rows are bottom up, so the Polaroid's deep foot is the first rows.
        var a = CgPicture(200, 120);
        var framed = (byte[])a.Clone();
        bool done = PhotoPixels.Frame(framed, 200, 120, CameraFrame.Polaroid);
        int foot = (int)Math.Round(120 * CameraRules.BorderFoot), side = (int)Math.Round(120 * CameraRules.BorderSide);
        bool footPaper = framed[(foot - 1) * 200 * 3] == 247 && framed[foot * 200 * 3 + 100 * 3] == a[foot * 200 * 3 + 100 * 3];
        bool topPaper = framed[(120 - 1) * 200 * 3 + 50 * 3] == 247 && framed[(120 - side - 1) * 200 * 3 + 100 * 3] == a[(120 - side - 1) * 200 * 3 + 100 * 3];
        var film = (byte[])a.Clone();
        PhotoPixels.Frame(film, 200, 120, CameraFrame.Film);
        int bar = (int)Math.Round(120 * CameraRules.FilmBar(200f / 120f));
        var thin = (byte[])a.Clone();
        PhotoPixels.Frame(thin, 200, 120, CameraFrame.Border);
        var none = (byte[])a.Clone();
        Check(done && footPaper && topPaper && film[0] == 0 && film[(120 - 1) * 200 * 3 + 3] == 0 && bar > 0 && film[bar * 200 * 3 + 100 * 3] == a[bar * 200 * 3 + 100 * 3]
              && thin[0] == 247 && thin[100 * 3 + 2 * 200 * 3] == a[100 * 3 + 2 * 200 * 3] && !PhotoPixels.Frame(none, 200, 120, CameraFrame.None) && none.SequenceEqual(a) && !PhotoPixels.Frame(new byte[10], 2, 2, CameraFrame.Border),
            "pixels: a Polaroid, a film and a thin border are burned into the photo (the foot deeper, the bars above and below, an even edge), and no frame changes nothing");
        // Turning: four quarters are the photo, two are its mirror, one swaps the sides.
        var p = CgPicture(30, 20);
        var q1 = new byte[p.Length];
        var q2 = new byte[p.Length];
        var q4 = new byte[p.Length];
        var tmp = new byte[p.Length];
        PhotoPixels.Turn(p, 30, 20, 1, q1);
        PhotoPixels.Turn(q1, 20, 30, 1, tmp);
        PhotoPixels.Turn(p, 30, 20, 2, q2);
        bool twice = tmp.SequenceEqual(q2);
        PhotoPixels.Turn(tmp, 30, 20, 2, q4);
        PhotoPixels.Turned(30, 20, 1, out int tw, out int th);
        PhotoPixels.Turned(30, 20, 2, out int tw2, out int th2);
        // The top-left pixel of the turned picture came from the bottom-left of the first, as a clockwise turn does.
        int topLeftOfTurned = ((30 - 1) * 20 + 0) * 3;
        int bottomLeftOfFirst = 0;
        Check(twice && q4.SequenceEqual(p) && tw == 20 && th == 30 && tw2 == 30 && th2 == 20 && q1[topLeftOfTurned] == p[bottomLeftOfFirst] && q1[topLeftOfTurned + 1] == p[bottomLeftOfFirst + 1],
            "pixels: a quarter turn swaps the sides clockwise, two quarters twice over are the half turn, and four are the photo again");
        // The grade: the natural filter is nothing; black and white has no colour; the sepia is warm; a brighter exposure lifts.
        var g0 = (byte[])a.Clone();
        bool natural = !PhotoPixels.Grade(g0, 200, 120, CameraRules.Filters[0], 0f) && g0.SequenceEqual(a);
        var mono = (byte[])a.Clone();
        PhotoPixels.Grade(mono, 200, 120, CameraRules.Filters[3], 0f);
        bool grey = true;
        for (int i = 0; i < mono.Length; i += 3)
        {
            grey &= Math.Abs(mono[i] - mono[i + 1]) <= 1 && Math.Abs(mono[i + 1] - mono[i + 2]) <= 1;
        }
        var sepia = (byte[])a.Clone();
        PhotoPixels.Grade(sepia, 200, 120, CameraRules.Filters[6], 0f);
        long r = 0, b = 0;
        for (int i = 0; i < sepia.Length; i += 3)
        {
            r += sepia[i];
            b += sepia[i + 2];
        }
        var bright = (byte[])a.Clone();
        PhotoPixels.Grade(bright, 200, 120, CameraRules.Filters[0], 1f);
        long before = a.Sum(v => (long)v), after = bright.Sum(v => (long)v);
        Check(natural && grey && r > b && after > before, "pixels: the natural filter changes nothing, black and white has no colour, sepia is warm and a stop more exposure is lighter");
        // The work of a shot at the photo's size, off the frame: timed.
        var screen4k = new byte[3840 * 2160 * 4];
        for (int i = 0; i < screen4k.Length; i += 4)
        {
            screen4k[i] = (byte)(i >> 12);
            screen4k[i + 3] = 255;
        }
        CameraRules.CropRect(3840, 2160, CameraAspect.FourThree, out int ax, out int ay, out int aw, out int ah);
        PhotoPixels.Fit(aw, ah, 1920, 1080, out int ow, out int oh);
        var photo = new byte[ow * oh * 3];
        var timer = Stopwatch.StartNew();
        PhotoPixels.Shrink(screen4k, 3840, 2160, 4, false, photo, ow, oh, ax, ay, aw, ah);
        double shrinkMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        PhotoPixels.Grade(photo, ow, oh, CameraRules.Filters[8], 0.5f);
        PhotoPixels.Frame(photo, ow, oh, CameraFrame.Polaroid);
        double gradeMs = timer.Elapsed.TotalMilliseconds;
        Console.WriteLine("        camera pixels: a 4K screen cut to 4:3 and shrunk to " + ow + " x " + oh + " in " + shrinkMs.ToString("0") + " ms, graded and framed in " + gradeMs.ToString("0") + " ms (a worker's, never a frame's)");
        Check(shrinkMs < 400 && gradeMs < 300, "pixels: the cut, the shrink, the grade and the frame of a 4K shot take under 0.7 s together on a worker (" + shrinkMs.ToString("0") + " and " + gradeMs.ToString("0") + " ms)");
    }

    // ---------------------------------------------------------------- what the lens saw

    private static void CgSeen()
    {
        var view = new PhotoSeenRules.View(0.25f, 0f, 0.75f, 1f);   // a 1:1 cut of a 2:1 screen
        Check(PhotoSeenRules.InFrame(0.5f, 0.5f, 20f, 150f, view) && !PhotoSeenRules.InFrame(0.1f, 0.5f, 20f, 150f, view) && !PhotoSeenRules.InFrame(0.5f, 0.5f, -3f, 150f, view)
              && !PhotoSeenRules.InFrame(0.5f, 0.5f, 400f, 150f, view) && !PhotoSeenRules.InFrame(0.5f, 1.2f, 20f, 150f, view), "camera sees: a point is in the shot only in front of the lens, in range and inside the part of the screen the photo keeps");
        float nearMiddle = PhotoSeenRules.Off(0.5f, 0.5f, view), edge = PhotoSeenRules.Off(0.7f, 0.9f, view);
        Check(nearMiddle < 0.001f && edge > nearMiddle && PhotoSeenRules.Better(0.01f, 10f, float.NaN, 0f) && PhotoSeenRules.Better(0.02f, 10f, 0.2f, 5f) && !PhotoSeenRules.Better(0.2f, 10f, 0.02f, 50f)
              && PhotoSeenRules.Better(0.105f, 8f, 0.1f, 40f) && !PhotoSeenRules.Better(0.105f, 80f, 0.1f, 40f), "camera sees: the building nearest the middle of the frame is the subject, the nearer of two about as central");
        var s = new PhotoSidecar { Hour = 21, Weather = "rain", Business = "Bloom Cafe", HasPeople = true, People = 7, Boost = true, Kind = (int)CameraKind.Portrait };
        Check(PhotoSeenRules.Tokens(s, false) == "portrait,night,rain,business,crowd,boost" && PhotoSeenRules.Tokens(new PhotoSidecar { Hour = 12, Weather = "sunny" }, true) == "selfie"
              && PhotoSeenRules.Tokens(new PhotoSidecar { Hour = 12, HasPeople = true, People = 2 }, false) == "people" && PhotoSeenRules.Tokens(new PhotoSidecar { Hour = 12 }, false) == null && PhotoSeenRules.People(500) == 99 && PhotoSeenRules.People(-2) == 0,
            "camera sees: a shot is tagged night, the weather when it is not a fair one, a business, a crowd or people, its mode");
        Check(PhotoSeenRules.TokenCount("a,b,c") == 3 && PhotoSeenRules.TokenAt("a,b,c", 1) == "b" && PhotoSeenRules.TokenAt("a,b,c", 3) == null && PhotoSeenRules.TokenCount(null) == 0
              && PhotoSeenRules.TagKey("night") == "reallife_ui_life_tag_night" && PhotoSeenRules.TagKey("rain") == "reallife_ui_hud_weather_rain" && PhotoSeenRules.TagKey("crowd") == "reallife_gallery_tag_crowd", "camera sees: tags are read back one by one and each has its text");
    }

    // ---------------------------------------------------------------- the albums, the search and the picker

    private static List<PhotoEntry> CgPhotos(int count)
    {
        return UiSamples.SamplePhotos(count, 40);
    }

    private static void CgFind()
    {
        List<PhotoEntry> photos = CgPhotos(500);
        var favourites = new HashSet<string>();
        var posted = new HashSet<string>();
        for (int i = 0; i < photos.Count; i++)
        {
            if (i % 7 == 0)
            {
                favourites.Add(photos[i].Name);
            }
            if (i % 11 == 0)
            {
                posted.Add(photos[i].Name);
            }
        }
        var q = new GalleryQuery();
        var shown = new List<PhotoEntry>();
        GalleryFind.Select(photos, q, favourites, posted, shown);
        Check(shown.Count == 500 && shown.Select(p => p.Name).Distinct().Count() == 500, "gallery: with no search, no filter and the album All, all 500 photos are shown");
        // The Flare picker asks the same question, so it offers every photo, not the latest five.
        string picker = Read(@"Ui\Flare\FlareScreen.Photos.cs"), sheet = Read(@"Ui\Flare\FlareScreen.Sheet.cs");
        Check(picker.Contains("GalleryFind.Select(d.Photos, _pickQuery, d.Favourites, d.Posted, _pickShown)") && !Regex.IsMatch(picker, @"PhotosMost|Math\.Min\(\w+, 5\)") && sheet.Contains("_photos.Count + 1")
              && Regex.IsMatch(picker, @"_pickShown = new List<PhotoEntry>\(PhotoStoreRules\.Cap"), "flare picker: it lists the Gallery's whole selection (the same rules, no cap of five), and the compose sheet has a Gallery chip to it");
        var chosen = new GalleryQuery { Kit = GalleryKit.All };
        GalleryFind.Select(photos, chosen, favourites, posted, shown);
        Check(shown.Count == 500, "flare picker: 500 photos in the Gallery are 500 in the picker");
        var counts = new int[(int)GalleryKit.Count];
        GalleryFind.Counts(photos, favourites, posted, counts);
        int selfies = photos.Count(p => p.Selfie || p.Side.Kind == (int)CameraKind.Selfie), businesses = photos.Count(p => !string.IsNullOrEmpty(p.Side.Business));
        Check(counts[0] == 500 && counts[1] == favourites.Count && counts[2] == selfies && counts[3] == businesses && counts[4] == 500 && counts[5] == posted.Count && selfies > 0 && businesses > 0,
            "gallery: the albums count their photos (all, favourites, selfies, businesses, places, posted on Flare)");
        foreach (GalleryKit kit in new[] { GalleryKit.Favourites, GalleryKit.Selfies, GalleryKit.Businesses, GalleryKit.Places, GalleryKit.Flare })
        {
            GalleryFind.Select(photos, new GalleryQuery { Kit = kit }, favourites, posted, shown);
            Check(shown.Count == counts[(int)kit] && shown.All(p => GalleryFind.InKit(p, kit, favourites, posted)), "gallery: the " + kit + " album shows exactly the photos it counts");
        }
        var search = new GalleryQuery { Search = "broadway" };
        GalleryFind.Select(photos, search, favourites, posted, shown);
        Check(shown.Count == photos.Count(p => p.Side.Place == "Broadway") && shown.Count > 0, "gallery: a search finds a place, whatever the case");
        search.Search = "bloom cafe";
        GalleryFind.Select(photos, search, favourites, posted, shown);
        Check(shown.Count == photos.Count(p => p.Side.Business == "Bloom Cafe") && shown.Count > 0, "gallery: a search finds a business, and every word of it must be there");
        search.Search = "40";
        GalleryFind.Select(photos, search, favourites, posted, shown);
        Check(shown.Count == photos.Count(p => p.Day == 40) && shown.Count == 8, "gallery: a number in a search is a day of the game's");
        search.Search = "zzz";
        GalleryFind.Select(photos, search, favourites, posted, shown);
        Check(shown.Count == 0, "gallery: a search that matches nothing shows nothing");
        var filter = new GalleryQuery { Place = "Broadway", Day = 0 };
        GalleryFind.Select(photos, filter, favourites, posted, shown);
        int placeCount = shown.Count;
        filter.Business = "Bloom Cafe";
        GalleryFind.Select(photos, filter, favourites, posted, shown);
        Check(placeCount == photos.Count(p => p.Side.Place == "Broadway") && shown.All(p => p.Side.Place == "Broadway" && p.Side.Business == "Bloom Cafe") && shown.Count <= placeCount,
            "gallery: the place and business filters narrow together");
        var values = new List<string>();
        GalleryFind.Values(photos, GalleryKit.All, favourites, posted, 0, values);
        string cycle = null;
        var seen = new HashSet<string>();
        for (int i = 0; i < values.Count + 2; i++)
        {
            cycle = GalleryFind.Next(values, cycle);
            if (cycle != null)
            {
                seen.Add(cycle);
            }
        }
        GalleryFind.Values(photos, GalleryKit.All, favourites, posted, 2, values);
        Check(seen.Count == 5 && values.Count > 5 && values.Count <= GalleryFind.ValuesMost && GalleryFind.Next(new List<string> { "a" }, "a") == null && GalleryFind.Next(new List<string>(), null) == null,
            "gallery: a filter's values are the album's distinct places, businesses or days, and tapping it goes through them and back to none");
        GalleryFind.Select(photos, new GalleryQuery { Sort = GallerySort.Oldest }, favourites, posted, shown);
        bool oldest = shown[0].Day <= shown[shown.Count - 1].Day;
        GalleryFind.Select(photos, new GalleryQuery { Sort = GallerySort.Place }, favourites, posted, shown);
        bool byPlace = true;
        for (int i = 1; i < shown.Count; i++)
        {
            byPlace &= string.Compare(shown[i - 1].Side.Place, shown[i].Side.Place, StringComparison.OrdinalIgnoreCase) <= 0;
        }
        GalleryFind.Select(photos, new GalleryQuery { Sort = GallerySort.Business }, favourites, posted, shown);
        Check(oldest && byPlace && string.IsNullOrEmpty(shown[shown.Count - 1].Side.Business) && !string.IsNullOrEmpty(shown[0].Side.Business), "gallery: sorting by oldest, place and business, a photo with no business last");
        // Thumbnails are asked for only in view: the album's layout names the photos it laid out.
        var model = new UiAlbumModel();
        GalleryAlbumFill.Fill(model, CgPhotos(500), new GalleryQuery(), favourites, posted, null, 40, 5, 4, 8, "photo:", UiSamples.SampleTexts((k, e) => e), true);
        var scene = new UiScene();
        scene.Begin("gallery", 1920f, 1080f, true);
        UiAlbum.Lay(scene, UiAt.Screen, model, 100f, 100f, 1200f, 700f, UiDefaultKeys.Keyboard);
        int cellsInView = model.ViewLast - model.ViewFirst + 1;
        Check(model.Count == 500 && model.ViewFirst == 0 && cellsInView > 5 && cellsInView <= 60, "gallery: of 500 photos only the rows in view and one either side are laid out and their thumbnails asked for (" + cellsInView + ")");
        // Photos fill their tiles: every cell asks for cover, and the crop is the box's shape, centred, for any photo's shape.
        int covered = 0, pictures = 0;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind == UiKind.Picture)
            {
                pictures++;
                covered += (scene.Items[i].Flags & UiFlags.Cover) != 0 ? 1 : 0;
            }
        }
        Check(pictures > 5 && covered == pictures, "gallery: every photo tile (" + pictures + ") asks for cover, so the picture fills it");
        bool shapes = true;
        foreach (float[] photo in new[] { new[] { 1920f, 1080f }, new[] { 1600f, 1200f }, new[] { 1000f, 1000f }, new[] { 1080f, 1920f } })
        {
            UiCover.Cut(photo[0], photo[1], 200f, 200f, out float cx, out float cy, out float cw, out float ch);
            bool square = Math.Abs(cw - ch) < 0.01f && cw <= photo[0] + 0.01f && ch <= photo[1] + 0.01f;
            bool centred = Math.Abs(cx - (photo[0] - cw) / 2f) < 0.01f && Math.Abs(cy - (photo[1] - ch) / 2f) < 0.01f;
            bool largest = Math.Abs(cw - photo[0]) < 0.01f || Math.Abs(ch - photo[1]) < 0.01f;   // as much of the photo as the shape allows
            shapes &= square && centred && largest;
        }
        UiCover.Cut(1920f, 1080f, 200f, 200f, out float sx, out _, out float sw, out float sh);
        UiCover.Cut(1080f, 1920f, 200f, 200f, out _, out float ty, out float tw, out float th);
        UiCover.Cut(1920f, 1080f, 320f, 180f, out float wx, out _, out float ww, out float wh);
        Check(shapes && Math.Abs(sw - 1080f) < 0.01f && Math.Abs(sx - 420f) < 0.01f && sh == 1080f && Math.Abs(tw - 1080f) < 0.01f && Math.Abs(th - 1080f) < 0.01f
              && Math.Abs(ty - 420f) < 0.01f && wx == 0f && ww == 1920f && wh == 1080f,
            "gallery: a 16:9, 4:3, 1:1 or 9:16 photo cut for a square tile is its centred square, and for its own shape it is left whole");
        model.Offset = 1e9f;
        UiAlbum.Lay(scene, UiAt.Screen, model, 100f, 100f, 1200f, 700f, UiDefaultKeys.Keyboard);
        Check(model.ViewLast == 499 && model.ViewFirst > 400, "gallery: scrolled to the end, it is the last photos in view");
    }

    // ---------------------------------------------------------------- deleting

    private static void CgDelete(string folder)
    {
        Directory.CreateDirectory(folder);
        string name = PhotoStoreRules.NameFor(new DateTime(2026, 11, 4, 12, 0, 0), false, 40, 21, 1);
        string photo = System.IO.Path.Combine(folder, name);
        File.WriteAllBytes(photo, new byte[] { 1, 2, 3 });
        PhotoThumbs.Write(PhotoThumbs.FileOf(photo), new byte[8 * 8 * 3], 8, 8);
        PhotoSidecarFile.Write(photo, new PhotoSidecar { Place = "x", Hour = 21, Day = 40 });
        Check(File.Exists(photo) && File.Exists(PhotoThumbs.FileOf(photo)) && File.Exists(PhotoSidecarRules.PathOf(photo)), "gallery delete: a photo has its file, its small copy and its sidecar");
        Check(PhotoStore.Delete(folder, name) && !File.Exists(photo) && !File.Exists(PhotoThumbs.FileOf(photo)) && !File.Exists(PhotoSidecarRules.PathOf(photo)),
            "gallery delete: deleting a photo deletes its file, its small copy and its sidecar");
        Check(PhotoStore.Delete(folder, name) && !PhotoStore.Delete(folder, "..\\x.jpg"), "gallery delete: deleting a photo twice is fine, and a name that is a path is refused");
    }

    // ---------------------------------------------------------------- the open cost

    private static void CgOpenCost()
    {
        List<PhotoEntry> photos = CgPhotos(500);
        var favourites = new HashSet<string>(photos.Where((p, i) => i % 7 == 0).Select(p => p.Name));
        var posted = new HashSet<string>(photos.Where((p, i) => i % 11 == 0).Select(p => p.Name));
        var texts = UiSamples.SampleTexts((k, e) => e);
        var q = new GalleryQuery();
        var shown = new List<PhotoEntry>(600);
        var album = new UiAlbumModel();
        var places = new List<string>();
        var businesses = new List<string>();
        var days = new List<string>();
        var counts = new int[(int)GalleryKit.Count];
        var scene = new UiScene();
        double best = double.MaxValue;
        for (int run = 0; run < 7; run++)
        {
            var t = Stopwatch.StartNew();
            GalleryFind.Select(photos, q, favourites, posted, shown);
            GalleryFind.Counts(photos, favourites, posted, counts);
            GalleryFind.Values(photos, q.Kit, favourites, posted, 0, places);
            GalleryFind.Values(photos, q.Kit, favourites, posted, 1, businesses);
            GalleryFind.Values(photos, q.Kit, favourites, posted, 2, days);
            GalleryAlbumFill.Fill(album, shown, q, favourites, posted, null, 40, places.Count, businesses.Count, days.Count, "photo:", texts, true);
            scene.Begin("gallery", 1920f, 1080f, true);
            LifePages.Gallery(scene, UiFrame.FullMenu(16f / 9f, "Gallery"), true, UiDefaultKeys.Keyboard, "Gallery", new UiNavEntry[6], 0, false, album, null, "Move", "Select", "Back");
            best = Math.Min(best, t.Elapsed.TotalMilliseconds);
        }
        Console.WriteLine("        gallery: the open of 500 photos (selected, counted, filled, laid out) takes " + best.ToString("0.00") + " ms the best of seven, wanted under " + 8.0.ToString("0") + " ms (the kit click budget, UiCost.ClickBudgetMs)");
        Check(best < 8.0, "gallery: opening with 500 photos costs " + best.ToString("0.00") + " ms of the kit's own work, under its " + 8.0.ToString("0") + " ms");
        // The listing of 500 photos' sidecars (a worker's, once a save): 500 small files.
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo_cg_list_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            for (int i = 0; i < 500; i++)
            {
                string photo = System.IO.Path.Combine(folder, photos[i].Name);
                File.WriteAllBytes(photo, new byte[] { 1 });
                PhotoSidecarFile.Write(photo, photos[i].Side);
            }
            var listed = new List<PhotoEntry>();
            var w = Stopwatch.StartNew();
            PhotoStore.List(folder, listed);
            int read = PhotoStore.ReadSides(folder, listed);
            double ms = w.Elapsed.TotalMilliseconds;
            Console.WriteLine("        gallery: a worker lists 500 photos and reads their 500 sidecars in " + ms.ToString("0") + " ms, once when a save is followed");
            Check(listed.Count == 500 && read == 500 && ms < 3000, "gallery: 500 photos are listed with their sidecars on a worker in " + ms.ToString("0") + " ms");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // ---------------------------------------------------------------- the sources

    private static void CgSources()
    {
        string mode = Read(@"Photos\CameraMode.cs"), view = Read(@"Photos\CameraView.cs"), host = Read(@"Ui\Life\ViewfinderHost.cs"), cache = Read(@"Photos\PhotoThumbCache.cs"), director = Read(@"Photos\PhotosDirector.cs");
        // Nothing on a frame while the camera is shut.
        string close = Between(mode, "public void Close(string why)", "private void GiveBack()");
        string awake = Between(mode, "private void Awake()", "private void OnDestroy()");
        string update = Between(mode, "private void OnUpdate()", "// The timer:");
        Check(awake.Contains("enabled = false;") && close.Contains("enabled = false;") && update.StartsWith("private void OnUpdate()") && Regex.IsMatch(update, @"if \(!Active\)\s*\{\s*enabled = false;\s*return;")
              && close.Contains("_countStart = -1f") && close.Contains("Ui.ViewfinderHost.Hide()") && Between(host, "public static void Hide()", "public static void ForShot(").Contains("SetActive(false)"),
            "camera idle: shut, the camera's component is off (no Update runs), its timer is stopped and the viewfinder's chrome is switched off");
        var frames = new List<string>();
        foreach (string file in Directory.GetFiles(System.IO.Path.Combine(PhotoScripts, "Photos"), "*.cs"))
        {
            if (Regex.IsMatch(File.ReadAllText(file), @"\b(?:private|public|internal)?\s*void (?:Update|LateUpdate|FixedUpdate)\(\)"))
            {
                frames.Add(System.IO.Path.GetFileName(file));
            }
        }
        frames.Sort(StringComparer.Ordinal);
        Check(string.Join(",", frames) == "CameraMode.cs,GalleryParts.cs" && !cache.Contains("MonoBehaviour :") && !Regex.IsMatch(cache + Read(@"Photos\PhotosSeen.cs") + Read(@"Photos\PhotoSeenRules.cs"), @"void (?:Update|LateUpdate)\(")
              && !Regex.IsMatch(director, @"void (?:Update|LateUpdate|FixedUpdate)\(\)") && !host.Contains("Update("),
            "camera idle: the only per-frame code of the Camera and Gallery is the open camera's (off while shut) and the old viewer's keys (off unless a photo is shown): " + string.Join(",", frames));
        Check(Regex.IsMatch(mode, @"private void Focus\(\)\s*\{\s*if \(_kind != CameraKind\.Portrait \|\| !CameraRules\.FocusDue\(_lookDue, Time\.unscaledTime, _lastLook\)\)") && Regex.Matches(mode, @"PhotosGame\.SubjectAt\(").Count == 1
              && Read(@"Photos\PhotosGame.cs").Contains("Physics.Raycast(t.position, t.forward, out RaycastHit hit"), "camera idle: the portrait's one ray is cast only in the portrait mode, after the aim changed, at most five times a second");
        Check(Regex.Matches(mode, @"PhotosSeen\.Look\(").Count == 1 && Between(mode, "private PhotoSidecar Note(", "// The controls away").Contains("PhotosSeen.Look(") && !Regex.IsMatch(Between(mode, "private void OnUpdate()", "private static readonly int HotLateUpdate"), @"PhotosSeen"),
            "camera idle: what the lens sees (the buildings and people in the frame) is looked at once a shot, never on a frame");
        // The live effects are one volume, the zoom is the field of view.
        string game = Read(@"Photos\PhotosGame.cs");
        Check(game.Contains("blur = profile.Add<DepthOfField>()") && game.Contains("color = profile.Add<ColorAdjustments>()") && Regex.Matches(game, @"ScriptableObject\.CreateInstance<VolumeProfile>\(\)").Count == 1 && mode.Contains("_camera.fieldOfView = fov;")
              && !mode.Contains("Instantiate(") && Regex.IsMatch(Between(game, "public static void DropGrade(", "/// <summary>The screen read"), @"CodeAssets\.DropProfile"),
            "camera live: the filter, the exposure and the portrait's blur are one post effect volume made on opening and destroyed on every way out; the zoom is the field of view");
        // Frames are no longer read off the screen: the view puts them away for the shot and the worker burns them in.
        Check(view.Contains("FlareKit.Show(_frame, !shot)") && game.Contains("PhotoPixels.Frame(photo.AsSpan(), w, h, shot.Frame)") && Regex.IsMatch(mode, @"shot\.Frame = _frame;") && mode.Contains("CameraRules.CropRect(shot.Width, shot.Height, aspect"),
            "camera shot: the cut and the frame are the worker's, the viewfinder's guide, shape and frame are put away for the frame that is read");
        // Thumbnails: only in view, capped, off the frame.
        Check(cache.Contains("Most = 120") && cache.Contains("Task.Run(") && cache.Contains("BudgetMs = 1.0") && Regex.IsMatch(cache, @"if \(_held\.Count <= Most\)") && Read(@"Ui\Life\GalleryScreen.cs").Contains("d.Thumbs.Want(d, d.Folder, _shown, _album.ViewFirst, _album.ViewLast)")
              && Read(@"Ui\Flare\FlareScreen.Photos.cs").Contains("d.Thumbs.Want(d, d.Folder, _pickShown, album.ViewFirst, album.ViewLast)"),
            "gallery thumbnails: the kit's Gallery and Flare's picker ask for the photos in view only, read on a worker, made textures within a millisecond a frame, at most 120 kept");
        Check(Regex.Matches(Read(@"Ui\Life\GalleryPictures.cs"), @"UiPictures\.Answer\(Find\);").Count == 1 && !Regex.IsMatch(Read(@"Ui\Life\GalleryScreen.cs"), @"UiPictures\.(?:Answer|Find)\b"),
            "gallery pictures: one answer among the kit's pictures, added once a session, and none from the Gallery's screen, so nothing can wrap another round in a ring");
        // The edit saves a new photo and never touches the original.
        string edit = Between(director, "internal IEnumerator SaveEdit(", "/// <summary>A token for a worker's answer");
        Check(edit.Contains("PhotoStore.NewPath(") && edit.Contains("PhotosGame.Finish(edit, path)") && edit.Contains("side.Original = original.Name") && !edit.Contains("File.Delete") && !edit.Contains("WriteWhole(System.IO.Path.Combine(folder, original"),
            "gallery edit: an edit is saved as a new photo with its own small copy and sidecar, saying which photo it came from; the original is not written to");
        string screen = Read(@"Ui\Life\GalleryScreen.Edit.cs");
        Check(screen.Contains("PhotosGame.Preview(snap, PreviewLong") && screen.Contains("new NativeArray<byte>(_edit.Pixels, Allocator.Persistent)") && screen.Contains("work.ContinueWith(_ => PhotosGame.Drop(edit))"),
            "gallery edit: the preview and the save are a worker's, the save works on a copy of the pixels, and the pixels outlive a preview still running");
        // The Flare change stays small: a sheet, a field, two commands and the compose sheet's chip.
        string flare = Read(@"Ui\Flare\FlareScreen.cs"), sheetCode = Read(@"Ui\Flare\FlareScreen.Sheet.cs");
        Check(!Read(@"Social\Flare\FlareDirector.Kit.cs").Contains("PhotoThumbCache") && sheetCode.Contains("return OpenPicker();") && flare.Contains("PickerPhotoCommand") && flare.Contains("PickerAlbumCommand"),
            "flare picker: the change is the picker's sheet, its field and its two commands, and the compose sheet's Gallery chip; the director and the city's posts are untouched");
    }

    // ---------------------------------------------------------------- Esc and Close

    private static void CgEscapes()
    {
        // The viewfinder in every state: Back and the Close button both close the camera.
        foreach (string state in new[] { "photo", "saved", "selfie", "portrait", "wide", "timer", "zoom", "night", "clean" })
        {
            var screen = new ViewfinderScreen();
            int closed = 0;
            screen.Actions.Close = () => closed++;
            var scene = new UiScene();
            UiSamples.Viewfinder(scene, UiFrame.FullMenu(16f / 9f, ""), true, UiDefaultKeys.Keyboard, UiWords("en"), state);
            bool target = scene.FindNode(LifePages.VClose, 0) >= 0 || state == "clean";   // the clean view keeps only the way back; Esc still closes
            bool back = screen.Back();
            bool press = screen.Press(LifePages.VClose, 0);
            Check(back && press && closed == 2 && target, "camera ways out: in the " + state + " state Esc and the Close button each close the camera (" + closed + ")");
        }
        string mode = Read(@"Photos\CameraMode.cs");
        string update = Between(mode, "private void OnUpdate()", "// The timer:");
        Check(update.Contains("TakeCancel()") && Between(update, "TakeCancel()", "TakeConfirm()").Contains("Close(\"the cancel key\")") && Between(mode, "public void Close(string why)", "private void GiveBack()").Contains("_countStart = -1f"),
            "camera ways out: the cancel key closes the camera in any state, a timer counting included");
        string gallery = Read(@"Ui\Life\GalleryScreen.cs");
        string back2 = Between(gallery, "public bool Back()", "public void Tab(int step)");
        int e = back2.IndexOf("_editing", StringComparison.Ordinal), v = back2.IndexOf("_viewing", StringComparison.Ordinal), s2 = back2.IndexOf("_select", StringComparison.Ordinal), f = back2.LastIndexOf("return false;", StringComparison.Ordinal);
        Check(e >= 0 && e < v && v < s2 && s2 < f, "gallery ways out: Esc leaves the editor, then the photo, then select mode, then (false) lets the window close; the window's own Close button closes at once");
        Check(Read(@"Photos\CameraApp.cs").Contains("Close = () => RealLifeOverhaul.Ui.UiShell.CloseMenu()") && Read(@"Ui\Life\CameraScreen.cs").Contains("public bool Back() => Do(CameraAnswer.Close);"),
            "camera ways out: the camera's own page (when the camera is refused) closes on Esc and its Close button");
        Check(Regex.IsMatch(Between(mode, "private void Gallery()", "// The shutter:"), @"Close\(""the Gallery button""\);\s*PhotosDirector\.Instance\?\.OpenLatestPhoto\(\);") && Read(@"Ui\Life\GalleryScreen.cs").Contains("Director?.OpenCamera(false, false)"),
            "camera ways out: the camera opens into the Gallery and the Gallery back into the camera");
    }
}

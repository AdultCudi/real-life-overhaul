using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The Planner, the Calendar, the camera when it cannot open, the Gallery and the viewfinder, over the streets they stand on.
    // The mod's own English (Locales/en.json), so the Gallery and camera show their real words, not their keys.
    private static UiWords _modEnglish;
    private static UiWords ModEnglish => _modEnglish ?? (_modEnglish = ReadModEnglish());

    private static UiWords ReadModEnglish()
    {
        var words = new Dictionary<string, string>();
        DialogRead(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", "en.json")), words);
        return (key, english) => key != null && words.TryGetValue(key, out string said) ? said : english;
    }

    static partial void AddLife(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => key != null && (key.StartsWith("photo:", StringComparison.Ordinal) || key == "viewfinder:latest" || key == "photoedit:preview") ? LifePicture(key) : before?.Invoke(key);
        screens.Add(new Screen { Name = "planner_today", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, ModEnglish, false), HoverCommand = UiBoard.ShiftCommand, HoverArg = 1, Walk = Moves("DRR") });
        screens.Add(new Screen { Name = "planner_today_empty", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, ModEnglish, true), HoverCommand = LifePages.Chip, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_upcoming", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerUpcoming(s, f, d, k, ModEnglish), HoverCommand = LifePages.Row, HoverArg = 0, Walk = Moves("DD") });
        screens.Add(new Screen { Name = "planner_week", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerWeek(s, f, d, k, ModEnglish), HoverCommand = UiBoard.ShiftCommand, HoverArg = 0, Walk = Moves("DR") });
        screens.Add(new Screen { Name = "planner_notes", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerNotes(s, f, d, k, ModEnglish, false), HoverCommand = LifePages.Add, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "planner_notes_empty", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerNotes(s, f, d, k, ModEnglish, true), HoverCommand = LifePages.Add, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "planner_jump", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, ModEnglish, false, 4), HoverCommand = UiBoard.ActionCommand, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_day", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerDay(s, f, d, k, ModEnglish), HoverCommand = LifePages.Row, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "planner_entry", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerEntry(s, f, d, k, ModEnglish, false), HoverCommand = UiPlanEntry.ActionCommand, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_entry_note", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerEntry(s, f, d, k, ModEnglish, true), HoverCommand = UiPlanEntry.ActionCommand, HoverArg = 3, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_note_form", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerForm(s, f, d, k, ModEnglish, false), HoverCommand = UiPlanForm.FieldCommand, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "planner_settings", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerForm(s, f, d, k, ModEnglish, true), HoverCommand = UiPlanForm.FieldCommand, HoverArg = 1, Walk = Moves("D") });
        screens.Add(new Screen { Name = "calendar_month", AppName = "Calendar", Icon = UiIcon.Calendar, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.CalendarMonth(s, f, d, k, ModEnglish, false), HoverCommand = UiDiary.DayCommand, HoverArg = 2, Walk = Moves("DRR") });
        screens.Add(new Screen { Name = "calendar_empty", AppName = "Calendar", Icon = UiIcon.Calendar, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.CalendarMonth(s, f, d, k, ModEnglish, true), HoverCommand = UiDiary.DayCommand, HoverArg = 2, Walk = Moves("R") });
        screens.Add(new Screen { Name = "camera_refused", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.CameraRefused(s, f, d, k, ModEnglish), HoverCommand = UiDecision.ButtonCommand, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_few", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 3, "grid"), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_full", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 300, "grid"), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_500", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 500, "grid"), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_empty", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 0, "grid"), HoverCommand = UiAlbum.Sort, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_select", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 40, "select"), HoverCommand = UiAlbum.Delete, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_search", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 120, "search"), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_flare", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, ModEnglish, 120, "flare"), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_viewer", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryViewer(s, f, d, k, ModEnglish, false), HoverCommand = UiPhotoViewer.Action, HoverArg = 1, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_viewer_old", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryViewer(s, f, d, k, ModEnglish, true), HoverCommand = UiPhotoViewer.Action, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_edit", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryEdit(s, f, d, k, ModEnglish), HoverCommand = UiPhotoEdit.Save, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "photo"), HoverCommand = LifePages.VShutter, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_saved", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "08.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "saved"), HoverCommand = LifePages.VOpen, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_selfie", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "selfie"), HoverCommand = LifePages.VTool, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_portrait", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "portrait"), HoverCommand = LifePages.VLens, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_wide", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "08.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "wide"), HoverCommand = LifePages.VZoomIn, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_timer", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "timer"), HoverCommand = LifePages.VShutter, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_zoom", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "zoom"), HoverCommand = LifePages.VExposureUp, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_night", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "08.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "night"), HoverCommand = LifePages.VTool, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_clean", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, ModEnglish, "clean"), HoverCommand = LifePages.VClean, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_longest", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, LifePortuguese, false), HoverCommand = UiBoard.ShiftCommand, HoverArg = 1, Walk = Moves("DR") });
    }

    private static readonly Dictionary<string, Sprite> LifePictures = new Dictionary<string, Sprite>();
    private static string _lifeThumbs;

    // A photo of the Gallery by its key: one of the ten of the mockup's street pictures, by the key's own hash; once each.
    private static Sprite LifePicture(string key)
    {
        if (LifePictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        int hash = 0;
        for (int i = 0; i < key.Length; i++)
        {
            hash = hash * 31 + key[i];
        }
        string folder = LifeThumbs();
        string path = Path.Combine(folder, "g" + (Math.Abs(hash) % 10).ToString("00") + ".jpg");
        if (folder.Length > 0 && File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(path));
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        LifePictures[key] = sprite;
        return sprite;
    }

    // The mockups' thumbnails: not-in-repo beside the repository (or above a worktree of it), never committed.
    private static string LifeThumbs()
    {
        if (_lifeThumbs != null)
        {
            return _lifeThumbs;
        }
        _lifeThumbs = string.Empty;
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
        {
            string thumbs = Path.Combine(dir.FullName, "not-in-repo", "previews", "ui", "mockups", "life", "thumbs");
            if (Directory.Exists(thumbs))
            {
                _lifeThumbs = thumbs;
                break;
            }
        }
        return _lifeThumbs;
    }

    private static Dictionary<string, string> _lifePortuguese;

    // Portuguese, the long labels among the three languages this job writes.
    private static string LifePortuguese(string key, string english)
    {
        if (_lifePortuguese == null)
        {
            _lifePortuguese = new Dictionary<string, string>();
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", "pt.json"));
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    int q = line.IndexOf('"');
                    if (q < 0)
                    {
                        continue;
                    }
                    int q2 = line.IndexOf('"', q + 1);
                    int c = line.IndexOf(':', q2 + 1);
                    int v = c < 0 ? -1 : line.IndexOf('"', c + 1);
                    int v2 = v < 0 ? -1 : line.LastIndexOf('"');
                    if (q2 > q && v2 > v)
                    {
                        _lifePortuguese[line.Substring(q + 1, q2 - q - 1)] = line.Substring(v + 1, v2 - v - 1);
                    }
                }
            }
        }
        return key != null && _lifePortuguese.TryGetValue(key, out string words) ? words : english;
    }
}

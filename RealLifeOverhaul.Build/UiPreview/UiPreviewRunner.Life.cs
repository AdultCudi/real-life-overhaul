using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Planner, the Calendar, the camera when it cannot open, the Gallery and the viewfinder, over the streets they stand on.
    static partial void AddLife(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "planner_today", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, English, false), HoverCommand = UiBoard.ShiftCommand, HoverArg = 1, Walk = Moves("DRR") });
        screens.Add(new Screen { Name = "planner_today_empty", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, English, true), HoverCommand = LifePages.Chip, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_upcoming", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerUpcoming(s, f, d, k, English), HoverCommand = LifePages.Row, HoverArg = 0, Walk = Moves("DD") });
        screens.Add(new Screen { Name = "planner_week", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerWeek(s, f, d, k, English), HoverCommand = UiBoard.ShiftCommand, HoverArg = 0, Walk = Moves("DR") });
        screens.Add(new Screen { Name = "planner_notes", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerNotes(s, f, d, k, English, false), HoverCommand = LifePages.Add, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "planner_notes_empty", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerNotes(s, f, d, k, English, true), HoverCommand = LifePages.Add, HoverArg = 0, Walk = Moves("D") });
        screens.Add(new Screen { Name = "calendar_month", AppName = "Calendar", Icon = UiIcon.Calendar, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.CalendarMonth(s, f, d, k, English, false), HoverCommand = UiDiary.DayCommand, HoverArg = 2, Walk = Moves("DRR") });
        screens.Add(new Screen { Name = "calendar_empty", AppName = "Calendar", Icon = UiIcon.Calendar, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.CalendarMonth(s, f, d, k, English, true), HoverCommand = UiDiary.DayCommand, HoverArg = 2, Walk = Moves("R") });
        screens.Add(new Screen { Name = "camera_refused", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.CameraRefused(s, f, d, k, English), HoverCommand = UiDecision.ButtonCommand, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_few", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, English, 3, false), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_full", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, English, 300, false), HoverCommand = UiAlbum.Photo, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_empty", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, English, 0, false), HoverCommand = UiAlbum.Sort, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_select", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "02.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryGrid(s, f, d, k, English, 12, true), HoverCommand = UiAlbum.Delete, HoverArg = 3, Walk = Moves("R") });
        screens.Add(new Screen { Name = "gallery_viewer", AppName = "Gallery", Icon = UiIcon.Image, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.GalleryViewer(s, f, d, k, English), HoverCommand = UiPhotoViewer.Action, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "12.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, English, false), HoverCommand = LifePages.VShutter, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "viewfinder_saved", AppName = "Camera", Icon = UiIcon.Camera, Backdrop = "08.jpg",
            Lay = (s, f, d, k) => UiSamples.Viewfinder(s, f, d, k, English, true), HoverCommand = LifePages.VOpen, HoverArg = 0, Walk = Moves("R") });
        screens.Add(new Screen { Name = "planner_longest", AppName = "Planner", Icon = UiIcon.Clock, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.PlannerToday(s, f, d, k, LifePortuguese, false), HoverCommand = UiBoard.ShiftCommand, HoverArg = 1, Walk = Moves("DR") });
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

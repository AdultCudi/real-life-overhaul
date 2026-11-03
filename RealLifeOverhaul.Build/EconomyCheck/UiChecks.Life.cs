using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Planner, the Calendar, the Camera's refusal, the Gallery and the viewfinder.
internal static partial class Program
{
    private static readonly int[] LifePlanCommands =
    {
        UiAppWindow.NavCommand, LifePages.Chip, LifePages.Row, LifePages.Add, LifePages.Second, LifePages.Locate,
        UiBoard.ShiftCommand, UiBoard.LaneCommand, UiBoard.DayCommand, UiBoard.ActionCommand,
    };

    private static readonly int[] LifeCalCommands =
    {
        UiDiary.DayCommand, UiDiary.MonthCommand, UiDiary.EntryCommand, LifePages.Add, LifePages.Second,
    };

    private static readonly int[] LifeGalleryCommands =
    {
        UiAppWindow.NavCommand, UiAlbum.Photo, UiAlbum.Sort, UiAlbum.Fav, UiAlbum.Select, UiAlbum.Delete,
        UiPhotoViewer.Action, UiPhotoViewer.Neighbour, UiPhotoViewer.Step, UiPhotoViewer.Fact,
    };

    private static readonly int[] LifeFinderCommands =
    {
        LifePages.VMode, LifePages.VFilter, LifePages.VFrame, LifePages.VShutter, LifePages.VClose, LifePages.VGrid, LifePages.VOpen,
    };

    static partial void UiCasesLife(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "planner":
                cases.Add(new UiScreenCase { Def = def, Name = "planner (a busy day)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerToday(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (an empty day)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerToday(s, f, d, k, t, true) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (upcoming)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerUpcoming(s, f, d, k, t) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (a week)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerWeek(s, f, d, k, t) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (notes)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerNotes(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (no notes)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerNotes(s, f, d, k, t, true) });
                break;
            case "calendar":
                cases.Add(new UiScreenCase { Def = def, Name = "calendar (a full month)", Title = "Calendar", Commands = LifeCalCommands,
                    Lay = (s, f, d, k, t) => UiSamples.CalendarMonth(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "calendar (an empty month)", Title = "Calendar", Commands = LifeCalCommands,
                    Lay = (s, f, d, k, t) => UiSamples.CalendarMonth(s, f, d, k, t, true) });
                break;
            case "camera":
                cases.Add(new UiScreenCase { Def = def, Name = "camera (refused)", Title = "Camera", Commands = DecisionCommands,
                    Lay = (s, f, d, k, t) => UiSamples.CameraRefused(s, f, d, k, t) });
                break;
            case "photo_gallery":
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (a few photos)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 3, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (full)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 300, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (empty)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 0, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (selecting)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 12, true) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (a photo)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryViewer(s, f, d, k, t) });
                break;
            case "viewfinder":
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder", Title = "Camera", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (saved)", Title = "Camera", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, true) });
                break;
        }
    }
}

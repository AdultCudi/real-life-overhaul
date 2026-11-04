using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Planner, the Calendar, the Camera's refusal, the Gallery and the viewfinder.
internal static partial class Program
{
    private static readonly int[] LifePlanCommands =
    {
        UiAppWindow.NavCommand, LifePages.Chip, LifePages.Row, LifePages.Add, LifePages.Second, LifePages.Locate,
        UiBoard.ShiftCommand, UiBoard.LaneCommand, UiBoard.DayCommand, UiBoard.ActionCommand, LifePages.Today,
        UiPlanDay.RowCommand, UiPlanDay.LocateCommand, UiPlanDay.LaterCommand, UiPlanPage.BackCommand, UiPlanPage.SettingsCommand,
        UiPlanEntry.ActionCommand, UiPlanEntry.LessCommand, UiPlanEntry.MoreCommand,
        UiPlanForm.FieldCommand, UiPlanForm.LessCommand, UiPlanForm.MoreCommand, UiPlanForm.OptionCommand, UiPlanForm.ActionCommand, UiPlanForm.PlaceCommand,
    };

    private static readonly int[] LifeCalCommands =
    {
        UiDiary.DayCommand, UiDiary.MonthCommand, UiDiary.EntryCommand, LifePages.Add, LifePages.Second, LifePages.Today,
    };

    private static readonly int[] LifeGalleryCommands =
    {
        UiAppWindow.NavCommand, UiAlbum.Photo, UiAlbum.Sort, UiAlbum.Fav, UiAlbum.Select, UiAlbum.Delete,
        UiPhotoViewer.Action, UiPhotoViewer.Neighbour, UiPhotoViewer.Step, UiPhotoViewer.Fact,
        UiAlbum.Search, UiAlbum.Share, UiAlbum.Camera, UiAlbum.FilterPlace, UiAlbum.FilterBusiness, UiAlbum.FilterDay, UiAlbum.FilterClear,
        UiPhotoEdit.Filter, UiPhotoEdit.Frame, UiPhotoEdit.Shape, UiPhotoEdit.Turn, UiPhotoEdit.Save, UiPhotoEdit.Reset, UiPhotoEdit.Cancel,
    };

    private static readonly int[] LifeFinderCommands =
    {
        LifePages.VMode, LifePages.VFilter, LifePages.VFrame, LifePages.VShutter, LifePages.VClose, LifePages.VOpen, LifePages.VZoomOut, LifePages.VZoomIn,
        LifePages.VLens, LifePages.VExposureDown, LifePages.VExposureUp, LifePages.VTool, LifePages.VFlip, LifePages.VFilterPage, LifePages.VClean,
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
                cases.Add(new UiScreenCase { Def = def, Name = "planner (jumped to now)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerToday(s, f, d, k, t, false, 4) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (a day)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerDay(s, f, d, k, t) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (an entry)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerEntry(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (a note's page)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerEntry(s, f, d, k, t, true) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (the note form)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerForm(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "planner (settings)", Title = "Planner", Commands = LifePlanCommands,
                    Lay = (s, f, d, k, t) => UiSamples.PlannerForm(s, f, d, k, t, true) });
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
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 3, "grid") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (full)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 300, "grid") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (500 photos)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 500, "grid") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (empty)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 0, "grid") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (selecting)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 40, "select") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (a search and a filter)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 120, "search") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (the Flare album)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryGrid(s, f, d, k, t, 120, "flare") });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (a photo)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryViewer(s, f, d, k, t, false) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (an old photo)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryViewer(s, f, d, k, t, true) });
                cases.Add(new UiScreenCase { Def = def, Name = "gallery (editing)", Title = "Gallery", Commands = LifeGalleryCommands,
                    Lay = (s, f, d, k, t) => UiSamples.GalleryEdit(s, f, d, k, t) });
                break;
            case "viewfinder":
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "photo") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (saved)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "saved") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (selfie)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "selfie") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (portrait)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "portrait") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (wide)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "wide") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (timer counting)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "timer") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (zoomed)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "zoom") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (night)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "night") });
                cases.Add(new UiScreenCase { Def = def, Name = "viewfinder (clean view)", Title = "", Commands = LifeFinderCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.Viewfinder(s, f, d, k, t, "clean") });
                break;
        }
    }
}

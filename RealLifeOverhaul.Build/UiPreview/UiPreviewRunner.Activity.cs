using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The activity kit over the world it stands in: a working day before it starts in the office and while it runs, a class
    // at a desk, a desk session's time in the chair, cooking at home at night, a bus ride on the street, the time skip at
    // night, the time of day tool indoors, the funeral, and the working day's card in German, the longest of the languages.
    static partial void AddActivity(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "activity_work_before", AppName = "Work", Icon = UiIcon.Briefcase, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityCardScene(s, f, d, k, English, UiSamples.ActivityWork(English, k, UiActivityState.NotStarted)),
            HoverCommand = UiActivity.ButtonCommand, HoverArg = 2, Walk = Moves("DRRLU") });
        screens.Add(new Screen { Name = "activity_work_running", AppName = "Work", Icon = UiIcon.Briefcase, Backdrop = "00.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityStripScene(s, f, d, k, English, UiSamples.ActivityWork(English, k, UiActivityState.Running)),
            HoverCommand = UiActivity.SkipCommand, Walk = Moves("LRR") });
        screens.Add(new Screen { Name = "activity_class", AppName = "Class", Icon = UiIcon.Cap, Backdrop = "03.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityCardScene(s, f, d, k, English, UiSamples.ActivityClass(English, k)),
            HoverCommand = UiActivity.StepCommand, Walk = Moves("DRRLU") });
        screens.Add(new Screen { Name = "activity_chair", AppName = "Desk", Icon = UiIcon.Presentation, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityStripScene(s, f, d, k, English, UiSamples.ActivityChair(English, k)),
            HoverCommand = UiActivity.CancelCommand, Walk = Moves("LRR") });
        screens.Add(new Screen { Name = "activity_cook", AppName = "Home", Icon = UiIcon.ForkKnife, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityStripScene(s, f, d, k, English, UiSamples.ActivityCook(English, k), true),
            HoverCommand = UiActivity.FastCommand, Walk = Moves("LRR") });
        screens.Add(new Screen { Name = "activity_bus", AppName = "Bus", Icon = UiIcon.Train, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityStripScene(s, f, d, k, English, UiSamples.ActivityBus(English, k)),
            HoverCommand = UiActivity.SkipCommand, Walk = Moves("LR") });
        screens.Add(new Screen { Name = "activity_skip", AppName = "Skip time", Icon = UiIcon.SkipForward, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivitySkipScene(s, f, d, k, English), HoverCommand = UiTimeSkip.CancelCommand, Walk = Moves("D") });
        screens.Add(new Screen { Name = "activity_time_of_day", AppName = "Time of day", Icon = UiIcon.Sun, Backdrop = "03.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityDayToolScene(s, f, d, k, English), HoverCommand = UiTimeOfDay.StepCommand, HoverArg = 1, Walk = Moves("DRUL") });
        screens.Add(new Screen { Name = "activity_funeral", AppName = "Funeral", Icon = UiIcon.Flag, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.ActivityFuneralScene(s, f, d, k, English), HoverCommand = UiSideCard.ChoiceCommand, HoverArg = 1, Walk = Moves("DDDRUU") });
        screens.Add(new Screen { Name = "activity_longest", AppName = "Arbeit", Icon = UiIcon.Briefcase, Backdrop = "05.jpg",
            Lay = (s, f, d, k) =>
            {
                UiSamples.ActivityGerman = true;
                try
                {
                    UiSamples.ActivityCardScene(s, f, d, k, BizGerman, UiSamples.ActivityWork(BizGerman, k, UiActivityState.NotStarted));
                }
                finally
                {
                    UiSamples.ActivityGerman = false;
                }
            },
            HoverCommand = UiActivity.ButtonCommand, HoverArg = 2, Walk = Moves("DRRLU") });
    }
}

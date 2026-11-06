using System;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The desk of the game's recruitment offices (Anderson Recruitment Corp and the others). The game's own dialogue refuses
    /// a player without the Basic HR course before it says anything else, so nobody could register for work there. Sitting at
    /// the desk now always offers two ways, in the game's dialogue panel: register with the jobs board (no course needed) or
    /// recruitment, which is the game's own dialogue as it was, its Basic HR page included. Taken over only while the
    /// working life is on; the game's maker is put back when it stops. Cost: nothing per frame; one dialogue per seat.
    /// </summary>
    public static class AgencyDesk
    {
        private static CareersDirector Director => CareersDirector.Instance;

        /// <summary>The working life is on: the desk's dialogue is the mod's.</summary>
        public static void Take()
        {
            CallDialogFactory.RegisterDialog(CallDialogType.RecruitmentAgencyDialog, Guard.Maker(() => new AgencyDeskDialog()));
        }

        /// <summary>The working life is off: the game's own dialogue answers again.</summary>
        public static void Give()
        {
            CallDialogFactory.RegisterDialog(CallDialogType.RecruitmentAgencyDialog, Guard.Maker(() => new RecruitmentAgencyDialog()));
        }

        internal static bool InPerson => DialogController.current != null && DialogController.current.dialogType != DialogType.PhoneCall;

        internal static bool BasicHrDone()
        {
            var diplomas = SaveGameManager.Current != null ? SaveGameManager.Current.PlayerDiplomas : null;
            for (int i = 0; diplomas != null && i < diplomas.Count; i++)
            {
                if (diplomas[i].name == DiplomaName.BasicHr && diplomas[i].completed)
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Check(string what, bool ok)
        {
            if (Meta.Dev.Verbose)
            {
                CareersGame.Logger?.Info("Careers check (recruitment desk, " + what + "): " + (ok ? "PASS" : "FAIL"));
            }
        }

        internal static DialogEntry Pick()
        {
            return new DialogEntry
            {
                messageData = "reallife_agency_desk_pick".Localize(),
                headerKey = "dialog_recruitment_agency_npc_name",
                SecondOptionTextOverride = "reallife_agency_desk_board",
                OnSecondOption = Board,
                ConfirmTextOverride = "reallife_agency_desk_hire".Localize(),
                OnConfirm = Hire,
                OnCancel = DialogController.current.CancelDialog
            };
        }

        // Recruitment: the game's own dialogue, which shows its Basic HR page itself when the course is not complete.
        private static DialogEntry Hire()
        {
            bool done = BasicHrDone();
            AgencyStep step = AgencyRules.Step(AgencyChoice.Recruitment, done);
            Check("recruitment chosen, Basic HR " + (done ? "complete" : "not complete"), step == (done ? AgencyStep.Recruit : AgencyStep.NeedBasicHr));
            new RecruitmentAgencyDialog();
            return null;
        }

        // The jobs board: never asks for the course. Registered already: said so, with what it gives and a way to renew.
        private static DialogEntry Board()
        {
            CareersDirector director = Director;
            WorkFile w = director != null ? director.Work : null;
            if (w == null)
            {
                return Guard.Entry(PhoneContacts.Closing("reallife_work_why_closed"));
            }
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            AgencyStep step = AgencyRules.Step(AgencyChoice.JobsBoard, BasicHrDone());
            Check("jobs board chosen", step == AgencyStep.Register);
            if (w.AgencyUntil >= today)
            {
                return Guard.Entry(new DialogEntry
                {
                    messageData = "reallife_agency_desk_already".Localize(new { day = CareersText.Day(w.AgencyUntil), more = ProRules.OpeningsAgency }),
                    headerKey = "dialog_recruitment_agency_npc_name",
                    ConfirmTextOverride = "reallife_agency_desk_renew".Localize(),
                    OnConfirm = Register,
                    OnCancel = DialogController.current.CancelDialog
                });
            }
            return Register();
        }

        private static DialogEntry Register()
        {
            CareersDirector director = Director;
            string why = director != null ? director.Register() : "reallife_work_why_closed";
            if (why != null)
            {
                return Guard.Entry(PhoneContacts.Closing(why));
            }
            return Guard.Entry(PhoneContacts.Closing("reallife_agency_desk_done", new { day = CareersText.Day(director.Work.AgencyUntil), more = ProRules.OpeningsAgency }));
        }
    }

    /// <summary>The dialogue the game opens at a recruitment office's desk: the two ways in person, the game's own on the phone.</summary>
    public sealed class AgencyDeskDialog : Dialog
    {
        public AgencyDeskDialog()
        {
            npcNameKey = "dialog_recruitment_agency_npc_name";
            if (!AgencyRules.Offered(AgencyDesk.InPerson, Settings.CareersJobs))
            {
                new RecruitmentAgencyDialog();
                return;
            }
            try
            {
                DialogController.current.ShowEntry(Guard.Entry(AgencyDesk.Pick()));
            }
            catch (Exception e)
            {
                CareersGame.Logger?.Error(e);
                new RecruitmentAgencyDialog();   // the game's own, so the office is never left shut by a fault
            }
        }
    }
}

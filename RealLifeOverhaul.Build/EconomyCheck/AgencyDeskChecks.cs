using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// The desk of a recruitment office (Scripts/Careers/AgencyDesk, Work/AgencyRules). The game's own dialogue refused anyone
// without the Basic HR course before saying anything else, so a player without it could not even register for work. A seat
// at the desk now always offers the jobs board (which never needs the course) and recruitment (which does: the game's own
// page says so). The rule is run here; the game's side, which the checker cannot run, is read from the sources.
internal static partial class Program
{
    private static string CareersSource(string file)
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Careers"));
        string path = System.IO.Path.Combine(folder, file);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }

    private static void AgencyDesk()
    {
        // ---------------------------------------------------------------- the rule
        Check(AgencyRules.Choices == 2 && !AgencyRules.NeedsBasicHr(AgencyChoice.JobsBoard) && AgencyRules.NeedsBasicHr(AgencyChoice.Recruitment),
            "recruitment desk: the jobs board never needs Basic HR, recruitment does");
        Check(AgencyRules.Step(AgencyChoice.JobsBoard, false) == AgencyStep.Register && AgencyRules.Step(AgencyChoice.JobsBoard, true) == AgencyStep.Register,
            "recruitment desk: the jobs board registers with or without Basic HR");
        Check(AgencyRules.Step(AgencyChoice.Recruitment, false) == AgencyStep.NeedBasicHr && AgencyRules.Step(AgencyChoice.Recruitment, true) == AgencyStep.Recruit,
            "recruitment desk: recruitment shows the Basic HR page until the course is complete, then goes on");
        Check(AgencyRules.Offered(true, true) && !AgencyRules.Offered(false, true) && !AgencyRules.Offered(true, false),
            "recruitment desk: the choice is offered in person while the working life is on; the phone and an off feature keep the game's dialogue");

        // ---------------------------------------------------------------- the game's side, from the sources
        string desk = CareersSource("AgencyDesk.cs");
        string work = CareersSource("CareersDirector.Work.cs");
        Check(InOrder(work, "private void StartWork()", "AgencyDesk.Take", "private void StopWork(bool unloading)", "AgencyDesk.Give"),
            "recruitment desk: the working life takes the game's recruitment dialogue when it starts and gives it back when it stops");
        Check(InOrder(desk, "CallDialogType.RecruitmentAgencyDialog", "new AgencyDeskDialog()", "CallDialogType.RecruitmentAgencyDialog", "new RecruitmentAgencyDialog()"),
            "recruitment desk: the desk's dialogue is the game's RecruitmentAgencyDialog slot, and the game's own is put back");
        string pick = CompanyMethod(desk, "internal static DialogEntry Pick()");
        Check(InOrder(pick, "SecondOptionTextOverride = \"reallife_agency_desk_board\"", "OnSecondOption = Board", "ConfirmTextOverride", "reallife_agency_desk_hire", "OnConfirm = Hire", "OnCancel"),
            "recruitment desk: two choices and a way to leave on the first page");
        string board = CompanyMethod(desk, "private static DialogEntry Board()");
        Check(board.Length > 0 && !board.Contains("RecruitmentAgencyDialog") && InOrder(board, "w.AgencyUntil >= today", "reallife_agency_desk_already", "return Register();"),
            "recruitment desk: the jobs board never opens the game's dialogue (the course gate); registered already is said, otherwise it registers");
        string hire = CompanyMethod(desk, "private static DialogEntry Hire()");
        Check(InOrder(hire, "AgencyRules.Step(AgencyChoice.Recruitment", "new RecruitmentAgencyDialog();", "return null;") && !hire.Contains("Register"),
            "recruitment desk: recruitment alone opens the game's dialogue, which shows its Basic HR page itself");
        Check(InOrder(desk, "public AgencyDeskDialog()", "AgencyRules.Offered(", "new RecruitmentAgencyDialog();", "catch (Exception e)", "new RecruitmentAgencyDialog();"),
            "recruitment desk: on the phone, with the feature off and after a fault the game's own dialogue answers");

        // ---------------------------------------------------------------- the texts, and the line in What's new
        string[] keys = { "reallife_agency_desk_pick", "reallife_agency_desk_board", "reallife_agency_desk_hire", "reallife_agency_desk_already", "reallife_agency_desk_renew", "reallife_agency_desk_done" };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool all = words != null;
            foreach (string key in keys)
            {
                all &= words != null && words.TryGetValue(key, out string v) && v.Length > 0 && !v.Contains('!') && !v.Contains((char)0x2014) && !v.Contains((char)0x2013);
            }
            all &= words != null && words["reallife_agency_desk_already"].Contains("{day}") && words["reallife_agency_desk_already"].Contains("{more}")
                   && words["reallife_agency_desk_done"].Contains("{day}") && words["reallife_agency_desk_done"].Contains("{more}");
            Check(all, "recruitment desk: its six texts in " + code);
        }
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            string part = code == "en" ? "register with the jobs board, which needs no course" : code == "pt" ? "cadastrar-se no quadro de empregos" : code == "zh-cn" ? "在招聘信息板上登记" : code == "de" ? "im Jobboard eintragen"
                : code == "fr" ? "s'inscrire au tableau des emplois" : code == "es" ? "inscribirse en el tablón de empleos" : code == "ru" ? "зарегистрироваться на доске вакансий" : code == "nl" ? "je inschrijven op het vacaturebord" : "iş panosuna kaydolmak";
            Check(AnyChangesRow(words, part), "recruitment desk: What's new says so in " + code);
        }
    }
}

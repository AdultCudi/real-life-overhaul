using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Data engineers hired for a data centre never started work (a player's report, 27 Oct 2026): the game gives a warehouse no desks
// and no shifts, so a professional the game hired there stood on its staff list for ever. A business with no shifts now seats only
// the rack crew (on the payroll), refuses any other job with its reason, withdraws an applicant accepted for it before the rule, and
// takes the game's own employees left there off it. The rules, the sources that use them, the test and the texts.
internal static partial class Program
{
    private static void DataEngineers()
    {
        DataEngineerRules();
        DataEngineerSources();
        DataEngineerTexts();
    }

    private static void DataEngineerRules()
    {
        RoleDef engineer = TalentCatalogue.Role(TalentCatalogue.DataEngineer);
        Check(engineer.Key == "data_engineer" && engineer.Family == Family.Data && engineer.InGame && engineer.Remote,
            "data engineers: the role is the game's programmer in the Data family, one the game hires");
        Check(HiringRules.NoShifts("ba:businesstype_warehouse") && !HiringRules.NoShifts("ba:businesstype_webdevelopmentagency") && !HiringRules.NoShifts("reallife:businesstype_softwarestudio")
              && !HiringRules.NoShifts("ba:businesstype_headquarters") && !HiringRules.NoShifts(string.Empty) && !HiringRules.NoShifts(null),
            "data engineers: only a warehouse, which a data centre is, has no desks or shifts; a tech firm's office, a web agency and headquarters have");

        bool shiftsSeatAll = true, noneSeatsOnlyRacks = true, crewSeats = true;
        for (int r = 0; r < TalentCatalogue.RoleCount; r++)
        {
            RoleDef role = TalentCatalogue.Role(r);
            shiftsSeatAll &= HiringRules.Seats(true, role);
            noneSeatsOnlyRacks &= HiringRules.Seats(false, role) == (role.Family == Family.Infrastructure);
        }
        for (int i = 0; i < HiringCatalogue.CrewRoles.Length; i++)
        {
            RoleDef crew = TalentCatalogue.Role(HiringCatalogue.CrewRoles[i]);
            crewSeats &= HiringRules.Seats(false, crew) && HiringRules.OnPayroll(crew, (int)Arrangement.OnSite, true, false);
        }
        Check(shiftsSeatAll, "data engineers: a business with shifts seats every one of the " + TalentCatalogue.RoleCount + " roles");
        Check(noneSeatsOnlyRacks, "data engineers: a business with no shifts seats the infrastructure family and no other role");
        Check(!HiringRules.Seats(false, engineer) && !HiringRules.Seats(false, TalentCatalogue.Role(TalentCatalogue.SoftwareEngineer))
              && !HiringRules.Seats(false, TalentCatalogue.Role(TalentCatalogue.DataScientist)) && HiringRules.Seats(true, engineer),
            "data engineers: a data engineer, a software engineer and a data scientist are not seated at a data centre but are at an office");
        Check(crewSeats, "data engineers: the rack crew a search firm is briefed for (technician, network engineer) are seated at a data centre, on the payroll");
        RoleDef devops = TalentCatalogue.Role(TalentCatalogue.DevOps);
        Check(HiringRules.OnPayroll(devops, (int)Arrangement.OnSite, false, false) && !HiringRules.OnPayroll(devops, (int)Arrangement.OnSite, false, true),
            "data engineers: a DevOps engineer hired by an ad at a data centre is on the payroll, as at an office is the game's own");
        Check(!HiringRules.OnPayroll(engineer, (int)Arrangement.OnSite, false, true) && HiringRules.OnPayroll(engineer, (int)Arrangement.Remote, false, true),
            "data engineers: a data engineer on site at an office is the game's employee, from home on the payroll, as before");
        Check(HiringRules.OnPayroll(TalentCatalogue.Role(TalentCatalogue.ProductManager), (int)Arrangement.OnSite, false, true),
            "data engineers: a role the game has no job for stays on the payroll");
        Check(HiringRules.Stranded(false, true) && !HiringRules.Stranded(true, true) && !HiringRules.Stranded(false, false),
            "data engineers: a game employee at a business with no shifts is stranded; one at an office, or one on the payroll, is not");
    }

    private static void DataEngineerSources()
    {
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string game = ModSource(@"Tech\People\TalentGame.cs");
        string app = ModSource(@"Tech\People\RolesApp.Hiring.cs");
        string tests = ModSource(@"Tech\TechDirector.Tests.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");
        string book = MethodText(ModSource(@"Interiors\InteriorGame.Basket.cs"), "public static int BookOffice(");
        Check(book.Contains("PlayerHelper.PlayerController == null || PlayerHelper.PlayerController.Character == null")
              && book.IndexOf("PlayerHelper.PlayerController == null", StringComparison.Ordinal) < book.IndexOf("BusinessTypeHelper.GetData(reg)", StringComparison.Ordinal),
            "data engineers sources: an office's booking stands down with no player, as at the game's teardown, before its station look-up can throw");
        string starts = MethodText(hiring, "private int Starts(int day)");
        Check(starts.Contains("HiringRules.Seats(shifts, s.P.Def)") && starts.Contains("HiringRules.OnPayroll(s.P.Def, ad.Work, ad.CrewSearch, shifts)")
              && starts.Contains("reallife_hire_note_start_nowhere") && !starts.Contains("ad.CrewSearch;"),
            "data engineers sources: a start asks whether the business seats the role, withdraws the applicant who cannot be, and puts the rest on the payroll by the one rule");
        Check(MethodText(hiring, "internal bool PostAd(").Contains("HiringRules.Seats(TalentGame.Shifts(at), r)") && MethodText(hiring, "internal bool StartSearch(").Contains("HiringRules.Seats(TalentGame.Shifts(at), r)"),
            "data engineers sources: a job ad and a search firm's brief both refuse a role the place cannot seat, with the reason");
        Check(app.Contains("bool seats = HiringRules.Seats(TalentGame.Shifts(_places[_adPlace]), r);") && Occurs(app, "TechDirector.SeatRefusal") == 2 && Occurs(app, "&& seats)") == 2,
            "data engineers sources: the new ad and the new search say why and leave their button off for such a place");
        Check(game.Contains("EmployeeHelper.UnassignEmployeeFromAllWorkshifts(e)") && game.Contains("public static bool Shifts(BuildingRegistration reg) => !HiringRules.NoShifts(TypeOf(reg));"),
            "data engineers sources: the game's employees stranded at a data centre are taken off it by the game's own unassigning");
        string day = MethodText(people, "private void PeopleDay(int day)");
        string unseat = MethodText(people, "internal int Unseat(int day, bool tell)");
        Check(day.Contains("Unseat(day, true);") && unseat.Contains("HiringRules.Stranded(TalentGame.Shifts(person.At), true)") && unseat.Contains("Logger?.Warn(") && unseat.Contains("ReadPeople(day);"),
            "data engineers sources: each day takes the stranded off, logs each one in a warning, tells the player once, and reads the people again");
        Check(tests.Contains("public void TestDataEngineers()") && tests.Contains("TalentGame.StationShift(p.Game, p.At)") && tests.Contains("Tech check (data engineers)")
              && tests.Contains(": \" + (ok ? \"PASS\" : \"FAIL\")") && mod.Contains("\"reallife_dataeng_test\", () => Tech.TechDirector.Instance?.TestDataEngineers()")
              && list.Contains("\"reallife_crew_test\", \"reallife_dataeng_test\")"),
            "data engineers sources: the test in Options reads each data engineer's place and desk shift, writes PASS or FAIL to the log and is on the feature's list");
        Check(game.Contains("w.type != WorkShiftType.Cleaning") && game.Contains("placed.ContainsKey(w.itemInstanceId)"),
            "data engineers sources: a desk shift is a shift that is not cleaning, at a station that is placed");
    }

    private static void DataEngineerTexts()
    {
        string[] keys =
        {
            "reallife_hire_no_seat", "reallife_hire_note_start_nowhere", "reallife_hire_note_unseated", "reallife_hire_note_unseated_one", "reallife_dataeng_test",
            "reallife_dataeng_test_pass", "reallife_dataeng_test_fail", "reallife_dataeng_test_none"
        };
        Check(AllIn(keys, out string missing), "data engineers texts: every key in en, pt and zh-cn (" + missing + ")");
        bool fits = true, plain = true, holes = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string k in keys)
            {
                string v = texts.TryGetValue(k, out string t) ? t : string.Empty;
                fits &= v.Length <= 240;
                plain &= !v.Contains("  ") && !v.Contains("!") && !v.Contains("—") && !v.Contains("–");
            }
            holes &= texts["reallife_hire_note_unseated"].Contains("{name}") && texts["reallife_hire_note_unseated"].Contains("{count}") && texts["reallife_hire_note_unseated"].Contains("{business}")
                     && texts["reallife_hire_note_unseated_one"].Contains("{name}") && texts["reallife_hire_note_start_nowhere"].Contains("{role}")
                     && texts["reallife_dataeng_test_fail"].Contains("{short}") && texts["reallife_dataeng_test_fail"].Contains("{count}") && texts["reallife_dataeng_test_pass"].Contains("{count}");
        }
        Check(fits, "data engineers texts: every notice is 240 characters or fewer in en, pt and zh-cn");
        Check(plain, "data engineers texts: no double spaces, exclamation marks or dashes");
        Check(holes, "data engineers texts: each notice keeps the {names} the code fills");

        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: data engineers and other professionals found through a job ad or a search firm for a data centre never started work" },
            { "pt", "Corrigido: engenheiros de dados e outros profissionais encontrados por um anúncio de vaga ou por uma empresa de busca para um data center nunca começavam" },
            { "zh-cn", "已修复：通过招聘广告或猎头公司为数据中心找来的数据工程师和其他专业人员一直无法开始工作" },
            { "de", "Behoben: Dateningenieure und andere Fachkräfte, die über eine Stellenanzeige oder eine Personalsuche für ein Rechenzentrum gefunden wurden" },
            { "fr", "Corrigé : les ingénieurs données et les autres professionnels trouvés par une annonce ou par un cabinet de recherche pour un centre de données" },
            { "es", "Corregido: los ingenieros de datos y otros profesionales encontrados por un anuncio de empleo o por una firma de búsqueda para un centro de datos" },
            { "ru", "Исправлено: инженеры данных и другие специалисты, найденные через объявление или поисковую фирму для дата-центра" },
            { "nl", "Opgelost: data engineers en andere professionals die via een vacature of een zoekbureau voor een datacenter werden gevonden" },
            { "tr", "Düzeltildi: bir veri merkezi için iş ilanıyla ya da arama firmasıyla bulunan veri mühendisleri ve diğer profesyoneller hiç işe başlayamıyordu" },
            { "pl", "Naprawiono: inżynierowie danych i inni specjaliści znalezieni przez ogłoszenie albo firmę rekrutacyjną dla centrum danych nigdy nie zaczynali pracy" },
            { "ko", "수정: 데이터 센터를 위해 구인 공고나 헤드헌팅 회사로 찾은 데이터 엔지니어와 다른 전문가들이 일을 시작하지 못했습니다" }
        };
        bool every = true, clean = true;
        string lacking = string.Empty;
        foreach (KeyValuePair<string, string> pair in lines)
        {
            Dictionary<string, string> texts = LocaleFile(pair.Key);
            bool has = WhatsNewHas(texts, pair.Value);
            every &= has;
            lacking += has ? string.Empty : pair.Key + " ";
            foreach (KeyValuePair<string, string> row in texts)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(pair.Value))
                {
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–")
                             && Occurs(row.Value, pair.Value) == 1;
                }
            }
        }
        Check(lines.Count == 11 && every, "data engineers texts: What's new tells of the fix, with its fix mark, in all eleven languages, in some row (" + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "data engineers texts: the fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }
}

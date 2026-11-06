using System;
using System.Collections.Generic;
using RealLifeOverhaul.Staffing;
using RealLifeOverhaul.Tech;

// Sales, support and customer service staff at a tech firm had no desk (a player's report, 2 Nov 2026): the game seats an employee at a
// workstation whose item lists one of their skills, its desk computers list customer service for nobody, and its cash register is sold
// to shops, not offices. Every role the mod offers at a tech firm now has a workstation the firm can furnish (Tech/Firms/SeatRules), the
// desks take the skill while the people part runs (SeatGame), and the roles pages and the Team tab say which workstation a role needs.
internal static partial class Program
{
    private const int SeatKinds = (int)FirmKind.Count;

    private static void TechSeats()
    {
        TechSeatRules();
        TechSeatSources();
        TechSeatTexts();
    }

    private static void TechSeatRules()
    {
        // every role at every kind of tech firm: a workstation the kind can furnish, and one that lists the role's skill
        bool seated = true, desks = true, noRegister = true, payroll = true;
        string gap = string.Empty;
        for (int kind = 0; kind < SeatKinds; kind++)
        {
            int first = SeatRules.FirstUnseated(kind);
            seated &= first < 0;
            gap += first < 0 ? string.Empty : kind + ":" + TalentCatalogue.Role(first).Key + " ";
            noRegister &= !SeatRules.Furnishes(kind, Workstation.Register);
            desks &= SeatRules.Furnishes(kind, Workstation.Desk);
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                RoleDef r = TalentCatalogue.Roles[i];
                Workstation need = SeatRules.Needs(r);
                payroll &= r.InGame == (need == Workstation.Desk) && (need == Workstation.Desk || need == Workstation.None);
                seated &= need == Workstation.None || (SeatRules.Furnishes(kind, need) && SeatRules.DeskSeats(r.GameSkill));
            }
        }
        Check(seated, "tech seats: every role of the catalogue has a seat at every kind of tech firm, in the five types and the game's web agency and graphic designer (" + (gap.Length > 0 ? "no seat for " + gap : "none lacking") + ")");
        Check(desks && noRegister, "tech seats: every kind of tech firm can furnish a desk and none a cash register, since a tech firm sells nothing over a counter");
        Check(payroll, "tech seats: a role the game hires sits at a desk and a role of the mod's payroll needs none; no role needs a register");

        // the roles of the report, by name: sales, support and customer service
        int[] reported =
        {
            TalentCatalogue.ItSupport, TalentCatalogue.CustomerSuccess, TalentCatalogue.SalesExecutive, TalentCatalogue.AccountManager, 16   // sales engineer
        };
        bool reportedSeated = true, onCustomerService = true;
        foreach (int id in reported)
        {
            RoleDef r = TalentCatalogue.Role(id);
            onCustomerService &= r.GameSkill == TalentCatalogue.CustomerService && r.InGame;
            for (int kind = 0; kind < SeatKinds; kind++)
            {
                reportedSeated &= SeatRules.Seated(kind, r);
            }
        }
        Check(onCustomerService && reportedSeated, "tech seats: the sales engineer, sales executive, account manager, customer success and IT support roles are hired under customer service and seated at a desk at every kind of firm");

        // what the game ships: its desks list no customer service, so the addition is what seats them (the check fails without it)
        Check(Array.IndexOf(SeatRules.GameDesk, TalentCatalogue.CustomerService) < 0 && Array.IndexOf(SeatRules.ModDesk, TalentCatalogue.CustomerService) >= 0
              && Array.IndexOf(SeatRules.GameDesk, TalentCatalogue.Programmer) >= 0 && Array.IndexOf(SeatRules.GameDesk, TalentCatalogue.GraphicDesigner) >= 0,
            "tech seats: the game's own desks list programmers and graphic designers and not customer service; the mod's addition is customer service");

        // the list the game's desks get: the skill added once, the game's own kept in order, the list given not changed
        string[] ships = (string[])SeatRules.GameDesk.Clone();
        string[] added = SeatRules.WithModDesk(ships);
        bool listed = added != null && added.Length == ships.Length + SeatRules.ModDesk.Length && Array.IndexOf(added, TalentCatalogue.CustomerService) == ships.Length;
        for (int i = 0; listed && i < ships.Length; i++)
        {
            listed &= added[i] == ships[i];
        }
        Check(listed && ships.Length == SeatRules.GameDesk.Length && Array.IndexOf(ships, TalentCatalogue.CustomerService) < 0 && SeatRules.WithModDesk(added) == null && SeatRules.WithModDesk(null) == null,
            "tech seats: the skill is added after the game's own and once; a list that has it, or none, is left as it is, and the list given is not changed");
        Check(SeatRules.PrimaryOf(-1).Length == 0 && SeatRules.PrimaryOf(SeatKinds).Length == 0 && SeatRules.PrimaryOf((int)FirmKind.WebAgency).Length == 1
              && SeatRules.PrimaryOf((int)FirmKind.Design)[0] == TalentCatalogue.GraphicDesigner && !SeatRules.Furnishes(-1, Workstation.Desk) && SeatRules.Furnishes(-1, Workstation.None)
              && !SeatRules.DeskSeats(null) && !SeatRules.DeskSeats(string.Empty) && SeatRules.Needs(null) == Workstation.None && SeatRules.Seated(-1, null),
            "tech seats: a kind the rules do not know has no desk to furnish, and no role or no skill needs or has a seat");

        // the five types of the sector are hired under customer service, which is why they need the addition: the game's web agency and graphic designer are not
        bool sectorHires = true;
        for (int kind = 0; kind < (int)FirmKind.WebAgency; kind++)
        {
            sectorHires &= Array.IndexOf(SeatRules.PrimaryOf(kind), TalentCatalogue.CustomerService) >= 0 && Array.IndexOf(SeatRules.PrimaryOf(kind), TalentCatalogue.Programmer) >= 0;
        }
        Check(sectorHires, "tech seats: the sector's five business types are hired under programmers and customer service, and a desk is offered to them by the programmers");

        // the rivals keep their staff in the same table of roles: the families they ask for in an advert are roles of this catalogue, each with a seat
        bool rivals = true;
        int[] wanted = new int[8];
        for (int kind = 0; kind < SeatKinds; kind++)
        {
            int n = FirmsRules.Wanted(kind, wanted);
            for (int i = 0; i < n; i++)
            {
                rivals &= SeatRules.Seated(kind, TalentCatalogue.Role(wanted[i]));
            }
        }
        Check(rivals, "tech seats: every role a firm of a kind asks for first, which the rivals' staffing follows, has a seat at that kind of firm");
    }

    private static void TechSeatSources()
    {
        string types = ModSource(@"Tech\Firms\TechTypes.cs");
        string game = ModSource(@"Tech\Firms\SeatGame.cs");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string director = ModSource(@"Tech\TechDirector.cs");
        string team = ModSource(@"Tech\People\RolesApp.Team.cs");
        string hiring = ModSource(@"Tech\People\RolesApp.Hiring.cs");
        string table = ModSource(@"Staffing\StaffTable.Roles.cs");
        Check(types.Contains("t.employeePrimarySkills = SeatRules.PrimaryOf(kind);") && !types.Contains("Skills[kind]") && !types.Contains("TalentCatalogue.CustomerService"),
            "tech seats: the business types are hired under the skills the seat rules list, the one table the offline check holds");
        Check(MethodText(people, "private void StartPeople()").Contains("SeatGame.Apply();") && MethodText(people, "private void StopPeople(bool keep, bool unloading)").Contains("SeatGame.Remove();")
              && director.Contains("SeatGame.Logger = Logger;"),
            "tech seats: the desks take the skill when the people part starts and give it back when it stops, so with the option off they are the game's own");
        Check(MethodText(game, "public static int Apply()").Contains("Remove();") && MethodText(game, "public static int Apply()").Contains("SeatRules.WithModDesk(now)")
              && MethodText(game, "public static int Apply()").Contains("Tech check (desks)") && MethodText(game, "public static int Apply()").Contains("PASS")
              && MethodText(game, "public static int Remove()").Contains("ReferenceEquals(Changed[i].suitableSkills, After[i])"),
            "tech seats: the game side changes only the desk items that lack the skill, keeps the list it replaced, gives back only what it changed, and writes a self-check line");
        Check(MethodText(game, "private static bool IsDesk(Item item)").Contains("ItemType.Computer | ItemType.EmployeeWorkstation") && MethodText(game, "private static bool IsDesk(Item item)").Contains("item.assignable")
              && !game.Contains("Update()") && !game.Contains("LateUpdate()"),
            "tech seats: a desk is an assignable computer workstation that programmers sit at, found by its type and skills and not by name; nothing runs per frame");
        Check(team.Contains("SeatGame.DesksFor(person.Game, person.At)") && team.Contains("TalentGame.StationShift(person.Game, person.At)") && team.Contains("reallife_roles_state_no_desk")
              && team.Contains("reallife_roles_file_no_desk") && team.Contains("reallife_roles_file_no_shift"),
            "tech seats: the Team tab says why a professional of the game's has no seat, no desk placed or no shift at one");
        Check(Occurs(hiring, "reallife_hire_seat_desk") == 2 && Occurs(hiring, "reallife_hire_seat_payroll") == 2 && table.Contains("reallife_staff_t_seat_desk") && table.Contains("reallife_staff_t_seat_payroll"),
            "tech seats: the job ad page, the search firm's page and the Staff and roles reference say which workstation a role needs");

        // the reference: every professional role says its workstation
        bool said = true;
        int roles = 0;
        foreach (StaffRole role in StaffTable.Roles)
        {
            if (role.Talent < 0)
            {
                continue;
            }
            roles++;
            string want = SeatRules.Needs(TalentCatalogue.Role(role.Talent)) == Workstation.Desk ? "reallife_staff_t_seat_desk" : "reallife_staff_t_seat_payroll";
            said &= Array.Exists(role.About, n => n.Key == want);
        }
        Check(said && roles == TalentCatalogue.RoleCount, "tech seats: the Staff and roles reference gives each of the " + TalentCatalogue.RoleCount + " professional roles its workstation");
    }

    private static void TechSeatTexts()
    {
        string[] keys =
        {
            "reallife_staff_t_seat_desk", "reallife_staff_t_seat_payroll", "reallife_hire_seat_desk", "reallife_hire_seat_payroll", "reallife_roles_state_no_desk",
            "reallife_roles_state_no_shift", "reallife_roles_file_no_desk", "reallife_roles_file_no_shift"
        };
        Check(AllIn(keys, out string missing), "tech seats texts: every key in en, pt and zh-cn (" + missing + ")");
        bool fits = true, plain = true, holes = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (string k in keys)
            {
                string v = texts.TryGetValue(k, out string t) ? t : string.Empty;
                fits &= v.Length > 0;
                plain &= !v.Contains("  ") && !v.Contains("!") && !v.Contains("—") && !v.Contains("–");
            }
            holes &= texts["reallife_roles_file_no_desk"].Contains("{name}") && texts["reallife_roles_file_no_desk"].Contains("{business}")
                     && texts["reallife_roles_file_no_shift"].Contains("{name}") && texts["reallife_roles_file_no_shift"].Contains("{business}");
        }
        Check(fits, "tech seats texts: every text is there and not empty in en, pt and zh-cn (none is a notice, so none has the 240 limit)");
        Check(plain, "tech seats texts: no double spaces, exclamation marks or dashes");
        Check(holes, "tech seats texts: the Team tab's lines keep the {name} and {business} the code fills");

        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: sales, support and customer service staff at a tech firm had no desk to sit at" },
            { "pt", "Corrigido: a equipe de vendas, suporte e atendimento de uma empresa de tecnologia não tinha mesa para sentar" },
            { "zh-cn", "已修复：科技公司的销售、支持和客服人员没有办公桌可坐" },
            { "de", "Behoben: Vertriebs-, Support- und Kundendienstpersonal in einer Tech-Firma hatte keinen Schreibtisch zum Sitzen" },
            { "fr", "Corrigé : le personnel de vente, de support et de service client d'une entreprise tech n'avait pas de bureau où s'asseoir" },
            { "es", "Corregido: el personal de ventas, soporte y atención al cliente de una empresa tecnológica no tenía escritorio donde sentarse" },
            { "ru", "Исправлено: у сотрудников продаж, поддержки и клиентской службы в технологической фирме не было стола, за который можно сесть" },
            { "nl", "Opgelost: verkoop-, support- en klantenservicemedewerkers bij een techbedrijf hadden geen bureau om aan te zitten" },
            { "tr", "Düzeltildi: bir teknoloji firmasındaki satış, destek ve müşteri hizmetleri çalışanlarının oturacak masası yoktu" },
            { "pl", "Naprawiono: pracownicy sprzedaży, wsparcia i obsługi klienta w firmie technologicznej nie mieli biurka, przy którym mogliby usiąść" },
            { "ko", "수정: 기술 회사의 영업, 지원, 고객 서비스 직원이 앉을 책상이 없었습니다" }
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
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, pair.Value) == 1;
                }
            }
        }
        Check(lines.Count == 11 && every, "tech seats texts: What's new tells of the fix, with its fix mark, in all eleven languages, in some row (" + (lacking.Length > 0 ? "lacking " + lacking : "none lacking") + ")");
        Check(clean, "tech seats texts: the fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }
}

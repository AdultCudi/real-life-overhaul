using System;
using System.Collections.Generic;
using IoPath = System.IO.Path;
using RealLifeOverhaul;

// A player was sent to "the office" for a performance review at the address of a furniture store: the employer's office is a
// help-wanted board that stands on the pavement beside the door of a city building of another business, and the texts said only
// "the office at <address>", so the door in front of the player led into the shop. The review's place is now one that can be met:
// the board while it stands (placed, and its building still in the city), named with the shop it stands outside and the way in
// (on the pavement, not inside); otherwise the phone, and a plain line says so. What only the game's side can do is checked in its source.
internal static partial class Program
{
    private static void MeetingPlaces()
    {
        MeetingPlaceRules();
        MeetingPlaceSources();
        MeetingPlaceTexts();
    }

    // Every combination of a board index, a count of boards, a building that stands or not, and a works site that is there or not.
    private static void MeetingPlaceRules()
    {
        int combinations = 0, boards = 0, works = 0, phones = 0;
        bool onlyStanding = true, reviewNeverWorks = true, phoneAlways = true, atBoardAlways = true, keysDistinct = true, deterministic = true;
        for (int count = 0; count <= 6; count++)
        {
            for (int board = -2; board <= 7; board++)
            {
                foreach (bool stands in new[] { false, true })
                {
                    foreach (bool site in new[] { false, true })
                    {
                        combinations++;
                        OfficeWhere where = CareerOfficeRules.Where(board, count, stands, site);
                        deterministic &= where == CareerOfficeRules.Where(board, count, stands, site);
                        OfficeWhere review = CareerOfficeRules.ReviewWhere(where);
                        // A board is only named when it is placed, below the count and its building still stands.
                        if (where == OfficeWhere.Board)
                        {
                            boards++;
                            onlyStanding &= board >= 0 && board < count && stands;
                        }
                        else if (where == OfficeWhere.Works)
                        {
                            works++;
                            onlyStanding &= board < 0 && site;
                        }
                        else
                        {
                            phones++;
                        }
                        // A board that is placed but gone, or a count that has lost it, is never a place to be sent to.
                        if (board >= 0 && (!stands || board >= count))
                        {
                            onlyStanding &= where == OfficeWhere.Phone;
                        }
                        // A review is at a standing board or by phone, never at a works site; by phone it can always be held, at a board only there.
                        reviewNeverWorks &= review == OfficeWhere.Board || review == OfficeWhere.Phone;
                        phoneAlways &= review != OfficeWhere.Phone || (CareerOfficeRules.CanReview(where, false) && CareerOfficeRules.CanReview(where, true));
                        atBoardAlways &= review != OfficeWhere.Board || (!CareerOfficeRules.CanReview(where, false) && CareerOfficeRules.CanReview(where, true));
                        // The text each place gets is its own, and a review that is not at a board never carries the board's twin.
                        string a = CareerOfficeRules.Key("k", OfficeWhere.Phone), b = CareerOfficeRules.Key("k", OfficeWhere.Works), c = CareerOfficeRules.Key("k", OfficeWhere.Board);
                        keysDistinct &= a != b && b != c && a != c && c == "k_board" && b == "k_at" && a == "k";
                    }
                }
            }
        }
        Check(combinations == 7 * 10 * 4, "meeting place: every combination of board, count, standing and works site was tried (" + combinations + ")");
        Check(onlyStanding, "meeting place: the board is named only while it is placed, below the count and its building stands; a works site only where there is no board and a site; nothing else is a place");
        Check(boards > 0 && works > 0 && phones > 0, "meeting place: the combinations reach a board (" + boards + "), a works site (" + works + ") and the phone (" + phones + ")");
        Check(reviewNeverWorks && phoneAlways && atBoardAlways,
            "meeting place: a review is at a standing board (held only in person there) or by phone (held from anywhere); a works site never holds one, and no review waits on a place that is not there");
        Check(keysDistinct && deterministic, "meeting place: each place has its own text key (plain, _at, _board) and the choice is deterministic");
        // The ordinary cases, by name.
        Check(CareerOfficeRules.Where(2, 6, true, false) == OfficeWhere.Board && CareerOfficeRules.Where(2, 6, false, false) == OfficeWhere.Phone
              && CareerOfficeRules.Where(6, 6, true, false) == OfficeWhere.Phone && CareerOfficeRules.Where(-1, 6, true, true) == OfficeWhere.Works
              && CareerOfficeRules.Where(-1, 0, false, false) == OfficeWhere.Phone,
            "meeting place: a standing board is the place; a board whose building is gone, or one past the count, falls back to the phone; the building trades keep their site; none is the phone");
        Check(CareerOfficeRules.CanReview(OfficeWhere.Works, false) && !CareerOfficeRules.CanReview(OfficeWhere.Board, false) && CareerOfficeRules.CanReview(OfficeWhere.Board, true),
            "meeting place: the building trades' review is by phone, a board's is at the board");
    }

    // The game's side: the place is asked of the board itself when the message is set and when the review is offered.
    private static void MeetingPlaceSources()
    {
        string director = ModSource(@"Jobs\CareerDirector.cs");
        string boss = ModSource(@"Jobs\CareerDirector.Boss.cs");
        string employment = ModSource(@"Jobs\CareerDirector.Employment.cs");
        string boards = ModSource(@"Jobs\JobBoards.cs");
        string dialog = ModSource(@"Jobs\CareerDialog.cs");
        string tests = ModSource(@"Jobs\JobDirector.Tests.cs");
        string stands = MemberText(boards, "public bool Stands(int board)");
        Check(stands.Contains("board >= 0") && stands.Contains("board < _boards.Count") && stands.Contains("_buildings[board] != null"),
            "meeting place: JobBoards.Stands is true only for a placed board whose building is still in the city");
        Check(boards.Contains("_places.Add(PlaceName(building.building.SpecialService, kind));") && boards.Contains("_places.Clear();") && boards.Contains("public string Place(int board)"),
            "meeting place: each board keeps the name of the shop it stands outside (the service's own name, else the game's name for its kind), and gives it up with the board");
        string depot = MemberText(director, "private bool DepotPlace(int employer, out Vector3 place, out string address)");
        Check(depot.Contains("!boards.Stands(board)") && depot.Contains("return false;"), "meeting place: an employer's board whose building is gone is no office (no address, no marker)");
        string where = MemberText(director, "public OfficeWhere WhereOf(int employer)");
        Check(where.Contains("CareerOfficeRules.Where(") && where.Contains("boards.Stands(board)"), "meeting place: where an employer is met is decided by the rules, from the board's own standing");
        string review = MemberText(director, "public string ReviewKey(string key, int employer, Dictionary<string, string> data)");
        Check(review.Contains("CareerOfficeRules.ReviewWhere(WhereOf(employer))") && review.Contains("Careers check (review place)") && review.Contains("KeyAt("),
            "meeting place: the review's text is chosen from where the review is held, with a self-check line in Developer diagnostics");
        string keyAt = MemberText(director, "private string KeyAt(string key, int employer, OfficeWhere where, Dictionary<string, string> data)");
        Check(keyAt.Contains("CareerOfficeRules.PlaceField") && keyAt.Contains("CareerOfficeRules.AddressField") && keyAt.Contains("OfficePlace(employer)"),
            "meeting place: a board's text is given the shop it stands outside and the address");
        Check(director.Contains("ReviewKey(\"reallife_career_review_due\"") && director.Contains("ReviewKey(\"reallife_career_text_review_due\"") && tests.Contains("ReviewKey(\"reallife_career_review_due\""),
            "meeting place: the review notice, its text message and the options page's test all name the place through ReviewKey");
        Check(!director.Contains("OfficeKey(\"reallife_career_review_due\"") && !tests.Contains("OfficeKey(\"reallife_career_review_due\""),
            "meeting place: no review notice is raised through OfficeKey any more");
        Check(MemberText(employment, "public bool CanReview(bool inPerson)").Contains("CareerOfficeRules.CanReview(WhereOf(CareerState.EmployerIndex), inPerson)"),
            "meeting place: the review is offered in person only at a board that stands, by phone otherwise (the rule, not a bare board index)");
        Check(boss.Contains("!boards.Stands(_depot[e])"), "meeting place: a boss is put out only at a board that stands");
        Check(dialog.Contains("ReviewText(career, \"reallife_career_note_review\")") && dialog.Contains("career.ReviewKey(") && dialog.Contains("place = career.OfficePlace(CareerState.EmployerIndex) ?? string.Empty"),
            "meeting place: the Work contact names the board, the shop and the address on its review note, its office page and its clock-on refusal");
        Check(dialog.Contains("career.OfficeKey(\"reallife_career_office_page\"") && dialog.Contains("career.OfficeKey(\"reallife_career_office_shown\"") && dialog.Contains("career.OfficeKey(\"reallife_career_office_busy\""),
            "meeting place: Show me the office words its answers for the board outside the shop");
        string show = MemberText(director, "public bool ShowOffice(int employer)");
        Check(show.Contains("reallife_career_office_mark") && show.Contains("OfficePlace(employer)"), "meeting place: the game's destination marker is labelled with the board outside the shop, not the shop");
        string clock = MemberText(director, "private bool Begin(");
        Check(clock.Contains("CareerOfficeRules.Key(\"reallife_career_at_depot\", WhereOf(index))"), "meeting place: the clock-on refusal names the board outside the shop too");
        string app = ModSource(@"Careers\CareerApp.Map.cs");
        string links = ModSource(@"Wiring\CareersLinks.cs");
        Check(app.Contains("\"place\", board.Place ?? string.Empty") && links.Contains("Place = career.OfficePlace(e)") && ModSource(@"Careers\CareersBridges.cs").Contains("public string Place;"),
            "meeting place: the Career app's Professions tab names the shop each board stands outside");
    }

    private static void MeetingPlaceTexts()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool ok = codes.Length == ModLanguages.PerFeature.Length && langs[0] != null;
        // Every board text of the review and the office says the board is outside, names the shop and the address, and keeps off the door.
        string[] board =
        {
            "reallife_career_review_due_board", "reallife_career_text_review_due_board", "reallife_career_note_review_board", "reallife_career_reminder_board",
            "reallife_career_text_reminder_board", "reallife_career_at_depot_board", "reallife_career_office_page_board", "reallife_career_office_shown_board",
            "reallife_career_office_busy_board"
        };
        bool named = ok;
        for (int l = 0; ok && l < 3; l++)
        {
            foreach (string key in board)
            {
                named &= langs[l].TryGetValue(key, out string text) && text.Contains("{place}") && text.Contains("{address}");
            }
        }
        Check(named, "meeting place: in en, pt and zh-cn every board text names {place} and {address}");
        ok = ok && langs[0].ContainsKey("reallife_career_review_due_board");
        Check(ok && langs[0]["reallife_career_review_due_board"].Contains("outside {place}") && langs[0]["reallife_career_review_due_board"].Contains("not inside")
              && langs[0]["reallife_career_note_review_board"].Contains("not inside") && langs[0]["reallife_career_office_page_board"].Contains("do not go inside"),
            "meeting place: the review texts say the board is outside the shop and the way in is not through its door");
        Check(langs[0]["reallife_career_review_due"].Contains("phone") && !langs[0]["reallife_career_review_due"].Contains("Go to the office")
              && langs[0]["reallife_career_text_review_due"].Contains("phone") && langs[0]["reallife_career_note_review"].Contains("phone"),
            "meeting place: the plain review texts, for an employer with no board standing, say it is held by phone and no longer send the player to an office");
        Check(langs[0]["reallife_career_boards_about"].Contains("not the way in") && langs[0]["reallife_career_boards_at"].Contains("{place}"),
            "meeting place: the Career app says the board is on the pavement outside a shop and that the shop's door is not the way in");
        Check(ModGuide("en", "a furniture store") && ModGuide("en", "speak to your boss at the board, outside") && ModGuide("pt", "uma loja de móveis") && ModGuide("zh-cn", "比如一家家具店"),
            "meeting place: the Guide's careers page tells where the board stands, the shop beside it, and that the boss is met at the board (en, pt, zh-cn)");
        // What's new: the fix, with each language's own mark, in some row of every language (en pt zh-cn de fr es ru nl tr; pl and ko by the whole-file rules).
        Check(FixLineEverywhere("to the help-wanted board on the pavement outside it", "ao quadro de empregos na calçada em frente", "店外人行道上的招聘板",
                                "zur Stellenanzeigentafel auf dem Gehweg davor", "au tableau d'offres d'emploi sur le trottoir devant",
                                "al tablón de ofertas en la acera de delante", "к доске вакансий на тротуаре перед ним",
                                "naar het vacaturebord op de stoep ervoor", "ilan panosuna gönderiyor"),
            "meeting place: What's new has the fix's line, with its fix mark, in some row of every language");
        // The other two languages' lines are held by the whole-file rules, and their rows must carry the mark as many times as the English does.
        bool marks = true;
        foreach (string code in new[] { "pl", "ko" })
        {
            Dictionary<string, string> own = LoadWords(IoPath.Combine(LocalesFolder(), code + ".json"));
            marks &= own != null && own.TryGetValue("reallife_changes_next", out string row) && row.StartsWith(FixMarks[code], StringComparison.Ordinal);
        }
        Check(marks, "meeting place: Polish and Korean have the fix in their next row, with their fix marks");
    }

    private static bool ModGuide(string code, string piece)
    {
        string path = IoPath.Combine(LocalesFolder(), code + ".json");
        Dictionary<string, string> words = LoadWords(path);
        return words != null && words.TryGetValue("reallife_guide_careers", out string page) && page.Contains(piece);
    }
}

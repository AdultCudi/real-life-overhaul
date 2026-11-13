using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The third pass over the Property and Services pages and the map's lists, checked game-free: the explanations under a table, the figures the old tables had a column for
// (a row's card, a hotel's blocks), the last line of a short list, the building filter the tenants' table takes, the pad's prompts, the game's building box hidden for a picked
// building and a full list of saved places that says so. Part of `--map5`.
internal static partial class Program
{
    private static void Estate3Checks()
    {
        // The notes of a table page stand under its table, whole, in the room the table gives up.
        var source = new NotedEstate();
        var screen = new EstateScreen(source, "estate");
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Property");
        screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        string texts = EstateTexts(scene);
        Check(texts.Contains("What moves a price.") && texts.Contains("What moves a rent.") && texts.Contains("How a mortgage goes."), "Property: the sentences a table page carries are drawn");
        float tableBottom = 0f, noteTop = float.MaxValue;
        for (int c = 0; c < scene.ClipCount; c++)
        {
            tableBottom = Math.Max(tableBottom, scene.Clips[c].Y + scene.Clips[c].H);
        }
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind == UiKind.Text && scene.Items[i].Text != null && scene.Items[i].Text.StartsWith("What moves", StringComparison.Ordinal) && scene.Items[i].Clip < 0)
            {
                noteTop = Math.Min(noteTop, scene.Items[i].Y);
            }
        }
        Check(tableBottom > 0f && noteTop >= tableBottom - 0.5f, "Property: the notes start under the table's last row, not over it");

        // A row's card shows while the pointer or the focus is on a row that has one; a row without one costs no page.
        screen.Pointed(EstateFace.TableBase + UiDataTable.Row, 0);
        Check(screen.View.Hover == 0 && !screen.Dirty, "Property: the pointer on a row with no card names it and writes nothing again");
        screen.Pointed(EstateFace.TableBase + UiDataTable.Row, 1);
        Check(screen.Dirty, "Property: the pointer coming to a row with a card writes the page again");
        screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(EstateTexts(scene).Contains("Owed this year"), "Property: a row's card is drawn while the pointer rests on it");
        screen.Pointed(-1, -1);
        Check(screen.Dirty, "Property: the card goes when the pointer leaves");
        screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
        Check(!EstateTexts(scene).Contains("Owed this year"), "Property: no card is drawn with the pointer off the rows");
        screen.Focused(EstateFace.TableBase + UiDataTable.Row, 1);
        Check(screen.View.Hover == 1, "Property: the pad's focus on a row raises its card as the pointer does");

        // The building filter: a row names its building as the second kind, and a link's choice keeps that building's rows alone.
        UiDataRow Row(string name, params int[] kinds) => new UiDataRow { Cells = new[] { name }, Kinds = kinds };
        var rows = new[] { Row("a", 0, 0), Row("b", 3, 0), Row("c", 0, 1), Row("d", 4, 1), Row("e", 4, 2) };
        int[] one = TableRules.Order(rows, -1, false, new[] { -1, 1 });
        int[] applicants = TableRules.Order(rows, -1, false, new[] { 4, 1 });
        Check(one.Length == 2 && one[0] == 2 && one[1] == 3 && applicants.Length == 1 && applicants[0] == 3, "tenants: the second filter keeps one building's rows, and with the first one its applicants");
        Check(TableRules.Held(5, 3) == -1, "tenants: a building that is gone falls back to all");

        // The sources.
        string mine = Map5Source("Ui/Estate/EstateLive.Mine.cs");
        string trade = Map5Source("Ui/Estate/EstateLive.Trade.cs");
        string works = Map5Source("Ui/Estate/EstateLive.Works.cs");
        string market = Map5Source("Ui/Estate/EstateLive.Market.cs");
        string unit = Map5Source("Ui/Estate/EstateLive.Unit.cs");
        string building = Map5Source("Ui/Estate/EstateLive.Building.cs");
        string face = Map5Source("Ui/Estate/EstateFace.cs");
        string model = Map5Source("Ui/Estate/EstateModel.cs");
        string[] oldNotes =
        {
            "reallife_estate_market_what", "reallife_ui_estate_note_market_rents", "reallife_ui_estate_note_mortgage", "reallife_estate_hotel_what", "reallife_estate_hotel_night_what",
            "reallife_ui_estate_note_hotel_staff", "reallife_estate_hotel_held_what", "reallife_ui_estate_note_mine", "reallife_ui_estate_note_mine_game", "reallife_ui_estate_note_notice",
            "reallife_estate_tenants_what", "reallife_estate_tenants_rent_what", "reallife_ui_estate_note_works", "reallife_ui_estate_note_no_new", "reallife_ui_estate_note_wear",
            "reallife_estate_works_market", "reallife_estate_hire_what"
        };
        string everything = mine + trade + works + market;
        var lost = new List<string>();
        foreach (string key in oldNotes)
        {
            if (!everything.Contains("\"" + key + "\""))
            {
                lost.Add(key);
            }
        }
        Check(lost.Count == 0 && everything.Contains("_m.Notes = "), "Property: every explanation the old Market, Mine, Tenants, Works, Hotel and Hire pages carried is a note of the page now" + (lost.Count > 0 ? " (missing " + string.Join(", ", lost) + ")" : string.Empty));
        Check(face.Contains("NotesBlock(m, w)") && face.Contains("Notes(s, at, m, x, under, w)") && MemberText(face, "private static void RollPage(").Contains("notes"),
            "Property: the roll's and a tab's table give up the room the notes take, so the table never runs under them");
        Check(mine.Contains("Card = BuildingCard(h)") || (mine.Contains("own.Card = BuildingCard(h)") && mine.Contains("unitRow.Card = card")), "Property: a building's, a unit's, a hotel's and a space's row of the roll carries the worth, the price paid and the year's figures");
        Check(mine.Contains("EstateText.T(\"reallife_estate_col_worth\")") && mine.Contains("\"reallife_estate_col_paid\"") && mine.Contains("\"reallife_estate_col_year_in\"") && mine.Contains("\"reallife_estate_col_year_out\"")
            && trade.Contains("reallife_ui_estate_card_kept_night") && trade.Contains("f.TakingsYear") && trade.Contains("f.CostsYear"),
            "Property: the Mine table's worth, paid and year columns and the Hotel table's two year columns are the old ones' figures, all on a card");
        Check(works.Contains("Card = !u.Let ? null") && works.Contains("\"reallife_estate_col_owed\"") && works.Contains("\"reallife_ui_estate_lease_ends\""), "Property: a tenant's row carries what is owed and the day the lease ends, the old table's two columns");
        Check(works.Contains("TakeFilter(places)") && works.Contains("\"reallife_ui_estate_all_buildings\"") && model.Contains("public string FilterPlace;") && model.Contains("FilterPlace = null;"),
            "Property: the tenants' table takes a building filter that a link on a building's page sets, and a tab opened by hand starts without one");

        // The last line of a list that shows fewer than there are.
        Check(unit.Contains("MoreRow(\"reallife_ui_estate_more_flats\"") && unit.Contains("MoreRow(\"reallife_ui_estate_more_applicants\"") && unit.Contains("MoreRow(\"reallife_ui_estate_more_visits\"")
            && building.Contains("MoreRow(\"reallife_ui_estate_more_flats\"") && building.Contains("MoreRow(\"reallife_ui_estate_more_visits\""),
            "Property: a unit's and a building's short lists of flats, applicants and visits end in a line that says how many there are and opens them");
        Check(mine.Contains("MoreRow(\"reallife_ui_estate_more_repairs\"") && works.Contains("MoreRow(\"reallife_ui_estate_more_late\"") && market.Contains("MoreRow(\"reallife_ui_estate_more_listings\"")
            && trade.Contains("MoreRow(\"reallife_ui_estate_more_cars\""), "Property: the repairs, the late tenants, a district's listings and the cars not on hire end in such a line too");
        Check(unit.Contains("_v.Tab = Array.IndexOf(NavTab, EstateDirector.TabTenants);") || mine.Contains("_v.Tab = Array.IndexOf(NavTab, EstateDirector.TabTenants);"), "Property: the flats' and applicants' lines open the Tenants tab");
        Check(!model.Contains("DayWords") && !model.Contains("ListALink") && !model.Contains("ListBLink"), "Property: fields nothing filled are gone");
        Check(Map5Source("Ui/Services/ServicesModel.cs").Contains("public string Guide;") && !Map5Source("Ui/Services/ServicesModel.cs").Contains("public UiFigureModel[] Strip") && !Map5Source("Ui/Services/ServicesModel.cs").Contains("public string Back;"),
            "Services: a band of figures and a way back that nothing filled are gone from its model");
        Check(Map5Source("Ui/Estate/EstateLive.cs").Contains("m.Prompts = Hints();") && Map5Source("Ui/Estate/DealerLive.cs").Contains("m.Prompts = EstateLive.Hints();")
            && Map5Source("Ui/Services/ServicesLive.cs").Contains("new UiPrompt { Action = UiAction.Move") && Map5Source("Ui/Services/ServicesLive.Round.cs").Contains("reallife_ui_svc_more_visits")
            && Map5Source("Ui/Services/ServicesLive.Sell.cs").Contains("reallife_ui_svc_more_after"), "Property, the showroom and Services: the pad's hints stand on the prompt bar (move, select, back), and a Services crew member's visits and the sale's dues say how many more there are");

        // The map: the game's building box is hidden for a picked building whether or not it was found when the dock stood up, and a full list of saved places says so.
        string game = Map5Source("Ui/MapDock/MapFiltersGame.cs");
        string picked = Map5Source("Ui/MapDock/MapLive.Picked.cs");
        Check(game.Contains("_conceal = conceal;") && MemberText(game, "private void HideCard()").Contains("_conceal.Add(card)") && game.Contains("new MapLive(found, _subway, HideCard)")
            && picked.Contains("_hideCard?.Invoke();") && MemberText(game, "private static Transform CardPanel()").Contains("data.parent"),
            "map dock: a newly picked building hides the game's own box again, found by its panel or by the parent of its halves, so the dock's block never stands beside it");
        MapPlaceModel place = UiSamples.MapPlace(UiWords("en"), "overview");
        place.Saved = false;
        place.SaveLabel = "Save";
        place.SaveFullLabel = "List is full";
        var placeScene = new UiScene();
        place.SaveFull = true;
        UiMapPlace.Lay(placeScene, place, frame, true, UiDefaultKeys.Keyboard);
        int save = placeScene.FindNode(UiMapPlace.SaveCommand, 0);
        bool saveOff = save >= 0 && placeScene.Nodes[save].Disabled && EstateTexts(placeScene).Contains("List is full");
        place.SaveFull = false;
        UiMapPlace.Lay(placeScene, place, frame, true, UiDefaultKeys.Keyboard);
        save = placeScene.FindNode(UiMapPlace.SaveCommand, 0);
        Check(saveOff && save >= 0 && !placeScene.Nodes[save].Disabled, "map place card: with the saved places full, Save says so and takes no press, and with room it is a button again");
        Check(MemberText(Map5Source("Ui/MapDock/MapLive.Place.cs"), "public bool Save()").Contains("MapSaved.Most"), "map place card: a full list refuses a new place instead of dropping the oldest");
    }

    // A source with a table of three rows, three sentences under it and cards on two of its rows, to lay the screen out against.
    private sealed class NotedEstate : IEstateSource
    {
        public int Generation => 0;

        public void Opened(EstateView view)
        {
        }

        public void Fill(EstateModel m, EstateView v)
        {
            m.Title = "Property";
            m.Nav = new[] { new UiNavEntry { Label = "Mine", Icon = UiIcon.Home }, new UiNavEntry { Label = "Tenants", Icon = UiIcon.People } };
            m.Tab = v.Tab;
            m.Shape = EstateShape.Table;
            m.HeadTitle = "Table";
            var cols = new[]
            {
                new UiDataColumn { Title = "Name", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Price", Kind = UiCellKind.Money, Width = 120f, Sortable = true }
            };
            var rows = new List<UiDataRow>();
            for (int i = 0; i < 3; i++)
            {
                rows.Add(new UiDataRow
                {
                    Cells = new[] { "Row " + i, "$" + (100 * (3 - i)) }, SortKeys = new long[] { TableRules.None, 100 * (3 - i) },
                    Card = i == 0 ? null : new UiDataCard { Column = 1, Title = "Row " + i, Labels = new[] { "Owed this year" }, Values = new[] { "$5" } }
                });
            }
            m.Table = new UiDataTableModel { Columns = cols, Rows = rows.ToArray(), RowCount = rows.Count };
            m.Table.Hover = -1;
            m.RowKeys = new[] { "a", "b", "c" };
            m.Notes = new[] { "What moves a price.", "What moves a rent.", "How a mortgage goes." };
        }
    }
}

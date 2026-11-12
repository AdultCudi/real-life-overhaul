using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Fame;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private static readonly string[] FameKinds =
        {
            "reallife_fame_kind_part", "reallife_fame_kind_district", "reallife_fame_kind_change", "reallife_fame_kind_invite", "reallife_fame_kind_promise", "reallife_fame_kind_request",
            "reallife_fame_kind_council"
        };

        private static readonly string[] FameLabelKeys = { "reallife_fame_label_part_0", "reallife_fame_label_part_1", "reallife_fame_label_part_2", "reallife_fame_label_part_3" };
        private readonly List<FameLever> _levers = new List<FameLever>(16);

        private static UiRole FameTone(int value) => value < 35 ? UiRole.Danger : value < 50 ? UiRole.Warn : value >= 65 ? UiRole.Ok : UiRole.Muted;

        // ------------------------------------------------------------------------------------------------- Standing

        // How the city sees you: the reputation and its four parts with what each means, then every row of it, the one chosen explained with what can be done.
        private void ReadStanding(FinanceFacts f, FinanceView v)
        {
            FinanceStandingFacts s = f.Standing;
            FameDirector director = FameDirector.Instance;
            FameFile file = director != null ? director.File : null;
            UtilitiesWords w = W;
            if (file == null)
            {
                f.StandingOn = false;
                return;
            }
            int day = FameGame.Day;
            s.Profile = FameText.Profile(FameRules.Profile(file.Wealth, file.Recognition, file.Overall));
            s.OverallValue = file.Overall;
            s.Overall = FameText.Opinion(file.Overall);
            s.Scandal = FameCity.InScandal(file, day) ? FameText.Fill("reallife_fame_scandal_line", "day", FameText.N(file.ScandalUntil)) : null;
            s.PartNames = new string[4];
            s.PartWords = new string[4];
            s.PartMarks = new int[4];
            s.PartNotes = new string[4];
            for (int i = 0; i < 4; i++)
            {
                s.PartNames[i] = FameText.Part((FamePart)i);
                s.PartWords[i] = FameText.Opinion(file.Rep[i]);
                s.PartMarks[i] = file.Rep[i];
                s.PartNotes[i] = FameText.T(FameText.PartAboutKeys[i]);
            }
            var rows = new List<FinanceRow>(24);
            var ids = new List<string>(24);
            StandingRows(file, rows, ids, w, day);
            int selected = ids.IndexOf(v.Standing);
            if (selected < 0)
            {
                selected = ids.Count > 0 ? 0 : -1;
                v.Standing = selected >= 0 ? ids[0] : null;
            }
            s.Ids = ids.ToArray();
            s.Table = FinanceKitTables.Make(FinanceKitTables.StandingColumns(w), rows, selected, w.T("reallife_fin_standing_none", "Nothing yet"),
                FameText.T("reallife_fame_empty"), UiIcon.Flag);
            s.Card = selected >= 0 ? StandingCard(director, file, v.Standing, w, day) : null;
        }

        private static FinanceRow Fr(string name, string sub, UiIcon icon, string stands, UiRole tone, string heading)
        {
            return new FinanceRow { Name = name, Sub = sub, Icon = icon, Cells = new[] { stands, heading ?? string.Empty }, Tones = new[] { tone, UiRole.None } };
        }

        private void StandingRows(FameFile f, List<FinanceRow> rows, List<string> ids, UtilitiesWords w, int day)
        {
            string part = FameText.T(FameKinds[0]);
            rows.Add(Fr(FameText.T("reallife_fame_row_wealth"), part, UiIcon.Coin, FameText.N(f.Wealth), FameTone(f.Wealth),
                FameText.Fill("reallife_fame_row_worth", "worth", FameText.Money(f.NetWorth))));
            ids.Add("wealth");
            rows.Add(Fr(FameText.T("reallife_fame_row_recognition"), part, UiIcon.Megaphone, FameText.N(f.Recognition), FameTone(f.Recognition), FameText.Tier(f.Tier)));
            ids.Add("recognition");
            for (int i = 0; i < 4; i++)
            {
                rows.Add(Fr(FameText.Part((FamePart)i), part, UiIcon.Star, FameText.Opinion(f.Rep[i]), FameTone(f.Rep[i]),
                    i == (int)FamePart.Integrity ? FameText.T("reallife_fame_heading_deeds") : FameText.Fill("reallife_fame_heading", "value", FameText.N(f.Target[i]))));
                ids.Add("part:" + i);
            }
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                rows.Add(Fr(FameGame.DistrictName(d.Name), FameText.T(FameKinds[1]), UiIcon.MapPin, FameText.Opinion(d.Opinion), FameTone(d.Opinion),
                    FameText.Fill("reallife_fame_known_by", "value", FameText.N(d.Known))));
                ids.Add("district:" + i);
            }
            for (int i = f.Invites.Count - 1; i >= 0; i--)
            {
                FameInvite v = f.Invites[i];
                rows.Add(Fr(FameText.InviteName(v.Kind), FameText.T(FameKinds[3]), UiIcon.Envelope, FameText.InviteState(v.State),
                    v.State == FameInviteState.Offered ? UiRole.Warn : v.State == FameInviteState.Missed ? UiRole.Danger : v.State == FameInviteState.Accepted ? UiRole.Ok : UiRole.Muted,
                    FameText.Fill("reallife_fame_when", "day", FameText.N(v.Day), "time", FameText.Hour(v.Hour))));
                ids.Add("invite:" + v.Id);
            }
            for (int i = f.Promises.Count - 1; i >= 0; i--)
            {
                FamePromise p = f.Promises[i];
                rows.Add(Fr(FameText.PromiseWords(p.Kind), FameText.T(FameKinds[4]), UiIcon.Handshake, FameText.PromiseState(p.State),
                    p.State == FamePromiseState.Kept ? UiRole.Ok : p.State == FamePromiseState.Broken ? UiRole.Danger : UiRole.Warn,
                        FameText.Fill("reallife_fame_by_day", "day", FameText.N(p.Due))));
                ids.Add("promise:" + p.Id);
            }
            for (int i = f.Requests.Count - 1; i >= 0; i--)
            {
                FameRequest q = f.Requests[i];
                rows.Add(Fr(FameText.RequestTitle(q), FameText.T(FameKinds[5]), UiIcon.Scales, FameText.RequestState(q.State),
                    q.State == FameRequestState.Open ? UiRole.Warn : q.State == FameRequestState.Accepted ? UiRole.Ok : UiRole.Muted, FameText.Money(q.Amount)));
                ids.Add("request:" + q.Id);
            }
            for (int i = f.Lobbies.Count - 1; i >= 0; i--)
            {
                FameLobby l = f.Lobbies[i];
                rows.Add(Fr(FameText.T("reallife_fame_lobby_row"), FameText.T(FameKinds[6]), UiIcon.Building, FameText.LobbyState(l.State),
                    l.State == FameLobbyState.Won ? UiRole.Ok : l.State == FameLobbyState.Lost ? UiRole.Danger : UiRole.Warn,
                        FameText.Fill("reallife_fame_percent", "value", FameText.N(l.Chance))));
                ids.Add("lobby:" + l.Id);
            }
            _levers.Clear();
            if (FameCity.MayLobby(f, day) && FameBridges.Levers != null)
            {
                try
                {
                    FameBridges.Levers(_levers);
                }
                catch (Exception)
                {
                    _levers.Clear();
                }
                for (int i = 0; i < _levers.Count; i++)
                {
                    rows.Add(Fr(_levers[i].Name, FameText.T(FameKinds[6]), UiIcon.Building, FameText.Fill("reallife_fame_about_percent", "value", FameText.N(LobbyChance(f))), UiRole.Muted,
                        FameText.Money(FameCity.LobbyFee(f.Weekly))));
                    ids.Add("council:" + _levers[i].Lever + ":" + (_levers[i].Position + 1));
                }
            }
            for (int i = f.Notes.Count - 1; i >= 0 && i >= f.Notes.Count - 12; i--)
            {
                FameNote n = f.Notes[i];
                bool press = n.Part == FameNote.Recognition;
                string amount = press ? FameText.Fill("reallife_fame_press_change", "value", (n.Delta / 10f).ToString("+0.0;-0.0", CultureInfo.InvariantCulture)) : FameText.Signed(n.Delta);
                rows.Add(Fr(FameText.Cause(n.Cause) + (n.District != null ? ", " + FameGame.DistrictName(n.District) : string.Empty),
                    FameText.T(FameKinds[2]), n.Delta > 0 ? UiIcon.TrendUp : UiIcon.TrendDown,
                    amount, n.Delta > 0 ? UiRole.Ok : UiRole.Danger, FameText.Fill("reallife_fame_day", "day", FameText.N(n.Day))));
                ids.Add("note:" + i);
            }
        }

        private static int LobbyChance(FameFile f)
        {
            return FameRules.Clamp(FameCity.LobbyBase(f) - (FameCity.RivalLeast + FameCity.RivalMost) / 2, FameCity.LobbyLeast, FameCity.LobbyMost);
        }

        private static int NumberAfter(string id, int from)
        {
            return id != null && id.Length > from && int.TryParse(id.Substring(from), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
        }

        private UtilitiesCard StandingCard(FameDirector director, FameFile f, string id, UtilitiesWords w, int day)
        {
            var card = new UtilitiesCard();
            var lines = new List<UtilitiesLine>(6);
            var actions = new List<UiActionModel>(3);
            if (id == "wealth")
            {
                card.Title = FameText.T("reallife_fame_row_wealth");
                lines.Add(Pair(FameText.T("reallife_fame_label_wealth").TrimEnd(' ', ':'), FameText.N(f.Wealth)));
                lines.Add(Say(FameText.Fill("reallife_fame_wealth_parts", "worth", FameText.Money(f.NetWorth), "week", FameText.Money(f.Weekly),
                    "count", FameText.N(f.Businesses), "properties", FameText.N(f.Properties))));
                lines.Add(Say(FameText.T("reallife_fame_wealth_about")));
            }
            else if (id == "recognition")
            {
                card.Title = FameText.T("reallife_fame_row_recognition");
                lines.Add(Pair(FameText.T("reallife_fame_label_recognition").TrimEnd(' ', ':'), FameText.Fill("reallife_fame_recognition_value",
                    "value", FameText.N(f.Recognition), "tier", FameText.Tier(f.Tier))));
                lines.Add(Say(FameText.Fill("reallife_fame_recognition_parts", "followers", FameText.N(f.Followers), "press", FameRules.Tenths(f.Press))));
                lines.Add(Say(FameText.T("reallife_fame_recognition_about")));
            }
            else if (id.StartsWith("part:", StringComparison.Ordinal))
            {
                int i = FameRules.Clamp(NumberAfter(id, 5), 0, 3);
                card.Title = FameText.Part((FamePart)i);
                lines.Add(Pair(FameText.T(FameLabelKeys[i]).TrimEnd(' ', ':'), FameText.Rep(f.Rep[i])));
                if (i != (int)FamePart.Integrity)
                {
                    lines.Add(Say(FameText.Fill("reallife_fame_page_heading", "value", FameText.N(f.Target[i]))));
                }
                lines.Add(Say(FameText.T(FameText.PartAboutKeys[i])));
            }
            else if (id.StartsWith("district:", StringComparison.Ordinal))
            {
                int i = NumberAfter(id, 9);
                FameDistrict d = i >= 0 && i < f.Districts.Count ? f.Districts[i] : null;
                if (d == null)
                {
                    return null;
                }
                card.Title = FameGame.DistrictName(d.Name);
                lines.Add(Pair(FameText.T("reallife_fame_label_opinion").TrimEnd(' ', ':'), FameText.Rep(d.Opinion)));
                lines.Add(Pair(FameText.T("reallife_fame_label_known").TrimEnd(' ', ':'), FameText.N(d.Known)));
                lines.Add(Say(FameText.Fill("reallife_fame_page_district", "shops", FameText.N(d.Shops), "jobs", FameText.N(d.Jobs), "raised", FameText.N(d.Raised))));
                lines.Add(Say(FameText.T("reallife_fame_district_about")));
            }
            else if (id.StartsWith("invite:", StringComparison.Ordinal))
            {
                FameInvite v = f.Invite(NumberAfter(id, 7));
                if (v == null)
                {
                    return null;
                }
                card.Title = FameText.InviteName(v.Kind);
                card.Chip = FameText.InviteState(v.State);
                lines.Add(Pair(FameText.T("reallife_fame_label_when").TrimEnd(' ', ':'), FameText.Fill("reallife_fame_when", "day", FameText.N(v.Day), "time", FameText.Hour(v.Hour))));
                if (v.State == FameInviteState.Accepted)
                {
                    lines.Add(Say(FameText.T("reallife_fame_invite_accepted")));
                    if (FamePress.Joinable(v, FameGame.Day, FameGame.MinuteOfDay / 60))
                    {
                        actions.Add(CardButton(FameText.T("reallife_fame_join"), UiButtonKind.Primary, UiIcon.Phone, FinanceAct.Join, true));
                    }
                }
                if (v.State == FameInviteState.Offered)
                {
                    lines.Add(Say(FameText.Fill("reallife_fame_invite_answer", "day", FameText.N(v.AnswerBy))));
                    actions.Add(CardButton(FameText.T("reallife_fame_decline"), UiButtonKind.Secondary, UiIcon.Close, FinanceAct.Refuse, false));
                    actions.Add(CardButton(FameText.T("reallife_fame_accept"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.Accept, true));
                }
            }
            else if (id.StartsWith("promise:", StringComparison.Ordinal))
            {
                int pid = NumberAfter(id, 8);
                FamePromise p = null;
                for (int i = 0; i < f.Promises.Count; i++)
                {
                    p = f.Promises[i].Id == pid ? f.Promises[i] : p;
                }
                if (p == null)
                {
                    return null;
                }
                card.Title = FameText.T("reallife_fame_promise_title");
                card.Chip = FameText.PromiseState(p.State);
                lines.Add(Say(FameText.Fill("reallife_fame_promise_made", "promise", FameText.PromiseWords(p.Kind), "day", FameText.N(p.Made), "where", FameText.Cause(p.Source))));
                lines.Add(Say(FameText.T("reallife_fame_promise_about")));
            }
            else if (id.StartsWith("request:", StringComparison.Ordinal))
            {
                FameRequest q = FameCity.Request(f, NumberAfter(id, 8));
                if (q == null)
                {
                    return null;
                }
                card.Title = FameText.RequestTitle(q);
                card.Chip = FameText.RequestState(q.State);
                if (q.State == FameRequestState.Open)
                {
                    lines.Add(Say(FameText.Fill("reallife_fame_request_answer", "day", FameText.N(q.Until))));
                    actions.Add(CardButton(FameText.T("reallife_fame_refuse"), UiButtonKind.Secondary, UiIcon.Close, FinanceAct.Refuse, false));
                    actions.Add(CardButton(FameText.T("reallife_fame_accept"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.Accept, true));
                }
                lines.Add(Say(FameText.RequestAbout(q)));
            }
            else if (id.StartsWith("lobby:", StringComparison.Ordinal))
            {
                int lid = NumberAfter(id, 6);
                FameLobby l = null;
                for (int i = 0; i < f.Lobbies.Count; i++)
                {
                    l = f.Lobbies[i].Id == lid ? f.Lobbies[i] : l;
                }
                if (l == null)
                {
                    return null;
                }
                card.Title = FameText.T("reallife_fame_lobby_row");
                card.Chip = FameText.LobbyState(l.State);
                lines.Add(Say(FameText.Fill("reallife_fame_lobby_line", "day", FameText.N(l.Day), "chance", FameText.N(l.Chance), "rival", FameText.N(l.Rival))));
                if (l.State == FameLobbyState.Pending)
                {
                    lines.Add(Say(FameText.Fill("reallife_fame_lobby_decides", "day", FameText.N(l.DecideDay))));
                }
            }
            else if (id.StartsWith("council:", StringComparison.Ordinal))
            {
                string[] parts = id.Split(':');
                if (parts.Length < 3 || !int.TryParse(parts[1], out int lever) || !int.TryParse(parts[2], out int position))
                {
                    return null;
                }
                position -= 1;
                FameLever found = default(FameLever);
                bool there = false;
                for (int i = 0; i < _levers.Count; i++)
                {
                    if (_levers[i].Lever == lever && _levers[i].Position == position)
                    {
                        found = _levers[i];
                        there = true;
                    }
                }
                if (!there)
                {
                    return null;
                }
                card.Title = found.Name;
                if (!string.IsNullOrEmpty(found.Effect))
                {
                    lines.Add(Say(found.Effect, UiRole.Text));
                }
                lines.Add(Say(FameText.Fill("reallife_fame_lobby_about", "influence", FameText.N(FameCity.Influence(f)), "chance",
                    FameText.N(LobbyChance(f)), "fee", FameText.Money(FameCity.LobbyFee(f.Weekly)),
                    "days", FameText.N(FameCity.LobbyDecideDays))));
                if (FameCity.MayLobby(f, day))
                {
                    actions.Add(CardButton(FameText.T("reallife_fame_lobby_do"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.Lobby, true));
                }
            }
            else
            {
                int i = NumberAfter(id, 5);
                FameNote n = i >= 0 && i < f.Notes.Count ? f.Notes[i] : null;
                if (n == null)
                {
                    return null;
                }
                card.Title = FameText.Cause(n.Cause);
                lines.Add(Pair(FameText.T("reallife_fame_col_what"),
                    n.Part == FameNote.Recognition ? FameText.T("reallife_fame_row_recognition") : FameText.Part((FamePart)FameRules.Clamp(n.Part, 0, 3))));
                lines.Add(Pair(FameText.T("reallife_fame_col_stands"), n.Part == FameNote.Recognition ? FameText.Fill("reallife_fame_press_change",
                    "value", (n.Delta / 10f).ToString("+0.0;-0.0", CultureInfo.InvariantCulture)) : FameText.Signed(n.Delta),
                    n.Delta > 0 ? UiRole.OkText : UiRole.DangerText));
                lines.Add(Pair(FameText.T("reallife_fame_label_when").TrimEnd(' ', ':'), FameText.Fill("reallife_fame_day", "day", FameText.N(n.Day))));
            }
            card.Lines = lines.ToArray();
            card.Actions = actions.ToArray();
            if (!string.IsNullOrEmpty(card.Chip))
            {
                card.ChipTone = UiRole.Accent;
            }
            return card;
        }

        // ------------------------------------------------------------------------------------------------- doing

        private bool DoStanding(FinanceAct act, FinanceView v, int arg)
        {
            FameDirector director = FameDirector.Instance;
            string id = v.Standing;
            if (director == null || id == null)
            {
                return false;
            }
            if (id.StartsWith("request:", StringComparison.Ordinal) && (act == FinanceAct.Accept || act == FinanceAct.Refuse))
            {
                director.AnswerRequest(NumberAfter(id, 8), act == FinanceAct.Accept);
                return true;
            }
            if (id.StartsWith("invite:", StringComparison.Ordinal))
            {
                int invite = NumberAfter(id, 7);
                if (act == FinanceAct.Join)
                {
                    director.JoinInvite(invite);
                    return true;
                }
                if (act == FinanceAct.Accept || act == FinanceAct.Refuse)
                {
                    director.AnswerInvite(invite, act == FinanceAct.Accept);
                    return true;
                }
            }
            if (act == FinanceAct.Lobby && id.StartsWith("council:", StringComparison.Ordinal))
            {
                string[] parts = id.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int lever) && int.TryParse(parts[2], out int position))
                {
                    director.Lobby(lever, position - 1);
                    return true;
                }
            }
            return false;
        }
    }
}

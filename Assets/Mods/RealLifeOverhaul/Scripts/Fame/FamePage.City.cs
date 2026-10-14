using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Fame
{
    // The Standing page's requests and the council: a row each in the table (what the city asks, the changes the council
    // could be pushed to make while the player has the influence, a push being decided), and the one picked in the white
    // box with its buttons. The changes on offer are the council's own (FameBridges.Levers), read when the page is written.
    public static partial class FamePage
    {
        private static readonly List<FameLever> Levers = new List<FameLever>(16);

        static partial void CityRows(FameDirector director, FameFile f, Func<string, TableRow> row, int day)
        {
            string kind = FameText.T(Kinds[KindRequest]);
            for (int i = f.Requests.Count - 1; i >= 0; i--)
            {
                FameRequest q = f.Requests[i];
                TableRow r = row("request:" + q.Id);
                Cells(r, KindRequest, FameText.RequestTitle(q), kind, (int)q.State, FameText.RequestState(q.State), FameText.Money(q.Amount), q.Amount,
                    q.State == FameRequestState.Open ? FameText.Fill("reallife_fame_answer_by", "day", FameText.N(q.Until)) : string.Empty, q.Until);
                r.Tones[2] = (byte)(q.State == FameRequestState.Open ? TableRow.Yellow : q.State == FameRequestState.Accepted ? TableRow.Green : TableRow.Plain);
            }
            kind = FameText.T(Kinds[KindCouncil]);
            for (int i = f.Lobbies.Count - 1; i >= 0; i--)
            {
                FameLobby l = f.Lobbies[i];
                TableRow r = row("lobby:" + l.Id);
                Cells(r, KindCouncil, FameText.T("reallife_fame_lobby_row"), kind, (int)l.State, FameText.LobbyState(l.State),
                    FameText.Fill("reallife_fame_percent", "value", FameText.N(l.Chance)), l.Chance, FameText.Fill("reallife_fame_by_day", "day", FameText.N(l.DecideDay)), l.DecideDay);
                r.Tones[2] = (byte)(l.State == FameLobbyState.Won ? TableRow.Green : l.State == FameLobbyState.Lost ? TableRow.Red : TableRow.Yellow);
            }
            Levers.Clear();
            if (!FameCity.MayLobby(f, day) || FameBridges.Levers == null)
            {
                return;
            }
            try
            {
                FameBridges.Levers(Levers);
            }
            catch (Exception)
            {
                Levers.Clear();
            }
            int chance = FameRules.Clamp(FameCity.LobbyBase(f) - (FameCity.RivalLeast + FameCity.RivalMost) / 2, FameCity.LobbyLeast, FameCity.LobbyMost);
            string fee = FameText.Money(FameCity.LobbyFee(f.Weekly));
            for (int i = 0; i < Levers.Count; i++)
            {
                TableRow r = row("council:" + Levers[i].Lever + ":" + (Levers[i].Position + 1));
                Cells(r, KindCouncil, Levers[i].Name, kind, chance, FameText.Fill("reallife_fame_about_percent", "value", FameText.N(chance)), fee, FameCity.LobbyFee(f.Weekly),
                    FameText.T("reallife_fame_lobby_offer"), 0);
            }
        }

        static partial void CityBox(FameDirector director, FameFile f, InfoBox box, string picked, Action<string> pick, Action back, ref bool written)
        {
            Action close = () => pick(null);
            int day = FameGame.Day;
            if (picked.StartsWith("request:", StringComparison.Ordinal))
            {
                FameRequest q = FameCity.Request(f, Number(picked, 8));
                if (q == null)
                {
                    return;
                }
                box.Title(FameText.RequestTitle(q));
                box.Chip(FameText.RequestState(q.State));
                box.Section("reallife_look_sec_now");
                if (q.State == FameRequestState.Open)
                {
                    box.Line(FameText.Fill("reallife_fame_request_answer", "day", FameText.N(q.Until)));
                    UnityEngine.Transform buttons = box.ButtonRow();
                    int id = q.Id;
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_fame_accept", () => { director.AnswerRequest(id, true); pick(picked); });
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_fame_refuse", () => { director.AnswerRequest(id, false); pick(picked); });
                }
                Buttons(box, close, back, FameText.RequestAbout(q));
                written = true;
                return;
            }
            if (picked.StartsWith("lobby:", StringComparison.Ordinal))
            {
                FameLobby l = null;
                int id = Number(picked, 6);
                for (int i = 0; i < f.Lobbies.Count; i++)
                {
                    l = f.Lobbies[i].Id == id ? f.Lobbies[i] : l;
                }
                if (l == null)
                {
                    return;
                }
                box.Title(FameText.T("reallife_fame_lobby_row"));
                box.Chip(FameText.LobbyState(l.State));
                box.Section("reallife_look_sec_now");
                box.Line(FameText.Fill("reallife_fame_lobby_line", "day", FameText.N(l.Day), "chance", FameText.N(l.Chance), "rival", FameText.N(l.Rival)));
                if (l.State == FameLobbyState.Pending)
                {
                    box.Line(FameText.Fill("reallife_fame_lobby_decides", "day", FameText.N(l.DecideDay)));
                }
                Buttons(box, close, back);
                written = true;
                return;
            }
            if (picked.StartsWith("council:", StringComparison.Ordinal))
            {
                string[] parts = picked.Split(':');
                int lever, position;
                if (parts.Length < 3 || !int.TryParse(parts[1], out lever) || !int.TryParse(parts[2], out position))
                {
                    return;
                }
                position -= 1;
                FameLever found = default(FameLever);
                bool there = false;
                for (int i = 0; i < Levers.Count; i++)
                {
                    if (Levers[i].Lever == lever && Levers[i].Position == position)
                    {
                        found = Levers[i];
                        there = true;
                    }
                }
                if (!there)
                {
                    return;
                }
                int chance = FameRules.Clamp(FameCity.LobbyBase(f) - (FameCity.RivalLeast + FameCity.RivalMost) / 2, FameCity.LobbyLeast, FameCity.LobbyMost);
                box.Title(found.Name);
                if (!string.IsNullOrEmpty(found.Effect))
                {
                    box.Line(found.Effect);
                }
                box.Note(FameText.Fill("reallife_fame_lobby_about", "influence", FameText.N(FameCity.Influence(f)), "chance", FameText.N(chance), "fee", FameText.Money(FameCity.LobbyFee(f.Weekly)),
                    "days", FameText.N(FameCity.LobbyDecideDays)));
                if (FameCity.MayLobby(f, day))
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_fame_lobby_do", () => { director.Lobby(lever, position); pick(null); });
                }
                Buttons(box, close, back);
                written = true;
            }
        }
    }
}

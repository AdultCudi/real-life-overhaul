using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Fame
{
    // The Standing page's invitations and promises (and, FamePage.City.cs, the city's requests and the council): a row
    // each in the table, and the one picked in the white box with what can be done about it.
    public static partial class FamePage
    {
        static partial void CityRows(FameDirector director, FameFile f, Func<string, TableRow> row, int day);
        static partial void CityBox(FameDirector director, FameFile f, InfoBox box, string picked, Action<string> pick, Action back, ref bool written);

        static partial void MoreRows(FameDirector director, FameFile f, Func<string, TableRow> row, int day)
        {
            string kind = FameText.T(Kinds[KindInvite]);
            for (int i = f.Invites.Count - 1; i >= 0; i--)
            {
                FameInvite v = f.Invites[i];
                TableRow r = row("invite:" + v.Id);
                Cells(r, KindInvite, FameText.InviteName(v.Kind), kind, (int)v.State, FameText.InviteState(v.State),
                    FameText.Fill("reallife_fame_when", "day", FameText.N(v.Day), "time", FameText.Hour(v.Hour)), v.Day * 24 + v.Hour,
                    v.State == FameInviteState.Offered ? FameText.Fill("reallife_fame_answer_by", "day", FameText.N(v.AnswerBy)) : string.Empty, v.AnswerBy);
                r.Tones[2] = (byte)(v.State == FameInviteState.Offered ? TableRow.Yellow : v.State == FameInviteState.Missed ? TableRow.Red : v.State == FameInviteState.Accepted ? TableRow.Green : TableRow.Plain);
            }
            kind = FameText.T(Kinds[KindPromise]);
            for (int i = f.Promises.Count - 1; i >= 0; i--)
            {
                FamePromise p = f.Promises[i];
                TableRow r = row("promise:" + p.Id);
                Cells(r, KindPromise, FameText.PromiseWords(p.Kind), kind, (int)p.State, FameText.PromiseState(p.State),
                    FameText.Fill("reallife_fame_by_day", "day", FameText.N(p.Due)), p.Due, FameText.Cause(p.Source), p.Made);
                r.Tones[2] = (byte)(p.State == FamePromiseState.Kept ? TableRow.Green : p.State == FamePromiseState.Broken ? TableRow.Red : TableRow.Yellow);
            }
            CityRows(director, f, row, day);
        }

        static partial void MoreBox(FameDirector director, FameFile f, InfoBox box, string picked, Action<string> pick, Action back, ref bool written)
        {
            Action close = () => pick(null);
            if (picked.StartsWith("invite:", StringComparison.Ordinal))
            {
                FameInvite v = f.Invite(Number(picked, 7));
                if (v == null)
                {
                    return;
                }
                box.Title(FameText.InviteName(v.Kind));
                box.Line(FameText.Pair("reallife_fame_label_state", FameText.InviteState(v.State)));
                if (v.State == FameInviteState.Accepted)
                {
                    box.Line(FameText.T("reallife_fame_invite_accepted"));
                }
                box.Line(FameText.Pair("reallife_fame_label_when", FameText.Fill("reallife_fame_when", "day", FameText.N(v.Day), "time", FameText.Hour(v.Hour))));
                if (v.State == FameInviteState.Offered)
                {
                    box.Line(FameText.Fill("reallife_fame_invite_answer", "day", FameText.N(v.AnswerBy)));
                    UnityEngine.Transform buttons = box.ButtonRow();
                    int id = v.Id;
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_fame_accept", () => { director.AnswerInvite(id, true); pick(picked); });
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_fame_decline", () => { director.AnswerInvite(id, false); pick(picked); });
                }
                Buttons(box, close, back, FameText.T(InviteAbout(v.Kind)));
                written = true;
                return;
            }
            if (picked.StartsWith("promise:", StringComparison.Ordinal))
            {
                int id = Number(picked, 8);
                FamePromise p = null;
                for (int i = 0; i < f.Promises.Count; i++)
                {
                    p = f.Promises[i].Id == id ? f.Promises[i] : p;
                }
                if (p == null)
                {
                    return;
                }
                box.Title(FameText.T("reallife_fame_promise_title"));
                box.Line(FameText.Pair("reallife_fame_label_state", FameText.PromiseState(p.State)));
                box.Line(FameText.Fill("reallife_fame_promise_made", "promise", FameText.PromiseWords(p.Kind), "day", FameText.N(p.Made), "where", FameText.Cause(p.Source)));
                box.Line(FameText.Fill(p.Kind == FamePromiseKind.Jobs ? "reallife_fame_promise_goal_jobs" : p.Kind == FamePromiseKind.Charity ? "reallife_fame_promise_goal_charity" : "reallife_fame_promise_goal_rep",
                    "goal", FameText.N(p.Goal), "now", FameText.N(p.Kind == FamePromiseKind.Jobs ? f.Staff : p.Kind == FamePromiseKind.Wages ? f.Rep[1] : f.Rep[0]), "day", FameText.N(p.Due)));
                Buttons(box, close, back, FameText.T("reallife_fame_promise_about"));
                written = true;
                return;
            }
            CityBox(director, f, box, picked, pick, back, ref written);
        }

        private static string InviteAbout(FameInviteKind kind)
        {
            switch (kind)
            {
                case FameInviteKind.Show: return "reallife_fame_invite_about_0";
                case FameInviteKind.Magazine: return "reallife_fame_invite_about_1";
                case FameInviteKind.Investors: return "reallife_fame_invite_about_2";
                default: return "reallife_fame_invite_about_3";
            }
        }
    }
}

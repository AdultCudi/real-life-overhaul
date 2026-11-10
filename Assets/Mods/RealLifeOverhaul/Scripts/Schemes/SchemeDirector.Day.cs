using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Schemes
{
    // The day: the game is read once, the rules play the day, and what they did is carried out: money, notices, the court.
    public sealed partial class SchemeDirector
    {
        private void DayTurn(int day)
        {
            ReadWorld(day);
            _events.Clear();
            SchemeRules.Day(_state, day, _world, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                try
                {
                    Carry(_events[i], day);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);   // one event's fault leaves the others to be carried out
                }
            }
            Verdicts(day);
            if (Verbose)
            {
                Logger?.Info("Schemes check (day " + day + "): " + _events.Count + " event(s), " + _world.Tills.Count + " business(es) read, cash in hand " + _state.Dirty
                             + ", heat " + string.Join("/", Array.ConvertAll(_state.Heat, h => h.ToString())) + ": PASS");
            }
        }

        // The game as the rules need it today: the option, the session, prices, the businesses, the claims still unpaid, the rivals and the council.
        private void ReadWorld(int day)
        {
            SchemeWorld w = _world;
            w.Clear();
            w.On = On;
            w.Session = SchemeGame.InSession;
            w.Level = SchemeGame.Level;
            w.Worth = Ask(SchemeBridges.Worth, SchemeGame.Cash);
            ReadShops();
            for (int i = 0; i < _shops.Count; i++)
            {
                w.Tills.Add(new SchemeTill { Place = _shops[i].Place, Average = _shops[i].Average, Cash = _shops[i].Cash && _shops[i].Mine });
            }
            for (int i = 0; i < _state.Acts.Count; i++)
            {
                SchemeAct a = _state.Acts[i];
                if (a.Kind == SchemeKind.Claim && a.Inquiry == 0 && SchemeBridges.ClaimUnpaid != null && SchemeBridges.ClaimUnpaid(a.Ref))
                {
                    w.Unpaid.Add(a.Ref);
                }
            }
            SchemeBridges.Rivals?.Invoke(w);
            SchemeBridges.Council?.Invoke(w.Council);
        }

        private void ReadShops()
        {
            _shops.Clear();
            SchemeBridges.Shops?.Invoke(_shops);
        }

        private static long Ask(Func<long> bridge, long fallback)
        {
            return bridge != null ? bridge() : fallback;
        }

        private static Dictionary<string, string> Data(params string[] pairs) => SchemeGame.Data(pairs);

        private static string RivalName(int tycoon)
        {
            return (SchemeBridges.RivalName != null ? SchemeBridges.RivalName(tycoon) : null) ?? string.Empty;
        }

        private static string RivalHolding(int tycoon)
        {
            return (SchemeBridges.RivalHolding != null ? SchemeBridges.RivalHolding(tycoon) : null) ?? string.Empty;
        }

        // One thing the day did, carried out in the game.
        private void Carry(SchemeEvent e, int day)
        {
            switch (e.Kind)
            {
                case SchemeEventKind.Washed:
                    if (SchemeBridges.Till == null || !SchemeBridges.Till(e.Text, e.Net, day))
                    {
                        _state.Dirty += e.Amount;   // the till could not take it: the cash stays in hand, none of it lost
                        Logger?.Info("Schemes: the till at " + e.Text + " could not take " + e.Amount + " today; it stays in hand.");
                    }
                    break;
                case SchemeEventKind.BankReport:
                case SchemeEventKind.Pattern:
                case SchemeEventKind.Opened:
                case SchemeEventKind.RivalDropped:
                case SchemeEventKind.FundLapsed:
                    if (Verbose)
                    {
                        Logger?.Info("Schemes check (event): " + e.Kind + " " + e.Text + " " + e.Amount + ", inquiry " + e.Inquiry + ", rival " + e.Tycoon + ": PASS");
                    }
                    break;
                case SchemeEventKind.Letter:
                    Letter(e);
                    break;
                case SchemeEventKind.Visit:
                    Visit(e);
                    break;
                case SchemeEventKind.Dropped:
                    SchemeGame.Tell(NotificationType.Success, "reallife_scheme_note_dropped", Data("what", SchemeText.Kind((SchemeKind)e.A)));
                    break;
                case SchemeEventKind.Caution:
                    SchemeGame.Tell(NotificationType.Info, "reallife_scheme_note_caution", Data("what", SchemeText.Kind((SchemeKind)e.A)));
                    break;
                case SchemeEventKind.Findings:
                    Findings(e, day);
                    break;
                case SchemeEventKind.Talked:
                    SchemeGame.Tell(NotificationType.Warning, "reallife_scheme_note_talked", null);
                    break;
                case SchemeEventKind.RivalActed:
                    SchemeBridges.RivalMoney?.Invoke(e.Tycoon, e.Amount);
                    break;
                case SchemeEventKind.RivalLetter:
                    Logger?.Info("Schemes: " + RivalName(e.Tycoon) + " is being looked into (" + (SchemeKind)e.A + ").");
                    break;
                case SchemeEventKind.RivalFound:
                    SchemeBridges.RivalMoney?.Invoke(e.Tycoon, -e.Amount);
                    Logger?.Info("Schemes: " + RivalName(e.Tycoon) + " was found out (" + (SchemeKind)e.A + "): " + e.Amount + " taken back with its surcharge.");
                    break;
                case SchemeEventKind.RivalTried:
                    RivalTried(e);
                    break;
                case SchemeEventKind.FundOffered:
                    SchemeGame.Tell(NotificationType.Info, "reallife_scheme_note_fund_offer", Data("holding", RivalHolding(e.Tycoon), "amount", SchemeText.Money(e.Amount),
                        "day", SchemeText.Whole(_state.Fund.Due)), 14f, () => ShowFund());
                    break;
                case SchemeEventKind.FundPaid:
                    SchemeGame.Give(e.Net, SchemeGame.FundBackKey);
                    SchemeBridges.RivalMoney?.Invoke(e.Tycoon, -e.Net);
                    SchemeGame.Tell(NotificationType.Success, "reallife_scheme_note_fund_paid", Data("holding", RivalHolding(e.Tycoon), "amount", SchemeText.Money(e.Net)));
                    break;
                case SchemeEventKind.FundLost:
                    SchemeGame.Tell(NotificationType.Error, "reallife_scheme_note_fund_lost", Data("holding", RivalHolding(e.Tycoon), "amount", SchemeText.Money(e.Amount)), 14f);
                    break;
                case SchemeEventKind.FundRecovered:
                    SchemeGame.Give(e.Amount, SchemeGame.FundBackKey);
                    SchemeGame.Tell(NotificationType.Success, "reallife_scheme_note_fund_back", Data("holding", RivalHolding(e.Tycoon), "amount", SchemeText.Money(e.Amount)));
                    break;
            }
        }

        // The letter: a text from the player's counsel where the courts run, and a notice either way.
        private void Letter(SchemeEvent e)
        {
            Dictionary<string, string> data = Data("what", SchemeText.Kind((SchemeKind)e.A));
            SchemeBridges.Letter?.Invoke("reallife_scheme_text_letter", data);
            SchemeGame.Tell(NotificationType.Warning, "reallife_scheme_note_letter", data, 14f);
            Logger?.Info("Schemes: inquiry " + e.Inquiry + " into " + (SchemeKind)e.A + ": the letter has come.");
        }

        // The visit: investigators at the business the newest act of the kind was done at, by business pressure's own rules where it runs.
        private void Visit(SchemeEvent e)
        {
            string place = null;
            for (int i = _state.Acts.Count - 1; i >= 0 && place == null; i--)
            {
                place = _state.Acts[i].Kind == (SchemeKind)e.A && _state.Acts[i].Place.Length > 0 ? _state.Acts[i].Place : null;
            }
            bool came = place != null && SchemeBridges.Visit != null && SchemeBridges.Visit(place);
            SchemeGame.Tell(NotificationType.Warning, _state.Watched ? "reallife_scheme_note_visit_watched" : "reallife_scheme_note_visit",
                Data("what", SchemeText.Kind((SchemeKind)e.A)), 14f);
            Logger?.Info("Schemes: inquiry " + e.Inquiry + " into " + (SchemeKind)e.A + ": the visit" + (came ? " (an inspection has started at " + place + ")." : "."));
        }

        // The findings: what was found is taken back whatever the wallet holds (the cash in hand first, by the rules), a club's
        // stakes go back to their households, the surcharge is taken, the insurer drops what it was cheated on, and the file
        // goes to the court. With no court the inquiry ends here.
        private void Findings(SchemeEvent e, int day)
        {
            SchemeInquiry q = _state.Inquiry(e.Inquiry);
            if (q == null)
            {
                return;
            }
            var kind = (SchemeKind)e.A;
            SchemeGame.Take(e.Amount - e.FromCash, SchemeGame.FoundKey);
            SchemeGame.Take(e.Net, SchemeGame.SurchargeKey);
            if ((kind == SchemeKind.Club || kind == SchemeKind.Wash) && e.Amount > 0)
            {
                GiveBack(e.Amount);
            }
            for (int i = 0; kind == SchemeKind.Claim && i < _state.Acts.Count; i++)
            {
                if (_state.Acts[i].Kind == SchemeKind.Claim && _state.Acts[i].Inquiry == q.Id)
                {
                    SchemeBridges.ClaimRefuse?.Invoke(_state.Acts[i].Ref);
                }
            }
            int number = SchemeBridges.Court != null && SchemeBridges.Court() && SchemeBridges.Charge != null
                ? SchemeBridges.Charge(e.Text, q.Evidence, e.Amount + e.Net, _state.Seed ^ q.Id * 7919)
                : 0;
            bool felony = e.B != 0;
            SchemeBridges.Exposed?.Invoke(felony);
            SchemeBridges.Story?.Invoke("reallife_scheme_news_own_" + kind.ToString().ToLowerInvariant());
            if (number > 0)
            {
                q.Case = number;
            }
            else
            {
                SchemeRules.Close(_state, q, InquiryEnd.Settled, day);
            }
            SchemeGame.Tell(NotificationType.Error, number > 0 ? "reallife_scheme_note_findings" : "reallife_scheme_note_findings_no_court",
                Data("what", SchemeText.Kind(kind), "amount", SchemeText.Money(e.Amount), "more", SchemeText.Money(e.Net)), 16f);
            Logger?.Info("Schemes: inquiry " + q.Id + " into " + kind + " made its findings on day " + day + ": " + e.Amount + " taken back (" + e.FromCash
                         + " of it from the cash in hand), " + e.Net + " on top, evidence " + q.Evidence + "; " + (number > 0 ? "case " + number + " opened for " + e.Text
                             : "no court to charge in") + ".");
        }

        // A club's money back to its households: at the district of the newest club's business.
        private void GiveBack(long amount)
        {
            string place = string.Empty;
            for (int i = _state.Acts.Count - 1; i >= 0 && place.Length == 0; i--)
            {
                place = _state.Acts[i].Kind == SchemeKind.Club ? _state.Acts[i].Place : place;
            }
            long took = SchemeBridges.Restore != null ? SchemeBridges.Restore(place, amount) : 0L;
            if (Verbose || took != amount)
            {
                Logger?.Info("Schemes check (restitution): " + amount + " owed back to the households, " + took + " reached their savings: "
                             + (took == amount ? "PASS" : "FAIL, the city's people are not this machine's to pay"));
            }
        }

        // A rival's verdict: a convicted one pays the court's fine by Justice's own sum and is barred and quiet for a while.
        private void RivalTried(SchemeEvent e)
        {
            bool convicted = e.B != 0;
            if (convicted)
            {
                SchemeRival r = _state.Rival[SchemeCatalogue.Clamp(e.Tycoon, 0, _state.Rival.Length - 1)];
                string charge = (SchemeKind)e.A == SchemeKind.Graft ? JusticeLaw.Bribery : (SchemeKind)e.A == SchemeKind.Expense ? JusticeLaw.TaxEvasion : JusticeLaw.Fraud;
                int fine = SchemeRules.FineOf(charge, Math.Max(0, r.Convictions - 1), _world.RivalWorth[SchemeCatalogue.Clamp(e.Tycoon, 0, SchemeState.Rivals - 1)]);
                SchemeBridges.RivalMoney?.Invoke(e.Tycoon, -fine);
                SchemeBridges.RivalBarred?.Invoke(e.Tycoon, SchemeCatalogue.RivalBarDays, SchemeCatalogue.RivalQuietDays);
            }
            Logger?.Info("Schemes: " + RivalName(e.Tycoon) + " was tried (" + (SchemeKind)e.A + "): " + (convicted ? "convicted." : "cleared."));
        }

        // The court's answer on each file it holds: the inquiry ends as the case ended, and Standing hears of a conviction.
        private void Verdicts(int day)
        {
            for (int i = 0; i < _state.Cases.Count; i++)
            {
                SchemeInquiry q = _state.Cases[i];
                if (q.Stage != InquiryStage.Charged || q.Case <= 0)
                {
                    continue;
                }
                int end = SchemeBridges.CaseEnd != null ? SchemeBridges.CaseEnd(q.Case) : (int)InquiryEnd.Settled;   // no court any more: nothing left to wait for
                if (end == 0)
                {
                    continue;
                }
                SchemeRules.Close(_state, q, (InquiryEnd)SchemeCatalogue.Clamp(end, (int)InquiryEnd.Dropped, (int)InquiryEnd.Settled), day);
                if (q.End == InquiryEnd.Convicted)
                {
                    SchemeBridges.Integrity?.Invoke(-3);
                }
                Logger?.Info("Schemes: inquiry " + q.Id + " into " + q.Kind + " is over: case " + q.Case + " ended " + q.End + ".");
            }
        }
    }
}

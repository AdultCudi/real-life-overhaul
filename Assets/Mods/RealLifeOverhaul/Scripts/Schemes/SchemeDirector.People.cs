using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>What came of money offered on a call: what they say, and whether it was taken.</summary>
    public struct FavourAnswer
    {
        public string Key;                 // what they say
        public string[] Pairs;
        public bool Taken;
        public string Words;               // more to read under it (a word's answer in full); null for none
    }

    // The people: what money can buy from somebody with a position, who is asked, and what a refusal weighs. Also what the
    // Legal file lists and lets the player do, and the small doors other features use (a bought favour, somebody hosted).
    public sealed partial class SchemeDirector
    {
        private const int RoleLawyer = 3, RoleOfficial = 6;   // Networking's own numbers for a law firm's partner and a city official

        private readonly List<string> _seats = new List<string>(6);

        /// <summary>
        /// The favours somebody the player knows could be paid for today, by the position they hold: none for somebody
        /// with no position, somebody who said no before, or with the schemes off. Asked when a call's menu is written.
        /// </summary>
        public static void Favours(int person, List<Favour> into)
        {
            into.Clear();
            SchemeDirector d = Instance;
            int role = On && d != null && SchemeBridges.RoleOf != null ? SchemeBridges.RoleOf(person) : 0;
            if (role <= 0 || SchemeRules.IsCold(d._state, SchemeRules.Contact(person)))
            {
                return;
            }
            SchemeState s = d._state;
            if (!s.Bought.Contains(person))
            {
                into.Add(Favour.Gift);
            }
            if (role == RoleOfficial)
            {
                into.Add(Favour.Word);
                if (SchemeBridges.Invite != null)
                {
                    into.Add(Favour.Access);
                }
                if (!SchemeGame.InSession && d.SeatFor() != null)
                {
                    into.Add(Favour.Vote);
                }
            }
            if (role == RoleLawyer)
            {
                if (d.Askable() != null)
                {
                    into.Add(Favour.Investigator);
                }
                int next = SchemeBridges.NextCase != null ? SchemeBridges.NextCase() : 0;
                if (next > 0 && !d.Asked(SchemeRules.Judge(next)))
                {
                    into.Add(Favour.Judge);
                }
            }
        }

        // The inquiry whose investigator can still be approached: one the player knows of, not yet before the court, not asked before.
        private SchemeInquiry Askable()
        {
            for (int i = 0; i < _state.Cases.Count; i++)
            {
                SchemeInquiry q = _state.Cases[i];
                if (q.Open && q.Known && q.Stage != InquiryStage.Charged && !q.Asked)
                {
                    return q;
                }
            }
            return null;
        }

        // Whether somebody was offered money before, taken or not: nobody is asked twice.
        private bool Asked(string who)
        {
            if (SchemeRules.IsCold(_state, who))
            {
                return true;
            }
            for (int i = 0; i < _state.Paid.Count; i++)
            {
                if (_state.Paid[i].Who == who)
                {
                    return true;
                }
            }
            return false;
        }

        // The councillor the player's push would be put to, by the seed, the push and the day's council; null with no push, no council or one already asked.
        private string SeatFor()
        {
            int push = SchemeBridges.PushPending != null ? SchemeBridges.PushPending() : 0;
            if (push <= 0)
            {
                return null;
            }
            _seats.Clear();
            SchemeBridges.Council?.Invoke(_seats);
            int seat = SchemeRules.Seat(_state.Seed, push, _seats.Count);
            string who = seat >= 0 ? SchemeRules.Councillor(_seats[seat]) : null;
            return who != null && !Asked(who) ? who : null;
        }

        /// <summary>What a favour costs today, and whether it would be paid out of the cash in hand (no paper) or the bank account (a trail).</summary>
        public static long PriceOf(Favour favour, out bool cash)
        {
            cash = false;
            SchemeDirector d = Instance;
            if (d == null || d._state == null)
            {
                return 0;
            }
            long price = favour == Favour.Vote && SchemeBridges.PushFee != null
                ? Math.Max(SchemeRules.PriceOf(d._state, favour, null, SchemeGame.Day, SchemeGame.Level), 2 * SchemeBridges.PushFee())
                : SchemeRules.PriceOf(d._state, favour, favour == Favour.Investigator ? d.Askable() : null, SchemeGame.Day, SchemeGame.Level);
            cash = !d._state.Watched && d._state.Dirty >= price;
            return price;
        }

        /// <summary>
        /// Money is offered to somebody the player knows for a favour. Who takes it is their own temper and, where the
        /// favour is somebody else's to give (a councillor's vote, an investigator's file, a judge's weighing), that
        /// person's too: fixed by the save's seed, so asking again or loading again changes nothing. Refused before any
        /// money moves when the wallet cannot pay it, the investors' money would be touched, or the favour cannot be had now.
        /// </summary>
        public static FavourAnswer Offer(int person, Favour favour)
        {
            SchemeDirector d = Instance;
            var no = new FavourAnswer { Key = "reallife_scheme_talk_not_now" };
            if (!On || d == null || !d.Ready("an offer"))
            {
                return no;
            }
            try
            {
                return d.Offered(person, favour);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return no;
            }
        }

        private FavourAnswer Offered(int person, Favour favour)
        {
            SchemeState s = _state;
            int day = SchemeGame.Day;
            string contact = SchemeRules.Contact(person);
            long price = PriceOf(favour, out bool cash);
            var answer = new FavourAnswer { Key = "reallife_scheme_talk_not_now" };
            if (SchemeRules.IsCold(s, contact) || price <= 0)
            {
                return answer;
            }
            if (!cash && !Affords(price))
            {
                answer.Key = "reallife_scheme_talk_short";
                answer.Pairs = new[] { "amount", SchemeText.Money(price) };
                return answer;
            }
            // Their own temper first: somebody who would not take money will not carry it to another either.
            Temper own = SchemeRules.TemperOf(s.Seed, Office.Contact, contact);
            Office office = Office.Contact;
            string who = contact;
            SchemeInquiry inquiry = null;
            int courtCase = 0;
            if (own == Temper.Takes && favour == Favour.Vote)
            {
                office = Office.Councillor;
                who = SeatFor();
            }
            else if (own == Temper.Takes && favour == Favour.Investigator)
            {
                office = Office.Investigator;
                inquiry = Askable();
                who = inquiry != null ? SchemeRules.Investigator(inquiry.Kind, s.Inquiries[(int)inquiry.Kind]) : null;
            }
            else if (own == Temper.Takes && favour == Favour.Judge)
            {
                office = Office.Judge;
                courtCase = SchemeBridges.NextCase != null ? SchemeBridges.NextCase() : 0;
                who = courtCase > 0 ? SchemeRules.Judge(courtCase) : null;
            }
            if (who == null || (favour == Favour.Vote && SchemeGame.InSession))
            {
                return answer;   // nobody to ask now: nothing is offered and no money moves
            }
            // A favour that cannot be had is found out before the money moves, for somebody who would take it.
            Temper theirs = SchemeRules.TemperOf(s.Seed, office, who);
            if (theirs == Temper.Takes && !Deliver(favour, person, inquiry, courtCase))
            {
                return answer;
            }
            if (inquiry != null)
            {
                inquiry.Asked = true;
            }
            OfferResult r = SchemeRules.Offer(s, favour, office, who, price, cash, day);
            if (r.Taken)
            {
                if (!cash)
                {
                    SchemeGame.Take(price, SchemeGame.FavourKey);   // counted against the wallet and the investors' floor above
                }
                answer.Taken = true;
                answer.Key = SchemeKeys.Taken[(int)favour];
                answer.Pairs = new[] { "amount", SchemeText.Money(price) };
                answer.Words = favour == Favour.Word ? Standing() : null;
                Logger?.Info("Schemes: " + favour + " bought from " + who + " for " + price + (cash ? " in cash" : " through the account") + "; graft heat "
                             + s.Heat[(int)SchemeKind.Graft] + ".");
                return answer;
            }
            if (office == Office.Contact)
            {
                SchemeBridges.Cooled?.Invoke(person);
            }
            if (r.Temper == Temper.Reports)
            {
                Reported(favour, inquiry, courtCase, price);
            }
            answer.Key = office == Office.Contact ? r.Temper == Temper.Reports ? "reallife_scheme_talk_refused_cold" : "reallife_scheme_talk_refused"
                : r.Temper == Temper.Reports ? "reallife_scheme_talk_carried_reported" : "reallife_scheme_talk_carried_refused";
            Logger?.Info("Schemes: " + favour + " offered to " + who + ": refused (" + r.Temper + "); graft heat " + s.Heat[(int)SchemeKind.Graft] + ".");
            return answer;
        }

        // What a favour does once it is taken; false, with nothing done, when it cannot be had now.
        private bool Deliver(Favour favour, int person, SchemeInquiry inquiry, int courtCase)
        {
            switch (favour)
            {
                case Favour.Gift:
                    SchemeRules.Buy(_state, person);
                    return true;
                case Favour.Access:
                    return SchemeBridges.Invite != null && SchemeBridges.Invite();
                case Favour.Vote:
                    return SchemeBridges.PushCarry != null && SchemeBridges.PushCarry();
                case Favour.Investigator:
                    SchemeRules.LoseFile(inquiry);
                    return inquiry != null;
                case Favour.Judge:
                    return SchemeBridges.Lean != null && SchemeBridges.Lean(courtCase, SchemeCatalogue.JudgeLean);
                default:
                    return true;
            }
        }

        // An offer that was reported: the investigator writes it into the file they hold, and a judge's goes to the court as a charge of its own.
        private void Reported(Favour favour, SchemeInquiry inquiry, int courtCase, long price)
        {
            if (favour == Favour.Investigator && inquiry != null)
            {
                inquiry.Pressed += SchemeCatalogue.EvidenceReported;
            }
            if (favour == Favour.Judge && SchemeBridges.Charge != null && SchemeBridges.Court != null && SchemeBridges.Court())
            {
                int number = SchemeBridges.Charge(JusticeLaw.PervertingJustice, 900, price, _state.Seed ^ courtCase * 104729);
                Logger?.Info("Schemes: the judge of case " + courtCase + " reported the offer: " + (number > 0 ? "case " + number : "no case could be opened") + ".");
            }
        }

        // Where every trail stands, in words: what a paid word from City Hall tells.
        private string Standing()
        {
            var b = new StringBuilder(320);
            for (int k = 0; k < SchemeCatalogue.Kinds; k++)
            {
                var kind = (SchemeKind)k;
                SchemeInquiry q = _state.OpenOf(kind);
                b.Append(k > 0 ? "\n" : string.Empty).Append(SchemeText.Kind(kind)).Append(": ");
                b.Append(q != null ? SchemeText.Fill("reallife_scheme_word_inquiry", "day", SchemeText.Whole(q.Opened), "share", SchemeText.Percent(q.Evidence))
                    : SchemeText.T(SchemeKeys.Bands[SchemeRules.Band(_state.Heat[k])]));
            }
            for (int i = 0; i < _state.Rival.Length; i++)
            {
                if (_state.Rival[i].Stage != InquiryStage.Closed)
                {
                    b.Append('\n').Append(SchemeText.Fill("reallife_scheme_word_rival", "rival", RivalName(i)));
                }
            }
            return b.ToString();
        }

        // ------------------------------------------------------------------------------------------------- the doors other features use

        /// <summary>True for a contact whose next favour is bought: Networking then does it without the friendship or the wait.</summary>
        public static bool Bought(int person)
        {
            SchemeDirector d = Instance;
            return d != null && d._state != null && !d._broken && d._state.Bought.Contains(person);
        }

        /// <summary>The bought favour was done: it is spent.</summary>
        public static void BoughtUsed(int person)
        {
            SchemeDirector d = Instance;
            if (d != null && d._state != null)
            {
                d._state.Bought.Remove(person);
            }
        }

        /// <summary>
        /// Somebody the player knows was hosted (a party, an evening laid on for them). For one who takes gifts their next
        /// favour is bought by it, with no money and no trail; anybody else had an evening out. True if a favour was bought.
        /// </summary>
        public static bool Hosted(int person)
        {
            SchemeDirector d = Instance;
            return On && d != null && SchemeBridges.RoleOf != null && SchemeBridges.RoleOf(person) > 0 && SchemeRules.Hosted(d._state, person);
        }

        // ------------------------------------------------------------------------------------------------- the Legal file

        /// <summary>What hangs over the player, a sentence each: every inquiry they know of, and a rival's fund that waits or went wrong.</summary>
        public static void FileLines(List<string> into)
        {
            SchemeDirector d = Instance;
            SchemeState s = d != null && !d._broken ? d._state : null;
            if (s == null)
            {
                return;
            }
            List<int> unpaid = d.Unpaid(d._unpaid);
            for (int i = 0; i < s.Cases.Count; i++)
            {
                SchemeInquiry q = s.Cases[i];
                if (!q.Open || !q.Known)
                {
                    continue;
                }
                into.Add(SchemeText.Fill(q.Stage == InquiryStage.Charged ? "reallife_scheme_file_charged" : q.Stage == InquiryStage.Visit ? "reallife_scheme_file_visit"
                    : "reallife_scheme_file_letter", "what", SchemeText.Kind(q.Kind), "day", SchemeText.Whole(q.Opened), "amount",
                    SchemeText.Money(SchemeRules.RepayCost(s, q.Kind, SchemeGame.Day, unpaid))));
            }
            SchemeFund f = s.Fund;
            if (f.State == FundState.Open || f.State == FundState.Placed || (f.State == FundState.Lost && !f.Reported))
            {
                into.Add(SchemeText.Fill(f.State == FundState.Open ? "reallife_scheme_file_fund_open" : f.State == FundState.Placed ? "reallife_scheme_file_fund_placed"
                    : "reallife_scheme_file_fund_lost", "holding", RivalHolding(f.Tycoon), "amount", SchemeText.Money(f.Amount), "day", SchemeText.Whole(f.Due)));
            }
        }

        /// <summary>
        /// What the player can do about it today, each with its name, what it means and what it says afterwards: pay a file
        /// back, tell the investigators everything, look at a rival's fund, report one that was a sham.
        /// </summary>
        public static void FileActs(Action<string, string, Func<string>> add)
        {
            SchemeDirector d = Instance;
            SchemeState s = d != null && !d._broken ? d._state : null;
            if (s == null || add == null)
            {
                return;
            }
            int day = SchemeGame.Day;
            List<int> unpaid = d.Unpaid(d._unpaid);
            for (int i = 0; i < s.Cases.Count; i++)
            {
                SchemeInquiry q = s.Cases[i];
                if (!q.Open || !q.Known || q.Stage == InquiryStage.Charged)
                {
                    continue;
                }
                SchemeKind kind = q.Kind;
                int id = q.Id;
                long cost = SchemeRules.RepayCost(s, kind, day, unpaid);
                if (cost > 0)
                {
                    add(SchemeText.Fill("reallife_scheme_file_repay", "what", SchemeText.Kind(kind), "amount", SchemeText.Money(cost)),
                        SchemeText.Fill("reallife_scheme_file_repay_hint", "share", SchemeText.Percent(SchemeCatalogue.SurchargePermille)), () => d.Repay(kind).Words);
                }
                if (!q.Coop)
                {
                    add(SchemeText.Fill("reallife_scheme_file_coop", "what", SchemeText.Kind(kind)), SchemeText.T("reallife_scheme_file_coop_hint"), () => d.Cooperate(id).Words);
                }
            }
            SchemeFund f = s.Fund;
            if (SchemeGame.InSession)
            {
                return;   // the rivals rest in a session: a fund of theirs waits, and nothing can be done about it there
            }
            if (SchemeRules.FundStands(s, day))
            {
                string holding = RivalHolding(f.Tycoon);
                if (!f.Checked)
                {
                    add(SchemeText.Fill("reallife_scheme_file_fund_look", "holding", holding, "amount", SchemeText.Money(SchemeRules.FundFee(f.Amount, SchemeGame.Level))),
                        SchemeText.T("reallife_scheme_fund_look_hint"), () => d.FundLook().Words);
                }
                add(SchemeText.Fill("reallife_scheme_file_fund_put", "holding", holding, "amount", SchemeText.Money(f.Amount)),
                    SchemeText.Fill(f.Checked ? f.Sham ? "reallife_scheme_fund_look_sham" : "reallife_scheme_fund_look_honest" : "reallife_scheme_fund_put_hint", "holding", holding),
                    () => d.FundPut().Words);
                add(SchemeText.Fill("reallife_scheme_file_fund_down", "holding", holding), SchemeText.T("reallife_scheme_fund_down_hint"), () => d.FundTurnDown().Words);
            }
            else if (f.State == FundState.Lost && !f.Reported)
            {
                add(SchemeText.Fill("reallife_scheme_file_fund_tell", "holding", RivalHolding(f.Tycoon)), SchemeText.T("reallife_scheme_file_fund_tell_hint"), () => d.FundTell().Words);
            }
        }
    }
}

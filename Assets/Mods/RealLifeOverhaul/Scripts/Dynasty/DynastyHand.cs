using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>What the steps of a handover do in the world: the director books the money and changes the played person; a check records it.</summary>
    public interface IHandWorld
    {
        /// <summary>The age of the founder in days, read before the heir takes over.</summary>
        int FounderAgeDays { get; }

        /// <summary>The cash part of the tax leaves the wallet. Returns what really left (the whole amount unless a booking was refused).</summary>
        long PayTax(long amount);

        /// <summary>What a retiring founder keeps leaves the wallet for their own fund. Returns what really left.</summary>
        long PayKeep(long amount);

        /// <summary>A bequest's money leaves the wallet, before it lands anywhere. Returns what really left.</summary>
        long Book(BequestLine line, long amount);

        /// <summary>A bequest lands where its line says; returns what really landed (never more than the amount).</summary>
        long Land(BequestLine line, long amount);

        /// <summary>Money that left for a bequest but did not land comes back to the wallet. Returns what really came back.</summary>
        long Refund(BequestLine line, long amount);

        /// <summary>The other features are told that the heir is the head: the family, the people, the education, the standing.</summary>
        void Succeed(Statement s);

        /// <summary>The played person becomes the heir: their name, their age, their share of the skills and courses.</summary>
        void Become(Statement s);
    }

    /// <summary>
    /// A handover in its steps, with no game in it. The statement is fixed when the handover begins (the freeze), so nothing that happens during it can change a figure; each
    /// step does one thing, keyed to a flag of its own that is set in the same step as its effect, so a save, a load, an error or the option off in the middle goes on at the
    /// step and nothing is done twice: the tax (its cash part out, the rest onto the debt), what a retiring founder keeps, each bequest (out of the wallet, then landed, then
    /// what did not land back), the tax worked again on what really landed, the other features told, the played person changed, the lineage. A booking that is refused stops the
    /// step with a <see cref="HandFault"/> and the amounts already taken are kept, so the step goes on from there. COST: one step a second while one runs.
    /// </summary>
    public static class DynastyHand
    {
        public const int StepTax = 0, StepKeep = 1, StepBequests = 2, StepPass = 3;

        /// <summary>The steps that come after the bequests: the tax worked again, the features told, the played person changed, the lineage.</summary>
        private const int After = 4;

        /// <summary>Begins a handover from its statement. False, and nothing begun, while one is under way.</summary>
        public static bool Begin(DynastyState st, Statement s)
        {
            if (st.Hand != null || s == null)
            {
                return false;
            }
            var h = new Handover();
            Copy(s, h.S);
            st.Hand = h;
            return true;
        }

        /// <summary>The steps a handover has in all, for the count on a page.</summary>
        public static int Steps(Handover h) => h == null ? 0 : 2 + h.S.Lines.Count + After;

        /// <summary>How many of them are done.</summary>
        public static int Done(Handover h)
        {
            if (h == null)
            {
                return 0;
            }
            int done = (h.TaxOut ? 1 : 0) + (h.KeepOut ? 1 : 0) + (h.Retaxed ? 1 : 0) + (h.Succeeded ? 1 : 0) + (h.Became ? 1 : 0) + (h.Passed ? 1 : 0);
            for (int i = 0; i < h.S.Lines.Count; i++)
            {
                done += h.S.Lines[i].Paid ? 1 : 0;
            }
            return done;
        }

        /// <summary>
        /// Runs every step that is left, one after the other, in this call: a death is settled before anything else can happen (the game's own end, a time skip). Returns true
        /// when the handover is over. A <see cref="HandFault"/> is not caught: the steps done stay done and the rest wait.
        /// </summary>
        public static bool Finish(DynastyState st, IHandWorld w, int day, int yearDays, int monthDays)
        {
            for (int guard = 0; guard < 64 && st.Hand != null; guard++)
            {
                if (Step(st, w, day, yearDays, monthDays))
                {
                    return true;
                }
            }
            return st.Hand == null;
        }

        /// <summary>One step. True when the handover is over: its statement is kept and the heir is the head.</summary>
        public static bool Step(DynastyState st, IHandWorld w, int day, int yearDays, int monthDays)
        {
            Handover h = st.Hand;
            if (h == null)
            {
                return true;
            }
            Statement s = h.S;
            if (!h.TaxOut)
            {
                if (!h.LoanIn)
                {
                    DynastyRules.Borrow(st.Loan, s.Loan, day, yearDays);
                    h.LoanIn = true;
                }
                h.TaxPaid += Taken(w.PayTax(s.TaxCash - h.TaxPaid), s.TaxCash - h.TaxPaid);
                if (h.TaxPaid < s.TaxCash)
                {
                    throw new HandFault("the tax's cash part could not be booked: " + h.TaxPaid + " of " + s.TaxCash + " left the wallet");
                }
                h.TaxOut = true;
                h.Step = StepKeep;
                return false;
            }
            if (!h.KeepOut)
            {
                if (s.Mode == HandMode.Retire)
                {
                    h.KeepPaid += Taken(w.PayKeep(s.Keep - h.KeepPaid), s.Keep - h.KeepPaid);
                    if (h.KeepPaid < s.Keep)
                    {
                        throw new HandFault("what the founder keeps could not be booked: " + h.KeepPaid + " of " + s.Keep + " left the wallet");
                    }
                    st.Elder = DynastyRules.Retired(s.From, w.FounderAgeDays, day, s.Keep, s.Taxable, s.Tax, yearDays, monthDays);
                }
                h.KeepOut = true;
                h.Step = StepBequests;
                return false;
            }
            for (int i = 0; i < s.Lines.Count; i++)
            {
                BequestLine l = s.Lines[i];
                if (l.Paid)
                {
                    continue;
                }
                Bequest(st, w, s, l, day);
                h.Step = StepBequests;
                return false;
            }
            if (!h.Retaxed)
            {
                // What did not land is no longer exempt: the tax is worked again on what really did, and the difference is owed with the rest of the estate's debt.
                long extra = DynastyRules.Retax(s);
                if (extra > 0)
                {
                    DynastyRules.Borrow(st.Loan, extra, day, yearDays);
                }
                h.Retaxed = true;
                h.Step = StepPass;
                return false;
            }
            if (!h.Succeeded)
            {
                w.Succeed(s);   // the family, the people, the education, the standing: they still read the founder by name
                h.Succeeded = true;
                return false;
            }
            if (!h.Became)
            {
                w.Become(s);
                h.Became = true;
                return false;
            }
            if (!h.Passed)
            {
                st.Line.Add(new LineEntry { Name = s.From, Generation = s.Generation, Day = day, Mode = s.Mode, Net = s.Net, Tax = s.Tax });
                while (st.Line.Count > DynastyCatalogue.LineKept)
                {
                    st.Line.RemoveAt(0);
                }
                st.Generation = s.Generation + 1;
                st.Will = new Will();       // the will was the founder's own: the new head writes theirs
                st.Gifts.Clear();           // ... and so were the gifts, which this estate has counted
                st.RetireTold = 0;
                st.Notes.Add(new DynastyNote { Day = day, Who = -1, Mode = s.Mode, Generation = st.Generation, Tax = s.Tax, From = s.From, To = s.To });
                while (st.Notes.Count > DynastyCatalogue.NotesKept)
                {
                    st.Notes.RemoveAt(0);
                }
                h.Passed = true;
                h.Step = StepPass;
            }
            st.Keep(s);
            st.Hand = null;
            return true;
        }

        // One bequest: its money leaves the wallet first (kept as it goes, so a refusal part way is taken up again where it stopped), then it lands, then what did not land comes back
        // to the wallet and stays in the estate with the heir. Each part has its own flag, set with its effect.
        private static void Bequest(DynastyState st, IHandWorld w, Statement s, BequestLine l, int day)
        {
            if (l.Booked < l.Amount)
            {
                l.Booked += Taken(w.Book(l, l.Amount - l.Booked), l.Amount - l.Booked);
                if (l.Booked < l.Amount)
                {
                    throw new HandFault("a bequest to " + l.Name + " could not be booked: " + l.Booked + " of " + l.Amount + " left the wallet");
                }
            }
            if (!l.Credited)
            {
                long landed = l.Amount > 0 ? Math.Max(0L, Math.Min(l.Amount, w.Land(l, l.Amount))) : 0L;
                l.Landed = landed;
                l.Credited = true;
                if (l.Landing == Landing.Family && landed > 0)
                {
                    Kin k = st.KinOf(l.Kind, l.Name, true);
                    k.Held += landed;
                    k.Day = day;
                }
            }
            if (l.Landed < l.Amount)
            {
                // What could not land after all comes back to the wallet and stays in the estate, with the heir; the tax on it is worked again at the end.
                long back = l.Amount - l.Landed;
                long came = Taken(w.Refund(l, back), back);
                l.Amount -= came;
                l.Booked -= came;
                s.Paid -= came;
                s.Residue += came;
                if (came < back)
                {
                    throw new HandFault("a bequest to " + l.Name + " that did not land could not come back: " + came + " of " + back);
                }
            }
            l.Paid = true;
        }

        private static long Taken(long paid, long asked) => Math.Max(0L, Math.Min(asked, paid));

        private static void Copy(Statement a, Statement b)
        {
            b.Day = a.Day;
            b.Mode = a.Mode;
            b.Generation = a.Generation;
            b.From = a.From;
            b.To = a.To;
            b.HeirKind = a.HeirKind;
            b.HeirId = a.HeirId;
            b.HeirAge = a.HeirAge;
            b.Upbringing = a.Upbringing;
            b.ByLaw = a.ByLaw;
            b.SpouseHeir = a.SpouseHeir;
            b.Level = a.Level;
            b.Cash = a.Cash;
            b.Reserved = a.Reserved;
            b.Other = a.Other;
            b.Net = a.Net;
            b.Keep = a.Keep;
            b.Gifts = a.Gifts;
            b.Charity = a.Charity;
            b.Spouse = a.Spouse;
            b.Prior = a.Prior;
            b.Taxable = a.Taxable;
            b.Tax = a.Tax;
            b.TaxCash = a.TaxCash;
            b.Loan = a.Loan;
            b.Paid = a.Paid;
            b.Residue = a.Residue;
            b.Scale = a.Scale;
            b.UnderWater = a.UnderWater;
            b.Lines.Clear();
            for (int i = 0; i < a.Lines.Count; i++)
            {
                BequestLine l = a.Lines[i];
                b.Lines.Add(new BequestLine
                {
                    Kind = l.Kind, Id = l.Id, Name = l.Name, Share = l.Share, Asked = l.Asked, Amount = l.Amount, Landing = l.Landing, Paid = l.Paid, Relief = l.Relief,
                    Booked = l.Booked, Credited = l.Credited, Landed = l.Landed
                });
            }
        }
    }
}

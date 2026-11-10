using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>What the steps of a handover do in the world: the director books the money and changes the played person; a check records it.</summary>
    public interface IHandWorld
    {
        /// <summary>The age of the founder in days, read before the heir takes over.</summary>
        int FounderAgeDays { get; }

        /// <summary>The cash part of the tax leaves the wallet.</summary>
        void PayTax(long amount);

        /// <summary>What a retiring founder keeps leaves the wallet for their own fund.</summary>
        void PayKeep(long amount);

        /// <summary>A bequest lands where its line says and leaves the wallet; returns what really landed and left (never more than the amount of the line).</summary>
        long Land(BequestLine line);

        /// <summary>The heir becomes the played head of the family.</summary>
        void Succeed(Statement s);
    }

    /// <summary>
    /// A handover in its steps, with no game in it. The statement is fixed when the handover begins (the freeze), so nothing that happens during it can change a figure; each
    /// step does one thing and says on the record that it did, so a save, a load or the option off in the middle goes on at the step and nothing is paid twice:
    /// the tax (its cash part out, the rest onto the debt), what a retiring founder keeps, each bequest, then the heir takes over. COST: one step a second while one runs.
    /// </summary>
    public static class DynastyHand
    {
        public const int StepTax = 0, StepKeep = 1, StepBequests = 2, StepPass = 3;

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
        public static int Steps(Handover h) => h == null ? 0 : StepPass + h.S.Lines.Count;

        /// <summary>How many of them are done.</summary>
        public static int Done(Handover h)
        {
            if (h == null)
            {
                return 0;
            }
            int done = (h.TaxOut ? 1 : 0) + (h.KeepOut ? 1 : 0) + (h.Passed ? 1 : 0);
            for (int i = 0; i < h.S.Lines.Count; i++)
            {
                done += h.S.Lines[i].Paid ? 1 : 0;
            }
            return done;
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
                DynastyRules.Borrow(st.Loan, s.Loan, day, yearDays);
                w.PayTax(s.TaxCash);
                h.TaxOut = true;
                h.Step = StepKeep;
                return false;
            }
            if (!h.KeepOut)
            {
                if (s.Mode == HandMode.Retire)
                {
                    w.PayKeep(s.Keep);
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
                long landed = l.Amount > 0 ? Math.Max(0L, Math.Min(l.Amount, w.Land(l))) : 0L;
                if (landed < l.Amount)
                {
                    // What could not land after all stays in the estate, with the heir: the tax is not worked again, so nothing is gained by it.
                    long back = l.Amount - landed;
                    l.Amount = landed;
                    s.Paid -= back;
                    s.Residue += back;
                }
                if (l.Landing == Landing.Family && landed > 0)
                {
                    Kin k = st.KinOf(l.Kind, l.Name, true);
                    k.Held += landed;
                    k.Day = day;
                }
                l.Paid = true;
                h.Step = StepBequests;
                return false;
            }
            if (!h.Passed)
            {
                w.Succeed(s);
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
                b.Lines.Add(new BequestLine { Kind = l.Kind, Id = l.Id, Name = l.Name, Share = l.Share, Asked = l.Asked, Amount = l.Amount, Landing = l.Landing, Paid = l.Paid });
            }
        }
    }
}

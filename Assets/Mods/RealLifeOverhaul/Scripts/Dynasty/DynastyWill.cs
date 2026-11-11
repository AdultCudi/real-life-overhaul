using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// A will being drafted and signed, with no game in it. The pages change a draft a press at a time and nothing counts until it is signed, which costs the fee once for
    /// whatever was changed. COST: a few operations a press.
    /// </summary>
    public static class DynastyWill
    {
        /// <summary>Names who carries on; a bequest the draft left them goes, since they take the rest anyway. None: the law's order decides.</summary>
        public static void SetHeir(Will draft, KinKind kind, int id, string name)
        {
            draft.HeirKind = kind == KinKind.Charity ? KinKind.None : kind;
            draft.HeirId = draft.HeirKind == KinKind.Person ? id : 0;
            draft.HeirName = draft.HeirKind == KinKind.None ? string.Empty : name ?? string.Empty;
            for (int i = draft.Bequests.Count - 1; i >= 0 && draft.HeirKind != KinKind.None; i--)
            {
                if (draft.Bequests[i].Same(draft.HeirKind, draft.HeirId, draft.HeirName))
                {
                    draft.Bequests.RemoveAt(i);
                }
            }
        }

        /// <summary>True when somebody is the heir the draft names.</summary>
        public static bool IsHeir(Will draft, KinKind kind, int id, string name)
        {
            return draft.HeirKind != KinKind.None && kind == draft.HeirKind && (kind == KinKind.Person ? id == draft.HeirId && (name ?? string.Empty) == draft.HeirName
                : (name ?? string.Empty) == draft.HeirName);
        }

        /// <summary>
        /// Moves the share of somebody a step up or down. A share that reaches nothing is taken out; a new one is added while there is room in the list and in the part the
        /// heir does not have to be left. The heir is left nothing by name. Returns the share they now have.
        /// </summary>
        public static int Move(Will draft, KinKind kind, int id, string name, int steps)
        {
            Bequest b = draft.Find(kind, id, name);
            int now = b != null ? b.Share : 0;
            if (kind == KinKind.None || steps == 0 || IsHeir(draft, kind, id, name))
            {
                return now;
            }
            int want = Math.Max(0, Math.Min(now + steps * DynastyCatalogue.ShareStep, DynastyRules.ShareRoom(draft, b)));
            want -= want % DynastyCatalogue.ShareStep;
            if (want == now)
            {
                return now;
            }
            if (b == null)
            {
                if (draft.Bequests.Count >= DynastyCatalogue.BequestsMost)
                {
                    return 0;
                }
                b = new Bequest { Kind = kind, Id = kind == KinKind.Partner || kind == KinKind.Child ? 0 : id, Name = name ?? string.Empty };
                draft.Bequests.Add(b);
            }
            b.Share = want;
            if (want <= 0)
            {
                draft.Bequests.Remove(b);
            }
            return want;
        }

        /// <summary>The age the founder means to retire at, a step up or down inside its bounds.</summary>
        public static void RetireAge(Will draft, int steps)
        {
            draft.RetireAge = DynastyCatalogue.Clamp(draft.RetireAge + steps * DynastyCatalogue.RetireStep, DynastyCatalogue.RetireLeast, DynastyCatalogue.RetireMost);
        }

        /// <summary>What the founder keeps when they retire, a step up or down the list.</summary>
        public static void Keep(Will draft, int steps)
        {
            draft.Keep = DynastyCatalogue.Clamp(draft.Keep + steps, 0, DynastyCatalogue.KeepSteps.Length - 1);
        }

        /// <summary>
        /// Signs a draft: it must stand, say something new and its fee must be paid (<paramref name="pay"/> takes it, false if the cash is not there: nothing is signed then).
        /// The signed will is a copy, so the draft can go on being changed.
        /// </summary>
        public static WillWhy Sign(DynastyState s, Will draft, Notary notary, int day, int level, Func<long, bool> pay, out long fee)
        {
            fee = 0;
            WillWhy why = DynastyRules.Valid(draft);
            if (why != WillWhy.Ok)
            {
                return why;
            }
            if (s.Will.Written && s.Will.SameTerms(draft))
            {
                return WillWhy.Same;
            }
            fee = DynastyRules.Fee(!s.Will.Written, notary.Own, level);
            if (fee > 0 && (pay == null || !pay(fee)))
            {
                return WillWhy.Broke;
            }
            long fees = s.Will.Fees + fee;
            s.Will = draft.Copy();
            s.Will.Written = true;
            s.Will.Day = day;
            s.Will.Fees = fees;
            s.Will.Firm = notary.Name ?? string.Empty;
            return WillWhy.Ok;
        }
    }
}

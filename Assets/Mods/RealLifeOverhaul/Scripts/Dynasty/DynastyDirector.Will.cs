using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>The will: a draft the pages change a press at a time, and the signing, which is the one thing that costs and counts.</summary>
    public sealed partial class DynastyDirector
    {
        private Will _draft;   // what the pages are changing; null: the signed will as it is. Not saved: an unsigned change is only that

        /// <summary>The will as the pages show it: the draft, begun from the signed one at the first change.</summary>
        public Will Draft => _draft ?? (_loaded ? _state.Will : null);

        private Will Drafting()
        {
            if (_draft == null)
            {
                _draft = _state.Will.Copy();
            }
            return _draft;
        }

        private bool MayDraft => Running && _state.Hand == null;

        /// <summary>Names who carries on in the draft (None: the order of the law).</summary>
        public void DraftHeir(KinKind kind, int id, string name)
        {
            if (MayDraft)
            {
                DynastyWill.SetHeir(Drafting(), kind, id, name);
                _revision++;
            }
        }

        /// <summary>Moves a share of the draft a step up or down.</summary>
        public void DraftShare(KinKind kind, int id, string name, int steps)
        {
            if (MayDraft)
            {
                DynastyWill.Move(Drafting(), kind, id, name, steps);
                _revision++;
            }
        }

        public void DraftRetireAge(int steps)
        {
            if (MayDraft)
            {
                DynastyWill.RetireAge(Drafting(), steps);
                _revision++;
            }
        }

        public void DraftKeep(int steps)
        {
            if (MayDraft)
            {
                DynastyWill.Keep(Drafting(), steps);
                _revision++;
            }
        }

        /// <summary>The draft is let go: the signed will stands as it was.</summary>
        public void DraftDiscard()
        {
            _draft = null;
            _revision++;
        }

        /// <summary>Who draws a will up today.</summary>
        public static Notary NotaryNow()
        {
            try
            {
                return DynastyBridges.Notary != null ? DynastyBridges.Notary() : default(Notary);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return default(Notary);
            }
        }

        private static readonly Func<long, bool> PayFee = fee => DynastyGame.Pay(fee, DynastyGame.FeeKey, false);

        /// <summary>Signs the draft at the law firm: the fee is taken from the cash over the investors' floor, and nothing is signed if it is not there.</summary>
        public WillWhy Sign(out long fee)
        {
            fee = 0;
            if (!MayDraft)
            {
                return WillWhy.Off;
            }
            Notary notary = NotaryNow();
            WillWhy why = DynastyWill.Sign(_state, Drafting(), notary, DynastyGame.Day, DynastyGame.Level, PayFee, out fee);
            if (why == WillWhy.Ok)
            {
                _draft = null;
                if (Verbose)
                {
                    Logger?.Info("Dynasty check (a will signed): the heir " + (_state.Will.HeirKind == KinKind.None ? "by the law's order" : _state.Will.HeirName) + ", "
                                 + _state.Will.Bequests.Count + " bequest(s) of " + _state.Will.Others() / 10f + "% in all, fee " + fee + ": "
                                 + (DynastyRules.Valid(_state.Will) == WillWhy.Ok && _state.Will.Others() <= DynastyCatalogue.OthersMost ? "PASS" : "FAIL"));
                }
            }
            _revision++;
            return why;
        }
    }
}

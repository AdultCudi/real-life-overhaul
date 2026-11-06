using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The overdraft for cash gone under nothing by any way: the game forces a charge (its rent, wages, a bonus, a boat, a fee) past what the cash
    /// holds, or the night's estimate missed a bill. The night's cover (<see cref="BeforeRun"/>) pays what leaves at midnight; this one pays whatever is
    /// left under nothing, at the money change itself (EconomyDirector.OnGameEvent) and, as a net, every game hour. Cost: one comparison of the cash
    /// per money change while it is under nothing, nothing while it is not.
    /// </summary>
    public sealed partial class CreditDesk
    {
        private int _unsaid;          // drawn since the last notice: one notice for the night's many charges, not one each
        private bool _covering;       // the cover's own payment is a money change too: it must not cover itself

        // The open overdraft that may be drawn on now, or null: one the player holds, not frozen, with room left, the option on and the lenders lending.
        private CreditAccount OverdraftToDraw()
        {
            if (_file == null)
            {
                return null;
            }
            CreditAccount overdraft = _file.Account(ProductKind.Overdraft);
            CreditRefusal refusal;
            if (overdraft == null || overdraft.Frozen || overdraft.Free <= 0 || !Settings.EconomyProducts || !CreditLedger.CanBorrow(out refusal))
            {
                return null;
            }
            return overdraft;
        }

        // Draws on the overdraft for a shortfall and puts it in the cash, in the game's list. Returns what was drawn.
        private int Draw(CreditAccount overdraft, int shortfall)
        {
            return Put(overdraft, AccountRules.Cover(overdraft, shortfall));
        }

        // What the overdraft has just paid (already on what is owed) goes into the cash.
        private int Put(CreditAccount overdraft, int paid)
        {
            if (paid > 0)
            {
                _wallet.Give(paid, Money.Cover, Name(overdraft));
                CreditLedger.Touch();
            }
            return paid;
        }

        /// <summary>If the cash is under nothing, the overdraft pays it back up to nothing, as far as its limit allows. Told later, once (<see cref="TellCover"/>).</summary>
        public void CoverShort()
        {
            if (_covering || _file == null)
            {
                return;
            }
            int cash = _wallet.Cash;
            if (cash >= 0)
            {
                return;
            }
            CreditAccount overdraft = OverdraftToDraw();
            if (overdraft == null)
            {
                return;
            }
            _covering = true;
            try
            {
                int paid = Put(overdraft, AccountRules.CoverCash(overdraft, cash));
                _unsaid += paid;
                if (Verbose && paid > 0)
                {
                    Logger?.Info("Economy check (overdraft, cash under nothing): " + cash + " in cash, the overdraft paid " + paid + ", " + overdraft.Owed + " of " + overdraft.Agreed + " drawn, "
                                 + _wallet.Cash + " in cash now: " + (paid <= -cash && overdraft.Owed <= overdraft.Agreed ? "PASS" : "FAIL"));
                }
            }
            finally
            {
                _covering = false;
            }
        }

        /// <summary>Tells the player once what the overdraft paid since the last notice, if anything.</summary>
        public void TellCover()
        {
            if (_unsaid <= 0 || _file == null)
            {
                return;
            }
            CreditAccount overdraft = _file.Account(ProductKind.Overdraft);
            int paid = _unsaid;
            _unsaid = 0;
            if (overdraft == null)
            {
                return;
            }
            Note(NotificationType.Info, "reallife_econ_note_cover_short",
                Data("amount", FinanceText.Money(paid), "owed", FinanceText.Money(overdraft.Owed), "limit", FinanceText.Money(overdraft.Agreed)));
        }

        /// <summary>The game hour's net: cash found under nothing (a save that loaded so, an overdraft opened since) is covered, then what was drawn is told.</summary>
        public void CoverHour()
        {
            CoverShort();
            TellCover();
        }
    }
}

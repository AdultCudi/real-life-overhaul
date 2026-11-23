namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The owner's own card from their own bank (Economy/OwnBank/Cards): an ordinary card of the credit file, with the same statement, minimum, interest and
    /// missed-payment ladder as any, that the file's one-card rule does not count (<see cref="CreditFile.Account"/>) and that the bank's books follow
    /// (<see cref="OwnCardSink"/>). The desk only opens it, closes it and lets it go when the bank ends; the bank decides the limit and the rate.
    /// </summary>
    public sealed partial class CreditDesk
    {
        private void ConnectOwnCard()
        {
            OwnCardSink.Owed = OwnCardOwed;
            OwnCardSink.Orphan = OrphanOwnCard;
            OwnCardSink.Open = OpenOwnCard;
            OwnCardSink.Close = CloseOwnCard;
        }

        private void DisconnectOwnCard()
        {
            OwnCardSink.Owed = null;
            OwnCardSink.Orphan = null;
            OwnCardSink.Open = null;
            OwnCardSink.Close = null;
        }

        /// <summary>What is owed on the owner's own card, in whole money; -1 if there is no such card.</summary>
        private int OwnCardOwed()
        {
            CreditAccount a = _file != null ? _file.OwnCard() : null;
            return a != null ? a.Owed : -1;
        }

        /// <summary>The owner's card opened with the limit and the rate agreed (hundredths of a per cent a year, the central bank's added day by day as on any card); false if one is open.</summary>
        private bool OpenOwnCard(int limit, int rateBp, string name)
        {
            if (_file == null || _file.OwnCard() != null || limit <= 0)
            {
                return false;
            }
            int day = Today;
            CreditAccount a = AccountRules.Open(ProductKind.CreditCard, limit, rateBp, day);
            a.Own = true;
            a.SecurityName = name ?? string.Empty;
            Opened(a);
            CreditLedger.Post(CreditKind.Application, limit, day, a.Key);
            _director.TermsChanged("the owner's own card opened");
            return true;
        }

        /// <summary>The owner's card closed, with nothing owed on it; false if something is.</summary>
        private bool CloseOwnCard()
        {
            CreditAccount a = _file != null ? _file.OwnCard() : null;
            if (a == null)
            {
                return true;
            }
            if (a.Owed > 0)
            {
                return false;
            }
            Closed(a);
            _director.TermsChanged("the owner's own card closed");
            return true;
        }

        /// <summary>The bank is gone: the card is an ordinary one with no bank, and what is owed on it is still owed.</summary>
        private void OrphanOwnCard()
        {
            CreditAccount a = _file != null ? _file.OwnCard() : null;
            if (a != null)
            {
                a.Own = false;
                CreditLedger.Touch();
            }
        }
    }
}

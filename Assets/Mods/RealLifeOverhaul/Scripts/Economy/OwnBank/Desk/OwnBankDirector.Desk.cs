using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's trading desk in its director (BankDesk): the desk opened or closed, the own book's mandate, its share of the capital and its risk limit, and
    /// the notice when the limit cuts the book. Choices are written as they are made and read by the next day. Nothing per frame.
    /// </summary>
    public sealed partial class OwnBankDirector
    {
        /// <summary>The desk opened or closed: clients' brokerage and the own book. Closed, the book is sold on the next day and the brokerage stops.</summary>
        public void SetDesk(bool on)
        {
            if (Running && _bank.Running && _bank.DeskOn != on)
            {
                _bank.DeskOn = on;
                _bank.DeskDirty = true;
                if (!on)
                {
                    _bank.DeskPeak = 0;
                }
                Logger?.Info("Own bank: the trading desk is " + (on ? "open" : "closed") + " from day " + Save.Day + ".");
                Changed();
            }
        }

        public void SetDeskMandate(int mandate)
        {
            if (Running && _bank.Running)
            {
                int m = BankDesk.MandateOf(mandate);
                if (m != _bank.DeskMandate)
                {
                    _bank.DeskMandate = m;
                    _bank.DeskDirty = true;
                }
            }
        }

        public void SetDeskShare(int share)
        {
            if (Running && _bank.Running)
            {
                int s = Math.Max(0, Math.Min(BankDesk.ShareMost, share / BankDesk.ShareStep * BankDesk.ShareStep));
                if (s != _bank.DeskShare)
                {
                    _bank.DeskShare = s;
                    _bank.DeskDirty = true;
                }
            }
        }

        public void SetDeskLimit(int limit)
        {
            if (Running && _bank.Running)
            {
                _bank.DeskLimit = Math.Max(BankDesk.LimitLeast, Math.Min(BankDesk.LimitMost, limit / BankDesk.LimitStep * BankDesk.LimitStep));
            }
        }

        // A cut told once: the book was sold down by the risk limit.
        private void TellDesk()
        {
            if (_day.DeskCut)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_desk_cut", TabDesk, "bank", _bank.Name, "limit", OwnBankText.Ratio(_bank.DeskLimit), "keep", OwnBankText.Ratio(BankDesk.CutKeep));
                Logger?.Info("Own bank: the desk's risk limit of " + _bank.DeskLimit + " thousandths cut the own book on day " + Save.Day + " to " + _bank.BookValue / 100 + " ("
                    + _bank.DeskCuts + " cut(s) so far).");
            }
        }
    }
}

using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's credit cards in its director (CardTerms): the owner's rules (issuing, the limit and the yearly fee by kind of client, the rate, the lines of
    /// approval), the queue of the borderline applicants and the owner's own card. Each choice is written as it is made and read by the next day. Nothing per frame.
    /// </summary>
    public sealed partial class OwnBankDirector
    {
        /// <summary>The card business switched on or off. Off, no new card is issued and what is out stays, earning and losing until it is paid.</summary>
        public void SetCards(bool on)
        {
            if (Running && _bank.Running && _bank.CardsOn != on)
            {
                _bank.CardsOn = on;
                Logger?.Info("Own bank: the card business is " + (on ? "open" : "closed to new cards") + " from day " + Save.Day + ".");
                Changed();
            }
        }

        public void SetCardIssue(int kind, bool on)
        {
            if (Running && _bank.Running && kind >= 0 && kind < CardTerms.Kinds)
            {
                _bank.CardIssue[kind] = on;
            }
        }

        public void SetCardLimit(int kind, int step)
        {
            if (Running && _bank.Running && kind >= 0 && kind < CardTerms.Kinds)
            {
                _bank.CardLimit[kind] = Math.Max(0, Math.Min(CardTerms.LimitSteps - 1, step));
            }
        }

        public void SetCardRate(int offset)
        {
            if (Running && _bank.Running)
            {
                _bank.CardRate = Math.Max(CardTerms.RateLeast, Math.Min(CardTerms.RateMost, offset / CardTerms.RateStep * CardTerms.RateStep));
            }
        }

        public void SetCardFee(int step)
        {
            if (Running && _bank.Running)
            {
                _bank.CardFee = Math.Max(0, Math.Min(CardTerms.FeeSteps.Length - 1, step));
            }
        }

        /// <summary>The credit scores a card is approved at once from and declined under; the second never above the first. What lies between is queued for the owner.</summary>
        public void SetCardLines(int auto, int decline)
        {
            if (Running && _bank.Running)
            {
                int a = Math.Max(CardTerms.AutoLeast, Math.Min(CardTerms.AutoMost, auto / CardTerms.LineStep * CardTerms.LineStep));
                int d = Math.Max(CardTerms.DeclineLeast, Math.Min(a, decline / CardTerms.LineStep * CardTerms.LineStep));
                _bank.CardAuto = a;
                _bank.CardDecline = d;
            }
        }

        /// <summary>The Black card tier offered or withdrawn. Withdrawn, no new Black card is issued and those out run down as clients give them up.</summary>
        public void SetBlack(bool on)
        {
            if (Running && _bank.Running && _bank.BlackOn != on)
            {
                _bank.BlackOn = on;
                Logger?.Info("Own bank: the Black card tier is " + (on ? "offered" : "withdrawn") + " from day " + Save.Day + ".");
                Changed();
            }
        }

        /// <summary>The Black tier's lines: the least wealth (an index of the steps), the least credit score, the yearly fee and the rewards (indexes). Each is held to its steps.</summary>
        public void SetBlackLines(int wealth, int score, int fee, int reward)
        {
            if (Running && _bank.Running)
            {
                _bank.BlackWealth = BlackTerms.Clamp(wealth, 0, BlackTerms.WealthLines.Length - 1);
                _bank.BlackScore = BlackTerms.ScoreOf(score);
                _bank.BlackFee = BlackTerms.Clamp(fee, 0, BlackTerms.FeeSteps.Length - 1);
                _bank.BlackReward = BlackTerms.Clamp(reward, 0, BlackTerms.RewardBp.Length - 1);
            }
        }

        /// <summary>The owner's answer to the applicants queued in a kind: all approved or all declined. False with a reason key if none wait.</summary>
        public bool DecideCards(int kind, bool approve, out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : null);
            if (why != null)
            {
                return false;
            }
            int n = OwnBankRules.CardDecide(_bank, kind, approve);
            if (n <= 0)
            {
                why = "reallife_ob_why_no_queue";
                return false;
            }
            Tell(NotificationType.Info, approve ? "reallife_ob_toast_cards_approved" : "reallife_ob_toast_cards_declined", TabCards, "count", n.ToString(), "kind", OwnBankText.Segment(kind));
            Changed();
            return true;
        }

        /// <summary>The owner's own card from the bank, at the limit given (whole money): opened in the credit file as a card of the bank's top tier. False with a reason key if it cannot
        /// be.</summary>
        public bool OpenOwnCard(long limitWhole, out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : _bank.OwnCard ? "reallife_ob_why_own_card_open" : !_bank.CardsOn ? "reallife_ob_why_cards_off" : null);
            if (why != null)
            {
                return false;
            }
            long most = OwnBankRules.OwnCardMost(_bank, Prices.Level) / 100;
            if (limitWhole <= 0 || limitWhole > most || OwnCardSink.Open == null)
            {
                why = most <= 0 ? "reallife_ob_why_own_card_capital" : OwnCardSink.Open == null ? "reallife_ob_why_off" : "reallife_ob_why_own_card_limit";
                return false;
            }
            int limit = (int)Math.Min(int.MaxValue / 2, limitWhole);
            int rate = Math.Max(CreditCatalogue.RateFloor * 100, CreditDesk.AgreedAt(ProductKind.CreditCard, CreditCatalogue.ScoreMax, BankTerms.House) - CardTerms.BlackDiscountBp);
            string name = OwnBankText.T(CardTerms.BlackKey) + ", " + _bank.Name;
            if (!OwnCardSink.Open(limit, rate, name))
            {
                why = "reallife_ob_why_own_card_open";
                return false;
            }
            _bank.OwnCard = true;
            _bank.OwnOwed = 0;
            _bank.OwnLimit = limit * 100L;
            _bank.OwnRateBp = rate;
            _bank.OwnDrawn = _bank.OwnPaid = _bank.OwnInterest = _bank.OwnFees = _bank.OwnLost = 0;
            Tell(NotificationType.Success, "reallife_ob_toast_own_card", TabCards, "limit", OwnBankText.Money(limit * 100L), "bank", _bank.Name);
            Logger?.Info("Own bank: the owner's card opened on day " + Save.Day + " with a limit of " + limit + " at " + rate + " hundredths of a per cent.");
            Changed();
            return true;
        }

        /// <summary>The owner's own card closed once nothing is owed on it. False with a reason key if something is.</summary>
        public bool CloseOwnCard(out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : !_bank.OwnCard ? "reallife_ob_why_no_own_card" : null);
            if (why != null)
            {
                return false;
            }
            ReconcileOwnCard();
            if (_bank.OwnOwed + _bank.OwnDrawn + _bank.OwnInterest + _bank.OwnFees - _bank.OwnPaid - _bank.OwnLost > 0 || OwnCardSink.Close == null || !OwnCardSink.Close())
            {
                why = "reallife_ob_why_own_card_owed";
                return false;
            }
            _bank.OwnCard = false;
            Changed();
            return true;
        }

        // The bank and the card agree on what is owed: what the credit file shows is what the bank is owed, with the bank's own record of what moved since its last day. After a stand-still
        // (the bank's option off while the credit side ran) the difference came from the card's own interest and the owner's payments.
        private void ReconcileOwnCard()
        {
            if (_bank == null || !_bank.Running || !_bank.OwnCard || OwnCardSink.Owed == null)
            {
                return;
            }
            int actual = OwnCardSink.Owed();
            long expected = _bank.OwnOwed + _bank.OwnDrawn + _bank.OwnInterest + _bank.OwnFees - _bank.OwnPaid - _bank.OwnLost;
            long diff = Math.Max(0, actual) * 100L - expected;
            if (diff > 0)
            {
                _bank.OwnInterest += diff;
            }
            else if (diff < 0)
            {
                _bank.OwnPaid += -diff;
            }
            if (actual < 0)
            {
                _bank.OwnCard = false;   // the card is closed: nothing is owed
            }
        }

        // The card the bank no longer holds: its owner still owes what is on it.
        private void LetOwnCardGo()
        {
            OwnCardSink.Orphan?.Invoke();
            if (_bank != null)
            {
                _bank.OwnCard = false;
            }
        }

        // What the card business tells at the month's turn: applicants waiting for the owner's answer.
        private void TellCards()
        {
            if (!_bank.Running || !_bank.CardsOn)
            {
                return;
            }
            int waiting = 0;
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                waiting += _bank.CardQueue[j];
            }
            if (waiting > 0)
            {
                Tell(NotificationType.Info, "reallife_ob_toast_cards_queue", TabCards, "count", waiting.ToString());
            }
        }
    }
}

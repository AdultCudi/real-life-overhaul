using System;
using UI.Notification;

namespace RealLifeOverhaul.Schemes
{
    // What a press does: each scheme refused before any money moves when it cannot be done, and recorded on its trail by the rules when it is.
    public sealed partial class SchemeDirector
    {
        /// <summary>Where a scheme's quiet button stands: a policy's page, the return, a business's books, a business's campaigns.</summary>
        public const int MatterClaim = 0, MatterExpense = 1, MatterBooks = 2, MatterClub = 3;

        private bool Shop(string place, out SchemeShopInfo shop)
        {
            ReadShops();
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Place == place)
                {
                    shop = _shops[i];
                    return true;
                }
            }
            shop = default;
            return false;
        }

        private static void Say(NotificationType type, string key, params string[] pairs)
        {
            SchemeGame.Tell(type, key, pairs != null && pairs.Length > 1 ? SchemeGame.Data(pairs) : null);
        }

        /// <summary>
        /// The words of a scheme's quiet button where its honest action stands, or null for no button: the option off, the
        /// business not the player's, or nothing there for the scheme to act on. Asked when a page is written, never per frame.
        /// </summary>
        public static string QuietLabel(int matter, string place, int cover)
        {
            SchemeDirector d = Instance;
            if (!On || d == null)
            {
                return null;
            }
            try
            {
                switch (matter)
                {
                    case MatterClaim:
                        return d.Shop(place, out SchemeShopInfo insured) && insured.Mine && (insured.Covers & (1 << cover)) != 0 ? "reallife_scheme_quiet_claim" : null;
                    case MatterExpense:
                        return SchemeBridges.TaxPercent != null && SchemeBridges.TaxPercent() > 0 ? "reallife_scheme_quiet_expense" : null;
                    case MatterBooks:
                        return d.Shop(place, out SchemeShopInfo kept) && kept.Mine ? "reallife_scheme_quiet_books" : null;
                    case MatterClub:
                        return d.Shop(place, out SchemeShopInfo selling) && selling.Mine && selling.Cash ? "reallife_scheme_quiet_club" : null;
                    default:
                        return null;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        /// <summary>The quiet button was pressed: the phone is put away and the scheme's page says what it is, what it brings and what it risks.</summary>
        public static void QuietOpen(int matter, string place, int cover)
        {
            SchemeDirector d = Instance;
            if (!On || d == null || !d.Ready("a scheme's page"))
            {
                return;
            }
            try
            {
                SchemeGame.PutPhoneAway();
                switch (matter)
                {
                    case MatterClaim:
                        d.ShowClaim(place, cover);
                        break;
                    case MatterExpense:
                        d.ShowExpense();
                        break;
                    case MatterBooks:
                        d.ShowBooks(place);
                        break;
                    case MatterClub:
                        d.ShowClub(place);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // An invented claim: put in by the insurer's own rules, and on the trail with what it will pay.
        private void DoClaim(string place, int cover, int size)
        {
            int day = SchemeGame.Day;
            if (!On || !Shop(place, out SchemeShopInfo shop) || !shop.Mine || (shop.Covers & (1 << cover)) == 0 || SchemeBridges.Claim == null || SchemeBridges.CoverMost == null)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_not_now");
                return;
            }
            SchemeShop kept = _state.Shop(place, true);
            if (day - kept.ClaimDay < SchemeCatalogue.ClaimGapDays)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_too_soon", "day", SchemeText.Whole(kept.ClaimDay + SchemeCatalogue.ClaimGapDays));
                return;
            }
            int loss = SchemeRules.ClaimLoss(SchemeBridges.CoverMost(cover), size);
            if (!SchemeBridges.Claim(place, cover, loss, out int id, out int payout, out int payDay) || payout <= 0)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_claim_none");
                return;
            }
            kept.ClaimDay = day;
            SchemeRules.Record(_state, SchemeKind.Claim, day, payout, size, 0, true, place, id);
            Say(NotificationType.Info, "reallife_scheme_note_claim_in", "business", shop.Name, "amount", SchemeText.Money(payout), "day", SchemeText.Whole(payDay));
            Logger?.Info("Schemes: an invented claim at " + shop.Name + " (cover " + cover + ", loss " + loss + "): claim " + id + " pays " + payout + " on day " + payDay
                         + "; heat " + _state.Heat[(int)SchemeKind.Claim] + ".");
        }

        // An invented expense: worth the game's tax on it, paid now as the tax saved, only while the game's tax falls due at all.
        private void DoExpense(int size)
        {
            int day = SchemeGame.Day, percent = SchemeBridges.TaxPercent != null ? SchemeBridges.TaxPercent() : 0;
            if (!On || percent <= 0 || SchemeBridges.Taxed == null || !SchemeBridges.Taxed())
            {
                Say(NotificationType.Warning, "reallife_scheme_note_expense_no_tax");
                return;
            }
            if (day - _state.ExpenseDay < SchemeCatalogue.ExpenseGapDays)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_too_soon", "day", SchemeText.Whole(_state.ExpenseDay + SchemeCatalogue.ExpenseGapDays));
                return;
            }
            long amount = SchemeRules.ExpenseAmount(AverageTakings(), size), gain = SchemeRules.ExpenseGain(amount, percent);
            if (gain <= 0)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_expense_no_tax");
                return;
            }
            _state.ExpenseDay = day;
            SchemeGame.Give(gain, SchemeGame.ExpenseKey);
            SchemeRules.Record(_state, SchemeKind.Expense, day, gain, size, 0, true, string.Empty, 0);
            Say(NotificationType.Info, "reallife_scheme_note_expense_in", "amount", SchemeText.Money(amount), "saved", SchemeText.Money(gain));
            Logger?.Info("Schemes: an invented expense of " + amount + " saved " + gain + " of tax at " + percent + "%; heat " + _state.Heat[(int)SchemeKind.Expense] + ".");
        }

        // An average day's takings of all the player's businesses together.
        private long AverageTakings()
        {
            ReadShops();
            long sum = 0;
            for (int i = 0; i < _shops.Count; i++)
            {
                sum += _shops[i].Mine ? Math.Max(0L, _shops[i].Average) : 0L;
            }
            return sum;
        }

        private void SetLift(string place, int lift)
        {
            if (!Shop(place, out SchemeShopInfo shop) || !shop.Mine || (!On && lift > 0))
            {
                Say(NotificationType.Warning, "reallife_scheme_note_not_now");
                return;
            }
            SchemeShop kept = _state.Shop(place, true);
            kept.Lift = SchemeRules.LiftStep(lift);
            Say(NotificationType.Info, kept.Lift > 0 ? "reallife_scheme_note_lift_on" : "reallife_scheme_note_lift_off", "business", shop.Name, "share",
                SchemeText.Percent(kept.Lift));
            Logger?.Info("Schemes: the books of " + shop.Name + " now show " + kept.Lift + " thousandths over the truth.");
        }

        private void SetWash(string place, int rate)
        {
            if (!Shop(place, out SchemeShopInfo shop) || !shop.Mine || (rate > 0 && (!On || !shop.Cash)))
            {
                Say(NotificationType.Warning, "reallife_scheme_note_not_now");
                return;
            }
            SchemeShop kept = _state.Shop(place, true);
            kept.Wash = SchemeRules.WashStep(rate);
            Say(NotificationType.Info, kept.Wash > 0 ? "reallife_scheme_note_wash_on" : "reallife_scheme_note_wash_off", "business", shop.Name, "share",
                SchemeText.Percent(kept.Wash));
            Logger?.Info("Schemes: the till of " + shop.Name + " now washes " + kept.Wash + " thousandths of an average day's takings a day.");
        }

        // A club sold to a district's households: only where the city's people are this machine's own, refused before any money moves otherwise.
        private void DoClub(string place, int size)
        {
            int day = SchemeGame.Day;
            if (!On || !Shop(place, out SchemeShopInfo shop) || !shop.Mine || !shop.Cash)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_not_now");
                return;
            }
            if (SchemeGame.InSession || SchemeBridges.People == null || !SchemeBridges.People() || SchemeBridges.Club == null)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_club_no_people");
                return;
            }
            SchemeShop kept = _state.Shop(place, true);
            if (day - kept.ClubDay < SchemeCatalogue.ClubGapDays)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_too_soon", "day", SchemeText.Whole(kept.ClubDay + SchemeCatalogue.ClubGapDays));
                return;
            }
            if (!SchemeBridges.Club(shop.District, _state.Acted, SchemeRules.ClubHouseholds(size), SchemeGame.Level, out long taken, out int joined) || taken <= 0)
            {
                Say(NotificationType.Warning, "reallife_scheme_note_club_none");
                return;
            }
            SchemeRules.ClubSold(_state, place, taken, joined, size, day);
            Say(NotificationType.Info, "reallife_scheme_note_club_in", "count", SchemeText.Whole(joined), "amount", SchemeText.Money(taken));
            Logger?.Info("Schemes: a club sold from " + shop.Name + " to " + joined + " household(s) took " + taken + "; cash in hand " + _state.Dirty + ", heat "
                         + _state.Heat[(int)SchemeKind.Club] + ".");
        }

        /// <summary>
        /// A kind's file is paid back before it is found: out of the cash in hand first for a club, the rest from the
        /// wallet, refused whole when the wallet cannot cover it. A club's stakes go back to their households. What to say.
        /// </summary>
        public SchemeSaid Repay(SchemeKind kind)
        {
            if (!Ready("paying back"))
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            int day = SchemeGame.Day;
            long cost = SchemeRules.RepayCost(_state, kind, day);
            if (cost <= 0)
            {
                return SchemeSaid.Of("reallife_scheme_file_repay_nothing");
            }
            long cash = kind == SchemeKind.Club && !_state.Watched ? Math.Min(_state.Dirty, cost) : 0L;
            if (!Affords(cost - cash))
            {
                return SchemeSaid.Of("reallife_scheme_file_repay_short", "amount", SchemeText.Money(cost - cash));
            }
            long paid = SchemeRules.Repay(_state, kind, day, out long fromCash);
            if (!SchemeGame.Pay(paid - fromCash, SchemeGame.RepayKey))
            {
                SchemeGame.Take(paid - fromCash, SchemeGame.RepayKey);   // the wallet moved since it was counted: what is settled on the file is paid all the same
            }
            if (kind == SchemeKind.Club)
            {
                GiveBack(paid);
            }
            Logger?.Info("Schemes: the " + kind + " file was paid back: " + paid + " (" + fromCash + " of it from the cash in hand).");
            return SchemeSaid.Of("reallife_scheme_file_repay_done", "amount", SchemeText.Money(paid));
        }

        /// <summary>The player tells the investigators of an inquiry everything. What to say.</summary>
        public SchemeSaid Cooperate(int inquiry)
        {
            SchemeInquiry q = Ready("telling them everything") ? _state.Inquiry(inquiry) : null;
            if (!SchemeRules.Cooperate(q))
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            Logger?.Info("Schemes: the player told the investigators of inquiry " + q.Id + " (" + q.Kind + ") everything.");
            return SchemeSaid.Of("reallife_scheme_file_coop_done", "what", SchemeText.Kind(q.Kind));
        }

        // ------------------------------------------------------------------------------------------------- a business sold

        /// <summary>Thousandths the books of a business show over the truth, to whoever values it for a sale; 0 while the schemes are off.</summary>
        public static int BooksLift(string place)
        {
            SchemeDirector d = Instance;
            SchemeShop shop = On && d != null ? d._state.Shop(place, false) : null;
            return shop != null ? shop.Lift : 0;
        }

        /// <summary>
        /// A buyer is about to pay for a business: true if they see through its dressed books, by how hard their kind looks.
        /// They then walk and say why, the books are plain again, and no money has moved.
        /// </summary>
        public static bool SaleSeen(string place, int buyerKind, int offer)
        {
            SchemeDirector d = Instance;
            SchemeShop shop = d != null && d._state != null && !d._broken ? d._state.Shop(place, false) : null;
            if (shop == null || shop.Lift <= 0 || !SchemeRules.SeenThrough(d._state.Seed, place, buyerKind, shop.Lift, offer))
            {
                return false;
            }
            SchemeRules.Walked(d._state, place);
            Say(NotificationType.Error, "reallife_scheme_note_sale_seen");
            Logger?.Info("Schemes: a buyer (kind " + buyerKind + ") saw through the books at " + place + " and walked; heat " + d._state.Heat[(int)SchemeKind.Books] + ".");
            return true;
        }

        /// <summary>A business was sold: the part of its price the dressed books made goes on the trail.</summary>
        public static void SaleDone(string place, long price)
        {
            SchemeDirector d = Instance;
            if (d == null || d._state == null || d._broken)
            {
                return;
            }
            SchemeAct act = SchemeRules.Sold(d._state, place, price, SchemeGame.Day);
            if (act != null)
            {
                Logger?.Info("Schemes: a business sold on dressed books for " + price + ": " + act.Gain + " of it was for sales that never happened; heat "
                             + d._state.Heat[(int)SchemeKind.Books] + ".");
            }
        }

        // ------------------------------------------------------------------------------------------------- a rival's fund

        /// <summary>The player puts the sum into the fund on offer: paid first, kept from the investors' floor, and the rival has it. What to say.</summary>
        public SchemeSaid FundPut()
        {
            SchemeFund f = Ready("a fund") ? _state.Fund : null;
            if (f == null || f.State != FundState.Open)
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            if (!Affords(f.Amount))
            {
                return SchemeSaid.Of("reallife_scheme_fund_short", "amount", SchemeText.Money(f.Amount));
            }
            if (!SchemeRules.FundPlace(_state, SchemeGame.Day) || !SchemeGame.Pay(f.Amount, SchemeGame.FundKey))
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            SchemeBridges.RivalMoney?.Invoke(f.Tycoon, f.Amount);
            Logger?.Info("Schemes: " + f.Amount + " placed in " + RivalHolding(f.Tycoon) + "'s fund until day " + f.Due + (f.Sham ? " (a sham)." : " (honest)."));
            return SchemeSaid.Of("reallife_scheme_fund_put_done", "holding", RivalHolding(f.Tycoon), "amount", SchemeText.Money(f.Amount), "day", SchemeText.Whole(f.Due));
        }

        /// <summary>The player has the fund looked at, for a fee: what it is, is said. What to say.</summary>
        public SchemeSaid FundLook()
        {
            SchemeFund f = Ready("a fund") ? _state.Fund : null;
            if (f == null || f.State != FundState.Open || f.Checked)
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            long fee = SchemeRules.FundFee(f.Amount, SchemeGame.Level);
            if (!Affords(fee) || !SchemeGame.Pay(fee, SchemeGame.FundFeeKey))
            {
                return SchemeSaid.Of("reallife_scheme_fund_short", "amount", SchemeText.Money(fee));
            }
            SchemeRules.FundCheck(_state);
            return SchemeSaid.Of(f.Sham ? "reallife_scheme_fund_look_sham" : "reallife_scheme_fund_look_honest", "holding", RivalHolding(f.Tycoon));
        }

        public SchemeSaid FundTurnDown()
        {
            return SchemeSaid.Of(Ready("a fund") && SchemeRules.FundDecline(_state) ? "reallife_scheme_fund_down_done" : "reallife_scheme_note_not_now");
        }

        /// <summary>The player reports a sham fund to the police: it falls on the rival's trail. What to say.</summary>
        public SchemeSaid FundTell()
        {
            if (!Ready("a report") || !SchemeRules.FundReport(_state))
            {
                return SchemeSaid.Of("reallife_scheme_note_not_now");
            }
            return SchemeSaid.Of("reallife_scheme_fund_tell_done", "holding", RivalHolding(_state.Fund.Tycoon));
        }

        // Whether the wallet can pay a sum and still keep what belongs to the investors of the player's firms.
        private static bool Affords(long amount)
        {
            long floor = SchemeBridges.Floor != null ? Math.Max(0L, SchemeBridges.Floor()) : 0L;
            return SchemeGame.Cash - amount >= floor;
        }
    }
}

using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Tax, the accountant and cash kept off the books: what the director does with <see cref="TaxRules"/>.
    ///
    /// WHAT THE GAME ALREADY HAS, AND IS LEFT EXACTLY AS IT IS: once a year (TaxHelper.RunDaily, on the day the year
    /// turns) it adds up every business's sales from its daily statements and any gambling winnings; if that passes
    /// its threshold it takes its tax rate of the total less the expenses it has marked deductible as they were paid
    /// (wages, rent, marketing through an agency, vehicles and more), adds its property tax, sends the bill, adds a
    /// tenth after twenty days and after thirty takes goods and vehicles. It has no return to file, no accountant and
    /// no way to keep sales out of the count.
    ///
    /// WHAT IS ADDED HERE, AND HOW:
    ///   deductible costs   the mod's own business costs are marked deductible as they are paid, the way the game
    ///                      marks its own (CompanyGame.Take), so the game's yearly tax deducts them itself. Nothing
    ///                      is worked out twice and there is no rebate for them
    ///   the return         a duty to file each quarter. Late, it costs a penalty (a transaction: way c), and two late
    ///                      running bring business pressure's own tax audit (way a)
    ///   the accountant     a monthly fee and, when a return is filed, a rebate of a share of the quarter's deductible
    ///                      costs (transactions: way c)
    ///   cash off the books each day, the tax the game would take on the hidden share of yesterday's sales is paid
    ///                      back as a rebate (a transaction: way c), but only while the last year's income passes the
    ///                      game's threshold, since under it the game takes no tax and there is nothing to keep back.
    ///                      The game's own count of sales is not changed. An audit takes it all back with a fine and,
    ///                      from a stated sum, opens a case in the mod's courts (Justice/TaxCases, through Wiring)
    /// Nothing of the game's tax is written: not its bill, not its list of deductible expenses, not its back-tax.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private int MonthDays => MacroCatalogue.MonthDays(CompanyGame.DaysPerYear);

        /// <summary>A quarter in game days: three of the city's months.</summary>
        public int QuarterDays => TaxRules.QuarterDays(MonthDays);

        /// <summary>Per cent of takings kept off the books, added up over the businesses; nothing while that option is off.</summary>
        public int Hidden()
        {
            int hidden = 0;
            for (int i = 0; Settings.EconomyCompanyHide && _state != null && i < _state.Shops.Count; i++)
            {
                hidden += _state.Shops[i].Hide;
            }
            return hidden;
        }

        partial void TaxDay(int day)
        {
            TaxFile t = _state.Tax;
            int yesterday = day - 1, hidden = Hidden();
            bool hiding = hidden > 0;

            if (t.Accountant >= 0 && TaxRules.Resigns(t.Accountant, hiding))
            {
                Resign();
            }
            else if (t.Accountant >= 0 && day >= t.AccountantNext)
            {
                int fee = TaxRules.Fee(t.Accountant, _index, hiding);
                if (Cost(fee, CompanyGame.AccountantKey, null, CompanyText.Accountant(t.Accountant), CostKind.Accountant, yesterday, "a month of accountant " + t.Accountant + " at today's prices" + (hiding ? ", with cash off the books" : string.Empty)))
                {
                    t.AccountantNext = day + MonthDays;
                }
                else
                {
                    Tell(NotificationType.Warning, "reallife_co_note_accountant_unpaid", new Dictionary<string, string> { { "name", CompanyText.Accountant(t.Accountant) } }, TabTax, "acc");
                    t.Accountant = -1;
                }
            }

            // Yesterday's hidden takings: the tax they would have borne is paid back, while the game's tax falls due at all.
            if (hiding)
            {
                bool asked = false, taxed = false;
                int percent = CompanyGame.TaxPercent;
                for (int i = 0; i < _shops.Count; i++)
                {
                    CompanyShop shop = _shops[i];
                    if (shop.File.Hide <= 0 || !shop.HasBooks || shop.Takings <= 0)
                    {
                        continue;
                    }
                    if (!asked)
                    {
                        asked = true;
                        taxed = CompanyGame.Taxed(yesterday);   // (one walk over the game's statements, on a day something is hidden)
                    }
                    int saved = taxed ? TaxRules.Saved(shop.Takings, shop.File.Hide, percent) : 0;
                    if (saved > 0)
                    {
                        Pay(saved, CompanyGame.HiddenKey, shop, shop.Name, yesterday, shop.File.Hide + "% of the " + shop.Takings + " taken on day " + yesterday + " kept off the books, at the game's tax rate of " + percent + "%");
                        t.Saved = (int)System.Math.Min(int.MaxValue / 2, (long)t.Saved + saved);
                    }
                    else if (Verbose)
                    {
                        Logger?.Info("Company check (tax, cash off the books at " + shop.Name + "): " + shop.File.Hide + "% of " + shop.Takings + " hidden; "
                                     + (taxed ? "the tax on it rounds to nothing" : "the last year's income is under the game's tax threshold, so no tax is due and nothing is paid back") + ": PASS");
                    }
                }
            }
            int before = t.Risk;
            t.Risk = TaxRules.RiskDay(t.Risk, hidden);
            if (Verbose && (before != t.Risk || hiding))
            {
                Logger?.Info("Company check (tax, the audit risk): " + hidden + "% hidden in all, the risk " + (before / 100f).ToString("0.00") + "% to " + (t.Risk / 100f).ToString("0.00") + "% a day, with accountant "
                             + t.Accountant + " " + (TaxRules.AuditChance(t.Risk, t.Accountant) / 100f).ToString("0.00") + "%; saved since the last audit " + t.Saved + ": PASS");
            }
            if (Settings.EconomyCompanyHide && TaxRules.Audited(t, _state.Seed, _state.Age))
            {
                Audit(day);
            }
            if (t.Owed > 0)
            {
                TakeOwed(yesterday);
            }

            if (TaxRules.QuarterOver(t, day, QuarterDays))
            {
                // (With what is in no business's statement: the gain on a business sold in the quarter, counted here once.)
                int from = t.QuarterFrom, takings = CompanyGame.Sales(from, yesterday) + (CompanyBridges.OtherIncome != null ? CompanyBridges.OtherIncome(from, yesterday) : 0);
                TaxRules.Close(t, takings, day);
                if (Verbose)
                {
                    Logger?.Info("Company check (tax, quarter " + t.ReturnQuarter + " closed): days " + from + " to " + yesterday + " (" + QuarterDays + " days, three months of " + MonthDays + "), takings " + takings
                                 + " by the game's own sum, the mod's deductible costs " + t.ReturnCosts + "; the return is due by day " + t.ReturnDue + ": PASS");
                }
                if (TaxRules.Files(t.Accountant, hiding))
                {
                    FileReturn(true);
                }
                else
                {
                    Tell(NotificationType.Info, "reallife_co_note_return_due", new Dictionary<string, string> { { "quarter", t.ReturnQuarter.ToString() }, { "day", t.ReturnDue.ToString() } }, TabTax, "ret");
                }
            }
            int penalty = TaxRules.LateDay(t, day);
            if (penalty > 0)
            {
                Charge(penalty, CompanyGame.PenaltyKey, null, string.Empty, yesterday, true, "the return for quarter " + t.ReturnQuarter + " was due by day " + t.ReturnDue + "; penalty " + t.ReturnPenalties + " of "
                                                                                           + CompanyCatalogue.PenaltiesMost + " at most, " + CompanyText.Share(CompanyCatalogue.PenaltyShare) + " of takings of " + t.ReturnTakings);
                Tell(NotificationType.Warning, "reallife_co_note_return_late", new Dictionary<string, string> { { "quarter", t.ReturnQuarter.ToString() }, { "amount", CompanyText.Money(penalty) } }, TabTax, "ret");
                if (TaxRules.LateAudit(t))
                {
                    LateAudit();
                }
            }
        }

        // Two late returns running: business pressure's own tax audit is asked for, at the business that takes most.
        private void LateAudit()
        {
            CompanyShop most = null;
            for (int i = 0; i < _shops.Count; i++)
            {
                most = most == null || _shops[i].Average > most.Average ? _shops[i] : most;
            }
            bool started = most != null && CompanyBridges.Audit != null && CompanyBridges.Audit(most.Reg);
            if (started)
            {
                Tell(NotificationType.Warning, "reallife_co_note_late_audit", new Dictionary<string, string> { { "business", most.Name } }, TabTax, "ret");
            }
            Logger?.Info("Company: " + CompanyCatalogue.LateForAudit + " late returns running: business pressure's own tax audit "
                         + (started ? "has started at " + most.Name + "." : "was asked for and did not start (its own rules said no, or it is switched off).") + " The count starts again.");
        }

        // The tax office goes through the books: what was kept back is owed again with a fine; from a stated sum it goes to court.
        private void Audit(int day)
        {
            TaxFile t = _state.Tax;
            int risk = t.Risk, backTax, fine;
            int owed = TaxRules.Audit(t, _level, day, out backTax, out fine);
            if (owed <= 0)
            {
                Tell(NotificationType.Info, "reallife_co_note_audit_clean", null, TabTax, null);
                Logger?.Info("Company: a tax audit on day " + day + " (risk " + (risk / 100f).ToString("0.00") + "% a day) found nothing kept back.");
                return;
            }
            for (int i = 0; i < _state.Shops.Count; i++)
            {
                _state.Shops[i].Hide = 0;   // the audit has ended it: the player may set it again, and the risk starts from nothing
            }
            bool court = TaxRules.Court(backTax, _level);
            int number = court && CompanyBridges.TaxCase != null ? CompanyBridges.TaxCase(backTax) : 0;
            t.CaseNumber = number > 0 ? number : t.CaseNumber;
            TakeOwed(null);
            Tell(NotificationType.Error, number > 0 ? "reallife_co_note_audit_court" : "reallife_co_note_audit_found", new Dictionary<string, string>
            {
                { "tax", CompanyText.Money(backTax) }, { "fine", CompanyText.Money(fine) }, { "owed", CompanyText.Money(t.Owed) }
            }, TabTax, null);
            Logger?.Info("Company: a tax audit on day " + day + " (risk " + (risk / 100f).ToString("0.00") + "% a day) found " + backTax + " of tax kept back; fine " + fine + " (" + CompanyText.Share(CompanyCatalogue.FineShare[_level])
                         + " at strictness " + _level + "); " + (court ? number > 0 ? "over " + CompanyCatalogue.CourtSum + ": case " + number + " opened in the courts" : "over " + CompanyCatalogue.CourtSum
                                                                                                                                                                  + ", but there is no court to open a case with" : "no court case")
                         + "; still owed " + t.Owed + "; every business's share is back at nothing.");
        }

        // What is owed after an audit is taken as far as the cash goes; the rest is carried to the next day.
        private void TakeOwed(int? day)
        {
            TaxFile t = _state.Tax;
            int owed = t.Owed, take = TaxRules.TakeOwed(t, CompanyGame.Cash);
            if (take > 0 && !Charge(take, CompanyGame.BackTaxKey, null, string.Empty, day, false, "of " + owed + " owed after an audit; " + t.Owed + " carried"))
            {
                t.Owed += take;   // the game refused the charge: it is still owed
            }
        }

        private void Resign()
        {
            TaxFile t = _state.Tax;
            Tell(NotificationType.Warning, "reallife_co_note_accountant_resigned", new Dictionary<string, string> { { "name", CompanyText.Accountant(t.Accountant) } }, TabTax, "acc");
            Logger?.Info("Company: accountant " + t.Accountant + " has resigned: cash is kept off the books.");
            t.Accountant = -1;
        }

        // A share is only ever one the strictness allows, and nothing while the option is off.
        partial void TaxLevel()
        {
            for (int i = 0; _state != null && i < _state.Shops.Count; i++)
            {
                _state.Shops[i].Hide = Settings.EconomyCompanyHide ? TaxRules.HideStep(_state.Shops[i].Hide, _level) : 0;
            }
        }

        // ---------------------------------------------------------------- what the app asks

        /// <summary>The player files the return that is waiting. Refused, with a sentence, while the game's own tax bill is overdue.</summary>
        public void FileReturn()
        {
            if (!On(CompanyPart.Tax) || !_state.Tax.ReturnWaiting)
            {
                return;
            }
            if (!CompanyGame.BooksInOrder())
            {
                Notifications.Show(NotificationType.Warning, "reallife_co_note_return_books", null, 9f, null, null, true, false);
                return;
            }
            FileReturn(false);
            Changed();
        }

        private void FileReturn(bool byAccountant)
        {
            TaxFile t = _state.Tax;
            int quarter = t.ReturnQuarter, takings = t.ReturnTakings, costs = t.ReturnCosts, tier = t.Accountant;
            bool helped = TaxRules.Files(tier, Hidden() > 0);
            bool late = TaxRules.File(t, Save.Day);
            int rebate = helped ? TaxRules.Rebate(tier, costs) : 0;
            if (rebate > 0)
            {
                Pay(rebate, CompanyGame.RebateKey, null, CompanyText.Accountant(tier), null, CompanyText.Share(CompanyCatalogue.AccountantSaving[tier]) + " of the quarter's deductible costs of " + costs);
            }
            int error = byAccountant && TaxRules.Errs(tier, _state.Seed, quarter) ? TaxRules.ErrorPenalty(takings) : 0;
            if (error > 0)
            {
                Charge(error, CompanyGame.ErrorKey, null, string.Empty, null, true, "an error in the return for quarter " + quarter + " by accountant " + tier + ": " + CompanyText.Share(CompanyCatalogue.AccountantErrorShare)
                                                                                  + " of a late penalty");
                Tell(NotificationType.Warning, "reallife_co_note_return_error", new Dictionary<string, string> { { "name", CompanyText.Accountant(tier) }, { "amount", CompanyText.Money(error) } }, TabTax, "ret");
            }
            Tell(late ? NotificationType.Warning : NotificationType.Success, byAccountant ? "reallife_co_note_return_filed_for" : "reallife_co_note_return_filed", new Dictionary<string, string>
            {
                { "quarter", quarter.ToString() }, { "name", CompanyText.Accountant(tier) }, { "rebate", CompanyText.Money(rebate) }
            }, TabTax, "ret");
            Logger?.Info("Company: the return for quarter " + quarter + " was filed " + (byAccountant ? "by accountant " + tier : "by the player") + (late ? ", late" : ", on time") + ": takings " + takings + ", the mod's deductible costs "
                         + costs + ", rebate " + rebate + (error > 0 ? ", a penalty of " + error + " for an error" : string.Empty) + ". Late returns running: " + t.LateRun + ".");
        }

        public void Hire(int tier)
        {
            if (!On(CompanyPart.Tax) || tier < 0 || tier >= CompanyCatalogue.Accountants || _state.Tax.Accountant == tier)
            {
                return;
            }
            bool hiding = Hidden() > 0;
            if (TaxRules.Resigns(tier, hiding))
            {
                Notifications.Show(NotificationType.Warning, "reallife_co_note_accountant_wont", new Dictionary<string, string> { { "name", CompanyText.Accountant(tier) } }, 9f, null, null, true, false);
                return;
            }
            int fee = TaxRules.Fee(tier, _index, hiding);
            if (!Cost(fee, CompanyGame.AccountantKey, null, CompanyText.Accountant(tier), CostKind.Accountant, null, "the first month of accountant " + tier))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            _state.Tax.Accountant = tier;
            _state.Tax.AccountantNext = Save.Day + MonthDays;
            Logger?.Info("Company: accountant " + tier + " hired for " + fee + " a month of " + MonthDays + " day(s); the next fee is taken on day " + _state.Tax.AccountantNext + ".");
            Changed();
        }

        public void Dismiss()
        {
            if (On(CompanyPart.Tax) && _state.Tax.Accountant >= 0)
            {
                Logger?.Info("Company: accountant " + _state.Tax.Accountant + " dismissed by the player.");
                _state.Tax.Accountant = -1;
                Changed();
            }
        }

        /// <summary>Sets the share of a business's takings kept off the books, to a step the strictness allows.</summary>
        public void SetHide(string place, int percent)
        {
            ShopFile file = On(CompanyPart.Tax) && Settings.EconomyCompanyHide ? _state.Shop(place) : null;
            if (file == null)
            {
                return;
            }
            file.Hide = TaxRules.HideStep(percent, _level);
            Logger?.Info("Company: cash off the books at " + ShopName(place) + " set to " + file.Hide + "% of takings (the most at strictness " + _level + " is " + TaxRules.HideMost(_level) + "%).");
            if (_state.Tax.Accountant >= 0 && TaxRules.Resigns(_state.Tax.Accountant, Hidden() > 0))
            {
                Resign();
            }
            Changed();
        }

        partial void TaxAlerts(int day)
        {
            TaxFile t = _state.Tax;
            if (t.ReturnWaiting && t.ReturnDue - day <= 3)
            {
                Alert(4, t.ReturnQuarter, System.Math.Max(day, t.ReturnDue), CompanyText.T("reallife_co_plan_return_title", "quarter", t.ReturnQuarter.ToString()),
                    CompanyText.T(day > t.ReturnDue ? "reallife_co_plan_return_late" : "reallife_co_plan_return_what"), null,
                    CompanyText.T("reallife_co_plan_return_missed", "amount", CompanyText.Money(TaxRules.Penalty(t.ReturnTakings))));
            }
            if (t.Accountant >= 0 && t.AccountantNext == day + 1)
            {
                Alert(8, 0, day + 1, CompanyText.T("reallife_co_plan_fee_title"), CompanyText.T("reallife_co_plan_fee_what", "name", CompanyText.Accountant(t.Accountant)),
                    CompanyText.T("reallife_planner_costs", "amount", CompanyText.Money(TaxRules.Fee(t.Accountant, _index, Hidden() > 0))), CompanyText.T("reallife_co_plan_fee_missed"));
            }
            if (t.Owed > 0)
            {
                Alert(13, 0, day + 1, CompanyText.T("reallife_co_plan_owed_title"), CompanyText.T("reallife_co_plan_owed_what"), CompanyText.T("reallife_planner_costs", "amount", CompanyText.Money(t.Owed)), null);
            }
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using Entities;
using Helpers;
using Streets;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's car finance and mortgages: the approval, the purchase it pays for, a security that goes, and a vehicle taken back.</summary>
    public sealed partial class CreditDesk
    {
        private const string VehicleBought = "ba:transaction_vehiclebought";    // the game's own
        private const string ItemSold = "ba:transaction_itemsold";
        private const string BuildingSold = "ba:transaction_buildingsold";

        private readonly HashSet<string> _owned = new HashSet<string>();
        private int _ownedCount = -1;

        /// <summary>
        /// An approval for a vehicle or a building on finance from the house bank. The game's purchase wants the whole price in
        /// cash, so the approved amount is paid in now; what the lender in fact finances becomes the account, the rest goes back.
        /// </summary>
        public ProductAnswer Approve(ProductKind kind, int amount, int depositPercent, int term)
        {
            return Approve(kind, BankTerms.House, amount, depositPercent, term);
        }

        /// <summary>An approval from one bank, on its terms; the account is held there.</summary>
        public ProductAnswer Approve(ProductKind kind, Lender bank, int amount, int depositPercent, int term)
        {
            if (!ProductCatalogue.Instalment(kind))
            {
                return ProductAnswer.Off;
            }
            ProductOffer offer = Offer(kind, bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                return offer.Answer;
            }
            if (amount < ProductTerms.FinanceLeast(kind))
            {
                return ProductAnswer.TooLittle;
            }
            if (amount > offer.Most)
            {
                return ProductAnswer.TooMuch;
            }
            depositPercent = Mathf.Clamp(depositPercent, ProductTerms.DepositLeast(kind), ProductTerms.DepositMost(kind));
            term = Mathf.Clamp(term, ProductTerms.TermLeast(kind), ProductTerms.TermMost(kind));
            if (_wallet.Cash < ProductTerms.DepositFor(amount, depositPercent))
            {
                return ProductAnswer.NoCash;
            }
            int day = Today;
            Relief r = _file.Relief;
            r.ApprovedKind = (int)kind;
            r.ApprovedAmount = amount;
            r.ApprovedDeposit = depositPercent;
            r.ApprovedTerm = term;
            r.ApprovedRateBp = offer.RateBp;
            r.ApprovedDay = day;
            r.ApprovedStreet = offer.Bank.Street;
            r.ApprovedNumber = offer.Bank.Number;
            RememberOwned();
            _wallet.Give(amount, Money.Advance, FinanceText.T(ProductCatalogue.KindKeys[(int)kind]));
            CreditLedger.Post(CreditKind.Application, amount, day, ProductCatalogue.KindSources[(int)kind]);
            if (Verbose)
            {
                Logger?.Info("Economy check (approval): " + kind + " for up to " + amount + " with " + depositPercent + "% down over " + term + " days at "
                             + (offer.RateBp / 100f).ToString("0.##", CultureInfo.InvariantCulture)
                             + "% from " + offer.Bank.Name + ", paid into cash; the purchase has until the end of day " + (day + ProductCatalogue.ApprovalDays) + "; " + _ownedCount
                                 + " owned now: PASS");
            }
            _director.TermsChanged("an approval");
            return ProductAnswer.Yes;
        }

        /// <summary>The approval ends with nothing bought: the money goes back to the lender, whether or not it is still there, with a charge for the days it was held.</summary>
        public void CancelApproval(bool ranOut)
        {
            Relief r = _file != null ? _file.Relief : null;
            if (r == null || !r.HasApproval)
            {
                return;
            }
            int amount = r.ApprovedAmount, cash = _wallet.Cash;
            string kind = FinanceText.T(ProductCatalogue.KindKeys[r.ApprovedKind]);
            int charge = ReliefRules.CancelCharge(r, Today, AccountRules.DaysPerYear);
            r.ClearApproval();
            _wallet.Force(amount, Money.AdvanceBack, kind);
            _wallet.Force(charge, Money.AdvanceCharge, kind);
            Note(ranOut ? NotificationType.Warning : NotificationType.Info, ranOut ? "reallife_econ_note_approval_over" : "reallife_econ_note_approval_cancelled",
                Data("kind", kind, "amount", FinanceText.Money(amount)));
            if (Verbose)
            {
                Logger?.Info("Economy check (approval ended): " + (ranOut ? "nothing was bought in time" : "cancelled by the borrower") + "; " + amount + " went back to the lender out of " + cash
                             + " in cash: PASS");
            }
        }

        // What the player owns of the approval's kind, so that a purchase is known by what is new.
        private void RememberOwned()
        {
            _owned.Clear();
            _ownedCount = -1;
            Relief r = _file != null ? _file.Relief : null;
            if (r == null || !r.HasApproval || Save == null)
            {
                return;
            }
            if (r.ApprovedKind == (int)ProductKind.Mortgage)
            {
                for (int i = 0; i < Save.realEstate.Count; i++)
                {
                    _owned.Add(Books.Key(Save.realEstate[i].address));
                }
                _ownedCount = Save.realEstate.Count;
                return;
            }
            for (int i = 0; i < Save.VehicleInstances.Count; i++)
            {
                _owned.Add(Save.VehicleInstances[i].id);
            }
            _ownedCount = Save.VehicleInstances.Count;
        }

        // While an approval stands: has the game added a vehicle or a building? One comparison of two counts while it has not.
        private void Purchases(int day)
        {
            Relief r = _file.Relief;
            if (!r.HasApproval || Save == null)
            {
                return;
            }
            if (ReliefRules.ApprovalOver(r, day))
            {
                CancelApproval(true);
                return;
            }
            var kind = (ProductKind)r.ApprovedKind;
            if (kind == ProductKind.Mortgage)
            {
                List<RealEstate> estates = Save.realEstate;
                if (estates.Count == _ownedCount)
                {
                    return;
                }
                for (int i = 0; i < estates.Count; i++)
                {
                    string key = Books.Key(estates[i].address);
                    if (!_owned.Contains(key))
                    {
                        Financed(kind, key, estates[i].address.ToFormattedString(), Mathf.RoundToInt((float)estates[i].purchasePrice), day);
                        return;
                    }
                }
                RememberOwned();
                return;
            }
            List<VehicleInstance> vehicles = Save.VehicleInstances;
            if (vehicles.Count == _ownedCount)
            {
                return;
            }
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleInstance v = vehicles[i];
                VehicleType type = v != null && !_owned.Contains(v.id) && !v.zeroSellingPrice ? v.VehicleType : null;
                if (type == null || !type.IsMotorVehicle || Dealerships.Stock(v))
                {
                    continue;   // a hand truck, a vehicle lent and not the player's to sell, or a dealership's stock (Core/Dealerships)
                }
                Transaction bought = Books.Newest(VehicleBought, "vehicleName", v.vehicleTypeName, r.ApprovedDay);
                int price = bought != null ? Mathf.RoundToInt(-bought.amount) : Mathf.RoundToInt(type.price);
                Financed(kind, v.id, v.vehicleTypeName, price, day);
                return;
            }
            RememberOwned();
        }

        // The purchase is made and paid from cash: the account opens for what the lender finances and the rest goes back. The
        // approval's interest is on the financed part only, up to yesterday, as the account charges its own from today.
        private void Financed(ProductKind kind, string security, string name, int price, int day)
        {
            Relief r = _file.Relief;
            int approved = r.ApprovedAmount, asked = r.ApprovedDeposit;
            int financed = ProductTerms.Financed(price, asked, approved);
            string kindName = FinanceText.T(ProductCatalogue.KindKeys[(int)kind]);
            CreditAccount account = financed > 0 ? AccountRules.Finance(kind, financed, r.ApprovedTerm, r.ApprovedRateBp, day, security, name, price) : null;
            if (account != null)
            {
                account.Street = r.ApprovedStreet;
                account.Number = r.ApprovedNumber;
            }
            int held = day - r.ApprovedDay + 1, charge = ReliefRules.PurchaseCharge(r, financed, day, AccountRules.DaysPerYear);
            r.ClearApproval();
            _owned.Clear();
            _ownedCount = -1;
            _wallet.Force(approved - financed, Money.AdvanceBack, kindName);
            _wallet.Force(charge, Money.AdvanceCharge, kindName);
            if (account == null)
            {
                return;
            }
            Opened(account);
            CreditLedger.Post(CreditKind.NewCredit, financed, day, account.Key);
            Note(NotificationType.Success, "reallife_econ_note_financed", Data("thing", Thing(account), "amount", FinanceText.Money(financed), "back", FinanceText.Money(approved - financed),
                "payment", FinanceText.Money(AccountRules.DueTomorrow(account, day))));
            if (Verbose)
            {
                Logger?.Info("Economy check (purchase on finance): '" + name + "' (" + security + ") bought for " + price + ": " + financed + " financed of " + approved + " approved, "
                             + (approved - financed)
                             + " returned, the buyer's own money " + (price - financed) + " (" + asked + "% asked), " + charge + " of interest on the financed part for the "
                             + (held - 1) + " day(s) before today: " + (financed <= approved && price - financed >= 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a purchase on finance");
        }

        // A financed vehicle or building that is gone: the sale goes to the lender as far as cash allows, the rest stays owed unsecured.
        private void Securities(int day)
        {
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || !a.Secured || SecurityThere(a))
                {
                    continue;
                }
                bool home = a.Kind == ProductKind.Mortgage;
                Transaction sale = home ? Books.Newest(BuildingSold, "address", a.SecurityName, day - 1) : Books.Newest(ItemSold, "itemSoldInfo", a.SecurityName, day - 1);
                int raised = sale != null ? Mathf.Max(0, Mathf.FloorToInt(sale.amount)) : 0;
                string name = Name(a), thing = Thing(a);
                int got = raised > 0 && !a.Test ? _wallet.TakeUpTo(Mathf.Min(raised, a.Owed), Money.Payment, name) : 0;
                bool pending = a.Ladder.RepoPending;
                AccountRules.Unsecure(a);
                AccountRules.Pay(a, got);
                if (a.Owed == 0)
                {
                    a.Closed = true;   // nothing was owed any more: a vehicle that was to go back after a bankruptcy
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                    if (got > 0)
                    {
                        CreditLedger.Post(CreditKind.PaidOffEarly, got, day, a.Key, 1, a.Test);
                    }
                }
                CreditLedger.Touch();
                if (!a.Closed || got > 0)
                {
                    Note(a.Closed ? NotificationType.Info : NotificationType.Warning, a.Closed ? "reallife_econ_note_sold_settled" : "reallife_econ_note_sold_owed",
                        Data("thing", thing, "amount", FinanceText.Money(got), "left", FinanceText.Money(a.Owed)));
                }
                if (Verbose)
                {
                    Logger?.Info("Economy check (security gone, day " + day + "): '" + a.SecurityName + "' of " + a.Kind + " #" + a.Id + " is no longer the borrower's ("
                                 + (sale != null ? "a sale for " + raised + " is in the game's transactions" : "no sale seen") + "): " + got + " taken, " + a.Owed + " stays owed unsecured"
                                 + (pending ? "; it was due to be taken back" : string.Empty) + ": PASS");
                }
            }
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Security.Length > 0 && Books.Business(m.Security) == null)
                {
                    if (Verbose)
                    {
                        Logger?.Info("Economy check (security gone, day " + day + "): the business at " + m.Security + " that secured the loan of " + m.Total.ToString("0")
                                     + " is no longer run by the borrower: the loan stays, unsecured: PASS");
                    }
                    m.Security = string.Empty;
                    m.Ladder.RepoDay = 0;
                }
            }
        }

        // A vehicle due back is taken with the game's own call once out of the player's sight, and sold against the balance.
        private void TakeVehicles(int day)
        {
            bool waiting = false;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || !a.Ladder.RepoPending)
                {
                    continue;
                }
                VehicleInstance vehicle = Books.Vehicle(a.Security);
                if (vehicle == null)
                {
                    continue;   // gone already: the hour's look at the securities deals with it
                }
                if (!OutOfSight(vehicle, out VehicleController controller))
                {
                    waiting = true;
                    continue;
                }
                string name = Name(a), thing = Thing(a);
                int owed = a.Owed, surplus;
                OverlayGuard.Release(controller);
                vehicle.Delete(controller);   // the game's own way of removing a vehicle of the player's
                int used = AccountRules.Repossess(a, day, out surplus);
                if (a.SecurityValue <= 0 || a.Test)
                {
                    surplus = 0;   // taken back in a bankruptcy: what it fetches is the creditors' (and a test pays nothing out)
                }
                _wallet.Give(surplus, Money.Surplus, name);
                if (a.Owed == 0)
                {
                    a.Closed = true;
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                }
                CreditLedger.Post(CreditKind.Repossessed, used + surplus, day, a.Key, 1, a.Test);
                CreditLedger.Touch();
                Note(NotificationType.Error, a.Owed > 0 ? "reallife_econ_note_repossessed_owed" : "reallife_econ_note_repossessed",
                    Data("thing", thing, "amount", FinanceText.Money(used + surplus), "left", FinanceText.Money(a.Owed), "days", ProductCatalogue.UnsecuredClaimDays.ToString()), null, 18f);
                if (Verbose)
                {
                    Logger?.Info("Economy check (repossession, day " + day + "): '" + a.SecurityName + "' (" + a.Security + ") taken out of the player's sight with the game's own call; bought for "
                                 + a.SecurityValue
                                 + ", sold for " + (used + surplus) + ": " + owed + " owed before, " + a.Owed + " after" + (surplus > 0 ? ", " + surplus + " paid to the borrower" : string.Empty)
                                 + ": " + (Books.Vehicle(a.Security) == null && a.Owed == owed - used ? "PASS" : "FAIL"));
                }
            }
            _repoWaiting = waiting;
        }

        private static bool OutOfSight(VehicleInstance vehicle, out VehicleController controller)
        {
            controller = null;
            if (Save == null || Save.ActiveVehicleId == vehicle.id)
            {
                return false;
            }
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].vehicleInstance != null && all[i].vehicleInstance.id == vehicle.id)
                {
                    controller = all[i];
                    break;
                }
            }
            if (controller == null)
            {
                return true;   // not in the scene the player is in
            }
            if (controller.controlledByPlayer)
            {
                return false;
            }
            Vector3 at = controller.transform.position;
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null && (player.transform.position - at).sqrMagnitude < RepossessionKeepAway * RepossessionKeepAway)
            {
                return false;
            }
            return EconomyBridges.InView != null && !EconomyBridges.InView(at);
        }
    }
}

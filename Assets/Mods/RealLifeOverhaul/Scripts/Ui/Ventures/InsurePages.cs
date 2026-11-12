using System;
using System.Collections.Generic;
using RealLifeOverhaul.Insurance;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Insurance pages do in the game: each is the director's own call, or the call of the feature whose cover it is, so a page can only reach what they allow.</summary>
    public interface IInsureActions
    {
        void BuyCar(string id, int tier);
        void ChangeCar(string id, int tier);
        void CancelCar(string id);

        /// <summary>Takes or changes the health plan (1 to 3) by Health's own call.</summary>
        void TakeHealth(int plan);
        void CancelHealth();
        void BuyHome(string place, int tier);
        void ChangeHome(string place, int tier);
        void CancelHome(string place);

        /// <summary>Buys a kind of cover at a business from an insurer by the Company's own call.</summary>
        void BuyBusiness(string place, int kind, int insurer);
        void CancelBusiness(string place, int kind);

        /// <summary>Puts a business claim in by the Company's own call.</summary>
        void FileClaim(int id);
    }

    /// <summary>
    /// The Insurance pages: the cars, the health plan, the homes, the businesses and the claims. Each opens on what is held and what it costs, with the terms of the
    /// picked cover beside it, and ends on a sentence of how it works and where the Guide explains it. A page shows only what the rules allow, and every number comes
    /// from <see cref="InsuranceFacts"/>, which the director fills from the state and the game. Game-free.
    /// </summary>
    public static class InsurePages
    {
        public const int TabCars = 0, TabHealth = 1, TabHomes = 2, TabBusiness = 3, TabClaims = 4, Tabs = 5;
        public const string CarKey = "c:", PlanKey = "h:", HomeKey = "m:", BizKey = "b:", ClaimKey = "k:";

        private static readonly UiIcon[] LineIcons = { UiIcon.Car, UiIcon.Heartbeat, UiIcon.Home, UiIcon.Store };
        private static readonly string[] LineEnglish = { "Car cover", "Health cover", "Home cover", "Business cover" };
        private static readonly string[] LineKeys = { "reallife_ins_line_car", "reallife_ins_line_health", "reallife_ins_line_home", "reallife_ins_line_business" };
        private static readonly string[] CarTierEnglish = { "Third party", "Standard", "Full" };
        private static readonly string[] HomeTierEnglish = { "Basic", "Standard", "Complete" };
        private static readonly string[] ClaimEnglish = { "Car repair", "Damages to another car", "Tow", "Repair at home", "Repair to your things" };

        public static void Fill(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w)
        {
            m.Title = w.T("reallife_ins_app", "Insurance");
            m.Icon = UiIcon.ShieldCheck;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            int open = 0;
            for (int i = 0; i < f.Claims.Count; i++)
            {
                open += f.Claims[i].Stage == 1 ? 1 : 0;
            }
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Car, Label = w.T("reallife_ins_tab_cars", "Cars") },
                new UiNavEntry { Icon = UiIcon.Heartbeat, Label = w.T("reallife_ins_tab_health", "Health") },
                new UiNavEntry { Icon = UiIcon.Home, Label = w.T("reallife_ins_tab_homes", "Homes") },
                new UiNavEntry { Icon = UiIcon.Store, Label = w.T("reallife_ins_tab_business", "Businesses") },
                new UiNavEntry { Icon = UiIcon.Receipt, Label = w.T("reallife_ins_tab_claims", "Claims"), Count = open > 0 ? open.ToString() : null }
            };
            m.Tab = Math.Max(0, Math.Min(Tabs - 1, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = w.T("reallife_ins_off_title", "Insurance is not running");
                m.OffBody = w.T("reallife_ins_off_body", "Load a save with Insurance switched on to insure your cars, your health, your homes and your businesses.");
                m.Runs = runs.ToArray();
                return;
            }
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ins_fig_premiums", "Premiums a month"), Value = w.Money(f.PremiumsMonth) },
                new UiFigureModel { Label = w.T("reallife_ins_fig_in_force", "Covers in force"), Value = f.InForce.ToString() },
                new UiFigureModel { Label = w.T("reallife_ins_fig_paid", "Paid by insurers this year"), Value = w.Money(f.PaidYear) }
            };
            switch (m.Tab)
            {
                case TabHealth: Health(m, v, f, act, w, runs); break;
                case TabHomes: Homes(m, v, f, act, w, runs); break;
                case TabBusiness: Business(m, v, f, act, w, runs); break;
                case TabClaims: Claims(m, v, f, act, w, runs); break;
                default: Cars(m, v, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        // The tier a page shows for the picked item: the item's own until the player presses the tier's row, and its own again when another item is picked.
        private static int Chosen(VentView v, string key, int own)
        {
            if (v.S1 != key)
            {
                v.S1 = key;
                v.A = own;
            }
            return InsuranceCatalogue.Tier(v.A);
        }

        private static string Effect(int thousandths)
        {
            int delta = thousandths - 1000;
            return (delta > 0 ? "+" : string.Empty) + VentWords.Share(delta * 10);
        }

        private static string Terms(VentWords w, int share, long excess)
        {
            if (share <= 0)
            {
                return w.T("reallife_ins_not_covered", "Not covered");
            }
            return excess > 0 ? w.F("reallife_ins_terms", "{share} over the first {excess}", "share", VentWords.Share(share * 10), "excess", w.Money(excess)) : VentWords.Share(share * 10);
        }

        private static VentBlock Guide(VentWords w) => VentBlock.Note(w.T("reallife_ins_guide", "Guide: Insurance"));

        // ---- the cars

        private static void Cars(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(8);
            var right = new List<VentBlock>(16);
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_cars", "Your cars")));
            int picked = -1;
            for (int i = 0; i < f.Cars.Count; i++)
            {
                picked = v.Pick == CarKey + f.Cars[i].Id ? i : picked;
            }
            picked = picked < 0 && f.Cars.Count > 0 ? 0 : picked;
            if (!f.CarsOn)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_cars_off", "Car ownership is switched off, so there are no car policies."), UiRole.Warn));
            }
            else if (f.Cars.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_cars_none", "You own no car to insure.")));
            }
            else
            {
                var items = new VentItem[f.Cars.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    CarCoverFact c = f.Cars[i];
                    string key = CarKey + c.Id;
                    items[i] = new VentItem
                    {
                        Icon = UiIcon.Car, Title = c.Name,
                        Sub = c.Insured ? w.F("reallife_ins_car_sub", "{tier}, {days} days left", "tier", w.T(InsuranceCatalogue.CarTierKeys[c.Tier], CarTierEnglish[c.Tier]), "days",
                            c.DaysLeft.ToString())
                            : w.T("reallife_ins_sub_none", "Not insured"),
                        Chip = Chip(w, c.Insured, c.WaitUntil, c.GraceUntil), ChipTone = c.Insured && c.WaitUntil == 0 && c.GraceUntil == 0 ? UiRole.Ok : UiRole.Warn,
                        Amount = w.F("reallife_ins_month", "{amount} a month", "amount", w.Money(c.Premium[c.Tier])), Run = runs.Add(() => { v.Pick = key; return true; })
                    };
                }
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_record", "Your record as a driver")));
            left.Add(VentBlock.Row(w.T("reallife_ins_row_grade", "Credit grade"), Effect(InsuranceCatalogue.CreditShare[InsuranceCatalogue.Clamp(f.Grade, 0,
                InsuranceCatalogue.CreditShare.Length - 1)])));
            left.Add(VentBlock.Row(w.F("reallife_ins_row_points", "Points on your licence: {n}", "n", f.Points.ToString()),
                Effect(1000 + InsuranceCatalogue.PointShare * InsuranceCatalogue.Clamp(f.Points, 0, InsuranceCatalogue.PointsMost))));
            left.Add(VentBlock.Row(w.F("reallife_ins_row_crashes", "Crashes that count: {n}", "n", f.Crashes.ToString()),
                Effect(1000 + InsuranceCatalogue.CrashShare * InsuranceCatalogue.Clamp(f.Crashes, 0, InsuranceCatalogue.CrashesMost))));
            if (picked >= 0)
            {
                CarCoverFact c = f.Cars[picked];
                string id = c.Id;
                int tier = Chosen(v, CarKey + id, c.Tier);
                right.Add(VentBlock.Section(c.Name));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_status", "Status"), Status(w, c.Insured, c.DaysLeft, c.WaitUntil, c.GraceUntil),
                    c.Insured && c.GraceUntil == 0 ? UiRole.Text : UiRole.Warn));
                right.Add(VentBlock.Choice(w.T("reallife_ins_row_tier", "Tier"), w.T(InsuranceCatalogue.CarTierKeys[tier], CarTierEnglish[tier]),
                    runs.Add(() => { v.A = (tier + 1) % InsuranceCatalogue.Tiers; return true; })));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_premium", "A month at this tier"), w.Money(c.Premium[tier])));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_repair", "Your own repairs"),
                    Terms(w, InsuranceCatalogue.CarRepairShare[tier], InsuranceRules.At(InsuranceCatalogue.CarRepairExcess[tier], f.Level))));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_liability", "Damages to another car"),
                    Terms(w, InsuranceCatalogue.CarLiabilityShare[tier], InsuranceRules.At(InsuranceCatalogue.CarLiabilityExcess[tier], f.Level))));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_tow", "A tow"), Terms(w, InsuranceCatalogue.CarTowShare[tier], 0)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_cap", "Pays at most in a year"), w.F("reallife_ins_cap_value", "{amount} in {n} claims", "amount",
                    w.Money(InsuranceRules.At(InsuranceCatalogue.CarYearCap[tier], f.Level)), "n", InsuranceCatalogue.CarYearClaims[tier].ToString())));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_noclaim", "No-claims discount"), VentWords.Share(c.NoClaim * 10)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_paid_year", "Paid this year"), w.F("reallife_ins_cap_value", "{amount} in {n} claims", "amount", w.Money(c.PaidYear), "n",
                    c.ClaimsYear.ToString())));
                var actions = new List<UiActionModel>(2);
                if (!c.Insured)
                {
                    actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_insure", "Insure at this tier"), Kind = UiButtonKind.Primary, Icon = UiIcon.ShieldCheck, Key = UiAction.Alt,
                        Disabled = c.Premium[tier] > f.Cash, Arg = runs.Add(() => { act.BuyCar(id, tier); return true; }) });
                }
                else
                {
                    actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_cancel", "Cancel cover"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                        Arg = runs.Add(() => { act.CancelCar(id); return true; }) });
                    if (tier != c.Tier)
                    {
                        bool up = tier > c.Tier;
                        if (up && !c.Sound)
                        {
                            right.Add(VentBlock.Note(w.T("reallife_ins_car_unsound", "Repair the car first. A damaged or broken down car cannot move to a higher tier."), UiRole.Warn));
                        }
                        actions.Add(new UiActionModel { Label = ChangeLabel(w, c.Change[tier]), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                            Disabled = (up && !c.Sound) || c.Change[tier] > f.Cash, Arg = runs.Add(() => { act.ChangeCar(id, tier); return true; }) });
                    }
                }
                right.Add(VentBlock.Buttons(actions.ToArray()));
            }
            right.Add(VentBlock.Note(w.F("reallife_ins_car_foot",
                "A new policy pays nothing in its first {days} days, and never for damage the car already had. A missed premium is asked for again each morning for {grace} days, then the "
                    + "cover lapses. A claim ends the no-claims discount.",
                "days", InsuranceCatalogue.CarWaitDays.ToString(), "grace", InsuranceCatalogue.GraceDays.ToString())));
            right.Add(Guide(w));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static string Chip(VentWords w, bool on, int waitUntil, int graceUntil)
        {
            return !on ? null : graceUntil > 0 ? w.T("reallife_ins_chip_grace", "Premium missed") : waitUntil > 0 ? w.T("reallife_ins_chip_wait", "First days") : w.T("reallife_ins_chip_on",
                "In force");
        }

        private static string Status(VentWords w, bool on, int daysLeft, int waitUntil, int graceUntil)
        {
            if (!on)
            {
                return w.T("reallife_ins_sub_none", "Not insured");
            }
            if (graceUntil > 0)
            {
                return w.F("reallife_ins_status_grace", "Premium missed. Asked again until day {day}", "day", graceUntil.ToString());
            }
            if (waitUntil > 0)
            {
                return w.F("reallife_ins_status_wait", "In force. Pays from day {day}", "day", (waitUntil + 1).ToString());
            }
            return daysLeft > 0 ? w.F("reallife_ins_status_on", "In force, {days} days of the month left", "days", daysLeft.ToString()) : w.T("reallife_ins_chip_on", "In force");
        }

        private static string ChangeLabel(VentWords w, int cost)
        {
            return cost > 0 ? w.F("reallife_ins_btn_change_cost", "Change to this tier for {amount}", "amount", w.Money(cost)) : w.T("reallife_ins_btn_change", "Change to this tier");
        }

        // ---- the health plan

        private static void Health(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w, VentRuns runs)
        {
            HealthCoverFact h = f.Health;
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(12);
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_plans", "Health plans")));
            if (!h.On)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_health_off", "Health is switched off, so there are no health plans."), UiRole.Warn));
                right.Add(Guide(w));
                m.Left = left.ToArray();
                m.Right = right.ToArray();
                return;
            }
            int picked = h.Plan > 0 ? h.Plan - 1 : 1;
            for (int i = 0; i < InsuranceCatalogue.Tiers; i++)
            {
                picked = v.Pick == PlanKey + i ? i : picked;
            }
            var items = new VentItem[InsuranceCatalogue.Tiers];
            for (int i = 0; i < items.Length; i++)
            {
                string key = PlanKey + i;
                items[i] = new VentItem
                {
                    Icon = UiIcon.Heartbeat, Title = h.Names[i],
                    Sub = w.F("reallife_ins_plan_sub", "Hospital {hospital}, doctor {doctor}", "hospital", VentWords.Share(h.HospitalShare[i] * 10), "doctor", VentWords.Share(h.DoctorShare[i] * 10)),
                    Chip = h.Plan == i + 1 ? w.T("reallife_ins_plan_yours", "Yours") : null, ChipTone = UiRole.Ok,
                    Amount = w.F("reallife_ins_month", "{amount} a month", "amount", w.Money(h.Premium[i])), Run = runs.Add(() => { v.Pick = key; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            if (h.Employer > 0 && h.Employer <= InsuranceCatalogue.Tiers)
            {
                left.Add(VentBlock.Note(w.F("reallife_ins_employer", "Your employer gives you the {plan} plan while you work there.", "plan", h.Names[h.Employer - 1]), UiRole.Ok));
            }
            int k = picked;
            bool yours = h.Plan == k + 1;
            right.Add(VentBlock.Section(h.Names[k]));
            right.Add(VentBlock.Row(w.T("reallife_ins_row_h_premium", "A month, at your age and record"), w.Money(h.Premium[k])));
            right.Add(VentBlock.Row(w.T("reallife_ins_row_h_hospital", "Of a hospital stay"), VentWords.Share(h.HospitalShare[k] * 10)));
            right.Add(VentBlock.Row(w.T("reallife_ins_row_h_excess", "You pay first, of each stay"), w.Money(h.Excess[k])));
            right.Add(VentBlock.Row(w.T("reallife_ins_row_h_doctor", "Of the doctor and a prescription"), VentWords.Share(h.DoctorShare[k] * 10)));
            right.Add(VentBlock.Row(w.T("reallife_ins_row_cap", "Pays at most in a year"), w.Money(h.YearCap[k])));
            if (yours)
            {
                right.Add(VentBlock.Row(w.T("reallife_ins_row_next", "Next premium"), w.F("reallife_ins_day_value", "Day {day}", "day", h.NextDue.ToString()),
                    h.Missed > 0 ? UiRole.Warn : UiRole.Text));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_paid_year", "Paid this year"), w.Money(h.PaidYear)));
                if (h.WaitUntil > 0)
                {
                    right.Add(VentBlock.Note(w.F("reallife_ins_status_wait", "In force. Pays from day {day}", "day", (h.WaitUntil + 1).ToString()), UiRole.Warn));
                }
            }
            var actions = new List<UiActionModel>(2);
            if (h.Plan > 0)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_cancel_plan", "Cancel your plan"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.CancelHealth(); return true; }) });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_take_plan", "Take this plan"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Disabled = yours || h.Premium[k] > f.Cash, Arg = runs.Add(() => { act.TakeHealth(k + 1); return true; }) });
            right.Add(VentBlock.Buttons(actions.ToArray()));
            right.Add(VentBlock.Note(w.T("reallife_ins_health_foot",
                "Health plans follow Health's rules: nothing that began before the plan or in its first days is covered. The Health app shows your bills and care.")));
            right.Add(Guide(w));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the homes

        private static void Homes(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(16);
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_homes", "Your homes")));
            int picked = -1;
            for (int i = 0; i < f.Homes.Count; i++)
            {
                picked = v.Pick == HomeKey + f.Homes[i].Place ? i : picked;
            }
            picked = picked < 0 && f.Homes.Count > 0 ? 0 : picked;
            if (f.Homes.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_homes_none", "You have no home to insure.")));
            }
            else
            {
                var items = new VentItem[f.Homes.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    HomeCoverFact h = f.Homes[i];
                    string key = HomeKey + h.Place;
                    items[i] = new VentItem
                    {
                        Icon = UiIcon.Home, Title = h.Name,
                        Sub = h.Active ? w.F("reallife_ins_home_sub", "{tier}, next premium day {day}", "tier", w.T(InsuranceCatalogue.HomeTierKeys[h.Tier], HomeTierEnglish[h.Tier]), "day",
                            h.NextDue.ToString())
                            : w.T("reallife_ins_sub_none", "Not insured"),
                        Chip = Chip(w, h.Active, h.WaitUntil, h.GraceUntil), ChipTone = h.Active && h.WaitUntil == 0 && h.GraceUntil == 0 ? UiRole.Ok : UiRole.Warn,
                        Amount = w.F("reallife_ins_month", "{amount} a month", "amount", w.Money(h.Premium[h.Tier])), Run = runs.Add(() => { v.Pick = key; return true; })
                    };
                }
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            if (picked >= 0)
            {
                HomeCoverFact h = f.Homes[picked];
                string place = h.Place;
                int tier = Chosen(v, HomeKey + place, h.Tier);
                right.Add(VentBlock.Section(h.Name));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_status", "Status"), Status(w, h.Active, 0, h.WaitUntil, h.GraceUntil), h.Active && h.GraceUntil == 0 ? UiRole.Text : UiRole.Warn));
                right.Add(VentBlock.Choice(w.T("reallife_ins_row_tier", "Tier"), w.T(InsuranceCatalogue.HomeTierKeys[tier], HomeTierEnglish[tier]),
                    runs.Add(() => { v.A = (tier + 1) % InsuranceCatalogue.Tiers; return true; })));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_premium", "A month at this tier"), w.Money(h.Premium[tier])));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_home_repair", "Repairs at home"),
                    Terms(w, InsuranceCatalogue.HomeRepairShare[tier], InsuranceRules.At(InsuranceCatalogue.HomeExcess[tier], f.Level))));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_belongings", "Repairs to your things"),
                    Terms(w, InsuranceCatalogue.BelongingsShare[tier], InsuranceRules.At(InsuranceCatalogue.HomeExcess[tier], f.Level))));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_cap", "Pays at most in a year"), w.F("reallife_ins_cap_value", "{amount} in {n} claims", "amount",
                    w.Money(InsuranceRules.At(InsuranceCatalogue.HomeYearCap[tier], f.Level)), "n", InsuranceCatalogue.HomeYearClaims[tier].ToString())));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_district", "Your district"), Effect(h.District)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_loading", "Recent claims"), Effect(1000 + h.Loading)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_noclaim", "No-claims discount"), VentWords.Share(h.NoClaim * 10)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_paid_year", "Paid this year"), w.F("reallife_ins_cap_value", "{amount} in {n} claims", "amount", w.Money(h.PaidYear), "n",
                    h.ClaimsYear.ToString())));
                var actions = new List<UiActionModel>(2);
                if (!h.Active)
                {
                    actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_insure", "Insure at this tier"), Kind = UiButtonKind.Primary, Icon = UiIcon.ShieldCheck, Key = UiAction.Alt,
                        Disabled = h.Premium[tier] > f.Cash, Arg = runs.Add(() => { act.BuyHome(place, tier); return true; }) });
                }
                else
                {
                    actions.Add(new UiActionModel { Label = w.T("reallife_ins_btn_cancel", "Cancel cover"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                        Arg = runs.Add(() => { act.CancelHome(place); return true; }) });
                    if (tier != h.Tier)
                    {
                        actions.Add(new UiActionModel { Label = ChangeLabel(w, h.Change[tier]), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                            Disabled = h.Change[tier] > f.Cash, Arg = runs.Add(() => { act.ChangeHome(place, tier); return true; }) });
                    }
                }
                right.Add(VentBlock.Buttons(actions.ToArray()));
            }
            right.Add(VentBlock.Note(w.F("reallife_ins_home_foot",
                "A repair at home is claimed on the home it happened at, so each home needs its own policy. A new policy pays nothing in its first {days} days. A missed premium is asked "
                    + "for again each morning for {grace} days, then the cover lapses.",
                "days", InsuranceCatalogue.HomeWaitDays.ToString(), "grace", InsuranceCatalogue.GraceDays.ToString())));
            right.Add(Guide(w));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the businesses

        private static void Business(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(4);
            var right = new List<VentBlock>(14);
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_business", "Your businesses")));
            int picked = -1;
            for (int i = 0; i < f.Business.Count; i++)
            {
                picked = v.Pick == BizKey + f.Business[i].Place + ":" + f.Business[i].Kind ? i : picked;
            }
            picked = picked < 0 && f.Business.Count > 0 ? 0 : picked;
            if (!f.BusinessOn)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_business_off", "The Company's business cover is switched off."), UiRole.Warn));
            }
            else if (f.Business.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_business_none", "You have no business to insure.")));
            }
            else
            {
                var items = new VentItem[f.Business.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    BizCoverFact b = f.Business[i];
                    string key = BizKey + b.Place + ":" + b.Kind;
                    items[i] = new VentItem
                    {
                        Icon = UiIcon.Store, Title = b.Name,
                        Sub = b.Active ? w.F("reallife_ins_biz_sub", "{cover}: {insurer}", "cover", b.KindName, "insurer", f.Insurers[InsuranceCatalogue.Tier(b.Insurer)])
                            : w.F("reallife_ins_biz_sub_none", "{cover}: not covered", "cover", b.KindName),
                        Chip = b.Active ? w.T("reallife_ins_chip_on", "In force") : null, ChipTone = UiRole.Ok,
                        Amount = b.Active ? w.F("reallife_ins_day", "{amount} a day", "amount", w.Money(b.Premium)) : string.Empty, Run = runs.Add(() => { v.Pick = key; return true; })
                    };
                }
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            if (f.BusinessOn && picked >= 0)
            {
                BizCoverFact b = f.Business[picked];
                string place = b.Place;
                int kind = b.Kind;
                string pickKey = BizKey + place + ":" + kind;
                if (v.S2 != pickKey)
                {
                    v.S2 = pickKey;
                    v.B = InsuranceCatalogue.StandardTier;
                }
                int insurer = b.Active ? InsuranceCatalogue.Tier(b.Insurer) : InsuranceCatalogue.Tier(v.B);
                right.Add(VentBlock.Section(w.F("reallife_ins_biz_title", "{cover} at {business}", "cover", b.KindName, "business", b.Name)));
                if (b.Active)
                {
                    right.Add(VentBlock.Row(w.T("reallife_ins_row_insurer", "Insurer"), f.Insurers[insurer]));
                    right.Add(VentBlock.Row(w.T("reallife_ins_row_b_premium", "Premium"), w.F("reallife_ins_day", "{amount} a day", "amount", w.Money(b.Premium))));
                    right.Add(VentBlock.Row(w.T("reallife_ins_row_b_term", "Term ends"), w.F("reallife_ins_day_value", "Day {day}", "day", b.Until.ToString())));
                    right.Add(VentBlock.Row(w.T("reallife_ins_row_b_claims", "Claims this term"), b.Claims.ToString()));
                }
                else
                {
                    right.Add(VentBlock.Choice(w.T("reallife_ins_row_insurer", "Insurer"), f.Insurers[insurer], runs.Add(() => { v.B = (insurer + 1) % InsuranceCatalogue.Tiers; return true; })));
                    right.Add(VentBlock.Row(w.T("reallife_ins_row_b_premium", "Premium"), w.F("reallife_ins_day", "{amount} a day", "amount", w.Money(b.Quote[insurer]))));
                }
                right.Add(VentBlock.Row(w.T("reallife_ins_row_b_excess", "You bear of a loss"), w.F("reallife_ins_b_excess_value", "{share}, {least} at least", "share",
                    VentWords.Share(b.ExcessShare[insurer] * 10), "least", w.Money(b.ExcessLeast[insurer]))));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_b_most", "One claim counts up to"), w.Money(b.Most)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_b_pays", "Paid after"), w.F("reallife_ins_days_value", "{n} days", "n", b.PayDays[insurer].ToString())));
                if (b.Active)
                {
                    right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_ins_btn_cancel", "Cancel cover"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                        Arg = runs.Add(() => { act.CancelBusiness(place, kind); return true; }) }));
                }
                else
                {
                    if (!b.Writes[insurer])
                    {
                        right.Add(VentBlock.Note(w.T("reallife_ins_biz_refuses", "This insurer will not write this cover here."), UiRole.Warn));
                    }
                    right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_ins_btn_buy_cover", "Buy this cover"), Kind = UiButtonKind.Primary, Icon = UiIcon.ShieldCheck,
                        Key = UiAction.Alt,
                        Disabled = !b.Writes[insurer], Arg = runs.Add(() => { act.BuyBusiness(place, kind, insurer); return true; }) }));
                }
            }
            right.Add(VentBlock.Note(w.T("reallife_ins_biz_foot", "Business cover follows the Company's rules: a claim must be put in, here or by a manager, and is paid some days later.")));
            right.Add(Guide(w));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        // ---- the claims

        private static void Claims(VentModel m, VentView v, InsuranceFacts f, IInsureActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(3);
            var right = new List<VentBlock>(10);
            left.Add(VentBlock.Section(w.T("reallife_ins_sec_claims", "Claims")));
            int picked = -1;
            for (int i = 0; i < f.Claims.Count; i++)
            {
                picked = v.Pick == ClaimKey + i ? i : picked;
            }
            if (f.Claims.Count == 0)
            {
                left.Add(VentBlock.Note(w.T("reallife_ins_claims_none", "No claim has been made yet.")));
            }
            else
            {
                var items = new VentItem[f.Claims.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    ClaimFact c = f.Claims[i];
                    string key = ClaimKey + i;
                    items[i] = new VentItem
                    {
                        Icon = LineIcons[InsuranceCatalogue.Clamp(c.Line, 0, LineIcons.Length - 1)], Title = ClaimTitle(w, c),
                        Sub = w.F("reallife_ins_claim_sub", "Day {day}. Bill {bill}, your part {part}", "day", c.Day.ToString(), "bill", w.Money(c.Loss), "part", w.Money(c.Loss - c.Payout)),
                        Chip = c.Stage == 1 ? w.F("reallife_ins_chip_put_in", "Put in by day {day}", "day", c.StageDay.ToString())
                            : c.Stage == 2 ? w.F("reallife_ins_chip_filed", "Paid on day {day}", "day", c.StageDay.ToString())
                            : c.Stage == 3 ? w.T("reallife_ins_chip_lapsed", "Not put in") : w.T("reallife_ins_chip_paid", "Paid"),
                        ChipTone = c.Stage == 0 ? UiRole.Ok : c.Stage == 2 ? UiRole.None : UiRole.Warn,
                        Amount = w.Money(c.Payout), Run = runs.Add(() => { v.Pick = key; return true; })
                    };
                }
                left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            }
            if (picked >= 0)
            {
                ClaimFact c = f.Claims[picked];
                int id = c.Id;
                right.Add(VentBlock.Section(ClaimTitle(w, c)));
                right.Add(VentBlock.Row(w.T(LineKeys[InsuranceCatalogue.Clamp(c.Line, 0, LineKeys.Length - 1)], LineEnglish[InsuranceCatalogue.Clamp(c.Line, 0, LineEnglish.Length - 1)]),
                    w.F("reallife_ins_day_value", "Day {day}", "day", c.Day.ToString())));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_c_loss", "The bill"), w.Money(c.Loss)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_c_part", "Your part"), w.Money(c.Loss - c.Payout)));
                right.Add(VentBlock.Row(w.T("reallife_ins_row_c_pays", "The insurer pays"), w.Money(c.Payout), UiRole.Ok));
                if (c.Stage == 1 && id >= 0)
                {
                    right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_ins_btn_file", "Put in the claim"), Kind = UiButtonKind.Primary, Icon = UiIcon.Send, Key = UiAction.Alt,
                        Arg = runs.Add(() => { act.FileClaim(id); return true; }) }));
                }
            }
            right.Add(VentBlock.Section(w.T("reallife_ins_sec_how", "How claims work")));
            right.Add(VentBlock.Note(w.T("reallife_ins_claims_how",
                "When a covered bill comes, you pay it less what your cover pays. A cover never pays more than the bill, and each claim counts against the next premiums for a time.")));
            right.Add(Guide(w));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static string ClaimTitle(VentWords w, ClaimFact c)
        {
            int kind = InsuranceCatalogue.Clamp(c.Kind, 0, ClaimEnglish.Length - 1);
            string name = c.KindName ?? w.T(InsuranceCatalogue.ClaimKeys[kind], ClaimEnglish[kind]);
            return w.F("reallife_ins_claim_title", "{kind}: {what}", "kind", name, "what", c.What ?? string.Empty);
        }
    }
}

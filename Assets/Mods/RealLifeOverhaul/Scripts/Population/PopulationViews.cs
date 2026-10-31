using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>The screens of the population's view in the Economy app: the city, a district, a building, a household, a person, a business and who contributes.</summary>
    public enum ViewKind : byte
    {
        City = 0,
        District = 1,
        Building = 2,
        Household = 3,
        Person = 4,
        Business = 5,
        Contributors = 6,
        Demands = 7,         // what the mod's own businesses take from the people (PopDemandViews)
        Demand = 8           // one business type's demand and where it comes from
    }

    /// <summary>How a figure is written (the renderer uses the game's own number and money formatting).</summary>
    public enum ViewFormat : byte
    {
        Count = 0,
        Money = 1,       // whole money
        Permille = 2,    // a share in thousandths, written as a per cent
        Key = 3,         // a locale key, written in the player's language
        Years = 4,
        Hours = 5
    }

    /// <summary>Where a row or the primary action leads: a screen and the record it shows.</summary>
    public struct ViewTarget
    {
        public ViewKind Kind;
        public int Index;            // the record's slot (district, building, household, person, business), -1 for none

        public bool Valid => Index >= 0 || Kind == ViewKind.City || Kind == ViewKind.Contributors || Kind == ViewKind.Demands;

        public static ViewTarget To(ViewKind kind, int index) => new ViewTarget { Kind = kind, Index = index };

        public static readonly ViewTarget None = new ViewTarget { Kind = ViewKind.Person, Index = -1 };
    }

    /// <summary>One figure: its label's locale key and its value.</summary>
    public struct ViewFigure
    {
        public string Label;
        public long Value;
        public ViewFormat Format;
        public string Text;          // for a Key figure: the key written

        public static ViewFigure Of(string label, long value, ViewFormat format) => new ViewFigure { Label = label, Value = value, Format = format };

        public static ViewFigure Word(string label, string key) => new ViewFigure { Label = label, Format = ViewFormat.Key, Text = key };
    }

    /// <summary>One row of a screen's list: what it is, one figure, and where it leads.</summary>
    public sealed class ViewRow
    {
        public string Title;         // a locale key when TitleKey, else a name or an address the renderer writes as it is
        public bool TitleKey;
        public int NameSeed;         // a person's or a household's name, made by the game's own name maker from this seed; 0 for none
        public bool Female;
        public string Subtitle;      // a locale key, or null
        public long Value;
        public ViewFormat Format;
        public ViewTarget Target;
    }

    /// <summary>
    /// One screen as the design system draws it (D:/BigAmbitionsMod/not-in-repo/design/DESIGN_SPEC.md; the owner, 1 Nov 2026: "absolutely beautiful ...
    /// not cluttered"): a title, a few figures at the top, one list with a header and where each row leads, a few details as key and value, one primary
    /// action, and the words to show when the list is empty. Only what the page shows: no colours, no layout, nothing of the game's. Game-free.
    /// </summary>
    public sealed class PopulationView
    {
        public ViewKind Kind;
        public int Index;
        public string Title;
        public bool TitleKey;
        public int NameSeed;
        public bool Female;
        public string Subtitle;
        public readonly List<ViewFigure> Hero = new List<ViewFigure>(4);
        public string RowsHeader;
        public readonly List<ViewRow> Rows = new List<ViewRow>(PopulationViews.RowsMost);
        public readonly List<ViewFigure> Details = new List<ViewFigure>(8);
        public string Primary;       // the primary action's label key, or null for none
        public ViewTarget PrimaryTarget = ViewTarget.None;
        public string Empty;         // the words for an empty list
    }

    /// <summary>
    /// The population's screens worked out from its records (the brief's tenth step and the owner's word of 1 Nov 2026: who contributes, seen): the city,
    /// each district, each building, each household (what it earned, paid in tax, spent and where, saved), each person (their job, pay, hours and their
    /// part of their employer's value added), each business (where its customers come from and their classes, its wage bill by resident, its taxes),
    /// and who contributes (the top employers, the districts that spend most, the player's share). Built when a screen opens, never per frame.
    /// Game-free (EconomyCheck's PopulationChecks builds every one).
    /// </summary>
    public static class PopulationViews
    {
        public const int RowsMost = 8;   // rows a list shows: a screen keeps to a few things
        private const string Pop = "reallife" + "_pop_";   // the words' keys (split, so the locale check does not take a prefix for a key)

        /// <summary>The city: its people at a glance, its districts, and the way to who contributes.</summary>
        public static PopulationView City(PopulationState s)
        {
            var v = new PopulationView { Kind = ViewKind.City, Index = 0, Title = "reallife_pop_view_city", TitleKey = true };
            Totals(s, out int people, out int households, out int workers, out int unemployed, out int jobs, out int vacancies, out int commuters, out long rent, out int renters);
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_people", people, ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_households", households, ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_unemployment", Share(unemployed, workers + unemployed), ViewFormat.Permille));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_rent", renters > 0 ? rent / renters / 100 : 0, ViewFormat.Money));
            v.RowsHeader = "reallife_pop_rows_districts";
            var order = new List<int>(s.Figures.Length);
            for (int d = 0; d < s.Figures.Length && d < s.Districts.Count; d++)
            {
                if (s.Figures[d].People > 0)
                {
                    order.Add(d);
                }
            }
            order.Sort((a, b) => s.Figures[b].People.CompareTo(s.Figures[a].People));
            for (int i = 0; i < order.Count && v.Rows.Count < RowsMost; i++)
            {
                int d = order[i];
                v.Rows.Add(new ViewRow { Title = s.Districts[d].Id, TitleKey = true, Value = s.Figures[d].People, Format = ViewFormat.Count, Target = ViewTarget.To(ViewKind.District, d) });
            }
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_births", s.BirthsLast, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_deaths", s.DeathsLast, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_moved_in", s.Region.MovedInLast, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_moved_out", s.Region.MovedOutLast, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_jobs", jobs, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_vacancies", vacancies, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_commuters", commuters, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_taxes", s.Books.TaxesLast / 100, ViewFormat.Money));
            v.Primary = "reallife_pop_act_contributors";
            v.PrimaryTarget = ViewTarget.To(ViewKind.Contributors, 0);
            v.Empty = "reallife_pop_empty_city";
            return v;
        }

        /// <summary>A district: its people, rents and jobs, its fullest buildings, and the way to its businesses.</summary>
        public static PopulationView District(PopulationState s, int d)
        {
            var v = new PopulationView { Kind = ViewKind.District, Index = d, Empty = "reallife_pop_empty_district" };
            if (d < 0 || d >= s.Districts.Count || d >= s.Figures.Length)
            {
                return v;
            }
            DistrictFigures f = s.Figures[d];
            v.Title = s.Districts[d].Id;
            v.TitleKey = true;
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_people", f.People, ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_unemployment", Share(f.Unemployed, f.Workers + f.Unemployed), ViewFormat.Permille));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_rent", f.MeanRent / 100, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_vacant", Share(f.Vacant, f.Flats), ViewFormat.Permille));
            v.RowsHeader = "reallife_pop_rows_buildings";
            var people = new int[s.Buildings.Count];
            for (int h = 0; h < s.Households.Count; h++)
            {
                int b = s.Households.Alive(h) ? s.Households.Building[h] : -1;
                if (b >= 0 && b < people.Length && s.Buildings[b].District == d)
                {
                    people[b] += s.Households.Size[h];
                }
            }
            foreach (int b in Top(people, RowsMost))
            {
                v.Rows.Add(new ViewRow { Title = AddressOf(s, b), Value = people[b], Format = ViewFormat.Count, Target = ViewTarget.To(ViewKind.Building, b) });
            }
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_households", f.Households, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_income", f.MeanIncome / 100, ViewFormat.Money));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_jobs", f.Jobs, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_births", f.Births, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_deaths", f.Deaths, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_taxes", f.Taxes / 100, ViewFormat.Money));
            int business = BiggestBusiness(s, d);
            if (business >= 0)
            {
                v.Primary = "reallife_pop_act_busiest";
                v.PrimaryTarget = ViewTarget.To(ViewKind.Business, business);
            }
            return v;
        }

        /// <summary>A building: its flats and who lives there.</summary>
        public static PopulationView Building(PopulationState s, int b)
        {
            var v = new PopulationView { Kind = ViewKind.Building, Index = b, Empty = "reallife_pop_empty_building" };
            if (b < 0 || b >= s.Buildings.Count)
            {
                return v;
            }
            v.Title = AddressOf(s, b);
            v.Subtitle = s.Districts.Count > s.Buildings[b].District && s.Buildings[b].District >= 0 ? s.Districts[s.Buildings[b].District].Id : null;
            int flats = b < s.FlatCount.Length ? s.FlatCount[b] : 0, free = 0, people = 0;
            for (int u = 0; u < flats; u++)
            {
                int h = s.Occupant[s.FlatFirst[b] + u];
                if (h == -1)
                {
                    free++;
                    continue;
                }
                if (h >= 0 && s.Households.Alive(h))
                {
                    people += s.Households.Size[h];
                    if (v.Rows.Count < RowsMost)
                    {
                        int head = s.Households.First[h];
                        v.Rows.Add(new ViewRow
                        {
                            NameSeed = NameSeed(s, head), Female = head >= 0 && s.People.Female[head] == 1, Subtitle = ClassKey(s.Households.Class[h]), Value = s.Households.Size[h],
                            Format = ViewFormat.Count, Target = ViewTarget.To(ViewKind.Household, h)
                        });
                    }
                }
            }
            v.RowsHeader = "reallife_pop_rows_households";
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_flats", flats, ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_people", people, ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_free", free, ViewFormat.Count));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_flat_rent", CityTable.FlatRentNow(s, b) / 100, ViewFormat.Money));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_flat_size", b < s.FlatSqm.Length ? s.FlatSqm[b] : 0, ViewFormat.Count));
            return v;
        }

        /// <summary>
        /// A household: what it earned last month, paid in tax, spent and saved; its people; its rent and to whom, its savings and debt; and where it
        /// spent today (its part of each business's takings, worked out from the day's pools).
        /// </summary>
        public static PopulationView Household(PopulationState s, PoolRules.DemandDay dd, int h)
        {
            var v = new PopulationView { Kind = ViewKind.Household, Index = h, Empty = "reallife_pop_empty_household" };
            if (!s.Households.Alive(h))
            {
                return v;
            }
            HouseholdTable hh = s.Households;
            int head = hh.First[h];
            v.NameSeed = NameSeed(s, head);
            v.Female = head >= 0 && s.People.Female[head] == 1;
            v.Subtitle = ClassKey(hh.Class[h]);
            int[] month = MonthOf(s, h);
            long income = (long)month[Ledger.Wages] + month[Ledger.Benefits] + month[Ledger.Pension] + month[Ledger.Returns];
            long taxes = (long)month[Ledger.IncomeTax] + month[Ledger.SalesTax] + month[Ledger.PropertyTax];
            long spent = 0;
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                spent += month[Ledger.SpendFirst + k];
            }
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_earned", income / 100, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_tax_paid", taxes / 100, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_spent", spent / 100, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_saved", month[Ledger.Saving] / 100, ViewFormat.Money));
            v.RowsHeader = "reallife_pop_rows_members";
            for (int p = head; p >= 0 && v.Rows.Count < RowsMost; p = s.People.Next[p])
            {
                v.Rows.Add(new ViewRow
                {
                    NameSeed = NameSeed(s, p), Female = s.People.Female[p] == 1, Subtitle = StatusKey(s.People.Status[p]), Value = s.Age(p, s.Day), Format = ViewFormat.Years,
                    Target = ViewTarget.To(ViewKind.Person, p)
                });
            }
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_rent_paid", month[Ledger.Rent] / 100, ViewFormat.Money));
            v.Details.Add(ViewFigure.Word("reallife_pop_fig_landlord", LandlordKey(hh.Landlord[h])));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_savings", hh.Savings[h], ViewFormat.Money));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_debt", hh.Debt[h], ViewFormat.Money));
            int top = TopSpend(s, dd, h, out long at);
            if (top >= 0)
            {
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_top_spend", at / 100, ViewFormat.Money));
                v.Primary = "reallife_pop_act_where";
                v.PrimaryTarget = ViewTarget.To(ViewKind.Business, top);
            }
            return v;
        }

        /// <summary>A person: their work, pay and hours, and their part of their employer's value added (by their share of its wage bill).</summary>
        public static PopulationView Person(PopulationState s, int p)
        {
            var v = new PopulationView { Kind = ViewKind.Person, Index = p, Empty = "reallife_pop_empty_person" };
            if (!s.People.Alive(p))
            {
                return v;
            }
            PersonTable pt = s.People;
            v.NameSeed = NameSeed(s, p);
            v.Female = pt.Female[p] == 1;
            v.Subtitle = StatusKey(pt.Status[p]);
            bool working = pt.Status[p] == (byte)Status.Worker;
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_age", s.Age(p, s.Day), ViewFormat.Years));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_pay", working ? LabourRules.DailyPay(s, p) / 100 : 0, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_hours", working ? pt.Hours[p] : 0, ViewFormat.Hours));
            v.Details.Add(ViewFigure.Word("reallife_pop_fig_education", EducationKey(pt.Education[p])));
            v.Details.Add(ViewFigure.Word("reallife_pop_fig_field", FieldKey(pt.Field[p])));
            int e = pt.Job[p];
            if (working && e >= 0)
            {
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_value_added", ValueAdded(s, p) / 100, ViewFormat.Money));
                int business = s.Employers.Business[e];
                if (business >= 0)
                {
                    v.Primary = "reallife_pop_act_employer";
                    v.PrimaryTarget = ViewTarget.To(ViewKind.Business, business);
                }
            }
            int h = pt.Household[p];
            if (v.Primary == null && h >= 0)
            {
                v.Primary = "reallife_pop_act_household";
                v.PrimaryTarget = ViewTarget.To(ViewKind.Household, h);
            }
            return v;
        }

        /// <summary>
        /// A person's part of their employer's value added last month, cents: its output last month by their share of its pay now (their pay over all
        /// its people's, the commuters' at the going rate), the rule that sums to no more than the employer's whole over its people.
        /// </summary>
        public static long ValueAdded(PopulationState s, int p)
        {
            int e = s.People.Job[p];
            if (e < 0 || e >= s.Employers.Count || s.People.Status[p] != (byte)Status.Worker)
            {
                return 0;
            }
            long output = s.Employers.OutputLast[e];
            long all = EmployerPay(s, e);
            return output <= 0 || all <= 0 ? 0 : output * LabourRules.DailyPay(s, p) / all;
        }

        /// <summary>An employer's pay a day now, cents: its residents' own and its commuters' at the going rate of its field.</summary>
        public static long EmployerPay(PopulationState s, int e)
        {
            long all = (long)s.Employers.Commuters[e] * LabourRules.CommuterPay(s, e);
            for (int q = 0; q < s.People.Count; q++)
            {
                if (s.People.Id[q] != 0 && s.People.Job[q] == e && s.People.Status[q] == (byte)Status.Worker)
                {
                    all += LabourRules.DailyPay(s, q);
                }
            }
            return all;
        }

        /// <summary>A business: its customers and takings, the districts its customers come from, its classes, its wage bill by resident and its taxes.</summary>
        public static PopulationView Business(PopulationState s, PoolRules.DemandDay dd, int b)
        {
            var v = new PopulationView { Kind = ViewKind.Business, Index = b, Empty = "reallife_pop_empty_business" };
            BusinessTable bt = s.Businesses;
            if (b < 0 || b >= bt.Count)
            {
                return v;
            }
            v.Title = bt.Key[b];
            v.Subtitle = bt.Type[b];
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_customers", bt.Visits[b], ViewFormat.Count));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_takings", bt.Takings[b] / 100, ViewFormat.Money));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_against_first", CustomerRules.Factor(bt.Takings[b], bt.Anchor[b], bt.AnchorDay[b]), ViewFormat.Permille));
            v.RowsHeader = "reallife_pop_rows_from";
            long[] from = CustomersFrom(s, dd, b);
            foreach (int d in Top(from, RowsMost))
            {
                v.Rows.Add(new ViewRow
                {
                    Title = d < s.Districts.Count ? s.Districts[d].Id : "?", TitleKey = d < s.Districts.Count, Value = from[d] / 100, Format = ViewFormat.Money,
                    Target = ViewTarget.To(ViewKind.District, d)
                });
            }
            long all = bt.ByClass[b * 3] + bt.ByClass[b * 3 + 1] + bt.ByClass[b * 3 + 2];
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_working", Share(bt.ByClass[b * 3], all), ViewFormat.Permille));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_middle", Share(bt.ByClass[b * 3 + 1], all), ViewFormat.Permille));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_upper", Share(bt.ByClass[b * 3 + 2], all), ViewFormat.Permille));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_month", bt.LastTakings[b] / 100, ViewFormat.Money));
            v.Details.Add(ViewFigure.Of("reallife_pop_fig_sales_tax", bt.LastTakings[b] * PopulationCatalogue.SalesTax / 1000 / 100, ViewFormat.Money));
            int e = bt.Employer[b];
            if (e >= 0 && e < s.Employers.Count)
            {
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_wage_bill", s.Employers.BillLast[e] / 100, ViewFormat.Money));
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_residents", s.Employers.Filled[e], ViewFormat.Count));
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_commuters", s.Employers.Commuters[e], ViewFormat.Count));
                int best = BestPaid(s, e);
                if (best >= 0)
                {
                    v.Primary = "reallife_pop_act_staff";
                    v.PrimaryTarget = ViewTarget.To(ViewKind.Person, best);
                }
            }
            return v;
        }

        /// <summary>
        /// Who contributes: the employers that paid most last month, the districts whose households spent most, and the player's share of the city's
        /// people in work, of its spending and of its taxes (the player's businesses' sales tax and the income tax of the people they employ).
        /// </summary>
        public static PopulationView Contributors(PopulationState s, PoolRules.DemandDay dd)
        {
            var v = new PopulationView { Kind = ViewKind.Contributors, Index = 0, Title = "reallife_pop_view_contributors", TitleKey = true, Empty = "reallife_pop_empty_contributors" };
            EmployerTable et = s.Employers;
            var bills = new long[et.Count];
            for (int e = 0; e < et.Count; e++)
            {
                bills[e] = et.BillLast[e] > 0 ? et.BillLast[e] : et.Bill[e];
            }
            v.RowsHeader = "reallife_pop_rows_employers";
            foreach (int e in Top(bills, RowsMost))
            {
                int business = et.Business[e];
                v.Rows.Add(new ViewRow
                {
                    Title = business >= 0 ? s.Businesses.Key[business] : et.Key[e], Subtitle = EmployerKey((EmployerKind)et.Kind[e]), Value = bills[e] / 100, Format = ViewFormat.Money,
                    Target = business >= 0 ? ViewTarget.To(ViewKind.Business, business) : ViewTarget.To(ViewKind.Building, et.Building[e])
                });
            }
            PlayerShare(s, out int employed, out int inWork, out long spending, out long allSpending, out long taxes, out long allTaxes);
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_your_people", Share(employed, inWork), ViewFormat.Permille));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_your_spending", Share(spending, allSpending), ViewFormat.Permille));
            v.Hero.Add(ViewFigure.Of("reallife_pop_fig_your_taxes", Share(taxes, allTaxes), ViewFormat.Permille));
            int topDistrict = -1;
            long most = 0;
            for (int d = 0; d < s.Figures.Length && d < s.Districts.Count; d++)
            {
                if (s.Figures[d].Spending > most)
                {
                    most = s.Figures[d].Spending;
                    topDistrict = d;
                }
            }
            if (topDistrict >= 0)
            {
                v.Details.Add(ViewFigure.Word("reallife_pop_fig_top_district", s.Districts[topDistrict].Id));
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_spent", most / 100, ViewFormat.Money));
                v.Primary = "reallife_pop_act_top_district";
                v.PrimaryTarget = ViewTarget.To(ViewKind.District, topDistrict);
            }
            return v;
        }

        /// <summary>
        /// The player's share: the city's people in work at the player's businesses, the spending at them today against all of it, and their taxes
        /// (sales tax in their takings and the income tax of their people) against the council's day.
        /// </summary>
        public static void PlayerShare(PopulationState s, out int employed, out int inWork, out long spending, out long allSpending, out long taxes, out long allTaxes)
        {
            employed = inWork = 0;
            spending = allSpending = taxes = allTaxes = 0;
            EmployerTable et = s.Employers;
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] == 0 || s.People.Status[p] != (byte)Status.Worker)
                {
                    continue;
                }
                inWork++;
                int e = s.People.Job[p];
                long tax = SpendRules.Tax(LabourRules.DailyPay(s, p));
                allTaxes += tax;
                if (e >= 0 && et.Players[e])
                {
                    employed++;
                    taxes += tax;
                }
            }
            BusinessTable bt = s.Businesses;
            for (int b = 0; b < bt.Count; b++)
            {
                long t = bt.Takings[b];
                long sales = t * PopulationCatalogue.SalesTax / 1000;
                allSpending += t;
                allTaxes += sales;
                if (bt.Owner[b] == (byte)Owner.Player)
                {
                    spending += t;
                    taxes += sales;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Where a business's customers came from today, cents by home district: its takings from each pool kind split by the share each origin's money is of that pool.</summary>
        public static long[] CustomersFrom(PopulationState s, PoolRules.DemandDay dd, int b)
        {
            int n = Math.Max(1, s.Districts.Count);
            var from = new long[n];
            BusinessTable bt = s.Businesses;
            if (dd == null || dd.Day < 0 || b < 0 || b >= bt.Count || dd.Districts != n)
            {
                return from;
            }
            int d = bt.District[b], k = bt.Kind[b];
            if (k < PopulationCatalogue.ShopCategoryFirst || k > PopulationCatalogue.ShopCategoryLast)
            {
                return from;
            }
            for (int kind = 0; kind < PoolRules.LineKinds; kind++)
            {
                for (int c = 0; c < PopulationCatalogue.Classes; c++)
                {
                    long pool = dd.SubPool[dd.LineIndex(kind, d, c, k)];
                    if (pool <= 0)
                    {
                        continue;
                    }
                    long mine = dd.Weight[b * PopulationCatalogue.Classes + c];
                    long total = dd.WeightTotal[dd.TotalIndex(d, c, k)];
                    if (total <= 0 || mine <= 0)
                    {
                        continue;
                    }
                    long take = pool * mine / total;
                    for (int o = 0; o < n; o++)
                    {
                        int r = dd.RouteIndex(kind, o, k);
                        long money = dd.LineMoney[dd.LineIndex(kind, o, c, k)];
                        long all = dd.RouteCum[r + n];
                        if (money <= 0 || all <= 0)
                        {
                            continue;
                        }
                        long routed = PoolRules.CumShare(money, dd.RouteCum[r + d], dd.RouteCum[r + d + 1], all);
                        from[o] += take * routed / pool;
                    }
                }
            }
            return from;
        }

        // The business a household spent most at today, and how much (cents); -1 for none.
        private static int TopSpend(PopulationState s, PoolRules.DemandDay dd, int h, out long amount)
        {
            amount = 0;
            if (dd == null || dd.Day < 0)
            {
                return -1;
            }
            var spent = new long[s.Businesses.Count];
            PoolRules.Attribute(s, dd, h, spent);
            int best = -1;
            for (int b = 0; b < spent.Length; b++)
            {
                if (spent[b] > amount)
                {
                    amount = spent[b];
                    best = b;
                }
            }
            return best;
        }

        private static int BestPaid(PopulationState s, int e)
        {
            int best = -1, pay = -1;
            for (int p = 0; p < s.People.Count; p++)
            {
                if (s.People.Id[p] != 0 && s.People.Job[p] == e && s.People.Pay[p] > pay)
                {
                    best = p;
                    pay = s.People.Pay[p];
                }
            }
            return best;
        }

        private static int BiggestBusiness(PopulationState s, int d)
        {
            int best = -1;
            long most = -1;
            for (int b = 0; b < s.Businesses.Count; b++)
            {
                if (s.Businesses.Seen[b] && s.Businesses.District[b] == d && s.Businesses.Takings[b] > most)
                {
                    most = s.Businesses.Takings[b];
                    best = b;
                }
            }
            return best;
        }

        private static void Totals(PopulationState s, out int people, out int households, out int workers, out int unemployed, out int jobs, out int vacancies, out int commuters,
                                   out long rent, out int renters)
        {
            people = households = workers = unemployed = jobs = vacancies = commuters = renters = 0;
            rent = 0;
            for (int d = 0; d < s.Figures.Length; d++)
            {
                DistrictFigures f = s.Figures[d];
                people += f.People;
                households += f.Households;
                workers += f.Workers;
                unemployed += f.Unemployed;
                jobs += f.Jobs;
                vacancies += f.Vacancies;
                commuters += f.Commuters;
                if (f.MeanRent > 0)
                {
                    rent += (long)f.MeanRent * f.Households;
                    renters += f.Households;
                }
            }
            if (people == 0 && s.HostPeople > 0)
            {
                people = s.HostPeople;   // a guest: the host's own count
                households = s.HostHouseholds;
            }
        }

        /// <summary>The places of the largest values, the most first, at most so many (none of nothing).</summary>
        public static List<int> Top(long[] values, int most)
        {
            var order = new List<int>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > 0)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) => values[b] != values[a] ? values[b].CompareTo(values[a]) : a.CompareTo(b));
            if (order.Count > most)
            {
                order.RemoveRange(most, order.Count - most);
            }
            return order;
        }

        private static List<int> Top(int[] values, int most)
        {
            var wide = new long[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                wide[i] = values[i];
            }
            return Top(wide, most);
        }

        private static int[] MonthOf(PopulationState s, int h)
        {
            var month = new int[Ledger.Lines];
            int row = h * Ledger.Lines;
            bool running = false;
            for (int i = 0; i < Ledger.Lines; i++)
            {
                running |= s.Households.Month[row + i] != 0;
            }
            Array.Copy(running ? s.Households.Month : s.Households.Last, row, month, 0, Ledger.Lines);   // the month so far, or the last while this one has nothing yet
            return month;
        }

        /// <summary>The seed of a person's name, the same on every machine: the city's seed and their id. 0 for none.</summary>
        public static int NameSeed(PopulationState s, int p)
        {
            return p >= 0 && p < s.People.Count && s.People.Id[p] != 0 ? unchecked((int)PopDraw.Hash(s.Seed, PopDraw.Link, s.People.Id[p], 0) | 1) : 0;
        }

        public static string AddressOf(PopulationState s, int b)
        {
            return b >= 0 && b < s.Buildings.Count ? PopulationState.KeyOf(s.Buildings[b].Street, s.Buildings[b].Number) : string.Empty;
        }

        public static int Share(long part, long all) => all <= 0 ? 0 : (int)Math.Min(1000L, Math.Max(0L, part * 1000 / all));

        public static string ClassKey(int cls) => cls == 0 ? "reallife_pop_class_working" : cls == 1 ? "reallife_pop_class_middle" : "reallife_pop_class_upper";

        public static string StatusKey(int status) => Pop + "status_" + ((Status)PopulationCatalogue.Clamp(status, 0, 6)).ToString().ToLowerInvariant();

        public static string EducationKey(int education) => Pop + "education_" + ((Education)PopulationCatalogue.Clamp(education, 0, 4)).ToString().ToLowerInvariant();

        public static string FieldKey(int field) => Pop + "field_" + ((Field)PopulationCatalogue.Clamp(field, 0, PopulationCatalogue.Fields - 1)).ToString().ToLowerInvariant();

        public static string LandlordKey(int landlord)
        {
            return landlord == Landlords.Own ? "reallife_pop_landlord_own" : landlord == Landlords.Player ? "reallife_pop_landlord_player" : "reallife_pop_landlord_city";
        }

        public static string EmployerKey(EmployerKind kind) => Pop + "employer_" + kind.ToString().ToLowerInvariant();
    }
}

using System.Collections.Generic;
using System.Globalization;
using Entities;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city's economy director: a rival opening beside the business that has done best.</summary>
    public sealed partial class CityEconomyDirector
    {
        private const int PurposeCopycat = 51;

        private readonly ProfitRuns<Address> _runs = new ProfitRuns<Address>();

        // The business that made the most of those in profit every day for two weeks may draw "a competitor undercuts you".
        private void Rivals(int day)
        {
            if (CityBridges.Copycat == null || _shops.Count == 0 || (_state.LastCopycatDay > 0 && day - _state.LastCopycatDay < MacroCatalogue.CopycatRestDays))
            {
                return;
            }
            int chance = MacroCatalogue.CopycatChance * CityModel.Trouble(_state.Cycle, _level) / 1000;
            if (CityModel.Draw(_state.Seed, PurposeCopycat, _state.Age, 1000) >= chance)
            {
                return;
            }
            CityShop best = null;
            float most = 0f;
            Runs(day);
            for (int i = 0; i < _shops.Count; i++)
            {
                float total;
                if (ProfitRun(_shops[i].Reg, day, out total) && total > most)
                {
                    most = total;
                    best = _shops[i];
                }
            }
            if (best == null)
            {
                return;
            }
            bool started = CityBridges.Copycat(best.Reg);
            if (started)
            {
                _state.LastCopycatDay = day;
                _state.News.Add(new CityNews { Day = day, Kind = CityNewsKind.Rival, Name = CityStore.Clean(best.Reg.BusinessName) });
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (city, rival): " + best.Reg.BusinessName + " made a profit on each of the last " + MacroCatalogue.CopycatDays + " days ("
                             + most.ToString("0") + " in all), the chance today was " + (chance / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "%: "
                             + (started ? "a rival opens beside it (the business feature's own issue)" : "the business feature did not start one there today") + ": PASS");
            }
        }

        // Every business's run of profit, in one pass over the last days' statements, so each is then a look-up.
        private void Runs(int today)
        {
            _runs.Begin();
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            int from = today - MacroCatalogue.CopycatDays;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;   // the game keeps them oldest first
                }
                _runs.Day();
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    if (statement != null && !ReferenceEquals(statement.Address, null))
                    {
                        _runs.Add(statement.Address, statement.TotalProfit);
                    }
                }
            }
        }

        // True if the game's daily statements show a profit at this business on each of the last so many days.
        private bool ProfitRun(BuildingRegistration reg, int today, out float total)
        {
            total = 0f;
            GameInstance save = Save;
            if (save == null || save.financialSummaries == null || reg == null || !reg.HasValidAddress)
            {
                return false;
            }
            Address address = reg.Address;
            if (!ReferenceEquals(address, null) && !(string.IsNullOrEmpty(address.streetName) && address.streetNumber == 0))
            {
                return _runs.Ran(address, MacroCatalogue.CopycatDays, out total);
            }
            return ProfitRunWalk(reg, today, out total);
        }

        // For an address equal to the game's "no address", which a statement with none also matches: walked for this one.
        private static bool ProfitRunWalk(BuildingRegistration reg, int today, out float total)
        {
            total = 0f;
            GameInstance save = Save;
            int days = 0;
            int from = today - MacroCatalogue.CopycatDays;
            List<FinancialSummary> summaries = save.financialSummaries;
            for (int i = summaries.Count - 1; i >= 0; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    if (statement != null && statement.Address == reg.Address)
                    {
                        if (statement.TotalProfit <= 0f)
                        {
                            return false;
                        }
                        total += statement.TotalProfit;
                        days++;
                        break;
                    }
                }
            }
            return days >= MacroCatalogue.CopycatDays;
        }
    }
}

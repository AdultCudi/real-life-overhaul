using System;
using System.Collections.Generic;
using Helpers;

namespace RealLifeOverhaul.Utilities
{
    public sealed partial class UtilitiesDirector
    {
        /// <summary>Current account books and portable forecast terms, without syncing places or accruing a day.</summary>
        public CompanionModule CompanionUtilities()
        {
            if (_failed)
            {
                return new CompanionModule("failed");
            }
            if (!_loaded || _state == null || !ReferenceEquals(_save, SaveGameManager.Current))
            {
                return new CompanionModule("unavailable");
            }
            var addresses = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (BuildingRegistration reg in _save.BuildingRegistrations)
            {
                if (reg != null && reg.HasValidAddress)
                {
                    addresses[UtilitiesGame.Place(reg)] = CompanionValues.Address(reg.StreetName, reg.StreetNumber);
                }
            }
            var rows = new List<object>();
            var unresolved = new List<object>();
            foreach (Account account in _state.Accounts)
            {
                if (addresses.TryGetValue(account.Place, out object address))
                {
                    rows.Add(UtilitiesCompanion.AccountRow(account, address, _state));
                }
                else
                {
                    // This owner can decode its own historic address, including a closed account's former premises.
                    int split = account.Place.LastIndexOf('~');
                    if (split > 0 && int.TryParse(account.Place.Substring(split + 1), out int number))
                    {
                        address = CompanionValues.Address(Uri.UnescapeDataString(account.Place.Substring(0, split)), number);
                        rows.Add(UtilitiesCompanion.AccountRow(account, address, _state));
                    }
                    else
                    {
                        unresolved.Add(account.Service.ToString().ToLowerInvariant());
                    }
                }
            }
            var kinds = new List<object>();
            foreach (string type in BusinessTypeHelper.BusinessTypeNames)
            {
                kinds.Add(CompanionSnapshot.Row("typeId", type, "kind", (int)UtilitiesGame.KindOf(type)));
            }
            var grace = new List<object>();
            foreach (string place in _state.Grace)
            {
                if (addresses.TryGetValue(place, out object address))
                {
                    grace.Add(address);
                }
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-utilities-v1",
                "terms", UtilitiesCompanion.Terms(Prices.Level, GameCalendar.DaysPerYear), "typeKinds", kinds,
                "accounts", rows, "unresolvedAccountServices", unresolved,
                "internet", UtilitiesCompanion.Internet(Prices.Level, GameCalendar.MonthDays, _state),
                "internetEnabled", true, "graceUntilDay", _state.GraceUntil, "graceAddresses", grace,
                "closureConditions", new[] { "power-not-supplied", "water-not-supplied", "required-internet-down" }));
        }
    }
}

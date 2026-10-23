using System;
using Newtonsoft.Json.Linq;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class CompanyDirector
    {
        /// <summary>Configured owner state only: no rounds, staff refresh, decisions or control transfer.</summary>
        public object CompanionOwnership(string operation, JObject target, BuildingRegistration reg)
        {
            if (!_running || _broken || _mgrBroken || _mgr == null)
            {
                return CompanionOwnershipUnknown(operation);
            }
            string place = CompanyGame.Place(reg);
            foreach (ManagerFile manager in _mgr.Managers)
            {
                if (manager.Region || manager.Place != place)
                {
                    continue;
                }
                if ((operation == "schedule" || operation == "hire")
                    && ManagerRules.Has(manager.Tasks, ManagerTask.Rota))
                {
                    return RealLifeOverhaul.CompanionOwnership.Decision("conditional", "rlo.managers", "rota",
                        "manager-controls-scheduling", "manager-may-rewrite-week");
                }
                if (operation == "hire" && ManagerRules.Has(manager.Tasks, ManagerTask.Hiring))
                {
                    return RealLifeOverhaul.CompanionOwnership.Decision("conditional", "rlo.managers", "hiring",
                        "manager-controls-hiring", "manager-may-hire-replacement");
                }
                if (operation == "wholesale" && ManagerRules.Has(manager.Tasks, ManagerTask.Stock))
                {
                    JArray items = (JArray)target["itemIds"];
                    if (items.Count == 0)
                    {
                        return RealLifeOverhaul.CompanionOwnership.Decision("refused", "rlo.managers", "stock",
                            "manager-controls-stock", "manager-may-rewrite-order");
                    }
                    foreach (JToken item in items)
                    {
                        if (!manager.Holds((string)item))
                        {
                            return RealLifeOverhaul.CompanionOwnership.Decision("refused", "rlo.managers", "stock",
                                "stock-line-not-player-held", "write-may-transfer-control-to-player");
                        }
                    }
                }
            }
            return null;
        }

        internal object CompanionPermit(BuildingRegistration reg, JArray week)
        {
            if (!On(CompanyPart.Licences) || _state == null)
            {
                return RealLifeOverhaul.CompanionOwnership.Decision("unknown", "rlo.licences", "schedule", "licence-state-unavailable");
            }
            ShopFile shop = _state.Shop(CompanyGame.Place(reg));
            foreach (JToken day in week)
            {
                if (!(bool)day["isOpen"])
                {
                    continue;
                }
                foreach (JToken hours in (JArray)day["hours"])
                {
                    if (LicenceRules.IsLate((int)hours["from"], (int)hours["to"])
                        && (shop == null || !LicenceRules.Valid(shop.Permits[(int)LicenceKind.Late], Today)))
                    {
                        return RealLifeOverhaul.CompanionOwnership.Decision("refused", "rlo.licences", "schedule",
                            "late-opening-permit-missing", "unpermitted-opening-hours");
                    }
                }
            }
            return null;
        }

        private static object CompanionOwnershipUnknown(string operation)
        {
            return RealLifeOverhaul.CompanionOwnership.Decision("unknown", "rlo.managers", operation,
                "manager-state-unavailable");
        }
    }
}

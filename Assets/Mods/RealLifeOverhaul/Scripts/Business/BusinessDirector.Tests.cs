using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>Business pressure's test buttons on the options page.</summary>
    public sealed partial class BusinessDirector
    {
        /// <summary>
        /// Test: starts the next kind of a group's issues at the business the player is in, or their first. Grace, rests,
        /// odds and the open limit are waived; what the story needs is not, and the notice says which.
        /// </summary>
        public void Test(PressureGroup group)
        {
            TestRefusal why = _broken || !BusinessState.Loaded ? TestRefusal.NotLoaded : !Settings.Business ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Business issue", why);
                return;
            }
            BuildingRegistration reg = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            if (!BusinessGame.IsBusiness(reg))
            {
                BusinessGame.Businesses(_shops);
                reg = _shops.Count > 0 ? _shops[0] : null;
                _shops.Clear();
            }
            if (reg == null)
            {
                Logger?.Info("Business issue test: not started (the player runs no business that takes money).");
                Notifications.Show(NotificationType.Warning, "reallife_biz_test_no_business", null, 9f);
                return;
            }
            string place = BusinessGame.Place(reg);
            var data = new Dictionary<string, string>(2) { { "business", BusinessGame.Name(reg) } };
            if (BusinessState.IssueAt(place) != null)
            {
                Logger?.Info("Business issue test: not started (" + BusinessGame.Name(reg) + " already has an open issue).");
                Notifications.Show(NotificationType.Warning, "reallife_biz_test_busy", data, 9f);
                return;
            }
            // The next kind of the group that can start here; if none can, the reason of the one whose turn it was.
            IssueDef[] all = BusinessCatalogue.All;
            _deal.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Group == group)
                {
                    _deal.Add(all[i]);
                }
            }
            string firstWhy = null;
            IssueDef firstDef = null;
            int start = _testAt[(int)group]++;
            for (int k = 0; k < _deal.Count; k++)
            {
                IssueDef def = _deal[(start + k) % _deal.Count];
                string reason = BusinessState.IssueOf(def.Id) != null ? "reallife_biz_test_kind_open" : BusinessIssues.Why(def, reg, true);
                if (reason == null)
                {
                    if (Open(def, reg, TimeHelper.CurrentDay))
                    {
                        _deal.Clear();
                        return;
                    }
                    reason = "reallife_biz_test_failed";
                }
                if (firstWhy == null)
                {
                    firstWhy = reason;
                    firstDef = def;
                }
            }
            _deal.Clear();
            data["issue"] = firstDef != null ? JobText.T(firstDef.NameKey) : string.Empty;
            Logger?.Info("Business issue test: not started (" + (firstDef != null ? firstDef.Id : group.ToString()) + ": " + firstWhy + ").");
            Notifications.Show(NotificationType.Warning, firstWhy ?? "reallife_biz_test_failed", data, 10f);
        }

        /// <summary>Test: every issue, footfall change and closure of business pressure's put back, the racket forgotten.</summary>
        public void TestResolveAll()
        {
            if (_broken || !BusinessState.Loaded)
            {
                TestRefusals.Tell(Logger, "Business resolve", TestRefusal.NotLoaded);
                return;
            }
            _visit.End();
            StandDown(false, true, false);
        }

        /// <summary>Test: the Business desk face to face, wherever the player is.</summary>
        public void TestDesk()
        {
            TestRefusal why = _broken || !BusinessState.Loaded ? TestRefusal.NotLoaded : !Settings.Business ? TestRefusal.FeatureOff
                : UiOpen() ? TestRefusal.SceneActive : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Business desk", why);
                return;
            }
            BusinessDesk.OpenInPerson(null);
        }
    }
}

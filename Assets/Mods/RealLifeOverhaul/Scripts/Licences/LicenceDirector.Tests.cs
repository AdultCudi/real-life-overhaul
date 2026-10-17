using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Driving licences' test buttons on the options page. One that cannot run says why.</summary>
    public sealed partial class LicenceDirector
    {
        private bool Ready(string test)
        {
            if (!LicenceState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (_broken)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff, 0, "switched off after an error this session");
                return false;
            }
            return true;
        }

        /// <summary>Test button: gives the licence if it is not in order, takes it away if it is.</summary>
        public void TestToggle(LicenceClass kind)
        {
            if (!Ready("Licence"))
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            if (LicenceState.Valid(kind, day))
            {
                LicenceState.Revoke(kind);
                Licences.Toast(NotificationType.Warning, "reallife_lic_test_revoked", "class", Licences.ClassName(kind), 6f);
            }
            else
            {
                if ((kind == LicenceClass.Commercial || kind == LicenceClass.Taxi) && !LicenceState.Valid(LicenceClass.Car, day))
                {
                    LicenceState.Grant(LicenceClass.Car, day);   // the van and the taxi need a car licence
                }
                LicenceState.SuspendedUntil = 0;
                LicenceState.Grant(kind, day);
                Licences.Toast(NotificationType.Success, "reallife_lic_test_granted", "class", Licences.ClassName(kind), 6f);
            }
        }

        /// <summary>Test button: the written test, here and now, with nothing charged. A pass counts.</summary>
        public void TestTheory()
        {
            if (!Ready("Theory test"))
            {
                return;
            }
            TestRefusal why = IsActive || _pending ? TestRefusal.SceneActive
                : !Settings.DrivingLicence || !Settings.DrivingSchool ? TestRefusal.FeatureOff
                : PlayerHelper.IsUsingVehicle ? TestRefusal.NotOnFoot
                : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Theory test", why);
                return;
            }
            _pendingFee = 0;
            _pending = true;
            _pendingService = SchoolService.Theory;
            _pendingSince = Time.unscaledTime;   // starts once the options page closes
        }

        /// <summary>Test button: a practical test from where the player stands, with nothing charged. A pass counts.</summary>
        public void TestPractical(LicenceClass kind)
        {
            if (!Ready("Practical test"))
            {
                return;
            }
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None)
            {
                why = IsActive || _pending ? TestRefusal.SceneActive
                    : !Settings.DrivingLicence || !Settings.DrivingSchool ? TestRefusal.FeatureOff
                    : PlayerHelper.IsUsingVehicle ? TestRefusal.NotOnFoot
                    : JobDirector.OnShift ? TestRefusal.OnJob
                    : PoliceAttention.IsBusy ? TestRefusal.PoliceBusy
                    : TestRefusal.None;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Practical test", why);
                return;
            }
            if (!_practical.Begin(kind, false, true, 0, PlayerHelper.GetPosition(), out string reason))
            {
                TestRefusals.Tell(Logger, "Practical test", TestRefusal.NothingFits, 0, reason);
            }
        }

        /// <summary>Test button: a routine roadside check now.</summary>
        public void TestCheck()
        {
            if (Ready("Roadside check"))
            {
                _enforcement.TestCheck();
            }
        }

        /// <summary>Test button: four penalty points, as for driving on the pavement.</summary>
        public void TestPoints()
        {
            if (Ready("Penalty points"))
            {
                _enforcement.Points(PointCode.Reckless);
            }
        }

        /// <summary>Test button, and the plain way to look: the licence card.</summary>
        public void ShowCard()
        {
            if (!Ready("Licence card") || IsActive)
            {
                return;
            }
            if (!_desk.ShowCard())
            {
                Licences.Toast(NotificationType.Info, "reallife_lic_toast", "text", DrivingSchool.CardText(), 20f);
            }
        }
    }
}

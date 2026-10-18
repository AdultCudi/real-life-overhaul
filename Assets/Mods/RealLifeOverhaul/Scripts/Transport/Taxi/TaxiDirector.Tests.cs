using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul
{
    /// <summary>The taxi director's test buttons, and the clean-up the save tools and the options page use.</summary>
    public sealed partial class TaxiDirector
    {
        /// <summary>"Test: call City Cabs": the conversation the contact opens, without the phone.</summary>
        public void TestCall()
        {
            try
            {
                TestRefusal state = TestRefusals.Street();
                if (state == TestRefusal.NotLoaded)
                {
                    TestRefusals.Tell(Logger, "Taxi", state);
                    return;
                }
                TaxiPhone.Open();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>"Test: bring a taxi at once": from close by, in view if need be, and with nothing asked.</summary>
        public void TestBring()
        {
            try
            {
                TestRefusal state = TestRefusals.Street();
                if (state != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Taxi", state);
                    return;
                }
                switch (Check(out bool failed))
                {
                    case TaxiRules.Refusal.None:
                        break;
                    case TaxiRules.Refusal.Off:
                        TestRefusals.TellKey(Logger, "Taxi", failed ? "reallife_taxi_test_failed" : "reallife_test_refused_feature_off");
                        return;
                    case TaxiRules.Refusal.OnItsWay:
                        TestRefusals.TellKey(Logger, "Taxi", "reallife_taxi_test_out");
                        return;
                    case TaxiRules.Refusal.InVehicle:
                        TestRefusals.Tell(Logger, "Taxi", TestRefusal.NotOnFoot);
                        return;
                    case TaxiRules.Refusal.Indoors:
                        TestRefusals.Tell(Logger, "Taxi", TestRefusal.Indoors);
                        return;
                    default:
                        TestRefusals.TellKey(Logger, "Taxi", "reallife_taxi_test_held");
                        return;
                }
                TaxiRules.StartBand(false, true, out float least, out float most);
                _roads.NewCall();
                if (!Bring(PlayerHelper.GetPosition(), least, most, true, false, null, out string why))
                {
                    TestRefusals.TellKey(Logger, "Taxi", "reallife_taxi_test_none", why);
                    return;
                }
                _fee = TaxiRules.Fee(TimeHelper.CurrentHour, Wet());
                Notifications.Show(NotificationType.Info, "reallife_taxi_note_coming", new Dictionary<string, string>(1)
                {
                    { "minutes", TaxiRules.Minutes(_plan.Metres, PlayerPrefSettings.GameSpeed).ToString() }
                }, 8f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>"Test: cancel the taxi": as "Cancel it" on the phone.</summary>
        public void TestCancel()
        {
            if (_step == Step.Idle)
            {
                TestRefusals.TellKey(Logger, "Taxi", "reallife_taxi_test_no_taxi");
                return;
            }
            Tell(Cancel(), "reallife_taxi_cancelled_fee", "reallife_taxi_cancelled_free", NotificationType.Info);
        }

        /// <summary>Lets a taxi that is out go, with nothing charged and nothing said: the save tools' and the options page's clean-up.</summary>
        public void StopAll()
        {
            if (_step != Step.Idle)
            {
                Released("the mod's clean-up", TaxiCab.Leave.Unseen);
            }
        }
    }
}

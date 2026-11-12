using Localizor;
using RealLifeOverhaul.Insurance;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Insurance pages from the director and carries every button to it, or to the feature whose cover it is: the same calls the app in the game's frame
    /// makes, the same notices. A page is written when the director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class InsuranceLive : IVentSource, IInsureActions
    {
        public static readonly InsuranceLive Instance = new InsuranceLive();
        public const string ScreenId = "insurance";

        private readonly InsuranceFacts _facts = new InsuranceFacts();
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => InsuranceGame.Money((long)x));
        private int _local;

        private InsuranceLive()
        {
        }

        public int Generation => (InsuranceDirector.Instance != null ? InsuranceDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
            int want = InsuranceDirector.TakeWantedTab();
            if (want >= 0)
            {
                view.Tab = want;
                view.Pick = null;
            }
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            if (d == null || !d.Facts(_facts))
            {
                _facts.Ready = false;
            }
            InsurePages.Fill(m, v, _facts, this, _words);
        }

        private static void NoMoney()
        {
            InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_no_money", null);
        }

        private string CarName(string id)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            if (d != null && d.Facts(_facts))
            {
                for (int i = 0; i < _facts.Cars.Count; i++)
                {
                    if (_facts.Cars[i].Id == id)
                    {
                        return _facts.Cars[i].Name;
                    }
                }
            }
            return string.Empty;
        }

        private string HomeName(string place)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            HomePolicy p = d != null && d.Running ? d.State.Home(place) : null;
            if (p != null && !string.IsNullOrEmpty(p.Name))
            {
                return p.Name;
            }
            if (d != null && d.Facts(_facts))
            {
                for (int i = 0; i < _facts.Homes.Count; i++)
                {
                    if (_facts.Homes[i].Place == place)
                    {
                        return _facts.Homes[i].Name;
                    }
                }
            }
            return string.Empty;
        }

        private static void Changed(bool ok, string what, string tier, int charged)
        {
            if (!ok)
            {
                NoMoney();
                return;
            }
            InsuranceGame.Tell(NotificationType.Info, charged > 0 ? "reallife_ins_note_changed_paid" : "reallife_ins_note_changed",
                InsuranceGame.Data("what", what, "tier", tier, "amount", InsuranceGame.Money(charged)));
        }

        public void BuyCar(string id, int tier)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            string name = CarName(id);
            int charged = 0;
            if (d != null && d.BuyCar(id, tier, out charged))
            {
                InsuranceGame.Tell(NotificationType.Info, "reallife_ins_note_car_bought", InsuranceGame.Data("car", name, "tier", InsuranceText.CarTier(tier),
                    "amount", InsuranceGame.Money(charged), "days", InsuranceCatalogue.CarWaitDays.ToString()));
            }
            else
            {
                NoMoney();
            }
            _local++;
        }

        public void ChangeCar(string id, int tier)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            string name = CarName(id);
            int charged = 0;
            bool unsound = false;
            bool ok = d != null && d.ChangeCar(id, tier, out charged, out unsound);
            if (unsound)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_car_unsound", null);
            }
            else
            {
                Changed(ok, name, InsuranceText.CarTier(tier), charged);
            }
            _local++;
        }

        public void CancelCar(string id)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            string name = CarName(id);
            if (d != null && d.CancelCar(id))
            {
                InsuranceGame.Tell(NotificationType.Info, "reallife_ins_note_cancelled", InsuranceGame.Data("what", name));
            }
            _local++;
        }

        public void TakeHealth(int plan)
        {
            InsuranceBridges.TakeHealth?.Invoke(plan);   // Health takes the first month and says what happened
            _local++;
        }

        public void CancelHealth()
        {
            InsuranceBridges.CancelHealth?.Invoke();
            _local++;
        }

        public void BuyHome(string place, int tier)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            int charged = 0;
            if (d != null && d.BuyHome(place, tier, out charged))
            {
                InsuranceGame.Tell(NotificationType.Info, "reallife_ins_note_home_bought", InsuranceGame.Data("home", HomeName(place), "tier", InsuranceText.HomeTier(tier),
                    "amount", InsuranceGame.Money(charged), "days", InsuranceCatalogue.HomeWaitDays.ToString()));
            }
            else
            {
                NoMoney();
            }
            _local++;
        }

        public void ChangeHome(string place, int tier)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            int charged = 0;
            bool ok = d != null && d.ChangeHome(place, tier, out charged);
            Changed(ok, HomeName(place), InsuranceText.HomeTier(tier), charged);
            _local++;
        }

        public void CancelHome(string place)
        {
            InsuranceDirector d = InsuranceDirector.Instance;
            string name = HomeName(place);
            if (d != null && d.CancelHome(place))
            {
                InsuranceGame.Tell(NotificationType.Info, "reallife_ins_note_cancelled", InsuranceGame.Data("what", name));
            }
            _local++;
        }

        public void BuyBusiness(string place, int kind, int insurer)
        {
            InsuranceBridges.BuyBusiness?.Invoke(place, kind, insurer);   // the Company takes the premium by the day and says what was bought
            _local++;
        }

        public void CancelBusiness(string place, int kind)
        {
            string what = string.Empty;
            for (int i = 0; i < _facts.Business.Count; i++)
            {
                BizCoverFact b = _facts.Business[i];
                if (b.Place == place && b.Kind == kind)
                {
                    what = InsuranceText.Fill("reallife_ins_biz_title", "cover", b.KindName, "business", b.Name);
                }
            }
            InsuranceBridges.CancelBusiness?.Invoke(place, kind);
            InsuranceGame.Tell(NotificationType.Info, "reallife_ins_note_cancelled", InsuranceGame.Data("what", what));
            _local++;
        }

        public void FileClaim(int id)
        {
            InsuranceBridges.FileBusinessClaim?.Invoke(id);   // the Company says when it will be paid
            _local++;
        }
    }
}

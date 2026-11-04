using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    internal sealed partial class CompanyLive
    {
        /// <summary>
        /// Does what a press on the page stands for, through the director that already owns it: the same calls the old app's buttons made. Moving about is the screen's own and never
        /// comes here. A typed figure comes in C. False when it could not be done (nothing to act on, the part off); the director says why in the game's own notice.
        /// </summary>
        public bool Do(CompanyAct a, CompanyView view)
        {
            CompanyDirector d = D;
            if (d == null || !d.Running)
            {
                return false;
            }
            try
            {
                bool done = Run(d, a);
                if (done)
                {
                    Touch();
                }
                return done;
            }
            catch (Exception e)
            {
                CompanyDirector.Logger?.Error(e);
                return false;
            }
        }

        private static bool Run(CompanyDirector d, CompanyAct a)
        {
            return CompanyDoes.Run(d, Opens.Instance, a);   // the map from a press to the director's call is game-free: the checks run it against a recording director
        }

        // The numbers the actions carry, game-free (CompanyDoes), pinned to the director's own: a mismatch fails to compile.
        private const int PinNumbers = 1 / (CompanyDirector.AnswerAgree == CompanyDoes.Agree && CompanyDirector.AnswerHaggle == CompanyDoes.Haggle && CompanyDirector.PlanName == CompanyDoes.PlanName
            && CompanyDirector.LimitWage == CompanyDoes.LimitWage && CompanyDirector.LimitStock == CompanyDoes.LimitStock && CompanyDirector.LimitPriceLow == CompanyDoes.LimitPriceLow
            && CompanyDirector.LimitPriceHigh == CompanyDoes.LimitPriceHigh && CompanyDirector.LimitBudget == CompanyDoes.LimitBudget && CompanyDirector.LimitKeep == CompanyDoes.LimitKeep
            && CompanyDirector.LimitMarketing == CompanyDoes.LimitMarketing && CompanyDirector.LimitLease == CompanyDoes.LimitLease && CompanyDirector.PlanHours == 4 ? 1 : 0);

        // The other apps and the game's own screens a press opens.
        private sealed class Opens : ICompanyOpens
        {
            public static readonly Opens Instance = new Opens();

            public bool BizMan(string place)
            {
                CompanyShop shop = CompanyDirector.Instance != null ? CompanyDirector.Instance.ShopAt(place) : null;
                return shop != null && CompanyGame.OpenBizMan(shop.Reg);
            }

            public void Sale(string place) => CompanyGame.OpenSale(place);

            public void Cleaning() => CompanyGame.OpenCleaning();

            public void Bank() => CompanyGame.OpenBank();

            public void Stocks() => CompanyGame.OpenListing();

            public void Options(string section)
            {
                if (Meta.SettingsApp.Installed)
                {
                    Meta.SettingsApp.Open("s:" + section);
                }
            }
        }
    }
}

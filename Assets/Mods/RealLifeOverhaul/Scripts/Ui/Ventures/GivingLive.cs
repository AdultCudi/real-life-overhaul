using System;
using Localizor;
using RealLifeOverhaul.Giving;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Giving pages from the director and carries every button to it: the same calls the app in the game's frame makes, the same notices. A page is written when the director's
    /// revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class GivingLive : IVentSource, IGiveActions
    {
        public static readonly GivingLive Instance = new GivingLive();
        public const string ScreenId = "giving";

        private readonly GiveFacts _facts = new GiveFacts();
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => GivingGame.Money((long)x));
        private int _local;

        private GivingLive()
        {
        }

        public int Generation => (GivingDirector.Instance != null ? GivingDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            GivingDirector d = GivingDirector.Instance;
            if (d == null || !d.Facts(_facts))
            {
                _facts.Ready = false;
            }
            GivePages.Fill(m, v, _facts, this, _words);
        }

        public void Give(int charity, int step)
        {
            GivingDirector d = GivingDirector.Instance;
            if (d == null)
            {
                return;
            }
            int amount = GivingRules.StepNow(step, GivingGame.Level);
            GiveResult r = d.GiveToCharity((Charity)Math.Max(0, Math.Min((int)Charity.Count - 1, charity)), amount);
            if (GivingResults.Moved(r))
            {
                GivingGame.Tell(NotificationType.Info, "reallife_giving_gave", GivingGame.Data("amount", GivingGame.Money(amount), "charity", GivingText.Charity(charity)));
            }
            else
            {
                GivingDirector.Say(r);
            }
            _local++;
        }

        public void Monthly(int charity, int step)
        {
            GivingDirector d = GivingDirector.Instance;
            if (d == null)
            {
                return;
            }
            bool set = d.SetStanding((Charity)Math.Max(0, Math.Min((int)Charity.Count - 1, charity)), step);
            GivingGame.Tell(set ? NotificationType.Info : NotificationType.Warning, set ? "reallife_giving_monthly_set" : "reallife_giving_monthly_full",
                GivingGame.Data("amount", GivingGame.Money(GivingRules.StepNow(step, GivingGame.Level)), "charity", GivingText.Charity(charity)));
            _local++;
        }

        public void StopMonthly(int charity)
        {
            GivingDirector d = GivingDirector.Instance;
            if (d != null && d.CancelStanding((Charity)Math.Max(0, Math.Min((int)Charity.Count - 1, charity))))
            {
                GivingGame.Tell(NotificationType.Info, "reallife_giving_monthly_stopped", GivingGame.Data("charity", GivingText.Charity(charity)));
            }
            _local++;
        }
    }
}

using Localizor;
using RealLifeOverhaul.Dynasty;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Legacy pages from the director and carries every button to it: the same calls the app in the game's frame makes, the same notices. A page is written when the
    /// director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class DynastyLive : IVentSource, IDynastyActions
    {
        public static readonly DynastyLive Instance = new DynastyLive();
        public const string ScreenId = "dynasty";

        private readonly DynastyFacts _facts = new DynastyFacts();
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => DynastyGame.Money((long)x));
        private int _local;

        private DynastyLive()
        {
        }

        public int Generation => (DynastyDirector.Instance != null ? DynastyDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            DynastyDirector d = DynastyDirector.Instance;
            if (d == null || !d.Facts(_facts, v.Tab == DynastyPages.TabEstate))
            {
                _facts.Ready = false;
            }
            DynastyPages.Fill(m, v, _facts, this, _words);
        }

        public void Heir(KinKind kind, int id, string name)
        {
            DynastyDirector.Instance?.DraftHeir(kind, id, name);
            _local++;
        }

        public void Share(KinKind kind, int id, string name, int steps)
        {
            DynastyDirector.Instance?.DraftShare(kind, id, name, steps);
            _local++;
        }

        public void RetireAge(int steps)
        {
            DynastyDirector.Instance?.DraftRetireAge(steps);
            _local++;
        }

        public void Keep(int steps)
        {
            DynastyDirector.Instance?.DraftKeep(steps);
            _local++;
        }

        public void Discard()
        {
            DynastyDirector.Instance?.DraftDiscard();
            _local++;
        }

        public void Sign()
        {
            DynastyDirector d = DynastyDirector.Instance;
            if (d == null)
            {
                return;
            }
            WillWhy why = d.Sign(out long fee);
            if (why == WillWhy.Ok)
            {
                DynastyGame.Tell(NotificationType.Success, "reallife_dynasty_signed", DynastyGame.Data("fee", DynastyGame.Money(fee)));
            }
            else
            {
                DynastyGame.Tell(NotificationType.Warning, DynastyText.KeyOf(why), null);
            }
            _local++;
        }

        /// <summary>Retiring is asked once more in the game's own question box, which says what it means; a yes begins the handover, and a no changes nothing.</summary>
        public void Retire()
        {
            DynastyDirector d = DynastyDirector.Instance;
            if (d == null)
            {
                return;
            }
            RetireWhy why = d.RetireWhyNow(out KinPerson _, out bool _);
            if (why != RetireWhy.Ok)
            {
                DynastyGame.Tell(NotificationType.Warning, DynastyText.KeyOf(why), null);
                _local++;
                return;
            }
            GameConfirm.Show("reallife_dynasty_retire_ask_title", "reallife_dynasty_retire_ask", Confirmed, null, "reallife_dynasty_retire_ask_yes", "reallife_dynasty_retire_ask_no", false, true);
            _local++;
        }

        private void Confirmed()
        {
            try
            {
                DynastyDirector d = DynastyDirector.Instance;
                RetireWhy why = d != null ? d.Retire() : RetireWhy.Off;
                if (why != RetireWhy.Ok)
                {
                    DynastyGame.Tell(NotificationType.Warning, DynastyText.KeyOf(why), null);
                }
                _local++;
            }
            catch (System.Exception e)
            {
                DynastyDirector.Logger?.Error(e);
            }
        }
    }
}

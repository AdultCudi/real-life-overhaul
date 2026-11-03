namespace RealLifeOverhaul.Ui
{
    /// <summary>The Economy app on the kit. The old Outlook page stays for the switch. Nothing a frame.</summary>
    public sealed class EconomyScreen : NewsHost, IUiScreen
    {
        public const string ScreenId = "economy";

        public EconomyScreen(INewsSource source) : base(ScreenId, NewsApp.Economy, UiIcon.Chart, source) { }
    }
}

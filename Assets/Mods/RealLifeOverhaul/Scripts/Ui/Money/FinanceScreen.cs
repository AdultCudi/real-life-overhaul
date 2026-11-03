namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Finance on the kit: your position, then the lines behind it. The old page stays underneath for the switch.
    /// Nothing a frame; the page is laid again when its numbers or its tab change.
    /// </summary>
    public sealed class FinanceScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "finance";

        public FinanceScreen(IMoneySource source) : base(ScreenId, MoneyApp.Finance, UiIcon.Wallet, source) { }
    }
}

namespace RealLifeOverhaul.Ui
{
    /// <summary>One bank's counter on the kit. Nothing a frame.</summary>
    public sealed class BanksScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "banks";

        public BanksScreen(IMoneySource source) : base(ScreenId, MoneyApp.Banks, UiIcon.Bank, source) { }
    }
}

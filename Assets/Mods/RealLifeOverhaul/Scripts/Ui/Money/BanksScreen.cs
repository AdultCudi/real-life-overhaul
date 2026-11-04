namespace RealLifeOverhaul.Ui
{
    /// <summary>One bank's counter on the kit: the bank picked, six tabs (Accounts, Loans, Overdraft, Card, Apply, Rating), each its own page of real figures. Nothing a frame.</summary>
    public sealed class BanksScreen : MoneyHost, IUiScreen, IUiTypingSubmit
    {
        public const string ScreenId = "banks";

        public BanksScreen(IMoneySource source) : base(ScreenId, MoneyApp.Banks, UiIcon.Bank, source) { }
    }
}

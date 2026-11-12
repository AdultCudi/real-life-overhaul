namespace RealLifeOverhaul.Ui
{
    /// <summary>The bank the player owns, on the kit: five tabs (Bank, Savers, Lending, Fund, Branches), each its own page of the bank's real books. Nothing a frame.</summary>
    public sealed class OwnBankScreen : MoneyHost, IUiScreen, IUiTypingSubmit
    {
        public const string ScreenId = "own_bank";

        public OwnBankScreen(IMoneySource source) : base(ScreenId, MoneyApp.OwnBank, UiIcon.PiggyBank, source) { }
    }
}

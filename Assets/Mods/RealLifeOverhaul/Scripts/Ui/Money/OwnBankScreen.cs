namespace RealLifeOverhaul.Ui
{
    /// <summary>The bank the player owns, on the kit. Nothing a frame.</summary>
    public sealed class OwnBankScreen : MoneyHost, IUiScreen
    {
        public const string ScreenId = "own_bank";

        public OwnBankScreen(IMoneySource source) : base(ScreenId, MoneyApp.OwnBank, UiIcon.PiggyBank, source) { }
    }
}

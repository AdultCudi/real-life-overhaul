namespace RealLifeOverhaul.Economy
{
    public sealed partial class CreditDesk
    {
        /// <summary>Additional credit accounts, absent from GameInstance.Loans. A read never collects a payment.</summary>
        public CompanionModule CompanionCredit()
        {
            if (_file == null)
            {
                return new CompanionModule("unavailable");
            }
            return new CompanionModule("ready", CreditCompanion.Accounts(_file.Accounts, Today));
        }
    }
}

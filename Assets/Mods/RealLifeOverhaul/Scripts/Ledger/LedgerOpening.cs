namespace RealLifeOverhaul.Ledger
{
    /// <summary>A business that opened in the city on a day: its name as the game shows it, and whether it is a rival's (else the city's). The facts of a story.</summary>
    public struct LedgerOpening
    {
        public string Firm;
        public int Day;
        public bool Rival;
    }
}

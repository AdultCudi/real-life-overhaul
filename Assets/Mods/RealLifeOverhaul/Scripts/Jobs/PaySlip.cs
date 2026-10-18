namespace RealLifeOverhaul
{
    /// <summary>
    /// What a piece of work paid, line by line; game-free, compiled by CatalogueCheck. The total is never below zero, so a
    /// bad shift cannot cost the player money they had.
    /// </summary>
    public struct PaySlip
    {
        public int Units;      // fares, parcels or flyers this covers
        public int Base;
        public int Distance;
        public int Tips;
        public int Bonus;
        public int Penalty;

        public int Total
        {
            get
            {
                int total = Base + Distance + Tips + Bonus - Penalty;
                return total > 0 ? total : 0;
            }
        }

        public void Add(in PaySlip other)
        {
            Units += other.Units;
            Base += other.Base;
            Distance += other.Distance;
            Tips += other.Tips;
            Bonus += other.Bonus;
            Penalty += other.Penalty;
        }

        /// <summary>The same slip with its penalty cut to what it could take off, so a slip of nothing stays nothing when added to another.</summary>
        public PaySlip Bounded()
        {
            PaySlip slip = this;
            int most = slip.Base + slip.Distance + slip.Tips + slip.Bonus;
            if (slip.Penalty > most)
            {
                slip.Penalty = most > 0 ? most : 0;
            }
            return slip;
        }
    }
}

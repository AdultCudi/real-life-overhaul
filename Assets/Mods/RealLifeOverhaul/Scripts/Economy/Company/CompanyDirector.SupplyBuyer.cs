namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the supply contracts (Scripts/Suppliers) ask of the managers: whether somebody decides about stock at a place today, and a manager's budget for the first
    /// delivery of a contract that manager signs. Asked on a day's or an hour's pass or a press, never per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        /// <summary>
        /// True where a manager answers stock at a place and somebody is at work to decide today: the place's manager, or the district's regional manager standing in for one
        /// off sick. A manager off sick with nobody to stand in decides nothing that day, as for every other kind of decision (<see cref="Decider"/>).
        /// </summary>
        internal bool DecidesStock(string place)
        {
            if (!Manages(place, ManagerTask.Stock))
            {
                return false;
            }
            ManagedPlace mp = ManagedAt(place);
            return mp != null && Decider(mp, out _, out _, out _);
        }

        /// <summary>
        /// The place's manager spends so much (whole money) of this month's budget on a stock order of the supply contracts: true, with the sum added to what they have spent,
        /// when it fits what is left of the budget; false, nothing spent, when it does not. The budget is the one that limits their overtime and hires.
        /// </summary>
        internal bool SpendStock(string place, long money)
        {
            ManagedPlace mp = ManagedAt(place);
            ManagerFile m = mp != null ? mp.Own : null;
            if (m == null || money < 0 || money > int.MaxValue / 4)
            {
                return false;
            }
            if (money > ManagerRules.Left(BudgetOf(m), m.Spent))
            {
                return false;
            }
            m.Spent += (int)money;
            return true;
        }
    }
}

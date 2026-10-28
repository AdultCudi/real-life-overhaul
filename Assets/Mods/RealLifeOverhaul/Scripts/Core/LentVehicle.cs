using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A vehicle the mod lends the player (the driving school's car, a job van) is on the save's vehicle list, so the
    /// game's hourly parking round would ticket it. Its lender calls <see cref="KeepFree"/> on its slow tick: it is kept
    /// legally parked with nothing owed, and a ticket written between two ticks is paid back at once, with what city
    /// policy adds to every ticket (Civic/CivicEffects). COST: a few field writes a second while lent.
    /// </summary>
    public static class LentVehicle
    {
        public const float Ticket = 125f;   // what the game's parking round charges (Helpers.ParkingSimulator.RunHourly)
        public const string RefundKey = "reallife_transaction_lent_ticket_refund";   // the cost of living counts it as a ticket not paid (Wiring/EconomyLinks)
        private const string PolicyKey = "reallife_transaction_lent_ticket_policy";

        /// <summary><paramref name="ticketsSeen"/>: the ticket count the lender last saw on this vehicle, 0 when it was made.</summary>
        public static void KeepFree(VehicleInstance instance, ref int ticketsSeen)
        {
            if (instance == null)
            {
                return;
            }
            if (instance.parkingState != ParkingState.Legal)
            {
                instance.parkingState = ParkingState.Legal;
            }
            if (instance.unpaidParkingAmount != 0f)
            {
                instance.unpaidParkingAmount = 0f;
            }
            int tickets = instance.parkingTickets != null ? instance.parkingTickets.Count : 0;
            if (tickets > ticketsSeen)
            {
                // The game's charge, under its parking fees so the day's statement nets it out; city policy's part apart.
                int lent = tickets - ticketsSeen;
                RealLifeOverhaul.Economy.BizBooks.Book(Ticket * lent, new TransactionInfo(RefundKey, "ba:transactioncategory_parkingfees"), null, null, force: true);
                int policy = CivicPolicy.TicketExtra;
                if (policy != 0)
                {
                    RealLifeOverhaul.Economy.BizBooks.Book(policy * lent, new TransactionInfo(PolicyKey), null, null, force: true);
                }
            }
            ticketsSeen = tickets;
        }
    }
}

using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A vehicle the mod lends the player (the driving school's car, a hired job van) is on the save's vehicle list while it
    /// is out, so the game's hourly parking round would ticket it like one of the player's own. It is not theirs to park:
    /// whoever lends it calls <see cref="KeepFree"/> on its slow tick. The vehicle is kept legally parked with nothing owed,
    /// and a ticket the game wrote all the same (its round can fall between two ticks) is paid back at once, under its own
    /// name in the player's transactions: the game's charge and what city policy adds to (or takes off) a ticket, which
    /// civic life charges on every ticket the game counts (Civic/CivicEffects). A few field writes a second while a
    /// vehicle is lent; nothing otherwise.
    /// </summary>
    public static class LentVehicle
    {
        public const float Ticket = 125f;   // what the game's parking round charges (Helpers.ParkingSimulator.RunHourly)
        public const string RefundKey = "reallife_transaction_lent_ticket_refund";   // the city's cost of living counts it as the game's ticket not paid (Wiring/EconomyLinks)
        private const string PolicyKey = "reallife_transaction_lent_ticket_policy";

        /// <summary><paramref name="ticketsSeen"/>: the count the lender last saw on this vehicle (0 when it was made).</summary>
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
                // The game's ticket as the game took it, and apart what city policy added to it or took off.
                int lent = tickets - ticketsSeen;
                // (Under the game's parking fees, as the ticket was: the day's statement nets them out.)
                GameManager.ChangeMoneySafe(Ticket * lent, new TransactionInfo(RefundKey, "ba:transactioncategory_parkingfees"), null, null, force: true);
                int policy = CivicPolicy.TicketExtra;
                if (policy != 0)
                {
                    GameManager.ChangeMoneySafe(policy * lent, new TransactionInfo(PolicyKey), null, null, force: true);
                }
            }
            ticketsSeen = tickets;
        }
    }
}

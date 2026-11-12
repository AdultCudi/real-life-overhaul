using System;
using UI.Notification;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise walk to a desk for. A test shift is driven where the player
    /// stands in their own vehicle near by, whatever its seats, condition or their licences, and counts for nothing: no level,
    /// no strike, none of the desk's day. Each says what happened in a notice and writes one "Side jobs check" line.
    /// </summary>
    public sealed partial class SideJobDirector
    {
        private bool _onTest;      // the shift that is on was started by a test button: its fares are not put on the driver's figures

        private bool TestReady()
        {
            if (!Running)
            {
                SideJobGame.Tell(NotificationType.Warning, "reallife_job_desk_test_off", 8f);
                return false;
            }
            return true;
        }

        /// <summary>Options, Tests: a cab shift (true) or a delivery shift (false) signed on for where the player stands, with nothing asked of the vehicle but that it is theirs and near.</summary>
        public void TestSignOn(bool cab)
        {
            if (!TestReady())
            {
                return;
            }
            try
            {
                SideKind kind = cab ? SideKind.Cab : SideKind.Parcels;
                CarController car = SideJobBridges.Car(SideJobRules.CarWithin);
                SideCar facts = SideJobGame.Read(car, out string name, out _);
                if (!facts.Found || !facts.Motor)
                {
                    SideJobGame.Tell(NotificationType.Warning, "reallife_job_desk_no_car", 9f, "metres", SideJobRules.CarWithin.ToString("0", Inv));
                    Logger?.Info("Side jobs check (test, " + kind + "): no motor vehicle of the player's own within " + SideJobRules.CarWithin.ToString("0", Inv) + " m: not started.");
                    return;
                }
                if (SideJobBridges.OnDeskShift())
                {
                    SideJobBridges.CallOff();
                }
                if (_on)
                {
                    SignedOff();
                }
                int drops = Math.Max(2, SideJobRules.Drops(facts.Cargo, false));
                bool begun = Begin(kind, car, new DeskShift
                {
                    Cab = cab, Seats = facts.Seats, Drops = drops, Vehicle = null, Test = true, Left = cab ? _leftCab : _leftParcels, Booked = cab ? _bookCab : _bookParcels
                });
                Logger?.Info("Side jobs check (test, " + kind + "): '" + name + "' (" + facts.Seats + " seats, room for " + facts.Cargo + ": " + drops + " drop(s) a run, condition " + facts.Condition
                             + "%, fuel " + facts.Fuel + "%): " + (begun ? "the shift is on in desk mode: PASS" : "the jobs' director refused it (the notice says why)"));
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        /// <summary>Options, Tests: the two desks, the driver's figures and each firm's books of the day before, in Player.log, with the cab office's in a notice.</summary>
        public void TestBooks()
        {
            if (!TestReady())
            {
                return;
            }
            try
            {
                for (int k = 0; k < 2; k++)
                {
                    var kind = (SideKind)k;
                    SideBooks books = Books(kind, _state.Day - 1);
                    SideBooks again = Books(kind, _state.Day - 1);
                    SideTally t = _state.At(kind);
                    bool same = books.Drivers == again.Drivers && books.Gross == again.Gross && books.Kept == again.Kept && books.Best == again.Best;
                    Logger?.Info("Side jobs check (books, " + kind + "): desk " + (_desks.Stands(kind) ? "stands outside '" + _desks.Shop(kind) + "', " + _desks.Address(kind) : "does not stand")
                                 + "; the driver today: " + t.UnitsToday + " unit(s), " + t.NetToday + " cleared, " + t.FuelToday + " of fuel, " + _state.Left(kind) + " left; the day before: "
                                 + books.Drivers + " other driver(s), " + books.Units + " unit(s), customers paid " + books.Gross + ", the firm kept " + books.Kept + ", the best cleared "
                                 + books.Best + "; asked twice, the same: " + (same && books.Kept <= books.Gross && books.Drivers > 0 ? "PASS" : "FAIL"));
                }
                SideBooks cab = Books(SideKind.Cab, _state.Day - 1);
                SideJobGame.Tell(NotificationType.Info, "reallife_job_desk_test_books_note", 12f, "drivers", cab.Drivers.ToString(Inv), "gross", JobText.Money(cab.Gross),
                    "kept", JobText.Money(cab.Kept), "best", JobText.Money(cab.Best));
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }
    }
}

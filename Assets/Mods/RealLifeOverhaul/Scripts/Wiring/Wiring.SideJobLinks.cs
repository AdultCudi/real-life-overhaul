using RealLifeOverhaul.Economy;
using RealLifeOverhaul.SideJobs;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The taxi and delivery shifts (SideJobs/SideJobBridges) with the rest of the mod:
        ///
        ///   PlacesReady, Place   the help-wanted boards (Jobs/JobBoards) and the board the employers' ladder gave each firm
        ///                        (CareerDirector.DepotOf): Five Borough Cabs' is the cab office, Hudson Parcel Co.'s the depot
        ///   Car                  the driver's own vehicle, as every driving job finds it (JobDirector.NearestCar: never
        ///                        another Going Public player's)
        ///   Start, OnDeskShift,
        ///   Busy, SignOff,
        ///   CallOff              the jobs' director drives the shift as its ride-hail or courier job in desk mode
        ///   PumpCents            the game's pump price, as the delivery trips read it (Economy/Logistics)
        ///
        /// Asked once a second at most, or on a press; never per frame. A director that is not there answers as nothing to offer.
        /// </summary>
        public static class SideJobLinks
        {
            internal static void Connect()
            {
                SideJobBridges.PlacesReady = PlacesReady;
                SideJobBridges.Place = Place;
                SideJobBridges.Car = within => JobDirector.NearestCar(within);
                SideJobBridges.Start = Start;
                SideJobBridges.OnDeskShift = () => JobDirector.Instance != null && JobDirector.Instance.OnDeskShift;
                SideJobBridges.Busy = () => JobDirector.TakesInteractKey || (JobDirector.OnShift && !JobDirector.Instance.OnDeskShift);
                SideJobBridges.SignOff = () => JobDirector.Instance?.Quit();
                SideJobBridges.CallOff = () => JobDirector.Instance?.QuitDesk();
                SideJobBridges.PumpCents = LogisticsGame.PumpCents;
            }

            internal static void Disconnect()
            {
                SideJobBridges.Reset();
            }

            private static bool PlacesReady()
            {
                JobDirector jobs = JobDirector.Instance;
                return jobs != null && jobs.HelpBoards != null && jobs.HelpBoards.Searched && jobs.Career != null && jobs.Career.Mapped;
            }

            private static SidePlace Place(SideKind kind)
            {
                JobDirector jobs = JobDirector.Instance;
                if (jobs == null || jobs.HelpBoards == null || jobs.Career == null)
                {
                    return default;
                }
                JobBoards boards = jobs.HelpBoards;
                int board = jobs.Career.DepotOf(CareerCatalogue.IndexOf(kind == SideKind.Cab ? CareerCatalogue.Cab : CareerCatalogue.Courier));
                if (board < 0 || !boards.Stands(board))
                {
                    return default;
                }
                return new SidePlace
                {
                    Known = true, BoardFront = boards.Front(board), BoardAt = boards.At(board), Outward = boards.Outward(board), Building = boards.Building(board),
                    Address = boards.Address(board), Shop = boards.Place(board)
                };
            }

            private static string Start(SideKind kind, DeskShift desk)
            {
                JobDirector jobs = JobDirector.Instance;
                if (jobs == null)
                {
                    return "reallife_job_no_off";
                }
                JobDef def = JobCatalogue.Find(kind == SideKind.Cab ? JobCatalogue.Taxi : JobCatalogue.Courier);
                return jobs.TakeDesk(def, desk, out string reason) ? null : reason ?? "reallife_job_no_now";
            }
        }
    }
}

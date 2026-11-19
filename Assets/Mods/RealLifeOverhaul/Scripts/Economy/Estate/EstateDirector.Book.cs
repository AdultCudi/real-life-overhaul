using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The agency's book of other owners' blocks in the director (AgencyBookRules): the day's hours shared out, offers decided, the blocks' days, the month's fees, and the
    /// player's own buildings put under the agency. Every sum of money is paid here from an event the rules made, once for each: a month's fee when the market's month turns
    /// (the rules refuse the same month twice), a letting fee on the day of the letting, a fine on the day of the inspection. Runs in the day event and on the player's
    /// answers from the app: nothing a frame.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private readonly List<BlockEvent> _blockEvents = new List<BlockEvent>(8);
        private readonly List<AgencyLandlord> _landlords = new List<AgencyLandlord>(16);

        /// <summary>The agency is open and the property part runs: pitches can be made and the player's own buildings put under it.</summary>
        public bool AgencyOpen => On(EstatePart.Property) && _state != null && _state.Agency.Running;

        // The rival landlords of the save as the day's read of the city's rents has them (none before the first read), for the offers' owners and the rival agencies.
        private IList<AgencyLandlord> Landlords()
        {
            _landlords.Clear();
            RentMapData rents = Rents;
            for (int i = 0; rents != null && i < rents.Landlords.Count; i++)
            {
                RentLandlord l = rents.Landlords[i];
                if (!string.IsNullOrEmpty(l.Id) && l.Units > 0)
                {
                    _landlords.Add(new AgencyLandlord { Id = l.Id, Units = l.Units, Homes = l.Homes });
                }
            }
            return _landlords;
        }

        // The units of the player's own buildings under the agency, for the agents' hours.
        private int OwnUnitsUnderAgency()
        {
            int units = 0;
            for (int i = 0; Duties && i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                units += h.Managed && h.Manager == (int)EstateManager.Agency ? h.Units.Count : 0;
            }
            return units;
        }

        // The hours shared out and each building under the agency told whether it is covered today. Before the buildings' mornings, and again after the offers are decided.
        private void AgencyCover()
        {
            AgencyFile agency = _state.Agency;
            int own = agency.Running ? OwnUnitsUnderAgency() : 0;
            AgencyBookRules.Allocate(agency, own);
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                bool covered = Duties && agency.Running && h.Managed && h.Manager == (int)EstateManager.Agency && !agency.Book.OwnPaused;
                if (h.Manager == (int)EstateManager.Agency && h.Covered != covered && Verbose)
                {
                    Logger?.Info("Estate check (own agency, " + h.Name + "): " + (covered ? "the agents cover it, run as a property manager runs it, no fee" : "the agents cannot cover it today")
                                 + "; " + agency.Capacity / 10f + " agent's hour(s) a day against " + agency.Reserved / 10f + " taken: PASS");
                }
                h.Covered = covered;
            }
        }

        // The agency is shut or its office is gone: the blocks lapse with nothing paid, and the buildings that were under it are the player's to run again.
        private void AgencyGone()
        {
            AgencyFile agency = _state.Agency;
            bool any = false;
            for (int i = 0; i < agency.Book.Blocks.Count && !any; i++)
            {
                any = agency.Book.Blocks[i].Stage != (int)BlockStage.Lost;
            }
            bool own = false;
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                if (h.Manager == (int)EstateManager.Agency)
                {
                    h.Manager = (int)EstateManager.Self;
                    own = true;
                }
                h.Covered = false;
            }
            if (any)
            {
                AgencyBookRules.CloseAll(agency);
            }
            agency.Reserved = 0;
            if ((any || own) && Verbose)
            {
                Logger?.Info("Estate check (agency book): the agency is shut, so its blocks lapse with nothing paid for the running month and the player's own buildings are his to run again: PASS");
            }
        }

        // The book's day, inside the agency's: the offers decided, the hours shared out, each block's day, the month's fees, and a new call for pitches.
        private void BookDay(int day)
        {
            AgencyFile agency = _state.Agency;
            AgencyBook book = agency.Book;
            _blockEvents.Clear();
            AgencyBookRules.Decide(agency, day, _blockEvents);
            AgencyCover();
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                AgencyBookRules.BlockDay(agency, book.Blocks[i], _state.Seed, day, _climate, _blockEvents);
            }
            AgencyBookRules.MonthEnd(agency, _state.Months, _blockEvents);
            ManagedBlock arrived = AgencyBookRules.Draw(agency, Landlords(), _state.Seed, day, _climate, _state.Real);
            if (arrived != null)
            {
                Tell(NotificationType.Info, "reallife_estate_note_block_offer", Data("owner", AgencyText.Owner(arrived.Owner), "units", arrived.Units.ToString(), "district",
                    EstateText.District(arrived.District)), TabAgency, "blk:" + arrived.Id);
            }
            BlockEvents(day);
        }

        // What the rules' events are to the books and to the player: each paid, charged or told once, then forgotten. Called with the events of one pass in _blockEvents.
        private void BlockEvents(int day)
        {
            AgencyFile agency = _state.Agency;
            AgencyBook book = agency.Book;
            int fees = 0, feeBlocks = 0, letFees = 0, lettings = 0;
            for (int i = 0; i < _blockEvents.Count; i++)
            {
                BlockEvent e = _blockEvents[i];
                ManagedBlock b = book.Find(e.Id);
                string name = b != null ? AgencyText.Block(b) : string.Empty;
                string office = agency.Running ? agency.Office : null;
                switch (e.Kind)
                {
                    case BlockEvent.Fee:
                        Pay(e.Amount, EstateGame.ManagementFeeKey, name, office, "a month's share of the rent collected at a block it manages");
                        fees += e.Amount;
                        feeBlocks++;
                        break;
                    case BlockEvent.Letting:
                        Pay(e.Amount, EstateGame.LettingFeeKey, name, office, "the fee for flats of a block it let again");
                        letFees += e.Amount;
                        lettings++;
                        break;
                    case BlockEvent.Fined:
                        Cost(e.Amount, EstateGame.FineKey, name, office, "the city's fine after a block failed its inspection", true);
                        Tell(NotificationType.Warning, "reallife_estate_note_block_fined", Data("block", name, "amount", EstateText.Money(e.Amount)), TabAgency, "blk:" + e.Id);
                        break;
                    case BlockEvent.Penalty:
                        Tell(NotificationType.Warning, "reallife_estate_note_block_penalty", Data("block", name, "amount", EstateText.Money(e.Amount)), TabAgency, "blk:" + e.Id);
                        break;
                    case BlockEvent.Won:
                        Tell(NotificationType.Success, "reallife_estate_note_block_won", Data("owner", AgencyText.Owner(b != null ? b.Owner : string.Empty), "block", name), TabAgency, "blk:" + e.Id);
                        break;
                    case BlockEvent.LostToRival:
                        Tell(NotificationType.Warning, "reallife_estate_note_block_lost_rival", Data("block", name, "agency", AgencyText.Agency(e.Name)), TabAgency, "blk:" + e.Id);
                        break;
                    case BlockEvent.Dropped:
                        Tell(NotificationType.Error, "reallife_estate_note_block_dropped", Data("owner", AgencyText.Owner(b != null ? b.Owner : string.Empty), "block", name,
                            "why", b != null ? AgencyText.Why(b) : string.Empty), TabAgency, "blk:" + e.Id);
                        break;
                }
                if (Verbose)
                {
                    Logger?.Info("Estate check (agency book, block " + e.Id + ", day " + day + "): event " + e.Kind + ", " + e.Amount + ": PASS");
                }
            }
            if (fees > 0)
            {
                Tell(NotificationType.Success, "reallife_estate_note_block_fees", Data("amount", EstateText.Money(fees), "count", feeBlocks.ToString()), TabAgency, null);
            }
            if (Verbose && (fees > 0 || letFees > 0))
            {
                Logger?.Info("Estate check (agency book, day " + day + "): " + fees + " in management fees from " + feeBlocks + " block(s) and " + letFees + " for " + lettings
                             + " letting(s), each paid once; " + book.Count(BlockStage.Held) + " block(s) held, " + book.Count(BlockStage.Offered) + " offer(s) open: PASS");
            }
            _blockEvents.Clear();
        }

        /// <summary>The player pitches for an offer, at the owner's asking fee or undercutting it. What to say is in the notice.</summary>
        public PitchResult PitchBlock(int id, bool undercut)
        {
            if (!On(EstatePart.Property))
            {
                return PitchResult.NoAgency;
            }
            EstateGame.ReadAgency(_state.Agency);
            AgencyCover();
            PitchResult result = AgencyBookRules.Pitch(_state.Agency, id, undercut);
            if (result != PitchResult.Pitched)
            {
                Tell(NotificationType.Warning, PitchWhy(result), null, TabAgency, "blk:" + id);
                return result;
            }
            ManagedBlock b = _state.Agency.Book.Find(id);
            Logger?.Info("Estate: the agency pitched for the " + (b != null ? b.Units : 0) + " flats of " + (b != null ? b.Owner : "?") + " at " + (undercut ? AgencyBookCatalogue.UndercutPct : 100)
                         + "% of the asking fee; the owner chooses on day " + (b != null ? b.Until : 0) + ".");
            Changed();
            return result;
        }

        private static string PitchWhy(PitchResult result)
        {
            switch (result)
            {
                case PitchResult.NoAgents: return "reallife_estate_why_no_agents";
                case PitchResult.NoHours: return "reallife_estate_why_no_hours";
                case PitchResult.Full: return "reallife_estate_why_blocks_full";
                case PitchResult.Already: return "reallife_estate_why_pitched";
                case PitchResult.NoAgency: return "reallife_estate_why_no_agency";
                default: return "reallife_estate_why_offer_gone";
            }
        }

        /// <summary>
        /// Show on map for a block tied to a real building of its owner's: the game's own destination marker, as the Careers pages' button sets one, and a notice saying where it is.
        /// </summary>
        public bool ShowBlock(int id)
        {
            ManagedBlock b = _state != null ? _state.Agency.Book.Find(id) : null;
            var address = b != null && b.Place.Length > 0 ? EstateGame.AddressOf(b.Place) : null;
            if (address == null || !Careers.DeskGame.Route(address.streetName, address.streetNumber))
            {
                Notifications.ShowError("reallife_career_why_route");
                return false;
            }
            Notifications.Show(NotificationType.Success, "reallife_estate_block_shown", Data("block", AgencyText.Block(b), "address", EstateGame.NameOf(b.Place)));
            return true;
        }

        public void WithdrawPitch(int id)
        {
            if (On(EstatePart.Property) && AgencyBookRules.Withdraw(_state.Agency.Book, id))
            {
                Changed();
            }
        }

        /// <summary>The player hands a block back to its owner. Nothing is owed: the running month's fee is not paid.</summary>
        public void ResignBlock(int id)
        {
            if (On(EstatePart.Property) && AgencyBookRules.Resign(_state.Agency, id))
            {
                AgencyCover();
                Changed();
            }
        }
    }
}

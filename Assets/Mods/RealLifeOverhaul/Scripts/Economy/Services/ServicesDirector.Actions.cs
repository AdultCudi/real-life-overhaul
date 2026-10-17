using System;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the cleaning service's pages ask of the director, and what the player changes there: the round's businesses,
    /// how each is served, the crew, the van and contracts. Each change runs once, on a press, and plans the round again.
    /// </summary>
    public sealed partial class ServicesDirector
    {
        /// <summary>The service's own cost a week: the crew's wages for their hours in the game, and the van over the last seven days.</summary>
        public int ServiceWeek(out int wages, out int van)
        {
            long sum = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                sum += (long)_crew[i].Wage * _crew[i].WeekHours / 100;
            }
            wages = (int)Math.Min(int.MaxValue / 4, sum);
            van = ServicesState.Week(_state.VanCost);
            return wages + van;
        }

        /// <summary>What an average member of the crew takes off a floor of this force in an hour; the contracts' reference cleaner while there is no crew.</summary>
        public int CrewRate(int force)
        {
            long sum = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                sum += CleaningRules.Rate(_crew[i].Skill, _crew[i].Mood, force);
            }
            return _crew.Count > 0 ? (int)(sum / _crew.Count) : ServicesCatalogue.ContractSkillRef / Math.Max(1, force);
        }

        /// <summary>The minutes of cleaning a day a client needs from such a cleaner; 0 until a day has been measured.</summary>
        public int NeedMinutes(CleaningClient c) => CleaningRules.NeedMinutes(c.NeedDay, CrewRate(c.Force));

        /// <summary>What a cleaner of its own at each of the player's shops on the round would cost a week, at the wage the game offers one as good as the crew.</summary>
        public int OwnWeek()
        {
            int skill = 0;
            for (int i = 0; i < _crew.Count; i++)
            {
                skill += _crew[i].Skill;
            }
            int wage = ServicesGame.GoingWage(_crew.Count > 0 ? skill / _crew.Count : 50);
            long sum = 0;
            for (int i = 0; i < _state.Clients.Count; i++)
            {
                sum += _state.Clients[i].Outside ? 0 : CleaningRules.OwnWeek(NeedMinutes(_state.Clients[i]), wage);
            }
            return (int)Math.Min(int.MaxValue / 4, sum);
        }

        /// <summary>The weekly fees of the contracts that run.</summary>
        public int ContractWeekFees()
        {
            int sum = 0;
            for (int i = 0; i < _state.Clients.Count; i++)
            {
                sum += _state.Clients[i].Outside ? _state.Clients[i].Fee : 0;
            }
            return sum;
        }

        private bool Ready(bool cleaning)
        {
            return _running && !_broken && Save != null && (cleaning ? CleaningOn : SaleOn);
        }

        private void CleaningGuard(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                CleaningFault(what, e);
            }
        }

        /// <summary>A shop of the player's goes on the round, served as needed until the player sets hours.</summary>
        public void AddClient(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            CleaningGuard("adding a business", () =>
            {
                BuildingRegistration reg = ServicesGame.Find(place);
                if (!ServicesGame.Cleaned(reg) || _state.Client(place) != null)
                {
                    return;
                }
                if (_state.Clients.Count >= ServicesCatalogue.ClientsMost)
                {
                    Notifications.ShowError("reallife_svc_refuse_full");
                    return;
                }
                var c = new CleaningClient { Place = place };
                _state.Clients.Add(c);
                ServicesGame.Read(c);
                c.WorstStart = c.Worst;
                c.CleanBefore = c.Clean;
                Logger?.Info("Cleaning service: '" + c.Name + "' is on the round, served as needed under a cleanliness of " + c.Threshold + " (now " + c.Clean + "; its own schedule has "
                             + ServicesGame.OwnCleaningHours(reg) + " cleaning hour(s) a week).");
                Replan("a business was added");
                Changed();
            });
        }

        /// <summary>A shop of the player's is taken off the round; a contract ends through <see cref="EndContract"/>.</summary>
        public void RemoveClient(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            CleaningGuard("removing a business", () =>
            {
                CleaningClient c = _state.Client(place);
                if (c == null || c.Outside)
                {
                    return;
                }
                _state.Clients.Remove(c);
                Logger?.Info("Cleaning service: '" + c.Name + "' is off the round.");
                Replan("a business was removed");
                Changed();
            });
        }

        /// <summary>How a client is served: 0 as needed, 1 to 3 that many visits a day.</summary>
        public void SetMode(string place, int mode)
        {
            Change(place, c => c.Mode = ServicesCatalogue.Clamp(mode, 0, ServicesCatalogue.VisitsMost), "a visiting pattern was changed");
        }

        public void SetHour(string place, int visit, int hour)
        {
            Change(place, c => c.Hours[ServicesCatalogue.Clamp(visit, 0, ServicesCatalogue.VisitsMost - 1)] = ServicesCatalogue.Clamp(hour, 0, 23), "a visit's hour was changed");
        }

        public void SetThreshold(string place, int threshold)
        {
            Change(place, c => c.Threshold = ServicesCatalogue.Clamp(threshold, 0, 100), "a threshold was changed");
        }

        private void Change(string place, Action<CleaningClient> change, string why)
        {
            if (!Ready(true))
            {
                return;
            }
            CleaningGuard("changing a visiting pattern", () =>
            {
                CleaningClient c = _state.Client(place);
                if (c == null)
                {
                    return;
                }
                change(c);
                Replan(why);
                Changed();
            });
        }

        public void SetVan(bool van)
        {
            if (!Ready(true) || _state.Van == van)
            {
                return;
            }
            CleaningGuard("the van", () =>
            {
                _state.Van = van;
                Logger?.Info("Cleaning service: the crew "
                             + (van ? "drives a van from now on (" + ServicesCatalogue.VanCostPerKm + " a kilometre at a price index of 100)" : "walks from now on") + ".");
                Replan("the van was " + (van ? "taken" : "given up"));
                Changed();
            });
        }

        /// <summary>A cleaner joins the crew or leaves it. Nothing of theirs in the game is changed either way.</summary>
        public void SetCrew(string id, bool on)
        {
            if (!Ready(true) || string.IsNullOrEmpty(id) || _state.InCrew(id) == on)
            {
                return;
            }
            CleaningGuard("the crew", () =>
            {
                if (on && _state.Crew.Count >= ServicesCatalogue.CrewMost)
                {
                    Notifications.ShowError("reallife_svc_refuse_crew_full");
                    return;
                }
                if (on)
                {
                    _state.Crew.Add(id);
                }
                else
                {
                    _state.Crew.Remove(id);
                    for (int i = _visits.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(_visits[i].CleanerId, id, StringComparison.Ordinal) && _visits[i].End > ServicesGame.Now)
                        {
                            _visitPool.Add(_visits[i]);   // what they had not finished is planned again for the others
                            _visits.RemoveAt(i);
                        }
                    }
                }
                ReadAll();
                CrewMember m = Member(id);
                Logger?.Info("Cleaning service: " + (m != null
                    ? m.Name + " joins the crew (cleaning " + m.Skill + ", satisfaction " + m.Mood + ", based at '" + m.BaseName + "', " + m.WeekHours + " hour(s) a week in the game's schedule)"
                    : "a cleaner leaves the crew") + ".");
                Replan("the crew changed");
                Changed();
            });
        }

        public void AcceptOffer(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            CleaningGuard("taking up a contract", () =>
            {
                CleaningOffer offer = _state.Offer(place);
                if (offer == null)
                {
                    return;
                }
                CleaningClient c = ContractRules.Accept(_state, offer, Save.Day);
                if (c == null)
                {
                    Notifications.ShowError("reallife_svc_refuse_contracts_full");
                    return;
                }
                ServicesGame.Read(c);
                Logger?.Info("Cleaning service: a contract with '" + c.Name + "': " + c.Fee + " a week for a floor kept at " + c.Standard + " (" + c.Sqm + " m2, " + c.DirtDay
                             + " hundredths of dirt a day, floor now at " + c.Now + "); served as needed under " + c.Threshold + ".");
                Replan("a contract began");
                Changed();
            });
        }

        public void RefuseOffer(string place)
        {
            if (!Ready(true))
            {
                return;
            }
            CleaningGuard("refusing a contract", () =>
            {
                CleaningOffer offer = _state.Offer(place);
                if (offer != null)
                {
                    _state.Offers.Remove(offer);
                    Changed();
                }
            });
        }

        /// <summary>A contract is given up, after the game's own question. The running week is not paid.</summary>
        public void EndContract(string place)
        {
            CleaningClient c = Ready(true) ? _state.Client(place) : null;
            if (c == null || !c.Outside)
            {
                return;
            }
            GameConfirm.Show("reallife_svc_confirm_end_title".Localize(new { business = c.Name }), "reallife_svc_confirm_end_body".Localize(new { business = c.Name }),
                () => CleaningGuard("ending a contract", () =>
                {
                    CleaningClient still = _state != null ? _state.Client(place) : null;
                    if (still == null || !still.Outside)
                    {
                        return;
                    }
                    _state.Clients.Remove(still);
                    Logger?.Info("Cleaning service: the contract with '" + still.Name + "' is given up; its running week is not paid.");
                    Replan("a contract ended");
                    Changed();
                }), null, null, null, true, true);
        }
    }
}

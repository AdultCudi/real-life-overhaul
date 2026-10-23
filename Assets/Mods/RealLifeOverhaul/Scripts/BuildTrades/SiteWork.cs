using System;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>
    /// "Work here" at a works site's notice (civic life's own conversation there, Civic/CityHallPhone): the trades the site has
    /// on at its stage, offered one at a time as the Gig Line offers its gigs, with the pay, what the work is and what it
    /// asks, then taken where the player stands. A shift of the player's builder or of the city's building control due today
    /// is clocked on to first. Built when the page is turned; nothing runs between.
    /// </summary>
    public static class SiteWork
    {
        private static readonly TradeSite Site = new TradeSite();

        /// <summary>The first page, for the site's notice conversation; never null.</summary>
        public static DialogEntry Page(int siteId)
        {
            try
            {
                if (!Settings.Trades)
                {
                    return Closing("reallife_trades_off");
                }
                if (!TradesBridges.SiteById(siteId, Site))
                {
                    return Closing("reallife_civic_site_gone");
                }
                string employer = CareerLinks.EmployerId;
                if ((employer == CareerCatalogue.Builders || employer == CareerCatalogue.Inspection) && CareerLinks.ShiftDueToday(TimeHelper.CurrentDay))
                {
                    return Rota(siteId);
                }
                return Offer(siteId, 0);
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return Closing("reallife_job_no_now");
            }
        }

        // The rota shift due today: clocked on to here, or the site's own work instead.
        private static DialogEntry Rota(int siteId)
        {
            return new DialogEntry
            {
                messageData = "reallife_trades_site_rota".Localize(new { title = CareerLinks.TitleKey.GetLocalization() }),
                ConfirmTextOverride = "reallife_trades_site_clock_on".Localize(),
                OnConfirm = () =>
                {
                    JobDirector director = JobDirector.Instance;
                    if (director == null || director.Career == null)
                    {
                        return Closing("reallife_job_no_off");
                    }
                    return director.Career.ClockOn(out string reason) ? Closing("reallife_trades_site_clocked") : Closing(reason ?? "reallife_job_no_now");
                },
                SecondOptionTextOverride = "reallife_trades_site_other",
                OnSecondOption = () => Offer(siteId, 0),
                OnCancel = PhoneContacts.HangUp
            };
        }

        // One trade of those the site has on now: its pitch with today's figures, then take it, what else, or leave.
        private static DialogEntry Offer(int siteId, int index)
        {
            if (!TradesBridges.SiteById(siteId, Site))
            {
                return Closing("reallife_civic_site_gone");
            }
            int on = TradeCatalogue.OnAt(Site.Kind, Site.Phase, Site.Working);
            int count = 0;
            for (int i = 0; i < TradeCatalogue.All.Length && i < 31; i++)
            {
                count += (on & (1 << i)) != 0 ? 1 : 0;
            }
            if (Meta.Dev.Verbose && index == 0)
            {
                TradesDirector.Logger?.Info("Trades check (site work): site " + Site.Id + " (kind " + Site.Kind + ", stage " + (Site.Phase + 1) + ", "
                                            + (Site.Working ? "working" : "after hours") + ") has " + count + " trade(s) on: "
                                            + (count > 0 ? "PASS" : "none to offer, as its kind and stage say") + ".");
            }
            if (count == 0)
            {
                return Closing(Site.Working ? "reallife_trades_site_none" : "reallife_trades_site_shut");
            }
            int pick = ((index % count) + count) % count;
            TradeDef trade = null;
            for (int i = 0, n = 0; i < TradeCatalogue.All.Length && i < 31; i++)
            {
                if ((on & (1 << i)) != 0 && n++ == pick)
                {
                    trade = TradeCatalogue.All[i];
                    break;
                }
            }
            JobDef def = JobCatalogue.Find(trade.Id);
            JobDirector director = JobDirector.Instance;
            string reason = null;
            bool qualified = TradesBridges.Holds(trade.Needs);
            bool available = qualified && director != null && director.Available(def, out reason);
            if (!qualified)
            {
                reason = trade.Needs.LacksKey;
            }
            int level = JobState.Level(def);
            float rate = JobCatalogue.PayFactor(level) * JobDirector.PayScale();
            int next = index + 1;
            var entry = new DialogEntry
            {
                messageData = "reallife_trades_site_offer".Localize(new
                {
                    job = def.NameKey.GetLocalization(),
                    about = def.PitchKey.GetLocalization(),
                    pay = (def.UnitPay * rate).ToCurrencyFormat(),
                    count = def.ShiftUnits.ToString(),
                    shift = (def.UnitPay * def.ShiftUnits * rate).ToCurrencyFormat(),
                    level = level.ToString(),
                    project = Site.Project ?? string.Empty,
                    status = (available ? "reallife_trades_site_ready" : reason ?? "reallife_job_no_now").GetLocalization()
                }),
                OnCancel = PhoneContacts.HangUp
            };
            if (available)
            {
                entry.ConfirmTextOverride = "reallife_jobline_take".Localize();
                entry.OnConfirm = () => Take(def);
                if (count > 1)
                {
                    entry.SecondOptionTextOverride = "reallife_jobline_next";
                    entry.OnSecondOption = () => Offer(siteId, next);
                }
            }
            else if (count > 1)
            {
                entry.ConfirmTextOverride = "reallife_jobline_next".Localize();
                entry.OnConfirm = () => Offer(siteId, next);
            }
            return entry;
        }

        private static DialogEntry Take(JobDef def)
        {
            JobDirector director = JobDirector.Instance;
            if (director == null)
            {
                return Closing("reallife_job_no_off");
            }
            // Asked again now: the site's stage or the day's work may have moved on while the page was read.
            return director.Take(def, out string reason) ? Closing("reallife_trades_site_go") : Closing(reason ?? "reallife_jobline_failed");
        }

        private static DialogEntry Closing(string key)
        {
            return PhoneContacts.Closing(key);
        }
    }
}

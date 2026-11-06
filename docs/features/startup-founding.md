# Founding a startup

Phase 4 of the startups rework. It sits on the firm account of [Startup money](startup-money.md): every cent a founded firm raises goes into that account like any raise, and the floor holds it for the investors.

## What the player can do

- **Found a startup** (Tech app, Startups or Funding tab, "Found a startup"): pick one of the player's tech businesses that has no cap table yet, a problem (the venture sectors, not games), a niche, a co-founder and their share, the articles and the first money. The legal fees are booked at the firm.
- **Niches** (Startups tab, "Niches"): 15 niches of the Business and Enterprise software and AI markets, each from the game's real business types (`NicheCatalogue`). A niche exists only if the city can have a business of one of its types. Buyers are the city's businesses of that kind counted from the game's registrations once a day (the population's staff, rents and lettings are added where it runs), times the region each stands for (`SectorRules.WorldAt`). The ceiling is the buyers at the going price. A small niche has the square root of its share of the market's rivals, and caps out.
- **Expansion**: a neighbouring niche (the bits each niche names) opens for a few days of the firm's burn and its buyers count in over 60 days.
- **Moods**: the city's cycle (felt by habit: groceries less than a night out), the season where `SeasonRules` models the niche's goods, rents for landlords (the population's rent index) and the AI wave for AI products. Where the mod holds no data the factor is 1000.
- **Go to market**, five stages, each a desk session at the owner's desk: discovery (two rounds of interviews), the first version (own night at the desk through `OwnDeskRules.Credit`, or the team), early users (fix, ask for word of mouth, ship), the launch channel (sales led, product led, partner that resells for a cut) and pricing. Fit is worked out each day from what was learned, retention, word of mouth, quality and the firm's name. It shows on the firm page, in the board pack and in the books.
- **Fit prices a raise**: before 350 a raise values the firm at 65 per cent, at the bar (650 with five paying customers) at full value, up to 30 per cent more above it, and the valuation's multiple moves with it. After fit investors ask for a seat.

## Pre-seed sources

| Source | What it does |
|---|---|
| Savings | Nothing raised, no account |
| Friends and family | one, two or four least tickets at 125 per cent of the pre-seed floor value, no seat, a 1x preference. If the firm fails and the family loses money, a partner is let down through family life (`FamilyApi.LetDown`); with no partner there is no cost |
| Angels | the existing angel groups, a sheet priced by `VentureRules.Sheet` from Selling and Relationships, no pitch |
| Accelerator | three tickets for 7 per cent, 60 days of mentoring (Selling, Product sense, Leadership through the study's `Learn`), then a demo day that adds term sheets from seed venture firms |

Friends and family and the accelerator are `Vc` lines of the cap table (ids 8 and 9 in `VentureCatalogue.Firms`, leading no round), so they count as investors for the floor and are paid first at a wind down.

## Co-founders and vesting

From the population's records when it runs, else the hiring pool. They go on the payroll with their own skills at half the going rate and hold `Founders` lines of the cap table. Shares vest over four years with a one year cliff (`VentureRules.Vested`) from the day of founding, as does the founder's own (`Acc.FounderDay`). A co-founder who leaves before the cliff takes nothing, after it the vested part; the rest is cancelled.

## Articles

Standard: investors with a seat can remove the founder at the ladder's third step. Founder friendly: they need the board's majority, they value the firm 8 per cent lower and the lawyer costs half as much again. With no investor on the board nobody can remove the founder, and the investors still sue. Legal fees are three due diligence reports, less with Law.

## Older saves

A running firm is placed on the first day it is seen at the stage it already is, with a niche picked from its market, seeded. Nothing about it changes (price, ladder, sales) until its player takes it up on the go to market page.

## Where the code is

`Scripts/Tech/Founding/` (`NicheCatalogue`, `NicheRules`, `FoundCatalogue`, `FoundRules`, `FoundedFirm`, `FoundedStore`, `FoundBridges`, `FoundText`), `TechDirector.Founding.cs`, `TechDirector.FoundDesk.cs`, `Wiring/Wiring.FoundLinks.cs`, `Ui/Tech/TechLive.Found.cs`. Save line `reallife:tech_founding`. Checks: `EconomyCheck --startup-founding`.

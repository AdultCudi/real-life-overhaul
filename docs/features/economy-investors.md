# Economy: investors and the market

## In a sentence

Investors who put money into a business of the player's for a share of its daily profit, a pitch that wins or loses that money, and a page that reads the game's own lettings as an investment, all in the **Ventures** app.

## Where the code is

- Folder: [`Scripts/Economy/Ventures`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Ventures/).
- Read first: `VenturesDirector.cs`, `VenturesCatalogue.cs` and `VenturesModel.cs`.
- The stock market this page describes is now the city's own, in [`Scripts/Markets`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/); see [the market](economy-markets.md).

## How it talks to other features

- Ventures calls no other feature and none calls it: `Wiring/VenturesLinks.cs` gives it the city's economy (the climate and the bank rate), the employers on the CV and whether the player is held.

Phase 4 of the economy (`Scripts/Economy/Ventures`, options "Investors", "The stock market" and "How demanding investors are", ids `ventures_investors`, `ventures_market`, `ventures_level`): investors who put money into a business of the player's for a share of its daily profit, a pitch that wins or loses that money, a stock market of the city's own companies moved by the news the mod already makes, and a page that reads the game's own lettings as an investment.

It has its own director, its own line in the save and its own app on the phone ("Ventures").
No method of the game's is patched and nothing is ever written into the game's business or property records: money moves through the game's own transaction call under the mod's own keys, and everything else is the save line.

| File | What it is |
| --- | --- |
| `VenturesCatalogue.cs` | every figure, the four levels, the names; `VenturesRandom` (seeded rolls that are a hash of what they are about) |
| `VenturesModel.cs` | `Investor`, `Offer`, `Pledge`, `Deal`, `Company`, `Holding`, `BusinessFigures`, `VenturesFile`; `IVenturesClimate` with `SteadyClimate` and `ClimateFrom` |
| `Cast.cs` | the save's investors and companies from its seed |
| `Deals.cs` | `OfferRules` (an offer, a counter, taking it up, money promised), `DealRules` (a deal's day, the ladder, buy-out), the transaction keys |
| `Pitch.cs` | the bank of 41 questions, which are asked, what an answer is worth, the check of a figure, the decision |
| `Market.cs` | a market day, the news tables, trading, dividends |
| `VenturesStore.cs` | the save line |
| `VenturesBridges.cs` | what Ventures asks of the rest of the mod; `VenturesPhone.Open(tab)`; `VenturesText` |
| `VenturesGame.cs` | what is read from the game (a business's figures, the true answers, rivals, kinds of business, rents) and `VenturesWallet` |
| `VenturesDirector.cs`, `.Tests.cs` | the component: start, the day, the hour, the save, what the app asks for, the test tools, the save tools |
| `PitchMeeting.cs` | the meeting in the game's list window |
| `VenturesApp.cs`, `.Investors.cs`, `.Market.cs`, `.Property.cs` | the app |
| `Wiring/VenturesLinks.cs` | the bridges filled in, and the Planner's rows |

The first seven are game-free: the offline checks compile them as they are.

## How it runs (no per-frame work)

`VenturesDirector` has no `Update`.
A coroutine on the real clock looks once a second: it starts when the save is ready, stands down when both options are off, keeps its handlers on `GlobalEvents.onNewDay`, `onNewHour` and the save relay (two reference comparisons while nothing has changed), puts the app on the phone, and asks a meeting in progress whether its window is still there.
Everything else is done on an event:

- **the day** (after the game's daily run, so yesterday's statements exist): offers and promises that have lapsed; each deal's share of yesterday's profit and its target; every seventh day the look at the businesses for an offer; the market's day and, every fifteenth, its dividends.
  Transactions are dated on the day that ended, as the game dates its own run.
- **the hour**: only while money is promised for a business not yet open: has one been opened.
- **the save**: the line is written.
- **the app** being opened or clicked: pages are written again; nothing runs while it is closed.

A fault anywhere logs one line (`Ventures: switched off for this session after an error in ...`) and Ventures does nothing more that session; what the save holds is left as it is.
Days on which Ventures did not run (an option off, a fault) are not made up afterwards: no share is taken for them and the market does not move.

## The cast (`Cast.Make`, once per save from its seed)

- Two or three tycoons (the first patient, the second demanding), three angels (patient or even), two venture firms (demanding).
  Names are invented: a first and a last name from two lists of sixteen, a firm from a list of six.
- Smallest sum: angel 4,000 / 5,000 / 7,500; tycoon 25,000 / 40,000 / 60,000; firm 75,000 / 100,000 / 150,000.
  Largest: angel 30,000 / 45,000 / 60,000; tycoon 400,000 / 750,000 / 1,500,000; firm 1,000,000 / 2,500,000 / 5,000,000.
  Diligence (how hard they check a figure): angel 25 to 45, tycoon 50 to 75, firm 78 to 95.
  Share of the profit they take: angel 8% to 20%, tycoon 15% to 35%, firm 20% to 40%.

  Each likes two of five sectors (shops, food and drink, offices and services, leisure, trade and industry; `VenturesCatalogue.SectorOf` sorts the game's business types by the words in their ids, "ba:businesstype_webdevelopmentagency" being an office).

- Twelve companies: one invented firm in each of eight sectors (transport, property, retail, food, security, utilities, banking, entertainment), then the game's own named rivals (`RivalsHelper.GetSpecialRivals`, four at most, listed as "<name> Holdings" in retail, food, entertainment and property in turn), then second invented firms to make twelve.

  Each starts at 20 to 240 a share, with an ordinary daily move of 0.8% to 2% and a dividend of 0.4% to 1.1% of the price each payout.

## An offer that comes to the player (`OfferRules`, `VenturesDirector.Look`)

Every 7 days each business is looked at.
One that made a profit on at least 10 of its last 14 days and whose mean profit over the last 7 is at least 1,500 a day gets an offer with a chance of 25% (40% if a free tycoon likes its sector; half of that in a slowdown, a quarter more in a boom: see the climate below).

The tycoon values the business at 60 days of that profit and asks a share a quarter (patient), a half (even) or three quarters (demanding) of the way up their range.

The sum is that share of the value, cut to their largest sum (the share shrinks with it) and to the room left on the business (all investors together hold 60% of one business's profit at most, two deals to a business, eight deals in all, four offers open).
Under their smallest sum there is no offer.

The offer names a growth target (10% in 30 days patient, 15% in 24 even, 25% in 18 demanding, scaled by the level) and waits 5 days.

Accept (the money is paid in at once), refuse, or counter once: 25% more money for the same share, or a share a quarter smaller for the same money.
The investor agrees with a chance of 40% / 30% / 15% by temperament plus 5 points for each profitable day over ten, meets half way (half the step) with 40% / 40% / 35%, and otherwise holds to the first terms.

The roll is a hash of the save's seed and the offer, so loading and trying again changes nothing.

## The pitch (`Pitch`, `PitchMeeting`)

"Raise money" in the app: a business the player runs, or a kind of business not yet open with a budget (10,000 to 500,000 in nine steps); then an investor (one approach to each in 10 days; a tycoon backs no business that is not open).
The phone is put away and the meeting is held in the game's list window, locked, the player standing still.

- **Questions**: 6 (angel), 7 (tycoon) or 8 (firm), drawn from the 41 that fit: facts / plan / founder in the mix 2/2/2, 3/2/2, 4/3/1.
  Where too few facts can be asked (no debts, no record, no credit file) the plan fills up.
- **A fact** (14 of them: yesterday's and the week's takings, yesterday's and the week's daily profit, staff, age, profitable days of 14, cash, credit rating, debts, convictions, businesses run, missed payments, what loans cost a day) is offered as the true figure (money in two figures that count) and one or two that sound better.

  Those are money 30% and 80% more, a count likewise and by one and two at least, a rating 40 and 100 higher, a debt halved and none.

  A true figure scores 4 to 10 by how strong it is.
  An untrue one scores as if true unless caught: the chance is the investor's diligence (moved by the level and, under a name of 70, by half the shortfall) times 45% for the nearer figure and 90% for the further, between 3% and 95%.

  Caught: the meeting ends, that investor sees nobody for 60 days and the founder's name with all investors falls by 25 (of 100; it starts at 70).

- **The plan** (18 questions, three answers each): points for each kind of investor, moved by their temperament (patient: careful +2, bold -1, personal +1, reckless -2; even: +1, +1, 0, -2; demanding: -1, +2, -1, -1) and for two questions by the business's sector (+2 or -2).
  The best answers differ by investor.
- **The founder** (9 questions): each answer says something the save shows or not (businesses run and how long, jobs held, missed payments and bankruptcy on the credit file, convictions, other backers, a debt in order).
  Said without the record behind it, it scores 1.
- **Score** = points x 100 / (questions x 10), +5 for a sector the investor likes, + (name - 70) / 5.
  Needed: angel 45, tycoon 55, firm 62 (the level adds -5, 0, +3, +6).

  At or over it, with "over" the part of the way from needed to 100: the business is valued at 40 to 90 days of its true recent profit and the investor takes from the most to the least of their range; the sum is their share of that value inside their smallest and largest sums.

  A business not yet open gets 30% to 70% of its budget (from an angel or a firm), paid when the player runs one more business of that kind than on the day of the promise, within 14 days (the deal starts on the one of that kind with no deal and the fewest daily statements: the one just opened).
  Else the promise lapses and nothing is owed.

  An angel backs a business under 30 days old with their smallest sum whatever its profit.

- **Worked example**: an even-tempered tycoon (sums 25,000 to 750,000) asks 7 questions and the answers score 8, 10, 7, 8, 6, 9, 8 = 56 of 70, a score of 80.
  They need 55, so "over" is 25 / 45 = 0.56: the business, making 4,000 a day, is valued at 40 + 0.56 x 50 = 68 days = 272,000, and their share is 35% - 0.56 x 20 = 23.89%.
  The offer is 64,900 for 23.89% of each day's profit.

  They say what helped most (the answer worth 10) and what counted against the founder (the one worth 6).
  `EconomyCheck` has this case.

- The decision is said in two or three sentences and, where there is an offer, the page has "Accept the terms", "Think it over" (the offer waits 5 days in the app, where it can also be countered) and "Decline".

## A deal (`DealRules`)

- Each day the investor is paid their share of yesterday's profit of that business from the game's own statement (`GameInstance.financialSummaries`), never of a loss, by a transaction ("Investor's share: <name>").
  Cash that is short is carried over and taken first the next day.
- **Target**: the recent daily profit grown by the investor's figure by a day.
  Met: mood +20, the name +3, a second round is offered at the next weekly look (70 days of profit; its money and share are added to the same deal), and a new target is set from the new profit, unless the investor has had their money back: then they set no more.
  Missed once: a warning and the same days again.

  Twice: a demand, to be answered in 5 days: a seat on the board (10 points more of the profit for 30 days; a firm already has one) or a promise to cut the mean daily costs of the last 14 days by 10% within 14 days (tested from the statements; broken, it counts as the next miss).
  Unanswered, the investor takes the seat (a firm: the promise).

  Three times: a demanding investor calls their money back (what they put in less 50% of what they were paid, 25% of what they put in at least) over 30 / 30 / 20 / 14 days by level; the share stops.
  After 5 daily payments missed running the rest is filed as a civil claim through `EconomyBridges.FileClaim` (Justice judges and collects it; with no court it stays owed).

  Anybody else, and everybody on Relaxed, loses interest: they keep their share and set no more targets, and one who has not had their money back will not back the founder again and costs 10 of the name.

- **Buy-out** at any time: the investor's share of 75 days of the mean profit of the last 14, never less than 110% of what they put in; for a sum called back, what is still called.
- **The business sold or closed**: the investor is owed what they put in less what they have been paid, by the day over the level's days (nothing if they have had it back).
- **Levels** (the presets set `ventures_level`): growth asked 50% / 100% / 125% / 150% of the investor's figure; diligence -15 / 0 / +10 / +20; score needed -5 / 0 / +3 / +6; nobody calls money back on Relaxed.
- What cannot be made safe by a rule is bounded: money for a business not yet open rests on a budget nobody can check, so a tycoon gives none, a firm's is called back when growth stops, and an angel's (60,000 at most) is lost to the angel once, with the angel and 10 of the name.

## The stock market (`Market`)

One step a day, at the day event:

move (in hundredths of a per cent) = 4 + the company's ordinary move x a seeded bell-shaped number

                                         + 0.004 x the gap to what the company is worth + the day's news + climate

What a company is worth rises by 0.04% a day, so prices follow it over the years.
The news of one day is capped at 1.5% for a company, less than the 2% it costs to sell and buy back, so reading the news before the market does is worth nothing.
The climate adds or takes 0.02% a day in a boom or a slowdown.
A price never goes under 1.00.

| News | Where it is read (public state only) | Sectors moved (hundredths of a per cent) |
| --- | --- | --- |
| storm, rain | `Events.Weather.Sky`, the game's `RainHelper.isRaining` | storm: transport -60, retail -40, entertainment -40, food -20, utilities +40, security +10; rain: retail -15, entertainment -15, transport +10 |
| heat wave, cold snap, ice | `Events.Weather.SpellOf(the day that ended)` | utilities +40 / +50 / +20; food +20 (heat); transport -10 / -50 (cold, ice); retail -20 / -15 |
| street market, parade or run or fireworks, protest, film crew | `Events.EventCalendar.For(the day that ended)` | food and retail up for a market; entertainment +40 for a parade or a film crew; retail -30 and security +30 for a protest |
| crime, break-in, road accident | `News.Recent(kind, 1 day)` (whether there was one) | security +40 / +50; retail -20 / -25; property -15 / -10; transport -30 (accident) |
| a council policy moved | `CivicPolicy.Position(lever)` against what Ventures last saw | by lever: policing, business tax, trade levy, grants, rents, transit, night life, permits, events, wages |
| an election counted | `CivicDirector.Instance.Government.State.Number` against the number last seen | every company +15 |
| a rival's move | `BusinessState.Issues`: a price war or a smear begun yesterday | the named rival's own company +60, other retail -10 |
| the bank rate | `IVenturesClimate.BankRate` against the rate last seen | banking +40 and property -30 for each point up |

Not linked, because nothing usable is public: how many crimes or accidents a day had, the paper's own items by kind (`News.Today` holds finished texts), a campaign's polls.

Trading: whole shares at the last price; a purchase is rounded up to whole money and a sale down; commission 1% of each, 5 at least; 5,000 shares of one company at most.
Dividends every 15 market days on the price of that day.
The last 30 closing prices of each company are kept; the app shows the move over them as a figure and their low and high (no chart is drawn).

**It is no money printer** (`EconomyCheck`, written out on every run): over ten game years of 60 days from eighty seeds, with news made up at about the city's rate, buying every company on the first day and holding returns 5.05% a year after commission, and the unluckiest and luckiest holders end with 1.50 and 1.81 times their money.

Seven rules of thumb all do worse than holding: buy after a fall and sell after a rise (-11.96% a year against holding), the opposite (-11.19%), buy under the thirty-day average and sell over it (-3.83%), buy for the dividend (-9.24%), buy on the day's good news (-8.15%), sell on the day's bad news (-1.22%), hold but sell what has run a tenth over its average (-0.72%).

The check fails if holding leaves 3% to 7% a year or any rule beats it by 1%.

The game's own investment funds (EconoView) are left alone: a fund follows its yearly rate; a share is one company.

## Property

The game already lets every building the player owns (`Helpers.RealEstateHelper.RunDaily`): tenants come and go by the rent set in BizMan against the market rent, the rent is paid in each night (`ba:transaction_rentrevenue`), a building for sale is sold to a competitor, and its market value follows its district.
So no second system was built.

The Property tab only reads: each building's price and day of purchase (`RealEstate`), its market value now (`Building.GetMarketValue`), how much is let and what that brings a day (`OccupancyPercentage`, `DailyIncome`), the rent of the last 7 and 30 days from the game's daily statements (`FinancialSummary.realEstateStatements`, kept for a year; the transaction list is cut after six days), the yield of a year's rent on the price paid, and the mortgage on it if Phase 2 has one.

Repairs, empty months and tenants of the mod's own were left out on purpose.

## The save line

`modData["reallife:economy_ventures"]`, version 1, a few kilobytes at its fullest (`VenturesStore`):

v=1~h:...~i:...~o:...~p:...~d:...~c:...~s:...

Parts are divided by `~`, records by `;`, fields by `|`.
`h` the header (seed, start day, name with investors, next id, meetings held, the day of the last weekly look, the last day settled, market days, the last market day, totals, the council's policies, elections and bank rate as last seen, what the app has not shown).
`i` the investors (11 numbers each).

`o` the open offers (4 at most: 13 numbers, the business's key, its name, a new business's type).
`p` money promised (3 at most: 11 numbers, the type).
`d` the deals (8 at most: 24 numbers, key, name).
`c` the companies (12: 9 numbers, what it is worth, a rival's listed name, 30 closing prices divided by commas); `s` the shares held.

A later version adds fields at the end of a record or a part with a new letter; this reader takes what it knows and skips the rest.
A line that cannot be read is replaced by a new file.
While the test tools have moved the market, the save keeps the market as it stood before them.

## Bridges

`Wiring/VenturesLinks.cs` fills `VenturesBridges`: `DayNews`, `Lever`, `Elections` (the table above), `JobsHeld` (`CareerState.History` and the present employer), `Held` (custody, prison, a hearing, the driving school, the police, a bus ride), and adds the Planner's source (an offer's last day, a promise's last day, a target's day, a demand's last day, tomorrow's payment of a sum called back, the next dividend day).

The legal record, the courts and the other debts are the economy's own bridges (`EconomyBridges.Legal`, `FileClaim`, `OtherDebts`), used as they are.
Members added to other features: none.

**The climate.** `VenturesBridges.Climate` is an `IVenturesClimate` (a phase of -1, 0 or 1 and a bank rate), at `SteadyClimate` by default.
`Wiring/VenturesLinks.Connect` joins the city's economy (`Scripts/Economy/City`) on: its boom is the market's boom (1), steady is 0, and its slowdown and its recession are the market's slowdown (-1); the bank rate is the steady rate with the central bank's points on it (`SteadyClimate.Rate + CityEconomy.BasePoints`).
With the city's economy off the climate stays steady and unmoved.

## Test tools (options page, "Show test tools")

A tycoon's offer for the best business.
A meeting with an angel, a tycoon, a venture firm (about the best business, or with none about a business not yet open).
The investors and companies in the log.
The market stepped 30 days (no dividend; the market as it stood is kept and is what a save keeps).
A dividend paid now (with no shares, ten test shares of the first company are given first).

An investor calls their money (a deal there is, or a test deal).
"Test: put ventures back" (test offers, deals, promises and holdings removed; the market, the name with investors and the investors' days as before the first test of the session; money a test paid stays).

## Log lines (with "Developer diagnostics" on)

- `Ventures: a file is opened for this save on day 12 (seed ...): 8 investors, 12 listed companies.` (once per save, always)
- `Ventures check (start): file version 1, 2 deal(s), ... Cast: ... Companies: ...: PASS`
- `Ventures check (the weekly look, day 21): <business> made a profit on 8 of its last 14 days and 900 a day lately (needs 10 and 1500): no offer.`
- `Ventures check (offer, day 21): <name> (Tycoon, Patient), a business with 12 profitable days of 14 and 3200 a day lately, chance 40%, roll 17: 38400 for 2000 bp (60 days of profit), growth 10% in 30 days, answer by day 26.`
- `Ventures check (pitch 3, day 22): <name> (Firm, Demanding, diligence 88) about <business> (...): 8 questions. True figures: ...`
- `Ventures check (pitch 3, question 2 of 8): reallife_ven_q_f03 (Fact), answer 2 = 1200 against the true 900: untrue, chance 39%, roll 71, not caught, 8 point(s), 15 of 20 so far.`
- `Ventures check (pitch 3, decision): points 7,8,...; liked sector True (+5), name with investors 70; score 71 against 62; recent profit 900 a day valued at 50 days, sums 100000 to 2500000: TooSmall 0 for 0 bp; best answer 2, worst 6.`
- `Ventures: pitch 3 to <name> about <business>: 49 of 80 points, score 66 (needs 62): TooSmall.` (always)
- `Ventures check (day 23): deal 4 with <name> on <business>: Paid 640 (yesterday's profit 3200, share 2000 bp, ...).`
- `Ventures check (market day 11, game day 23): 2 piece(s) of news (Storm, Policy 7 by 100), climate 0. <company> 43.20 to 42.71 (news -60 bp, Storm); ...`
- `Ventures check (dividend, day 27): market day 15, 3 holding(s) paid 412 in all.`
- `Ventures check (trade): buy 100 of <company> at 43.21: Done; cash 100000 to 95635, now holding 100 at a cost of 4365.`
- `Ventures: tab 1 written, 12 row(s); 1.9 ms.`
- `Ventures: leaving this save. Shares sold for ... in all, with no commission. ...` (always, from the save tools)

## Checks

`EconomyCheck` compiles the seven game-free files too and `VenturesChecks.cs` adds 195 checks (488 in all): the cast for 300 seeds.
The offer rule, the counter over 600 offers, a second round, money promised.
A deal over 400 days of profits and losses to the unit, arrears, buy-out, a business sold.
The ladder day by day for a demanding tycoon, a patient one, Relaxed, a firm's promise kept and broken, the claim.

The save line (round trip, a later version's line, damage, the caps, the test tools' market).
The bank and the draw of questions, and the words of every question in `en.json`.
Truth never fatal, a caught figure always ending the meeting.
The best answers differing by investor.
The founder's answers against the record.
The decision's sums, the worked example.
The market's determinism, news, cap, commission, limit, dividends, and the ten-year runs above.

## Not verified (the game could not be run)

Everything on screen: that four tabs fit the bar; how many lines the white box holds (the deal's page with a demand is the longest: about twelve lines and two rows of buttons); that the list window opened in the same frame the phone is put away shows on the HUD; how long answers wrap in the list's entries.

That `BuildingRegistration.GetDisplayName`, `creationDay` and the daily statements read as the decompiled code says for a real save.
That a transaction with no address and no category shows in EconoView under its `_label`.
The rivals' names as the game gives them (`SpecialRival.rivalData.rivalName`).
The app icon's glyph (`Icon-Building`).
Whether a business opened in a building rented before the promise is found (it is counted by its kind, so it should be).

Every figure's feel in play: the sums against what businesses really earn, how often an offer comes.

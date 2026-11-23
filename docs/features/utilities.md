# Power and water

## In a sentence

Every place the player rents or owns has a power account and a water account, billed by the month at the city's prices, with cuts for bills left unpaid; the folder also holds home internet and the broadband company.

## Where the code is

- Folder: [`Scripts/Utilities`](../../Assets/Mods/RealLifeOverhaul/Scripts/Utilities/).
- Read first: `UtilitiesDirector.cs` (what it does each morning and month), `UtilitiesRules.cs` and `UtilitiesCatalogue.cs` (game-free, checked by EconomyCheck) and `UtilitiesStore.cs` (the save).

## How it talks to other features

- `Wiring/UtilityLinks.cs` fills in `UtilitiesBridges`: the credit grade for a deposit, who lives at the home (Family, the roommate), closures for want of power or water (Business), dry taps and dark screens at home (Home), and the amounts owed in the Finance app.

Every place the player rents or owns (`BuildingRegistration.RentedByPlayer`, a home or a business) has an account for power (City Light and Power) and one for water (Riverside Water).
The rules are game-free and checked by EconomyCheck (`UtilitiesCatalogue`, `UtilitiesRules`, `UtilitiesStore`); the director reads the game and does what they say.

- **Accounts.** A rented place has the landlord's, supplied at once: the player just pays.
  A bought place (`BuildingOwnedByPlayer`) is supplied for three days at half as much again (the temporary supply) and then cut, unless an account is applied for in person at the office's desk; it connects the next morning.

  A poor credit rating (Economy's grade, through `UtilitiesBridges.Grade`) asks for a deposit of one or two months' estimate, never a refusal; it comes back after six bills paid by their due day in a row, or with the last bill.
  A place given up has its last bill at once, the deposit set against it. The places are read again each game hour as well as each morning, so a place whose lease ended, or that was sold with it, is let go within the hour and never closed or billed again (`UtilitiesRules.Fate`, `UtilitiesDirector.Gone.cs`).
  An underground car park (the game's street `ba:street_parking`, size `ba:buildingsize_parking`) is never a place, even when another mod sells one as a business: the game gives its address no registration of its own and its layout no floor, dirt or furniture, so it has no premises to supply (`UtilitiesRules.Serves`). An account a save holds for one is written off, its deposit given back.

- **Use.** Each morning yesterday's use is added to the month's, at that day's prices (`Prices.Level`): a home by its floor (`Building.totalSqm`) and the people in it (the player, a partner who moved in, children at home, a roommate: `UtilitiesBridges.Household`).
  A business by its floor and its kind (the game's business types, from offices to industry), a quarter of it on a day it does not open (its week of opening hours).

  Both by the season (the weather's).

- **Bills.** At the turn of the month (a twelfth of the year) each account is billed, due in half a month (two to fourteen days).
  Auto debit (the Utilities app) pays on the due day.
  A bill a day overdue has the late fee added once; three days after its due day the supply is cut, and reconnecting at the desk costs a fee as well.
- **A cut.** A business without power closes (Business's own switch, `UtilitiesBridges.CloseFor`, hour by hour), as one without water does where it needs water (food and drink, salons, gyms).
  At home the shower and the sink give nothing without water, screens and the mod's cooking nothing without power (`HomeTidy.HasPower`, `HasWater`), and a mood modifier stands for each (`ModState.NoPower`, `NoWater`); hygiene and its colds follow by themselves.
- **The office.** The largest office building of the city that no other feature has made its own (not the courthouse), with a lettered sign by its door; the desk is the game's own list (`DeskList`, `UtilitiesDesk`): pay a bill, apply for an account or a reconnection, the accounts that connect tomorrow.
  It lights up on the city map. Its building, and 13 7th Street while it is reserved for it, are held from the game's own letting ([the game's own letting](../reference/map-places.md#the-games-own-letting-leaves-them-alone)).
- **The app.** Every account with where it stands, the month so far, what is owed and when, and auto debit; the white box has the picked account's use, bills and deposit, the auto debit switch and "Pay now".
- **Pay all now, and several bills together** (`BillPayRules`, `UtilitiesDirector.PayMany`; the old app's `UtilitiesApp`, the kit's `UtilitiesScreen` and `UtilitiesLive`). On the Accounts tab, **Pay all now** pays what is owed at every place of the player's, and a bill can be ticked (a switch in an account's box and a Ticked column on the old interface; **Pick bills to pay** on the kit, the table's own picking with **Pick every bill** and **Pay ticked bills**) and the ticked ones paid. Each asks first, in the game's own question box, with the number of bills, their total and the player's cash. When the cash falls short nothing is paid: a notice says the total, the cash and the shortfall, and when some bills can be covered a second box offers to pay only those, taken in due-date order with a bill that does not fit passed over so a smaller one after it is still paid; the player has to say yes to that. Each bill is paid by the one `Pay` that a bill paid by hand goes through, so every bill has its own bank line (the business's deductible, a home's not) and its account is cleared at once, which is why the auto debit, which takes only a bill still owed, never takes it a second time. The cash is read again when the player confirms, a bill the game refuses stops the run and what was paid stands, and one notice at the end says how many were paid and for how much. Ticks are remembered by place and service and dropped when the bill is paid by any route. A purely additive pair of buttons, so with no option: they appear only while something is owed. Nothing per frame; a pass over the accounts and an insertion sort of the few that owe when a button is pressed. EconomyCheck: OwnWorkPayChecks (`--util-pay`).

- **The internet.** Four rival providers (`InternetCatalogue`: cable for the whole city but The Hamptons, fibre in three districts only, fixed wireless and a copper line everywhere), each with its plans, reliability and a price that walks month by month within bounds (a promotion now and then: `InternetRules.NextPromo`).

  A plan is bought in the app (no office: a private provider sells online) and installed two days later; on a contract of twelve months its price is held and the installation free, month by month it costs a fifth more; leaving a contract early costs half of each month left.
  It is billed with power and water, a day's share of the month at a time, and cut the same way.

  Outages are drawn from the save's own seed, the day and the provider (`InternetRules.Draw`), so a reload changes nothing; a day with one is not charged, and a business plan is repaired in half the time.
  Web design and software companies and the headquarters (`InternetCatalogue.Needs`) cannot open without a connection that works.

- **Leased lines** (`LeasedLineCatalogue`, `LeasedLineRules`, `UtilitiesDirector.BuyLine`). A data centre (a warehouse of the player's the tech sector has fitted out, asked through `UtilitiesBridges.LineSite`, filled by `UtilityLinks`) can buy the carriers' business fibre in four steps, 5, 10, 40 and 100 Gbps, beside its plan: the account keeps its plan and gains a line (`Account.LineStep`, `LineDay`, and `LineNext`, `LineNextDay` for a change), and the place's speed is the faster of the two (`UtilitiesDirector.Speed`, `TechLinks.Mbps`), so every limit and every firm's work reads it. Nothing else can have one: a home, a shop or an office is refused with the reason before any money is taken. The line is sold in every district, the ones a free update adds included (`InternetCatalogue.Leased` reaches everywhere): where Brightvein's fibre runs the installation costs a month and a half of the price and takes the step's days (7, 10, 14 or 21), with only Kestrel's cable it costs half as much again and takes a week longer, and where nothing runs (The Hamptons, Uptown, Cedar Heights, Upper East Side and any new district) it costs two and a half times and takes two weeks longer: the honest version of raising a limit where the street has nothing. A month costs $2,000, $1,600, $1,400 and $1,200 for each Gbps (so $10,000, $16,000, $56,000 and $120,000), month by month with no contract, billed with the place's internet from the day it is live (`LeasedLineRules.AddDay`); no outage is drawn for it (carriers hold a service level). A change to another step orders the new line beside the running one, which carries on and is billed until the new one is live, so a hall never loses its connection to grow; cancelling the connection ends the line too, and the line can be given up alone. A hall with no plan has the line alone (an account with provider -1). The player's own broadband company sells no leased line: its plans reach the player's own places free of charge and with no installation, so a free 100 Gbps line from it would undo the price. The prices come from `InfraRules`: a rack of the old kind takes $4,200 a month at the rivals' list price (40 units at $3.50 a day) and keeps $2,678 after its power ($864) and its writing down ($657), and a new rack takes $84,000 and keeps $72,000 or more; a rack uses 100 Mbps and an AI rack 400, so a line costs $200, $160, $140 and $120 a standard rack at its steps, at most 5% of an old rack's takings and 92% of its profit kept, and EconomyCheck (`--leased`) prints those sums for both kinds of rack at both prices and holds them. The Internet page lists the lines among the providers of a data centre on the old interface and the kit, with the fee, the wait and what the district has before the player buys, and "Cancel the leased line" beside "Cancel the connection". The save line has the four figures at the end of each account's record; an older save reads none.
- **Speed.** One game-free table (`Core/NetSpeedRules.cs`) says what a plan's speed does to the work over it, its tiers where the providers' plans fall: under 50 Mbps 70% of the usual pace, 50 Mbps 80%, 100 Mbps 90%, 300 Mbps (the usual business plan) 100%, 1000 Mbps 105%, 2000 Mbps 108%.
  A lean stretches it: a game studio half as much again, an AI lab and an online shop twice as much, a consultancy or a graphic designer half as much, always within 30% and 120%.
  A tech firm's day's building, design, web and testing points go by its office's plan, read once with the day's crew (`FirmsRules.Connection`, `TechBridges.PlanMbps`); with no working plan the firm is closed by the hour as before, so the share reads 100%.
  An online shop's day's bound on orders goes by its building's plan (`OnlineRules.Most`, `OnlineBridges.Mbps`), read with the day's orders and again each hour, which also stops the hour's orders in an outage; with no working connection it takes none.
  Shops without a connection when speed first counts in a save join the grace (`InternetRules.AddGrace`; the save keeps the day as `SpeedMet`, after the providers' months).
  Both read the speed through one function (`Wiring/UtilityLinks.Speed`), unknown with the utilities or the option Internet speed matters (`utilities_net_speed`) off, and then everything runs exactly as before.
  The Tech app's firm page, the Web shop app's site page and the Utilities app's plan picker show the figures from the same rules (`UtilitiesBridges.FirmWork`, `ShopMost`).

- **Your own broadband company** (`BroadbandCatalogue`, `BroadbandRules`, `BroadbandCompany`).
  Late in a game and very costly: the city's operator's licence (with a headquarters: its office, and where it bills from), then a fibre network dug district by district, each a fortune and two of the game's months.
  Where it is open the company is a fifth provider (`InternetCatalogue.Own`, set by `BroadbandRules.Apply`), and the player's own places connect free.

  Each district's homes and businesses (`BroadbandCatalogue.Market`) choose among the providers there month by month.
  Each provider has a weight: the rivals' holds as the market stands when the company is founded, so that nothing moves without a newcomer, and the company's grown with the district's trust over nine months.

  The weight goes by its price against its usual, squared, and the company's also by its reliability (the maintenance spent: outages on twenty days in a thousand as usual) and its marketing (each dollar winning less than the last).

  Each share moves a fifth of the way to its weight's share a month.
  The rivals answer: a rival's price is held under its highest by the company's share of its markets (`BroadbandRules.AnswerCap`).

  Each morning the day is booked at the headquarters (`UtilitiesGame.Book`): the customers' bills as takings (none on a day the network failed, nor without a headquarters), engineers and the call centre as salaries, the network (backbone, boxes and upkeep) as the cost of what it sells, the marketing as marketing; at the turn of the month the new connections' installations.

  `GameBooks.Refresh` enters the takings and the network's row in the headquarters' statement, so the game's tax and its bank count them; the running costs are deductible, the capital (the licence, the networks dug) is not, and the sale is not taxed.

  A buyer (Harbour Fibre) pays four years of the last twelve months' profit, or three tenths of what the network cost if that is more, and runs the network on at the usual prices; the player's places on it pay like anybody's, and the licence goes with it.

  The checks hold that a district pays back its network in about two years, that the market keeps still without a newcomer, that nobody is won in a day, and that one squeezed month cannot raise the price of a sale by more than four times its gain.

- **Bills at a business** (power, water, the internet, their fees) are deducted by the game's tax as its own charges are (`UtilitiesGame.Pay`, deductible); a home's, and a deposit, are not.

The save line (`reallife:utilities`, version 2): the days added up, the seed, the months and each rival's price, each account's place (escaped) and figures, and the broadband company (`~n:`, none in a line of version 1).
Nothing runs per frame but the office's panel check once a second; the effects once an hour, the books, outages, prices and the company's day once a morning.

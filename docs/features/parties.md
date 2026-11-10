# Parties and company events

## In a sentence

Under the living economy's master option **Living population**, the player hosts the people they know at home or at a place in the city, holds events for the staff of a business, and is now and then asked to a party of somebody else's, each for a fee and with gains that are capped by the week and the month however the money is split; the rival tycoons' shops hold staff events by the same rule.

## Where the code is

- Folder: [`Scripts/Parties`](../../Assets/Mods/RealLifeOverhaul/Scripts/Parties/); the pages in [`Scripts/Ui/Parties`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/Parties/); the wiring in `Scripts/Wiring/Wiring.PartyLinks.cs`.
- Option: "Parties and company events" (`living_parties`, on by default, under `living_population`). Off, nothing is booked, begun, charged, refunded or missed, the app is off the phone, and what is planned is kept.
- Save: one line, `reallife:parties`. Checks: `EconomyCheck --parties` (`PartyChecks.cs`).

```
Parties/                       the first nine are game-free and compiled by EconomyCheck
  PartyCatalogue                 every number, with its unit and where it comes from; the enums
  PartyFile                      Party, StaffEvent, StaffPlan, Invite, Lift (dated gains), RivalEvent, and the file that holds them
  IPartyWorld                    everything the steps ask of the game and the other features
  PartyRules                     time, the seeded draws, the cap, cost, room, who comes and why, turnout, quality, worth, the ceiling
  PartyDesk                      a party from booking to end, calling off, missing it, invitations, the clock's look, the day's round
  StaffDesk                      a company event from booking to end, the lift and its fade, a manager's schedule, the rivals' outlets
  PartyStore                     the save's one line
  PartyHosted                    the door for another feature: "the player hosted X with guests Y"
  PartyKeys                      the locale keys of a place, a tier, a kind, an answer, a state, a refusal
  PartyGame                      the only file that touches the game: IPartyWorld answered from the game
  PartyDirector                  the one-second timer, the save, the option, the question box at the place, the calls the pages make
  PartyDirector.Tests            the three tests
  PartyApp                       the phone app: the kit's pages, and without the interface a page of what is coming
  PartyBridges, PartyText, PartyGuide
Ui/Parties/
  PartyFacts, PartyPages         the pages as plain data, game-free: Host a party, Staff events, Diary
  PartiesLive                    reads the facts from the director and carries every button to it
```

Every step is written against `IPartyWorld`, so the offline checks drive the very code the game runs, with a world of their own that keeps a ledger of every money move. The order of each step is the rule: the option and the session are asked first, a refusal comes before any money, and money moves before anything is recorded.

## A party with friends

**Booking** (`PartyDesk.Host`, from the app's Host a party page):

- Place: the player's home, a restaurant or a nightclub's bar. The venues are the social life's own for a meal and for drinks (`SocialCatalogue.Outing(...).Match`, found with `SocialPlaces.Find` within 2,500 m of the player and kept by address); the game has no hall. A venue must be open for every hour of the party by its own week of hours.
- Food and drink a head, at the start's prices, from the social life's own outing prices (`SocialCatalogue.Outing(kind).BaseCost`) and asked of `PriceRules.At` at the city's price level:

| tier | a head at home | at a venue (150 %) | lasts | quality before the rest | worth at quality 100 |
|---|---|---|---|---|---|
| Snacks and soft drinks | two coffees, 12 | 18 | 2 h | 55 | 450 per mille of the cap |
| A meal | a meal, 24 | 36 | 3 h | 70 | 700 |
| A meal and drinks | a meal and drinks, 56 | 84 | 4 h | 85 | 1000 |

- The cost is a head for everybody who said yes and for the host, paid at once from the player's pocket (`reallife_transaction_party_host`, through `BizBooks.Book`).
- When: the start is a whole hour from 10:00 to 22:00, at least 3 hours and at most 14 days ahead; one party a day, three planned at most.
- Room: a guest to 8 square metres of the place's floor (`BuildingHelper.GetBuildingSquareMeters`), 4 at least, and 16 (home), 24 (restaurant) or 40 (bar) at most.
- Guests: anybody met and in touch. What each answers is settled when the invitations go out, by the first reason that holds (`PartyRules.Answer`), from what the social life keeps of them:

| answer | when |
|---|---|
| Does not know you well | not met, drifted away, or an acquaintance with closeness under 20 for the home, 10 for a venue |
| Still cross with you | a memory of being stood up, snapped at, money kept or a break-up in the last 14 days |
| Asleep then, At work then | their own sleep and free hours (`SocialPerson.Asleep`, `FreeAt`; weekends 10:00 to 23:00) |
| Too far for them | the place is in another district: a homebody always, an acquaintance three times in ten |
| Having a rough day | mood -2 today: half the time |
| Has something on | 8 in 100, the flaky 25 in 100, the reliable, family and a partner never |
| Would come | otherwise |

**Being there.** The party is begun at the place, never from the app: from 30 minutes before the start, while the host is inside the home or within 25 m of the venue's door (or inside it), the game's own question box asks, and "Begin" settles who turns up (`PartyRules.ShowsUp`: the odds the social life cancels any plan with, never the reliable) and passes the time to the party's end with the game's own time skip. In a Going Public session the clock is shared, so no time is skipped and the party runs in the session's time. Over 20 minutes after the start the host is late; 75 minutes after it with no host the party is **missed**: nothing comes back, and every guest who said yes loses 6 trust and 5 affection (half of standing one person up) and remembers being stood up.

**How it went** (`PartyRules.Quality`, 0 to 100): the tier's figure; +5 when three in four of those who said yes came, -10 under half; at home +5 for tidiness 70 or more, -15 under 40, -25 with no power, -10 with no water; -15 for a late host, -20 for one gone at the end. From 60 it is a good party; under 40 a bad one.

**What it gives.** Each guest who came gets the party's worth times the quality times how much it is their kind of thing (the social life's liking, 0.7 to 1.4), as a share of a cap:

- The cap is 8 familiarity, 6 affection and 3 trust in any 7 days (`PartyCatalogue.WindowDays`), about one outing. Every gain is kept with its day (`Lift.Gains`) and counts in full for seven days, so five parties in a week give a guest no more than one lavish one.
- No party lifts a figure above 70 of 100 (`PartyCatalogue.Ceiling`), and a party is no date: the stages that are asked for (dating, a partner) are untouched.
- A party that gave half the cap or more counts as a thing done together. The guest remembers the party.
- A bad party gives nothing and costs a point of affection with each guest. A party nobody came to does nothing and says so.
- At home each guest who came leaves the flat 2 points messier.
- A party that 10 guests or more came to is a piece in the next morning's City Ledger (`PartyHosted.Large`, through the paper's own chain of pieces, `News.Extra`).

**Calling it off** (`PartyDesk.Cancel`): 80 % back a day or more ahead and nobody minds; 50 % under a day, and each who said yes loses a point of trust and of affection; nothing in the last 3 hours, with the social life's own cost of a plan called off at short notice (-4 trust, -3 affection). A party never held gives nothing, so nothing is kept by booking and cancelling. A place found shut or gone on the day calls the party off with everything back and nobody minding.

**A venue of the player's own.** The food and drink are charged at cost (the home's price, no service), and nothing is booked there as a sale: no fee of a party or an event ever comes back as takings.

## Other people's parties

Once a day from 10:00 (`PartyDesk.InviteDay`), when 10 days have passed since the last invitation and none is open, a draw of 6 in 100 has a friend or closer who is in touch (never family, never somebody living with the player) ask the player to a party of theirs in two days at 19:00, at a restaurant or bar open then, by a text of the social life's own with its limits; with no text sent there is no invitation. The answer is given in the Diary:

| what the player does | what it does |
|---|---|
| says yes, is at the place in its time and stays to the end (150 minutes) | 600 per mille of the same weekly cap with the host, a thing done together, remembered |
| says no | -1 affection |
| never answers | -2 affection |
| says yes and does not come within 75 minutes | -6 trust, -5 affection, remembered as being stood up |

## A company event

**Booking** (`StaffDesk.Book`, from the Staff events page): a business of the player's with staff, one of three kinds, 3 hours to 14 days ahead, and at most one event a business within 7 days of another. The cost is a head for every one of the staff, paid at once at the business's own address under `reallife_transaction_parties_staff`, which `BizLines` lists as a running cost, so it stands in the business's statement.

| kind | a head (start's prices) | lasts | can start |
|---|---|---|---|
| Staff lunch | a meal, 24 | 1 h | 11:00 to 14:00 |
| Evening out | a meal and drinks, 56 | 3 h | 17:00 to 21:00 |
| Team day | a meal, drinks, the cinema and the gym, 113 | 8 h | 09:00 to 10:00 |

**Company money.** An event is refused when paying it would take the wallet under what it keeps for the investors of the player's firms (startup money's floor, `TechDirector.FloorNow`); so is a party. Spending at a firm's own address is the firm's own lawful spending ([startup money](startup-money.md)); it is bounded here by one event a week and the kinds' prices, and nothing of it comes back to the player.

**The working time it takes.** When the event's hour comes the business is closed for the opening hours it covers, by business pressure's closures (`BusinessDirector.CloseFor`, `ClosureSource.StaffEvent`), and opens again by itself; everybody but the sick comes. Outside opening hours nobody at or under 20 satisfaction comes, nor one in five by a draw. A business that is open then and cannot be shut has its event called off with everything back. An event with no staff left, or at a business no longer the player's, does nothing, says so and gives the money back.

**What it gives**, when it ends, to each who came (`StaffDesk.LiftOne`): a lunch 330, an evening out 580 and a team day 1000 per mille of a cap of 12 points of the game's satisfaction and 8 points of the company's standing in any 30 days; half to somebody underpaid (the company's own rule, `StaffRules.Underpaid`, or the game's wage formula where the company keeps no file). The satisfaction is given once and the game's own hourly rule wears it down again; the standing wears off evenly over the 30 days and is all taken back (`StaffDesk.Fade`, once a day). Quitting, strikes and rivals' offers read satisfaction and standing as before, so an event lowers them only as far as it lifts those.

**A manager's schedule.** Under The manager's schedule a business's manager (appointed in the Company app and at work) is told to hold a kind every 30, 60 or 90 days: the day before it is due they book it for the next day by the very rules above. With no manager the schedule waits; a refusal is told once a week.

## The rivals and the city

- **Rival tycoons** (`StaffDesk.RivalDay`, called by the rivals' own day through `RivalBridges.StaffEvents`): each outlet with a crew holds a staff event every 45 to 59 days (a draw of its own), a lunch (50 in 100), an evening out (35) or a team day (15), at the player's price a head, paid from its tycoon's fortune; an outlet whose tycoon cannot pay holds none. Its crew's standing is lifted by the same cap and fades the same way. The morning after, the City Ledger's rivals' pieces say so, and the Diary lists the last six.
- **People of the city** hold parties of their own and ask the player: see Other people's parties.

## Where the player finds it

- **The Parties app** on the phone (Social and life folder): Host a party, Staff events and the Diary, on the kit's app window (`VentScreen`, screen `parties`). Without the RLO interface the app shows what is coming, with each thing's answer and its way out; a new party or event is planned on the interface.
- **People**: the overview has "Host a party", which ends the call and opens the app there.
- **Company**, Staff: "Hold a staff event" opens the app on Staff events.
- **Planner and Calendar**: a party, an accepted invitation and a company event stand on their day and hour at their place (source `parties`).

## Going Public

A guest of another player's city can book, begin, call off, answer and schedule nothing, and is refused before the place is read or any money is booked (`PartyDesk.Stand`); nothing is missed or drawn for them meanwhile. A host plays as alone, with no time skipped. Every draw is from the save's own seed (`PartyRules.Roll`), nothing is sent, and only the player's own businesses and people are touched. The rivals rest in a session, and so do their events.

## For another feature

`PartyHosted.Held` is raised once when a party the player hosted ends, with its place, tier, quality, cost and the guests who came; `PartyHosted.Since(file, day, list)` (or `PartyDirector.Hosted`) reads those still kept (the last eight). It says what happened and changes nothing.

## The save

`reallife:parties`: "v=1|seed|next id|day settled|last invitation|off since", then one part a record led by a letter (P party with its guests, E event, S schedule, I invitation, L lift with its dated gains, R rival's event). A save with no line starts with nothing. New fields go at the end of a record; a part or a field this version does not know is passed over, so a line from a newer version is read as far as it goes. A line whose head cannot be read is kept as it was read. Numbers use the invariant culture. The business's closure for an event is business pressure's own save.

With the option off the line is still written. A party, an invitation or an event that came due while it was off is called off, with everything back and nobody minding, when it is on again; one under way is settled then.

## Cost

No Update. A one-second timer on the real clock compares the game's minute; when it has moved, a pass over the handful of open records. The day's round runs once a day. While a party's time has come, one look a second at whether the host is there (a venue's door is looked up once and kept). The pages are written when the director's revision moves or a button is pressed.

## Tests

- **A friend asks me to a party that starts this hour**: an invitation from a friend at a restaurant or bar open now.
- **What is planned next starts this hour**: the next party, accepted invitation or company event is moved to now.
- **Run the day's round of parties now**: the lifts fade, the managers book what is due, and an invitation is drawn as if the gap were over.

With Developer diagnostics on: `Parties check (host)`, `(event)`, `(begin)`, `(test invitation)`, `(test round)`, each with what was asked against what happened and PASS or FAIL.

## What was left out, and why

- **Guests meeting one another.** The social life keeps how each person stands with the player and nothing between two of its people (`SocialModel.cs`, `SocialPerson`: no field names another person), so a party has nobody to introduce to anybody.
- **A hall.** The game has no such business type; the venues are the two the social life already meets people at.
- **A large party on Flare.** Flare's posts are written by its own accounts from its own rules and have no door for another feature's piece, so a large party reaches the paper and not Flare.
- **The player attending their own staff event.** It is the staff's; the owner's part is the booking.

## Not verified in the game

That the game's question box comes up at the place and its "Begin" starts the time skip; that the skip ends exactly at the party's end; how the app's pages look on the kit and how its older page looks without it; that a business closed for an event opens again at its hour; the People page's second button; the wording and length of the notices on screen.

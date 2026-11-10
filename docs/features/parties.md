# Parties and company events

## In a sentence

Under the living economy's master option **Living population**, the player hosts the people they know at home or at a place in the city, holds events for the staff of a business, and is now and then asked to a party of somebody else's, each for a fee and with gains that are capped by the week and the month however the money is split; the rival tycoons' shops hold staff events by the same rule.

## Where the code is

- Folder: [`Scripts/Parties`](../../Assets/Mods/RealLifeOverhaul/Scripts/Parties/); the pages in [`Scripts/Ui/Parties`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ui/Parties/); the wiring in `Scripts/Wiring/Wiring.PartyLinks.cs`.
- Option: "Parties and company events" (`living_parties`, on by default, under `living_population`). Off, nothing is booked, begun, charged, refunded or missed, the app is off the phone, and what is planned is kept (what came due meanwhile is settled when it is on again: see The save).
- Save: one line, `reallife:parties`. Checks: `EconomyCheck --parties` (`PartyChecks.cs`).

```
Parties/                       the first nine are game-free and compiled by EconomyCheck
  PartyCatalogue                 every number, with its unit and where it comes from; the enums
  PartyFile                      Party, StaffEvent, StaffPlan, Invite, Lift (dated gains), RivalEvent, and the file that holds them
  IPartyWorld                    everything the steps ask of the game and the other features
  PartyRules                     time, the seeded draws, the cap, cost, room, who comes and why, turnout, quality, worth, the ceiling
  PartyDesk                      a party from booking to end, calling off, missing it, invitations, the clock's look, the day's round
  StaffDesk                      a company event from booking to end, the lift and its fade, a manager's schedule, the rivals' outlets
  PartyStore                     the save's one line (and the open and the first write of it)
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

**Calling it off** (`PartyDesk.Cancel`): 80 % back a day or more ahead and nobody minds; 50 % under a day, and each who said yes loses a point of trust and of affection; nothing in the last 3 hours, with the social life's own cost of a plan called off at short notice (-4 trust, -3 affection). A party never held gives nothing, so nothing is kept by booking and cancelling.

**Every way a party is given up by the host is calling it off at the hour it was done** (`PartyDesk.Withdraw`: the refund and the guests' minds are those of that hour's notice, never a full refund):

- The option switched off: the hour is the minute the option was found off, taken when it is on again and the party's time came meanwhile.
- A venue of the player's own that they shut (their switch or opening hours, not a closure the mod or the city made) or no longer hold: the hour is the day's round of 10:00 that finds it (`PartyDesk.Watch`, one place look a day for each party at a venue of their own) or, if that did not, the party's own hour.

**A place that cannot hold the party for any other reason** (`PartyDesk.Gone`): a venue of somebody else's found shut, a closure of the mod's or the city's on a venue of the player's own, or a home the host no longer holds (they moved house since the booking). The party is called off with everything back and nobody minding, found at the party's hour if the host is there (`Begin`) and at the 75 minutes after it if they are not, so a host who moved house is never counted as having stood their guests up. The home a party leaves messier, and the tidiness it is judged by, are those of the address it was booked at, not of the home the player is in.

**A venue of the player's own.** The food and drink are charged at cost (the home's price, no service), and nothing is booked there as a sale: no fee of a party or an event ever comes back as takings.

## Other people's parties

Once a day from 10:00 (`PartyDesk.InviteDay`), when 10 days have passed since the last invitation and none is open, a draw of 6 in 100 has a friend or closer who is in touch (never family, never somebody living with the player) ask the player to a party of theirs in two days at 19:00, at a restaurant or bar open then, by a text of the social life's own with its limits; with no text sent there is no invitation. The answer is given in the Diary:

| what the player does | what it does |
|---|---|
| says yes, is at the place in its time and is there when it ends (150 minutes after the start) | 600 per mille of the same weekly cap with the host (the one cap that hosted parties use: a party and an invitation to the same person in a week give together no more than it), a thing done together, remembered |
| says yes, joins, and is gone when it ends | nothing gained, -1 affection as a no, and the Diary says "Left early" |
| says no | -1 affection |
| never answers | -2 affection |
| says yes and does not come within 75 minutes | -6 trust, -5 affection, remembered as being stood up |

Presence is asked at the end, as it is for a party the player hosts, and whatever the time skip did: in a Going Public session the clock is shared, no time is skipped, and the player can join and walk off; the invitation then pays nothing.

## A company event

**Booking** (`StaffDesk.Book`, from the Staff events page): a business of the player's with staff, one of three kinds, 3 hours to 14 days ahead, and at most one event a business within 7 days of another (one held for nobody counts too). The cost is a head for every one of the staff, paid at once at the business's own address under `reallife_transaction_parties_staff`, which `BizLines` lists as a running cost, so it stands in the business's statement. The event is booked for those staff: their ids are kept with it (`StaffEvent.Crew`).

| kind | a head (start's prices) | lasts | can start |
|---|---|---|---|
| Staff lunch | a meal, 24 | 1 h | 11:00 to 14:00 |
| Evening out | a meal and drinks, 56 | 3 h | 17:00 to 21:00 |
| Team day | a meal, drinks, the cinema and the gym, 113 | 8 h | 09:00 to 10:00 |

**Company money.** An event is refused when paying it would take the wallet under what it keeps for the investors of the player's firms (startup money's floor, `TechDirector.FloorNow`); so is a party. Spending at a firm's own address is the firm's own lawful spending ([startup money](startup-money.md)); it is bounded here by one event a week and the kinds' prices, and nothing of it comes back to the player.

**The working time it takes.** When the event's hour comes the business is closed for the opening hours it covers, by business pressure's closures (`BusinessDirector.CloseFor`, `ClosureSource.StaffEvent`), and opens again by itself; everybody but the sick comes. Outside opening hours nobody at or under 20 satisfaction comes, nor one in five by a draw. A business that is open then and cannot be shut has its event called off with everything back.

**An event begins only within 30 minutes of its hour** (`PartyCatalogue.EventLateMinutes`, `StaffDesk.Begin`). The game's time skip (a party, a job, a night's sleep) runs its own hourly rules for every hour it passes but gives the mod's clock only its one look a second, so a skip can carry the clock past an event's whole hours. Such an event was not held: it is called off with everything back, and the business is never shut for it afterwards. The business's real opening hours were not closed during the skip, so nothing is claimed for them. Seen within 30 minutes of its hour, an event begins and the business is shut for the hours still to run.

**At the event's hour** (`StaffDesk.Begin`), of the staff it was booked for:

- A head is paid for each. Those who have left the staff by the hour are not fed: their heads come back in full, and a notice says so. Somebody hired since the booking was not paid for and gains nothing.
- With none of them left, the event does nothing, says so and gives the money back.
- A business no longer the player's is the player's doing (they sold it): the event is called off by the notice at the moment it is found (the day's round of 10:00, `StaffDesk.Watch`, or the event's hour), not a full refund.

**At its end** (`StaffDesk.Settle`) each of those it was booked for, still on the staff and able to come, gets their share. One who leaves during the event was fed and stays paid. A business sold while it runs leaves nobody to count: the event was held, stays paid, and says so. If nobody could come (all off sick, all unable after hours), the event does nothing, says so and gives the money back; it still counts for the week and for a manager's schedule, which counts from its day, so a manager does not book into the same cause the next day. An event of the manager's schedule that was called off, for whatever cause that was not the staff's, is likewise not tried again before the schedule's days have run from its day.

**What it gives**, when it ends, to each who came (`StaffDesk.LiftOne`): a lunch 330, an evening out 580 and a team day 1000 per mille of a cap of 12 points of the game's satisfaction and 8 points of the company's standing in any 30 days; half to somebody underpaid (the company's own rule, `StaffRules.Underpaid`, or the game's wage formula where the company keeps no file). The satisfaction is given once and the game's own hourly rule wears it down again; the standing wears off evenly over the 30 days and is all taken back (`StaffDesk.Fade`, once a day). Quitting, strikes and rivals' offers read satisfaction and standing as before, so an event lowers them only as far as it lifts those. An event from a save made before its staff were kept counts whoever is on the staff, as it did.

**Calling an event off** (`StaffDesk.Cancel`) is the same as for a party: 80 % back a day or more ahead, 50 % under a day, nothing in the last 3 hours, to the business's books. The option switched off and the business sold are calling it off at the hour they were done (as for a party above).

**A manager's schedule.** Under The manager's schedule a business's manager (appointed in the Company app and at work) is told to hold a kind every 30, 60 or 90 days: the day before it is due they book it for the next day by the very rules above. With no manager the schedule waits; a refusal is told once a week.

## The rivals and the city

- **Rival tycoons** (`StaffDesk.RivalDay`, called by the rivals' own day through `RivalBridges.StaffEvents`): each outlet with a crew holds a staff event every 45 to 59 days (a draw of its own), a lunch (50 in 100), an evening out (35) or a team day (15), at the player's price a head, paid from its tycoon's fortune; an outlet whose tycoon cannot pay holds none. A tycoon pays only from the worth above the floor the rivals' own day keeps a fortune at (`RivalRules.At(StartWorth, level)`, which `Grow` lifts a lower one back to every day): money paid from under it would come straight back, so with less than the cost above it no event is held. An outlet counts its days from the event it last held, else from its opening move's landing; when that move is no longer kept the count starts from the day the outlet was first seen, and that day is saved (the `M` part), so it is not restarted every day. Its crew's standing is lifted by the same cap and fades the same way. The morning after, the City Ledger's rivals' pieces say so, and the Diary lists the last six.
- **People of the city** hold parties of their own and ask the player: see Other people's parties.

## Where the player finds it

- **The Parties app** on the phone (Social and life folder): Host a party, Staff events and the Diary, on the kit's app window (`VentScreen`, screen `parties`). Without the RLO interface the app shows what is coming, with each thing's answer and its way out; a new party or event is planned on the interface.
- **People**: the overview has "Host a party", which ends the call and opens the app there.
- **Company**, Staff: "Hold a staff event" opens the app on Staff events.
- **Planner and Calendar**: a party, an accepted invitation and a company event stand on their day and hour at their place (source `parties`).

## Going Public

A guest of another player's city can book, begin, call off, answer and schedule nothing, and is refused before the place is read or any money is booked (`PartyDesk.Stand`); nothing is missed or drawn for them meanwhile. A host plays as alone, with no time skipped. Every draw is from the save's own seed (`PartyRules.Roll`), written with the save's first line (`PartyStore.Put`), so the answers on the planning page do not draw again on each load; nothing is sent, and only the player's own businesses and people are touched. The rivals rest in a session, and so do their events.

## A second save in one run

The director's file belongs to the save that was open when it was read (`PartyDirector.Mine`). A second save loaded in the same run is read by the clock's next look; until then the previous save's file is handed to nobody: the pages, the paper, the Planner, the booking calls and the rivals' day (`RivalDay`, which may come first) all see no file, and the rivals' outlets hold their events on the next day instead.

## For another feature

`PartyHosted.Held` is raised once when a party the player hosted ends, with its place, tier, quality, cost and the guests who came; `PartyHosted.Since(file, day, list)` (or `PartyDirector.Hosted`) reads those still kept (the last eight). It says what happened and changes nothing.

## The save

`reallife:parties`: "v=1|seed|next id|day settled|last invitation|off since", then one part a record led by a letter (P party with its guests, E event with the ids of the staff it was booked for at the end, S schedule, I invitation, L lift with its dated gains, R rival's event, M the day a rival outlet's count started from). The line is written from the first save, with nothing booked, so the seed is kept. A save with no line starts with nothing. New fields go at the end of a record; a part or a field this version does not know is passed over, so a line from a newer version is read as far as it goes. A line whose head cannot be read is kept as it was read. Numbers use the invariant culture. The business's closure for an event is business pressure's own save.

**Limits.** A line holds at most 64 parties, 512 events, 64 invitations and 96 rivals' events (`PartyCatalogue.Stored...`); past that the oldest finished ones are left out when it is read, never one still to come or under way. Lifts (the caps' records), schedules and marks are never left out for want of room: their limits (20,000, 2,000, 2,000) only guard against a damaged line, and the rules keep far fewer.

With the option off the line is still written. A party, an invitation or an event that came due while it was off is settled when it is on again: a party or an event is called off as if by the player at the minute the option was found off, by that minute's notice (the refund and, for a party, the guests' minds), an invitation is dropped with nobody minding; one under way is settled then.

## Cost

No Update. A one-second timer on the real clock compares the game's minute; when it has moved, a pass over the handful of open records. The day's round runs once a day, with one place look for each planned party at a venue of the player's own and one staff look for each planned event. While a party's time has come, one look a second at whether the host is there (a venue's door is looked up once and kept). The pages are written when the director's revision moves or a button is pressed.

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

## What the game's time skip does

Checked against the game's code (`TimeMachine.Update` runs `GameManager.RunMainGameTick` with the minutes that frame moves, `Hourly` rules run for every hour it crosses and `NewDay` for every midnight, with wages, rents and `EnergyHelper.UpdateEnergy(minutes)` for the needs): a party or a team day costs its hours honestly, and each charge falls once per hour or day crossed, none skipped and none twice. What does not run per hour is the mod's own clock, one look a second: it can see a game minute far on from the last. That is why a staff event only begins within 30 minutes of its hour (above), a party is settled by presence at its end, and the day's round runs once for the day it finds. In a Going Public session with a shared clock no skip is started and the party runs in the session's time.

## Not verified in the game

That the game's question box comes up at the place and its "Begin" starts the time skip; that the skip ends exactly at the party's end; how the app's pages look on the kit and how its older page looks without it; that the mod's clock sees an event's hour in time during a normal game day, and what a time skip leaves of it (an event passed in one is called off, as above); that a business closed for an event opens again at its hour; the People page's second button; the wording and length of the notices on screen.

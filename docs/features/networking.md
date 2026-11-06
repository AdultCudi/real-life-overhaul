# Networking

## In a sentence

Some of the people the player meets hold a position (a buyer at an importer, a wholesaler's manager, a law firm's partner, a recruiter, an investor, a city official, a landlord), said when they are met and on the People page, and as the relationship grows each does the player a favour or two, once each.

## Where the code is

- Folder: [`Scripts/Networking`](../../Assets/Mods/RealLifeOverhaul/Scripts/Networking/), namespace `RealLifeOverhaul.Networking`.
- Pairing: [`Wiring/Wiring.NetworkLinks.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Wiring/Wiring.NetworkLinks.cs).

| File | What it is |
| --- | --- |
| `NetworkFile` | the positions, the favours, a person's record (`NetContact`), the save's file: game-free |
| `NetworkRules` | who holds a position, the two favours of each, when one is ready, the gap, the seeded hash: game-free |
| `NetworkCatalogue` | the places people work for and every locale key: game-free |
| `NetworkStore` | the save's line `reallife:network`: game-free |
| `NetworkBridges` | what it asks of the social life, the careers and the investors |
| `NetworkDirector` | the record, meeting, the day's text, asking a favour; its part `.Tests` |
| `NetworkGame` | the only file touching the game: the save's data, importers and wholesalers, their contacts, delivery contracts, buildings for sale, the destination, notifications |
| `NetworkGuide` | the feature list's entry, its Guide page's facts |

## Who holds a position

Rolled once, when somebody is met, from the social life's cast seed (`SocialState.CastSeed`, saved with the cast) and the person's place in the cast (`NetworkRules.RoleOf`, a fixed hash, never `string.GetHashCode` or an unseeded roll), so a person is the same on every machine and every load. The record keeps what was rolled, so a later change to the tables moves nobody.

| met | chance (per mille) |
| --- | --- |
| Mom, the old friend | none |
| by chance (street, cart, gym, home, shop, online, street talk) | 180 |
| after a shift | 260 |
| at an evening (Standing) | 300 |
| brought along by a friend | 450 |
| brought along by Mom | 600 |

Seven positions, dealt evenly. A buyer's importer and a manager's wholesale store are the city's own (`NetworkGame.Supplier`: the lowest draw over the game's buildings of that kind, so no list order matters); other places are names from `NetworkCatalogue` (an official's office is a locale key).

Everybody met before the feature existed, or while its option was off, is rolled once, quietly, as met by chance, on the first day's round of a save (`NetworkDirector.Sweep`).

## Being told

- In the meeting: the friend who introduces them says what they do (`reallife_social_net_intro_*`), or they say it themselves (`reallife_social_net_meet_*`), through `SocialLinks.Position`. The words are worked out by the same roll before numbers are swapped (`NetworkDirector.Peek`), and `SocialLinks.Met` (from `SocialLife.Meet`, every way of meeting) records the same.
- A notification (`reallife_social_net_met`) and a first text in their thread (`reallife_social_net_text_*`, outside the day's cap).
- The People page: their position and what they could do next, or that they have done what they can.

## The favours

| position | first (a friend, known 3 days) | second (a close friend or more) |
| --- | --- | --- |
| buyer at an importer | the importer's contact | a wholesaler's contact |
| wholesaler's manager | the wholesaler's contact | the delivery fee waived |
| law firm partner | freelance work | a job lead |
| recruiter | a job lead | freelance work |
| investor | an investor meeting | a property tip |
| city official | a property tip | a job lead |
| landlord | a property tip | an investor meeting |

- One favour every 3 game days from anybody (`NetworkRules.GapDays`); never one twice (`NetContact.Given`, a bit each).
- When one is ready, the day's round of the social life (`SocialLinks.DayDone`) has that person text to call (`reallife_social_net_ready_*`), once, under the social life's limits. On a call, Something else offers it first (`SocialPhone.NetworkLabel`, `NetworkAsk`).
- A favour that cannot be done now (no profession to work in, no contracts with that wholesaler, nothing for sale) is said with its reason and can be asked again another day.

What each does:

- **Job lead** (`CareersDirector.Lead`): a vacancy of the city's in a profession the player is qualified for, at the best rung within reach (a better one than the job held), open 14 days, as a referral (`Via.Referral`: ten more of the hire score at the screening and the interview), with the friend's name on it (`Opening.Lead`, saved at the end of the vacancy's record only, so an application's and the job's records never shift). The Career app says who put the player's name forward.
- **Freelance work** (`CareersDirector.Referred`): a contract offered at once on the Own work tab, as the day's roll would make it.
- **Importer or wholesaler** (`NetworkGame.Introduce`): the game's own contact of the place, as talking to its manager in person gives it (`BuildingRegistration.GetOrAddBusinessContact`), so its call opens the game's partnership or delivery dialog; the place becomes the destination.
- **Better terms** (`NetworkGame.WaiveFees`): the delivery fee of every delivery contract the player has with that wholesaler set to nothing (the contract's own saved field). In a Going Public session only the player's own businesses' contracts.
- **Investor meeting** (`VenturesDirector.Introduced`): one of the save's investors (not a tycoon while the player has no business open) sees the player for a pitch from today, whatever their diary or an old pitch said.
- **Property tip** (`NetworkGame.Tip`): a building for sale drawn for the person, made the destination; its seller takes an offer of 90 % of the asking price through the game's own purchase screen (`BuildingForSale.acceptOfferRate`). In a Going Public session the tip names the building and changes nothing.

## Rules kept

- No Update, no coroutine, no save handler: it is asked on a meeting, on a call and once a game day; the line is written into the save's data when something changes. Idle cost: nothing.
- The save's own line `reallife:network`; a save loads without the mod. A line this version cannot read is kept as it was read and the feature stands down for that save.
- Nothing is sent over the network; everything is each player's own cast and things.
- Option: "People with positions" (`social_networking`, on) in the social life's group. Off: nobody is rolled, told or asked.

## Test buttons

- "Test: a friend introduces somebody with a position, here" (`NetworkDirector.TestIntro`): the old friend (else Mom) brings the next person to be met, holding the next position in turn each press.
- "Test: call somebody with a position, a favour ready": their ready text in the thread and the call opened, the favour ready whatever the stage.
- "Test: every favour at once for somebody you know": all seven done for one person, nothing kept as done.

Each writes a `Networking check (...)` line ending PASS or FAIL. Offline: EconomyCheck's `NetworkChecks.cs` (`--networking`).

## Not verified in the game

That the game's contact of an importer opens its partnership dialog when given this way, that a waived delivery fee shows as nothing in BizMan, and that an offer at 90 % of the asking price is taken by the purchase screen.

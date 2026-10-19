# Family

## In a sentence

A partner who moves in, an engagement and a wedding, children, the wider family and the hard times, each part optional.

## Where the code is

- Folder: [`Scripts/Family`](../../Assets/Mods/RealLifeOverhaul/Scripts/Family/).
- Read first: `FamilyDirector.cs` (the one component), `FamilyState.cs` (the save), `FamilyBalance.cs` (every cost and threshold) and `FamilyBridges.cs` (what other features implement, call and may read).

| File | What it is |
| --- | --- |
| `FamilyDirector` | the one component: a tick a second; its parts `.Prompt` (the key prompt, the objective, questions), `.Sequences` (fade, time skip, the photograph, scenes), `.Tests` (the test buttons) and `.HardTimes` (the difficult moment's test button) |
| `FamilyScene`, `FamilyModifiers` | the base of a staged scene; the moods in the game's happiness registry |
| `FamilyState` | per-save state as one JSON string in modData |
| `FamilyBalance` | every cost, threshold and time scale |
| `FamilyBridges` | ISocialBridge, IHomeBridge, ITravelBridge, ILeisureBridge, ILawBridge and FamilyApi: what other features implement, call and may read |
| `Partner` | who the partner is: Social's person when its bridge is set, else the one record the default keeps |
| `FamilyCast` | the family's own three characters (seeded looks, formal or day wear) and FamilyProps (wish-list props with plain-shape stand-ins) |
| `FamilyPlaces` | the city's own buildings by kind of business (jeweller, planner, florist, hospital, school, theatre, restaurant...), the home, floor spots indoors |
| `HomeLife` | the timetable, moving in, routines, their share of the rent, chores, support, evenings out; `.AtHome` (the partner shown at home) and `.Places` (where they stand or sit, the cot and the high chair) |
| `Errands` | somewhere to be on a day (a scan, a dinner, the wedding, a school play) or to shop: marker, objective line, prompt at the door, what a miss does |
| `Courtship` | the ring at the jeweller, the answer and its odds; ProposalScene |
| `WeddingPlanner`, `WeddingDay`, `WeddingScene` | the plan (venue, size, extras, date, deposit, invitations, postponing, cold feet), the day itself, the ceremony |
| `Children` | deciding, pregnancy and adoption, the nursery, the day and the week, costs; `.Arrival` (appointments, birth, adoption), `.Growing` (milestones, school starting, birthdays), `.School` (school events, parties), `.Tests` |
| `WalkScene` | the pushchair walk |
| `Relatives` | Mom and the in-laws, the four family dinners of the game's year, next of kin |
| `HardTimes` | the difficult moments (ten questions with two answers, asked as they always were), counselling, time apart, coming home, divorce and custody |
| `HardTimesWatch` | when a difficult moment comes and which: what is going on, read once a game hour and once a game day, handed to the rules; the optional line it keeps in the save |
| `HardTimesGame` | the one place the difficult moments read the game: the money and its last seven midnights, the player's own places, the home's address, inside a business of their own |
| `HardTimesRules` | game-free (EconomyCheck, `HardTimesChecks.cs`): each cause's level, each moment's causes, the hour, the days between, how long a cause must last, the rest, the save's line |
| `HardTimesBridges` | `IHardTimesBridge`: what home life, careers, the social life, travel and the economy know of the player's days |
| `FamilyPhone` | the Family, Someone special and in-laws contacts and their calls; `.Choices` (everything that can be done from a conversation) |
| `FamilyPage` | the Family page's five pages |
| `FamilyTalk / FamilyText / FamilyCalendar` | conversations and menus, text and its fillers, what happens by the hour and the day |

## How it talks to other features

- `Wiring/FamilyLinks.cs` fills in Social (the partner is Social's person), Home (care when ill, broken nights) and Law (whether the player is locked up); see [The bridges](#the-bridges-familybridges-familyapi).
- `Wiring/Wiring.HardTimesLinks.cs` fills in `HardTimesBridges.Life`: what home life, careers, the social life, travel and the economy know of the player's days, for the difficult moments.
- Travel and Leisure have no matching door, so they stay at their defaults.

A partner who moves in, an engagement and a wedding, children, the wider family, and the hard times (`Scripts/Family`).
Everything is optional: the group "Family" on the options page has a switch for the whole of it and one each for the partner being shown at home, engagement and wedding, children, relatives, and hard times (chores, arguments, separation, divorce: off, and none of them ever happen).

Nothing depends on who anybody is: the partner is whoever the player is seeing, in any pairing, and the default introduction follows the "introduces you to" choice (anyone / women / men).

**What family life puts on screen.** Everything is the game's own interface; the mod draws nothing of its own.
A prompt ("buy a pushchair", "talk to Sam", "go in to the scan") is the game's panel over a place (`GameUi.DoorPanel`, a `WorldPanel` with one button): over the partner when the prompt is about them (`Follow`), otherwise over the player.

The first line says what it is, the button says the verb (`reallife_family_button_*`, one for each prompt key) and the game writes the player's key after it and runs it on that key or a click.

The objective (where to be and until when, the pushchair walk, the aisle) is the headline and first line of a copy of the game's job panel at the top right (`ShiftCard`); one notification stands in if that copy cannot be made.
The place markers were already the game's own destination marker (`FamilyPlaces.Guide`) and are unchanged.

Cost: nothing per frame; the director's one-second tick (five a second in a scene) tells the panel only what changed, and the texts are rebuilt only when the prompt or the objective changes.
A fault in the button's action switches family life off for the session with one log line, as before.

If the game's panel is showing another place the prompt waits and is tried again on the next tick; there is no other fallback for a prompt, because the same errands are listed on the Family page and the objective still shows.
Log lines with diagnostics on: `Family check (prompt panel): <prompt key>: the game's own panel is up: PASS` and `Family check (objective card): ... PASS`.
None of this has been seen in the game.

State is one JSON string in the save's modData (`reallife:family`, `FamilyState`); the moods are happiness modifiers with the mod's prefix, so `Core/ModState` takes them out of the save like the rest.
A save made with the mod loads without it.
What the save proper does keep is what the mod's other contacts already keep: three phone contacts (Family, Someone special, The in-laws) and transaction lines.

## What can be shown, and what is state

**Children are never shown as walking characters.** The game's people are adults: `AppearanceAgeHelper` clamps age to 18..80 and age only fades the wrinkles of the head material in (`_Fade`, 18 to 60).

There is no height or proportion value in `CharacterData` (gender, colour, strength, fatness, age, elements), no child mesh under either gender model, and the only way to make somebody small is to scale the whole rig, which gives a small adult (adult head-to-body proportions, adult face, adult clothes) and looks wrong.
So:

- a baby or toddler is a **pushchair** on a walk (the modelled prop, pushed by the player with `push_cart` on the arms over the game's own walk), a **cot** and a **high chair** standing at home (modelled; clean plain-shape stand-ins without the bundle), with the swaddled `baby_bundle` lying in the modelled cot while a child is under one (`FamilyProps.Tuck`: a piece of the cot object, pooled with it), and a **sound** now and then as a notice;
- a school-age child is a name and an age on the Family page, school reports, drawings on the fridge (messages), a school play at a real theatre and a parents' evening at a real school to go to, birthdays;
- no scaled-down adult is used anywhere.

**The partner is shown** at home (while the player is in that home and the timetable says they are in) and in three scenes: the proposal, the wedding, the pushchair walk.

They stand and walk; they never sit or lie: the game's chairs are driven by its customers' behaviour trees and its beds by its own sleep activity, neither of which one of our characters can use reliably, so "asleep" and "in bed" are state and a notice.

The family's own pool is **three characters** (`FamilyCast`: the partner, and for a wedding the officiant and one guest of the family's).
A wedding borrows up to four more from the living city's pool when it has them free, as guests in the clothes they stand in.

Everybody is the living city's kind of person (`CityPerson`): moved only by the navigation agent, standing only on spots found and tested for room (`CitySpots` out of doors; `FamilyPlaces.Floor` on the home's own navigation mesh, with room to the nearest edge and a complete path from the player).

Out of doors people walk in from out of view and are taken away out of view; a wedding is set out and struck under a cut to black.
At home they are there when the player walks in, or come in at the door when it is not being looked at.

## Living together

- **Moving in** needs the two of you close enough for long enough (default bridge: affection 60 and seven days together; Social: its own "partner" stage) and a home of the player's own (a rented residential building) with floor for the household: 30 m2 for two and 15 more per child (`Building.totalSqm`; a home whose size the game does not give counts as big enough).
- **Timetable** by what the partner does for a living (`HomeLife.Where`): office hours for most, earlies and lates for a nurse or a paramedic, evenings for a chef, mornings for a barista.
  With Social keeping the partner, what they do is their job in People (`PersonInfo.JobKey`, `PartnerJobRules`): a job Family knows keeps its hours (a cook or a bartender a chef's evenings), the rest work office hours, each with its own day's pay; a retired partner stays at home and brings in nothing.
- **Half days** as the player prospers (`PartnerWork`, rules in `PartnerWorkRules`, its own switch "Partner cuts back at work as you prosper"): once the game's daily income has been $800 or more for seven days in a row (`IncomeRules`: the family keeps the last seven days' figures, and the worst of them counts; every step of prosperity the partner goes by wants such a week and cash in hand) and the player has $10,000 in cash at the start's prices, a partner who lives with them texts to ask to cut back, and the talk waits in the Family contact. A yes makes their working day four hours from its start and halves their pay, so their share of the rent, and is followed by three warm texts over the next days (`PartnerGlad`: from the next day, three and six days on). The partner's words about the player's fortune (`PartnerVoice`, rules in `PartnerVoiceRules`) come between 09:00 and 21:00: none for 7 to 12 days after moving in, then 5 to 10 days apart, each a text in their thread and a notice with their name that opens their messages. An ask stays open a week, with a reminder on the fifth and sixth day; after the seventh it lapses, as a no, and while one is open nothing else is raised. It is talked over face to face at home: speaking to the partner opens it first, and their prompt says they want to talk. Warm words after a yes follow their own days; a no is asked again 30 days later; "back to full days" undoes it. Every new partner, or none, starts on full days. Off, nothing is asked and their days are full.
- **A bigger wish** (`PartnerWish`, rules in `PartnerWishRules`, under the same switch): once the game's daily income has been $5,000 or more for seven days in a row and the player has $100,000 in cash at the start's prices, a partner who lives with the player and still works asks once, by a text, either to stop working or to retrain for the job they always wanted (which, and which job, from their seed; never the job they have). Stopping: no work and no pay, undone by "back to work" from the Family contact. Retraining: 20 days at home without pay, then the new job, its timetable and its pay on full days, told by a text. A no is asked again 30 days later; a yes is never asked again of that partner. A new partner starts afresh. While they earn nothing (stopped, or retired in People) the player can ask them to chip in for the rent from the Family contact: with harmony at 60 or more they put a quarter of a week's pay at their old job towards it each week for four weeks; otherwise they would rather not (harmony -3), and the player can ask again 14 days later.
  An evening out of their own about one day in five.
- **No Family contact with the social life** (`FamilyPhone.Hides`: the social life keeps the partner, and home life with its routines is on, so the fridge's panel is there): the contact is left out, an earlier session's taken off the phone, and everything it offered is face to face. Talking to the partner at home has the whole list; the **Family** button in the home fridge's panel (`HomeFridgeButton`, `FamilyPhone.Board`) opens the family's page and the list without the partner, leaving out what needs them in the room; the partner's own People call has **Family matters** (`FamilyPhone.CallMatters`): making the home the player stands in theirs, counselling, time apart (call, come home, part or divorce) and the in-laws (Mom's, their favour). Standing in another home of the player's, rented or owned, the partner's messages in People have a **Make this our new home** button (`FamilySms`): the player's text, then theirs, and it is the family home if it has room. The button's click is the mod's, so it is taken off before every save and put back five seconds after; a save never has it. The family's own texts (school, drawings, the adoption agency) are notices meanwhile.
- **One day for People's partner** (`HomeLife.Where`, rules in `PartnerJobRules.Asleep`/`Work`): while the social life keeps the partner, the family's day is People's. They sleep by People's hours (a night owl from 02:00 to 10:00, anyone else from 23:00 to 08:00) and work on weekdays through the longer busy stretch around the hours People has them free (free 17 to 23: 8 to 17; a night owl free 11 to 18: 18 to 02), cut to four hours on half days and gone once they stop, retire or study. People asks the family back whether they are free (`SocialPerson.FreeOf`), so a call, a text or a plan never finds them asleep or at work when the family has them at home, or the other way round. Without the social life the family's own trades keep their hours.
  Asleep 23:00 to 06:00.
  At home for three days after a child arrives.
  Short days if they are the one looking after small children.
- **At home**: they stand by what the game calls a cooker, fridge, coffee machine (a cup held, a sip now and then), a sofa, television, armchair or bookshelf (reading the paper, or watching), or failing all furniture on clear floor; every minute or so they walk to another of those places.

  With a baby under a year old they hold it (`hold_baby`) at some of them, and a place beside the cot, where they rock it (`rock_cot`), is one of the places.
  Up to two of the places are beside something to sit on, and there they sit down (see [Sitting](#sitting-peopleseatingcs-the-partner-at-home-a-bench-on-a-walk)).

  They wave and greet the player at the door (lines by the hour and by how the player is), and within reach, facing them, the interaction key opens a conversation: both stand, face each other and animate (`talk_a` / `talk_b`).

- **Conversation**: a chat about what has really happened (the player ill, a shift worked today and what it paid, the baby, rain, a low mood, the wedding or the birth being days away), cooking together and an evening in front of the television (Home bridge; default a few lines, a meal and a mood), an evening out.
- **Money**: every evening at 18:00 their share comes in as its own income line: the smaller of half the day's rent and 35 % of their day's pay, never more than 600 (in a home with no rent: a fifth of their pay, at most 60).
  Halved while they work short days for the children.
- **Chores** (about three days in ten, a question with two answers), **neglect** (four days with not a word and the home cools, with a text every third day; while Social supplies the partner, its calls, texts, outings and meetings with them count as a word too, through the bridge's `PersonInfo.LastContactDay` and `TalkRules.LastTalked`), **broken promises** (standing them up, missing an appointment), and **arrests** (`FamilyApi.PlayerArrested`, told by `Wiring.Arrests.FamilyHears` once as it happens; only with a partner) all move "harmony", the one figure for how things are at home (0..100, shown on the Family page in words).
- **Support**: ill and at home in the evening, they look after the player (Home bridge; default eight hours off the illness and a mood); after a bad day (happiness under 35) they say something that helps.
- **Evenings out**: Leisure bridge, else Social's outing, else dinner at the nearest real restaurant: the game's destination marker, a key prompt at its door, the bill (90), two hours skipped, and a line.
  Not turning up is noticed.

## Sitting (`People/Seating.cs`): the partner at home, a bench on a walk

**What the game has to sit on, and how it seats its own people.** Looked for in the decompiled game:

- every chair, armchair, sofa, stool and indoor bench a player can place is a `Controllers.SeatController` (an `ItemController`), with one or more sitting positions (`SittingPosition`: one that is free, or null) and its own record of which are taken (`Occupied`, `OnSittingChanged`);
- the city's street benches are `OutsideBenchController`, each with `sittingPosition` (the player's) and `aiSittingPosition` (where the game itself seats a pedestrian while the bench is near, `_npc`);
- a person is seated by `ThirdPersonCharacter.SitOnChair(seat)` (the game's own sitting pose, the navigation agent off, the body on the seat's position and turned as it is, the seat told it is taken) and taken off it by `ThirdPersonCharacter.Reset()`.
  Its customers, its employees and the player in its own activities all go through those two.
- beds are the game's sleep activity's and are not seats: nobody of the mod's lies down.

So nobody of the mod's is posed on a seat by the mod.
`Seating` uses the game's two calls, and adds what they leave open: where the person stood (they stand up there, on the mesh, and not wherever the agent lands), and what "free" is.

```csharp
bool   CityPerson.Sit(Transform seat)     // anybody from a cast: the family's partner, a companion, a city person
void   CityPerson.Rise()                  // safe at any time; also done by WalkTo, Work, Reset and Surrender
bool   CityPerson.Seated
Seating.IsSeat(item)                      // an ItemController that is a SeatController
Seating.Free(item)                        // a sitting position of it with nobody on ANY of its places, or null
Seating.InReach(from, seat)               // within 2.4 m: near enough to sit down without being seen to jump
Seating.Bench(near, within)               // the nearest OutsideBenchController (one physics query: on a slow timer only)
Seating.SecondPlaceFree(bench)            // its pedestrian's place is empty (the game's own field, read when asked)
new PlayerSeat().Sit(player, seat) / .Stand() / .Seated   // the player, held by the game's own RestActivity blocker
```

Rules kept: a whole-body clip is never played on somebody seated (`CityPerson.Work` stands them up first), nobody walks off a seat with the agent off (`WalkTo` stands them up first), and a person given back to a pool or knocked down is stood up first (`Reset`, `Surrender`), so every existing way out of every scene already gets people off seats.

**The partner at home** (`HomeLife`).
`Scan` makes up to two of the partner's six places "beside a seat": any `SeatController` in the home with a clear piece of floor by it that is not in the doorway (`Usable`: 1.6 m from the entry, 1.5 m from the other places, on the same mesh).

When they have walked to such a place (`Settle`) they sit down if `Seating.Free` gives a position (so never on a seat the player, or anybody, is on) within reach, for 70 to 160 seconds (`Mode.Sitting`); otherwise they stand there as in the living room.

They get up (`GetUp`: back where they stood) when that time is over and the player is not right beside them, when it is time to leave or to go to bed, when the seat is switched off or taken away, when a scene takes them over (`Hand`), and when the player presses Talk: the panel is offered over them seated, and they stand up to talk.

While seated they have the game's sitting pose and nothing in their hands.

**A bench on a walk** (`OutingScene`).
On a walk, every second and a half while both are on foot and within 6 m of each other, `Seating.Bench` looks 4.5 m round the player for a street bench; with one in reach the game's panel offers "Sit down" (`reallife_social_button_sit`), once a walk.

On the button: the player is seated on the bench's own place (`PlayerSeat`); the companion on its second place if the game has no pedestrian there and it is in reach, otherwise they stand by, facing; the bench's `spawnPedestrians` is switched off meanwhile and put back, so the game seats nobody on top of them.

After four seconds the companion says something and the panel offers "Get up"; after 90 seconds they get up by themselves.
A rest counts as 40 m of the walk's 160.
They are got up at once by anything that wants the player (`Wiring.Scenes.PlayerHeld`: an arrest, a panel of the mod's; the hospital), when the scene is cut (a time skip, a door) and when it ends in any way (`LeaveBench` in `Cut` and `End`).

Outings that go indoors are as they were: the door, the fade, the time passing.
Nobody sits at a table, and the Guide says so.

Cost: nothing per frame.
The home is scanned once on entering, as before; the partner's tick is the family's once-a-second one; the bench query is one `OverlapSphereNonAlloc` every 1.5 s of a walk and nothing otherwise.

Log lines with diagnostics on: `People check (seat): this home has N place(s) to sit among M ...`, `... the partner sat down on <item>, 0.00 m from where they stood, for N s: PASS`, `... the partner stays standing by <item> (in use, or out of reach)`, `... the partner got up after N s (<why>): PASS`, `... a street bench is N m from the player on a walk: sitting down is offered`, `... the player sat down on a street bench; the companion sat down beside them`, `... up from the street bench (<why>): the player is free to walk: PASS`.

Not checked in the game: any of it.
To look at first: that one of the mod's characters takes the game's sitting pose at all (they are the game's own character type, but from the mod's pool) and sits at the right height on each kind of seat.
That the player seated outside the game's own activity keeps the camera and can click the panel's button.
That standing up puts both back on the pavement.

Whether the game's bench panel ("rest") still opens on a click while the player sits there through the mod.
A screenshot of the partner on a sofa and one of the two on a bench settle the look.

## Engagement and wedding

- **The ring** is bought inside a real jeweller (`ba:businesstype_jewelrystore`): a key prompt opens the counter conversation with four rings (1,200 / 4,500 / 12,000 / 30,000).
- **The proposal** is wherever the player chooses: "ask them to come" from the Family contact brings the partner walking in from out of view to the player; at home they are already there.

  The player goes down on one knee when they choose to: `propose_kneel_down` once (2.8 s, held on its last pose), then the `propose_kneel` loop the moment that is over (`JobBody.Chain`, stepped by the director's tick of the body), with `ring_box` open on the right palm at the clips' grip from the first frame.
  The question is asked when the box has come up (the clip's `present` mark).

  After the answer they get up with `propose_kneel_up`, the box still in the hand, and it is put away when they stand.

  (A bundle without the way down fades into the loop from standing; one without the loop uses the mod's `kneel_work`, empty-handed.) The partner steps round to 1.25 m in front of them first (where the clips look for a face) and has stopped before the knee goes down, so the two face each other; their answer is `propose_react_yes` or `propose_react_unsure`.

  The answer is theirs: base odds from how close the two are and how things are at home, plus the ring (up to +0.22), plus the moment: an evening +0.08, a weekend +0.04, a dry sky out of doors +0.04 (rain -0.08), the end of a night out the same day +0.10, the player dressed in clothes the game calls formal +0.05, the small hours -0.15, an argument in the last two days -0.20.

  Too soon (under 14 days, or not close enough) is always "not yet".
  A "not yet" says what would have helped and costs nothing but a week before asking again.

- **Planning** at a real event planning agency's counter (they add 15 %, send the invitations and order the flowers) or by the two of you from the phone (the invitations are then yours to send from the Family contact, the flowers yours to order inside a real florist).

  Venue: a registry wedding at the city offices (the tax office stands in for a town hall, which the city does not have) 500, an open-air wedding on the spot where the player stands when they choose it 1,500, a restaurant 6,000, a theatre 12,000.
  Size: simple x1, classic x1.8, grand x3.2.
  Flowers 800, music 1,200, catering 85 a guest.
  A day 7, 14 or 21 days ahead.

  A quarter is paid as a deposit, the rest on the day.

- **Guests**: Social's close people, or by default Mom, the old friend (Alex) and the in-laws, plus a crowd by the size (4 / 14 / 30).
  Without invitations only the family comes.
- **Outfits**: the partner, the officiant and the family's guest wear what the game tags Formal (their own face and hair, from the same seed as every day).
  The mod's wardrobe (`Character/CharacterCatalogue`) has no formal wear yet; the player wears what they choose, and being dressed formally counts at the proposal.
- **The day** (11:00 to 20:00): the destination marker, and at the venue a key prompt.

  Under a cut to black the ceremony is set out on the pavement outside the venue's doors (every place tested for room; if the essentials do not fit it says so, and after two such failures the next press holds a small ceremony told rather than shown): officiant at the front, guests in rows, and by what was paid for an arch (classic and up), flower stands (flowers), a runner down the aisle (grand).

  The partner walks up the aisle; so does the player.

  Vows with a choice of two, their vow, then the rings: `ring_present` on the player and `ring_receive` on the partner, started together.
  They are made for a pair facing 1.10 m apart, so during the officiant's welcome the partner takes the step that makes it so (`CityPerson.StepTo`); if the two do not end 0.98 to 1.22 m apart the player's half plays alone, as before.

  Then the pronouncement, and applause: the officiant and the family's guest clap, the borrowed guests clap and `cheer_loop` by turns, each starting a little later than the last, and the partner plays `cheer`.
  A photograph follows (the HUD hidden for two frames; saved under `RealLifeOverhaul_shots` in the game's data folder).

  The first dance is `slow_dance_a` on the player and `slow_dance_b` on the partner, 0.45 m apart and facing, started together for 8 s; the partner takes the step in, and the pair is played only if the gap really reached is within 0.10 m of that, else it is left out.
  Then the screen goes dark for the reception (four hours) and everybody is taken away unseen.

  Then the bill is settled, the gifts counted (about 140 a guest times the root of the size), and the story of the party told.

- **Married**: a lasting mood (+10, renewed daily while home is not unhappy), next of kin, and the offer of a honeymoon: Travel bridge, else three days skipped (a day at a time) for 2,500 and a four-day mood.

  Put off, it stays in the Family contact's "Things to do" until it is taken or `HoneymoonWindowDays` (60) days after the wedding (`HoneymoonOpen`); `HoneymoonTaken` is set when it is paid for and given back if the trip is called off, and is cleared by the next wedding.
  A save from before the flag counts as taken if its memories hold the trip.

- **No dead ends**: the day can be put back a week (10 %), called off (the deposit is kept; still engaged), the engagement broken off (still together, badly hurt).
  A missed day moves itself three days once and is forgiven; missed twice, the booking is lost.
  Cold feet the night before: the player's own (talk it through, or sleep on it) or theirs (reassure them at the venue, or give them a week at no charge).

  Walking away mid-ceremony strikes the set under a cut; the day can be begun again while it lasts.

- A kind of place the city does not have at the time (its businesses change hands) never blocks anything: rings and flowers are then ordered by telephone from the Family contact, an appointment whose building cannot be found is kept from home, and a wedding whose venue has gone is held wherever the player stands.
- The casino boat is not offered as a venue: boarding it is a sequence of the game's own with its own time skip, and nothing of ours can be set out on it.

## Children

- **Deciding** is a talk at home: both must want it.
  The partner may not be sure (about one in five); they need time, not persuading, and have come round by a second talk ten days later in a happy home.
  Then: a baby (carried by either of you: it is asked) or adoption (a baby, a toddler, or a child of school age).
- **Pregnancy**: three quarters of a child's year (21 days at the default scale).
  A scan at a fifth and at half way (120 each), a birth class at three quarters, each an appointment at a real hospital or clinic with the marker, a prompt and an hour or two skipped.

  **The nursery**: a cot (450, inside a furniture store), a pushchair (380, a gift or clothing shop), supplies (220, a supermarket), each a prompt inside a real shop of that kind.
  **The birth** (1,800): "it is time", seven hours to reach the hospital, a long night there, then a name (as many second thoughts as wanted).

  Missed, the child is born all the same and the player is told to come home; if the player is the one carrying, it cannot be missed.

- **Adoption**: papers filed in person (1,500; a law firm, else the tax office), a home visit a third of the way through (be at home that day), the call, the day to meet.
  A missed day is simply moved.
- **Then**: three days at home (a mood, and the partner there all day).
  Broken nights for the first half year unless there is a nanny, every other night when the partner lives in (Home bridge; default a mood).
  Who looks after children under five: you (energy every day, closest to them), the partner (short days, half their share of the rent), a nursery (900 a month a child), a nanny (2,200 a month).

  Check-ups at a quarter, one, two and four years (80); pushchair walks until three.
  Milestones kept as dated memories (first smile, sat up, first word, first steps, first sentence, out of the pushchair, first day at school, lost tooth, rode a bike, ten, thirteen, a first job).
  School from five: public (free) or private (1,500 a month, better reports).
  The school run some weekday mornings for whoever wants it.

  A report every third of a year.
  Drawings on the fridge between three and nine.
  A school play a year from six to eleven and a parents' evening a year, both to attend.
  Birthdays (cake at home, a party at home for 150, or at a restaurant for 600) with a present from Grandma.
  Pocket money from six (0 / 10 / 25 / 50 a week).

  A college fund (500 a time; 40,000 pays for all of it); grown up at eighteen.
  Monthly figures are charged a quarter each week.
  Up to three children.

- **A pushchair walk** (from the Family contact, out of doors): 220 m wherever the player likes, the pushchair in front of them, the partner alongside when they are home and free; small sights on the way; the key ends it early (half way counts).
  Good for the mood, the child and the two of you.
- **Time scale**: one year of a child's age per four in-game weeks by default (options: 1 to 12 weeks).
  The game's own year (`gameVariables.daysPerYear`, 60 days) is used only for the holiday calendar.

## Wider family, hard times

- Mom texts at every milestone, sends small presents (250 for a new grandchild, 40 a birthday), and drops by on some Sundays.
  The in-laws arrive with the engagement as a contact of their own: a chat, advice that reads the household as it is, and now and then a favour asked (a lift, a hand with a move, a loan of 300 repaid in a week).
- Four family dinners a year on the game's own calendar (at 22, 47, 72 and 97 % of its year): Mom writes the day before; with a household it is at home (120 for the food, three hours), otherwise at Mom's (four hours, from the Family contact).
  Missing one costs nothing but the dinner.
- Next of kin is a line on the Family page (the partner once married, Mom otherwise; it can be changed) and nothing else.
- **Difficult moments** are questions with two answers, ten of them, asked in the game's question box as they always were; which answer is the better one differs.
  Each comes of what is going on in the player's life (`HardTimesWatch`, `HardTimesRules`), read once a game hour (the evening at ten, the small hours, the household, home life, careers, the social life, travel) and once a game day (the money), never at random:

  | the moment | what brings it |
  |---|---|
  | "home by seven, it is past ten" | evenings not home at ten while the partner was (two or more in a week, tonight one of them), nights away, long hours; a new child makes it worse; only for two who live together |
  | "where do you go?" | days without a call, a text, a visit or a word at home (three living together, four apart), stress at home life's "Stressed", sleep owed past "Short of sleep", long hours (home life's hours on a job, careers' burnout), nights away |
  | "you forgot what today was" | the partner's birthday (the social life) or the anniversary (the wedding day, else the day they began, each of the game's years) with no word by six in the evening; a date, an appointment, counselling, the birth or the wedding missed, or an outing of the social life stood up or kept waiting, and no time together since |
  | "the bank statement" | a purchase of a quarter or more of the money in hand (a building, a car, shares, anything that is not a running cost or the family's own), in the red at midnight and still, owing more than is owned, a loan of 5,000 or more this week, or most of the week's money gone; for two who live together, are engaged or married |
  | "your mother rang me" | meals skipped or a poor diet (home life's), with Mom in the phone; for two who live together |
  | "a wet towel on the bed" | a messy home, a new child, stress or too little sleep; and with nothing wrong at all, now and then: about one day in forty, three weeks after the last moment at the least |
  | "you always say sorry" | two or more quick sorries in a row within a fortnight, while something is still wrong |
  | "the shop gets the best of you" | evenings and nights at the player's own business (living at it); for two who live apart, a business owner who is never seen |
  | "we were supposed to leave" | an hour late for a child's school play, parents' evening or party, or for a holiday dinner at Mom's |
  | "the plan for the new place, another one" | a new place taken besides the one they have, or the home moved, within five days |

  When: a lasting cause must have lasted (a strong one at once or a day later, a middling one a day or two, a mild one two or three; a day sooner in a tense home, under 40, a day later in a warm one, from 70), each moment has its hour of the day (the evening; ten or eleven for coming home late; at once for an outing), and two moments are two days apart at the least, three in a home that is so so and five in a warm one (two for a strong cause).
  A moment airs everything wrong at the time: those troubles rest a week to nine days and come back together if they are still there, while something new can come up sooner.
  Which of the fitting moments comes, and its hour, vary a little with the partner and the day; the same state gives the same moment on every machine.
  A cause gone by its hour (they talked, made up with time together, the money came back) brings nothing.

  None comes on the wedding day, and a trip the partner came along on is time together.
  The options: "Arguments, time apart and divorce" off (as the Relaxed preset sets it) and nothing is read and no moment comes; relatives off, no call from Mom; children off, no new child; a part of home life off, none of its figures count; the social life off, family life's own last word and the anniversary only.
  Saved: one optional line in the family's state (`FamilySave.Hard`: the evenings and nights out, when each cause began and last aired, the places counted, the broken promises, the sorries); a save without it starts with nothing remembered, which brings no moment.
  In a Going Public session: the local player's own family, money and buildings only (`GoingPublic.Mine`); nothing is sent.
  Counselling (180 a session at a clinic, an hour) is offered from the Family contact while harmony is under 60 (`FamilyBalance.HarmonyCounsel`, "a little tense" and lower) or the two are apart, and mends the most the first times.
  Harmony under 15 for five days and they move out; time apart can also be asked for.

  A call a day, counselling and a little time bring harmony back, and at 55 they can be asked home.
  After a week apart either may end it: married, that is a divorce with a settlement of a fifth of the cash in hand, at most 250,000, never a business; unmarried, a parting with nothing to settle.

  The children stay with the player and spend every other weekend, or every weekend, with the other parent, who then pays half of what the children cost.
  Nothing in any of this harms a child.

## The phone

- **Family**: five short pages (household, the family, coming up, memories, budget) and "things to do", one at a time with "do it", "something else" and leaving, which is every decision above.
- **The partner's texts** come from Family's stand-in partner contact while Social does not keep the partner, and from the partner's own contact in People while it does (`FamilyPhone.PartnerContact`, through `ISocialBridge.PartnerContact`), so they are always a text in a thread and never only a notice.
- **Someone special** (only while Social does not keep the partner): ask a friend to introduce you to someone, and from then on ring them: a chat a day, an evening out, and the same things to do.
- **The in-laws**: a chat, advice, or the favour they asked.
  With the phone contacts option off, "Test: open the Family page" opens the same page face to face.

## The bridges (`FamilyBridges`, `FamilyApi`)

Each is null until its owner sets it (`FamilyBridges.Social = mine;` when its own state is loaded, back to null when it unloads); until then Family uses its own default.
All calls are from the main thread.

| bridge | its owner implements | Family's default without it |
|---|---|---|
| `ISocialBridge` | `Active`, `Ready`, `TryGetPartner(out PersonInfo)`: the one person the player is dating or partnered with (Stage 4 or 5: name, look seed, sex, affection and trust 0..100, days together); `Changed(FamilyEvent, affection, trust)`: what happened to the two (moved in, engaged, married, argued, shared time, broken promise, separated, divorced...), with a suggested change in points; `StartOuting(kind, done)`: "dinner" (true if it started; `done(true)` when it went well); `ClosePeople(list, most)`: who would come to a wedding. It calls `FamilyApi.PartnerChanged()` when the partner changes outside Family's flows and may call `FamilyApi.SharedTime(points)` | one partner record in the family state (a name, a look seed, affection), met and rung through "Someone special" |
| `IHomeBridge` | `CookTogether`, `WatchTogether`, `CareWhenSick(partner)`, `BrokenNight(wakings)`, `HealthVisit(reasonKey)`: true if Home did it its own way | a few lines, a meal, a mood; eight hours off the illness; the "up in the night" mood |
| `ITravelBridge` | `Honeymoon(partner, days, done)` | 2,500, three days skipped, a mood |
| `ILeisureBridge` | `DateNight(partner, done)`, `FamilyOuting("school_play" or "birthday", child, done)` | dinner at a restaurant's door; the play and the party as an errand and a skip |
| `ILawBridge` | `InCustody` (a cell or a prison term; family life waits) | nothing is known; arrests are told with `FamilyApi.PlayerArrested(severity 1..3)`, which `Wiring.Arrests.FamilyHears` calls once for each arrest (1 a night in the cells, 2 when a case is open for it) and each prison sentence (3), only with a partner |
| `IHardTimesBridge` (`HardTimesBridges.Life`) | `Days(ref HardSigns, partner)`, once a game hour: home life's hours on a job, stress, sleep owed, meals skipped and diet, how tidy the home is (each while its part is on); careers' burnout; the social life's last call, text or meeting with the partner, their last outing, the last stand-up or lateness, their birthday; travel's trip with the partner. `Money(ref HardSigns)`, once a game day: net worth as the Finance app shows it, the businesses, and from the game's transactions of the last three days the largest purchase and what was borrowed | unknown, and no moment comes of it; family life's own records and the game's money (in hand, its last seven midnights) still count |

`Wiring/FamilyLinks.cs` fills in Social, Home and Law.
Social: the partner is Social's person who is dating or with the player; `Active` is Social's option, `Ready` is "its people are read" (Family waits for that before reading who the partner is); `StartOuting` answers false, so Family runs its own evening, because Social's outings are planned with a venue and a time by its people.

Home: `CareWhenSick` shortens Home's illness by eight hours, `BrokenNight` adds half an hour of sleep debt for each waking; Home has no routine for cooking or television for two, or for recording a health visit, so those keep Family's default.
`ITravelBridge` and `ILeisureBridge` have no matching door (a trip is bought at the travel agent's desk, and a leisure activity is the player's own at a venue), so they stay at their defaults.

A partner who is engaged, married or living in and is not Social's person stays on Family's own record (`Partner.Kept`); a dating stand-in with nobody from Social to replace them ends when Social is on, and the "Someone special" contact is not made again (an existing one answers and says where the partner is).

`FamilyApi` is safe to read at any time: `HasPartner`, `LivesTogether`, `Engaged`, `Married`, `Children`, `PartnerName`, `WeddingToday`.
`FamilyDirector.TakesInteractKey` is true while one of its key prompts is up (the mod's own E/F on doors stands aside, as it does for a job's prompt).

## Cost

Nothing per frame while nobody of the family's is out and no prompt is up beyond a few comparisons.
One tick a second (five a second during a staged scene): the hour and the day compared, the home looked at only while the player is inside a building, the errand list only while it has entries.
The home's furniture is read once on walking in.
The difficult moments: once a game hour a few dozen field reads, one pass over the city's building registrations and the save's line rebuilt; once a game day the economy's walk over the game's last days of transactions.

Looking for the nearest building of a kind is one pass over the city's buildings when a conversation asks or an errand becomes due.
A fault in any tick is logged once and switches family life off for the session; the save is left as it was.

## Test buttons

A button that does nothing says why.

- Partner moves in now (needs a rented home; with Social absent a partner is made up on the spot).
- Partner asks to cut back now (needs a partner who lives with you and the switch on): the text, then the talk in the Family contact.
- Partner shares a bigger wish about work now (same needs): the text, then the talk in the Family contact.
- Propose here (a ring is supplied; the answer is a real roll at 90 % or better).
- Start wedding planning (engaged at once if need be).
- Hold the wedding here, now (a classic wedding with everything, out of doors; free: no bill is charged and no presents come in, `WeddingScene.Free`).
- Start a pregnancy / move it on a step (scan, scan, class, birth).
- Start an adoption / move it on a step.
- Add a year to the children's ages.
- Pushchair walk now (needs a child under three).
- A difficult moment from what is going on: the one that fits best now, whatever its hour and the days since the last (its cause is in Player.log); with nothing wrong between the two, a notice says so.
- Open the Family page.
- Clear all family state.

## Wish-list for the asset pipeline (no code change needed)

Clips (played by name when the bundle has them, the fallback in brackets until then): `propose_kneel` (the mod's `kneel_work`): down on the right knee, right hand held up and forward at chest height with a small box, head up, held as a loop.
`propose_react_yes` (`clap`): hands to the mouth, a step back, a nod, 2.2 s once.
`propose_react_unsure` (`talk_a`): a hand to the chest, a small shake of the head, 2.2 s once.

`ring_present` (`job_hand_over`): takes the other's left hand and slides a ring on at 1.05 m, 3 s once.
`cheer` (`wave_hello`): both arms up, once.
**All five are delivered and played, and so are** `propose_kneel_down`, `propose_kneel_up`, `ring_receive` and `cheer_loop` (see [Engagement and wedding](#engagement-and-wedding)).
**Played since:** `slow_dance_a` / `_b` (the first dance, after the wedding photograph), `hold_baby` and `rock_cot` (the partner at home), `cheer_once` (a busker's listener, the chess onlooker).
Still wanted: `sit_sofa` (only if seats are solved).

Props (drawn from the prop bundle by these ids when present; plain shapes from `FamilyProps` until then, origin on the floor, facing +z, metres): `wedding_arch` (2.1 wide, 2.4 high, 0.3 deep, flowers along the top), `flower_stand` (a pedestal 0.95 high with an arrangement, 0.4 across), `aisle_runner` (a strip 1.1 wide by 1.0 long, laid end to end), `cot` (0.72 x 0.9 x 1.26), `high_chair` (0.48 x 1.05 x 0.5).

Not used yet: `bouquet` (no clip holds it), `birthday_cake` (a birthday is told, not shown).
`ring_box` comes with the kneeling clips and `baby_bundle` lies in the cot (both delivered and used).
Still wanted: `toy_box`.

## Not verified in the game

Written against the decompiled code and never run: whether a home's interior has a navigation mesh for the game's NPC agent type (the player's is tried second; with neither the partner is not shown and it says so in the log).
How the game's Formal clothes look on a seeded face.
That the sizes of real apartments make 30 m2 for two a sensible bar.
The look of every clip on the partner indoors.

Turning the player's body to face the partner at the altar (set once on the character's transform while they are held).
The three-day time skip of the default honeymoon.
The photograph with the game's HUD objects switched off for two frames; the plain-shape stand-ins' materials indoors.
Which of the game's business types a given save actually has (a kind the city lacks is said so, and the conversation offers what there is).
The difficult moments' readings in play: the evening read at ten (or the first game hour after it, past a time skip) and the night in the small hours, the partner's hours, a move seen from the home's address, the social life's partner matched by name; built and checked offline only.

## Clashes looked for, and what was done

The game's own bindings are the JSON in its `BigAmbitions.InputSystem.PlayerInput` (one asset, always enabled; which code polls an action decides where it counts) plus a few hard-coded keys.
Its driving keys beyond these (throttle, brake, gears, handbrake) are in an NWH asset that is not in the decompiled code, so that column is not complete.

| key | the game | the mod | verdict |
|---|---|---|---|
| Enter | Confirm: a question's yes, the game's panels | the "Garage" button of the game's panel in a repair bay | the game's own routing (`OverlayUI.OnPlayerActionPressed`); the mod reads no key for it. The Garage key (K, earlier B) is gone |
| F | Interact | `Interact` | by design: the mod's prompts stand aside while the game shows its own (`TakesInteractKey`, and the game's overlay) |
| E | rotate the camera right (the game's own pedestrian camera) | `InteractAlt` | only while the game shows no interaction of its own (`JobKeys.InteractDown`); the Events prompt lacked that test and has it now |
| V, G | nothing | `CameraToggle`, `GentleThrottle` | free |
| Left Alt | snap in placement and the interior designer | `FreeCursor` | different screens |
| Right mouse | right click | `LookBehind` (driving) | the mod feeds the game's own right-button state, as before |
| F7, F8 | hide the interface / free camera, in the game's debug builds or with `-console` only | `PerfProbe`, `PerfBench` (Shift), `PhoneDump` | no clash in a normal game |
| F9, F10 | nothing | `CharacterReport`, `PhotoMode` | free |
| 1, 2, 3 | tools in the interior designer | a row of three choices (leisure, transit, the theory paper, the cell, the courts' panel); photo mode | one at a time: a choice row is on screen only while its owner has the player, and none of them with the designer open |
| Space, Enter, Tab, Esc, N, M, T, P, O, I, Q, C, H, [ ], F1, F2, F5, F12, Backspace, Left Shift | the game's | nothing of the mod's outside photo mode (paused) | free of the mod |

Free in both for a future action: F3, F4, F6, F11, the digits 4 to 0, and ; ' / \ - =. The letters L, U, Y, Z, J and X are used only inside photo mode, R only in the game's placement.

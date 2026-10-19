# The living city

## In a sentence

People at work and about their day on the pavements near the player: sweepers, window cleaners, coffee carts with queues, chess players, couriers, joggers and more, at most three sites and eight people at a time.

## Where the code is

- Folder: [`Scripts/CityLife`](../../Assets/Mods/RealLifeOverhaul/Scripts/CityLife/), with the activities in `Activities/` and the clip player any feature can use in `Animation/`.
- Read first: `CityLifeDirector.cs` (what is out and where), `CityCatalogue.cs` (every activity, one entry each) and `CityActivity.cs` (the base every activity shares).
- To add an activity, see [Adding an activity to the living city](../contributing/adding-things.md#adding-an-activity-to-the-living-city).

| File | What it is |
| --- | --- |
| `CityLifeDirector` | what is out and where: three sites and eight people at most, set up and taken away only out of view; the test buttons and why each refused |
| `CityCatalogue` | every activity: who, when, how often, in the rain or not, the activity that plays it (one entry each) |
| `CityActivity` | ICityActivity (find room, begin, tick, end) and the base every activity shares: people who walk in (Arrive) and walk off (Depart) |
| `Activities/` | SoloActivity (one person, data only: sweeper, window cleaner, painter on a ladder, tai chi, newspaper, doorman...), RoadCrewActivity, ChatActivity, BuskerAudienceActivity, JoggerActivity, StallActivity (coffee and hot-dog carts with a queue that walks up, is served and walks off), CourierActivity (sack truck, parcel to a shopfront), WalkingActivities (the mail round; pushchair; a traveller with a suitcase; umbrellas in the rain), StandingScenes (chess table, a driver leaning on a parked car, a door with a queue, a gardener at a street tree) |
| `CityPeople` | the pool of eight (not the accident pool's six): made a person at a time, hidden when out of view, knocked down by the player's car |
| `CityPerson` | one of them: stand, walk, play a clip with its tool, drop it when hit |
| `CityOutfits` | work clothes from the game's own garments by name, in hi-vis and council colours |
| `CitySpots` | where people may stand: pavement at street level, clear, away from doors; a shopfront at a measured distance |
| `CityProps` | the prop objects, made once and reused |
| `ClipLibrary` | the mod's animation clips by name, from Bundles/reallife_anim |
| `Animation/` | the clip player any feature can use (no game types in here: the asset pipeline tests these very files) |
| `Animation/ActivityAnimator` | plays a clip on one character over the game's controller: cross-fade in, hold, hand back; a masked layer (arms, one arm, upper body) over walking; speed matched to the ground |
| `Animation/ActivityAnimators` | steps the players that are fading and the hands on tools; switched off when there are none |
| `Animation/HeldTool` | a tool on a hand bone at the grip its clip was made with |
| `Animation/TwoBoneReach` | puts the second hand on the tool after the animation |

## How it talks to other features

- Its pool of eight is its own (`CityPeople`) and never takes from the street scenes' pool.
- Other features play clips through `Animation/ActivityAnimator` and find room with `CitySpots`; civic life asks how many of the eight are out (`Wiring/CivicLinks.cs`).
- Both chess scenes (Leisure's corner and the living city's) ask `CitySpots.ChessClear`.
  They refuse a candidate whose table, stools, approaches or possible onlooker places overlap a crossing.
  One non-allocating box query includes triggers tagged Crosswalk or on the Crosswalk layer, regardless of the lights
  or traffic options. A full query buffer refuses the candidate too. The check runs only when choosing a place;
  normal walking can still cross roads. Developer diagnostics report the clearance result for each queried candidate.
  The Crosswalk layer is in `GameContract.Layers`, so the mod names it in Player.log if a game update drops it; the tag still finds crossings then.

## Rules the living city keeps

- The living city (`Scripts/CityLife`) has its own pool of eight (`CityPeople`) and never takes from `People/ActorPool`.
  Its people are placed only where `CitySpots` found and tested room, only out of view (the test buttons are the one exception; and even then whoever comes later walks in from out of view), and move only by their navigation agent with the game's walk.

  Its budget is fixed: eight people, three sites, one tick a second per site, a path per walker per stretch; per frame only a turn on the spot or a timed moment, and only while there is one.
  A prop is always exactly one thing's: in a hand, brought along, standing in an activity's dressing, or fallen; `End` gives all of them back in every state.

  A tool is in a hand only at the grip the clip being played was made with (`CityPerson.Work`); an activity whose clip or prop cannot be had does not start.
  Nobody gestures at nobody: a greeting plays only when someone on foot is in front, and a conversation always has both people animating at each other.

## Clips nothing played

A maintainer's script lists every clip the bundle's builder declares (`CityAssets/scripts/clips*.py`, 160) and the ones no script names.
There were 25.
Six now have a home in the living city, each where the clip's own note says it belongs:

| clip | where | how |
|---|---|---|
| `sit_chat_a`, `sit_chat_b` | new activity `bench_chat` (`BenchChatActivity`, Leisure, two people, 9:00 to 21:00) | a `precinct_bench` with its back to a wall (`CitySpots.Shopfront`, 0.45 m off it, pavement under both ends and in front); the two put exactly on the seat 0.55 m apart, as the chess players are on their stools; the talker on the right, since `_a` is turned to its left |
| `eat_standing` (with `takeaway_box`) | new entry `street_lunch` (Everyday, 11:00 to 15:00) | `SoloSpec.HoldNow`: the one-off is played, its last pose (the carton on the palm) is held, and it is played again every 4 to 8 s |
| `drink_stand` (with `mug`) | new entry `mug_break` (Everyday, 7:00 to 17:00) | the same, every 7 to 14 s |
| `phone_answer` | the existing `phone_call` | `SoloSpec.Ring`: after 14 to 26 s the call ends (the game's pose and phone go), 5 to 9 s later the clip takes the phone to the ear, its phone is put away and the game's `TalkingPhone` takes over, as the clip's note asks |
| `point_show` | the existing `doorman` | `Greet`: somebody on foot in front of him is shown the way in |

`HoldNow` and `Ring` are two fields of `SoloSpec`, so another such person is still one catalogue entry.
The budget is the director's and is unchanged (three sites, eight people); the Guide's count is `CityCatalogue.All.Length` (33 now); each start writes the director's own line, `City life: <id> started N m away; ...`.
A clip or prop missing from the bundle leaves `bench_chat`, `street_lunch` and `mug_break` unstarted and the other two as they were.

A second pass wired eight more, each where its clip's own note says it belongs:

| clip | where | how it starts, how it ends, what it falls back to |
|---|---|---|
| `carry_suitcase_walk` (with `suitcase`) | new entry `traveller` (Leisure, 6:00 to 23:00), `StrollActivity` with `StrollKind.Suitcase`: three or four stretches of pavement, 16 to 30 m each | the clip is a whole-body walk made for 1.3 m/s, so it is played only while the agent is really moving and its speed is set from the agent's (`CityPerson.KeepStep`, `ActivityAnimator.MatchSpeed`, as the crews are kept in step) once a second on the director's tick; held up (under 0.2 m/s, two seconds after setting off) they go back to the game's own walk with the case in one hand. No landmark is known to the catalogue, so it is placed like the pushchair. Without the clip or the case the entry does not start |
| `cheer_once` | the listener at the busker (`BuskerJob`) | the song just played was good (the quality of the "good" toast); when the listener is about to leave, instead of leaving at once they cheer once (the game's pedestrian, the mod's clip, 1.8 s), and the clip is taken off with `ActivityAnimators.Forget` before they are handed back, on every way (the pedestrian is pooled). If the clip cannot be played they leave as before |
| `cheer_once` | an onlooker at the street chess table (`ChessCorner`) | a third person from the pool, to the side of the table (`watch_nod`), only where the pool has three free after the opponent; when the player wins (7 of 10) they cheer once and go back to watching on the corner's own second tick. Taken in with the table |
| `hold_baby` (with `baby_bundle`) | the partner at home (`HomeLife`), with a baby under a year old (`FamilyBalance.BabyUntil`, the age the cot shows its swaddled baby to) | one of the ways they stand at a place: every other day at the living-room places, always at a clear-floor place. The cot's bundle is hidden while they hold it and lies in the cot again when they put it down (`HomeLife.Cradle`), walk, are spoken to, or leave. A child of one or two is not held: there is no toddler shape, and a held empty bundle would be wrong |
| `rock_cot` | the partner at home, a place of its own beside the cot (`CotPlace`, found when the cot is put down) | the feet 0.68 m to the cot's side (the clip's figure), within 8 cm of the mesh; the partner is brought to it and then set on it exactly (`NavMeshAgent.Warp`, a few centimetres), facing the cot. **The cot does not move with the push** (the clip says it should; moving it needs a per-frame step the mod does not take), so the hands go 2.8 cm either side of the rail |
| `slow_dance_a`, `_b` | the wedding, after the photograph (`WeddingScene`, `Step.Dance`) | the partner takes the step to 0.45 m (`CityPerson.StepTo`), the gap really reached is measured and the pair is played only within 0.10 m of it, both in the same call, for 8 s; both are released on every way out (`Abandon`, `End`). If the gap is not reached, or either clip is missing, the ceremony goes on to the reception as before |

Eleven stay unplayed.
Each was looked at for a place among the features that exist:

| clip | why not |
|---|---|
| `strap_hang` | made for standing in a moving bus: there is no bus interior |
| `bonnet_check` | needs a car with its bonnet open: the game's cars do not open theirs, and no scene is a breakdown |
| `refuel_hold` | emptying a `jerry_can` into a filler: the mod has no fuel and no scene of running out |
| `oath_hand` | the raised hand of an oath: a hearing is pages in a list window and nobody is played in it |
| `podium_speak` | needs a lectern 1.10 m up: none is modelled, and a rally's speaker has a placard |
| `placard_hold_arm` | not needed by design: the code masks `placard_hold` itself to the right arm |
| `throw_ball` | a free throw: no ball and no hoop are modelled |
| `eat_sit` | seated at a table 0.75 m high whose edge is 0.20 m ahead: no seat in the game or the mod has a table at a known place in front of it |
| `box_lift_with`, `_b` | two people lifting a box 1.20 m long: no such prop (the move's boxes are 0.45 m and carried one each) |
| `collector_lean` | the collector of "Business pressure" would lean on a counter 0.975 m up whose near edge is 0.32 to 0.36 m ahead, the player 1.1 m beyond it. A visit is played on open floor 1.7 m from the player and the game's shop furniture gives no measured counter (`BusinessVisit` says so); played anywhere else the clip reads as a man bent forward with a hand in the air. Left listed; should a shop ever offer a measured counter it is one line in `BusinessVisit` |

Not checked in the game: any of it.
A screenshot of the bench with the two on it settles whether they sit on the seat and not in it (the seat is 0.46 m, the clips are made for 0.45) and whether the bench stands clear of the wall.

For the second pass: the hands on the rack's rail and on the cot's rail, the baby in the partner's arm, the traveller's feet on the pavement, the clasped hands of the first dance.
The log (developer diagnostics on) says the first time each clip is played, `Clips: the '<clip>' clip is played by <who> (said the first time only).`, and the dance says the gap it measured.

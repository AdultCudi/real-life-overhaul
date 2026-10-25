# Justice

## In a sentence

Courts and prison: an arrest or a serious charge opens a case with evidence, bail, a lawyer, a hearing you attend at the courthouse, a verdict and a sentence, all of it one line in the save.

## Where the code is

- Folder: [`Scripts/Justice`](../../Assets/Mods/RealLifeOverhaul/Scripts/Justice/). The files are in the table below.
- Read first: `JusticeLaw.cs` (every charge, cap, fee and formula, game-free), `JusticeDirector.cs` (the clock and the switchboard) and `JusticeBridges.cs` (the door for the rest of the mod).

## How it talks to other features

- Other features call `Justice.Arrest`, `Justice.Summon`, `Justice.PoliceContact` and `Justice.CrashVictim`; see [For other features](#for-other-features).
- Justice asks Law, Licences, Social and Transport through `Justice.Bridges`, each with a default that works alone. `Wiring/LawJustice.cs`, `LicenceLinks.cs` and `LifeLinks.cs` fill them in.
- An arrest is one path through Crime, Law, Justice and Licences: see [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).

Courts and prison (`Scripts/Justice`).
Tickets stay tickets; an arrest, or a charge for something serious, opens a **case**.
A case has charges (each with its own evidence strength), a bail decision, a court date two to four days ahead, a hearing that has to be attended, and an outcome.

Everything is one line in the save's `modData` (`reallife:justice`); a save made at any moment loads without the mod with the player standing in the street, because the player is never put anywhere the base game does not know.

| file | what it is |
|---|---|
| `JusticeLaw` | every charge, cap, fee and formula: evidence, the case against the accused, bail, the sentence, appeals (game-free) |
| `JusticeState` | what a save remembers, and its one line of text (game-free) |
| `JusticeBridges` | `Justice` (the door for the rest of the mod), `Justice.Bridges` (Law, Licences, Social, Transport, each with a default), `JusticeReports` |
| `JusticeDirector` | the clock and the switchboard: hourly business, what is waiting for the player, the watchdog, the test buttons |
| `JusticePanel` | the screen: courtroom, cell and Legal file are pages of it, in the game's own list window (`GameUi.DeskList`), three answers at most, answered by a click |
| `Courthouse` | which building it is, the sign on its frontage, where one stands to go in |
| `LegalCounsel` | the law firms, their rating and fee, what counsel can do before the hearing; the Legal contact and its conversation |
| `CourtHearing` | one hearing, page by page |
| `PrisonStay` | custody (hours) and prison (days) as time passing; `JusticeHold`: everything changed on the game while the player is held |
| `CommunityService` | a played litter-picking shift |
| `LegalFile` | the Legal file page and what can be done from it |

## On screen: the game's own interface

Nothing in the justice system is drawn by the mod any more (`JusticeHud` and the full-screen panel are gone).
Not seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| the hearing, bail, the cell, the prison mornings, the summary, the Legal file | pages of `JusticePanel`, which is a `GameUi.DeskList` (a copy of the subway station window): the title and who is present under the headline, what is said as a note with the speaker's name over it, and an entry with a button for each answer (at most three) with its hint under its name. Answered by a click. Locked (no cancel key) while the player is held; the Legal file, which holds nobody, can be put away with the cancel key. While the screen is faded to black by the game the list stands on the canvas of the game's black cover, and goes back to the HUD's when the cover is gone | the justice system switches itself off for the session with one line (`Justice check (page list) ... FAIL`); the player is let go and cases and sentences stay in the save |
| the courthouse door ("Attend the hearing", "Hand yourself in", "Report for service") and the lawyer's desk ("Speak to a lawyer") | the game's panel over a place (`GameUi.DoorPanel`), told once a second | tried again the next second while the game's panel shows another place |
| the court's marker | the game's destination marker (`GameUi.GuideMark`) | not shown while a job or an outing has the marker |
| community service: the litter, the pieces picked and the hours owed | the panel over the litter with "Pick it up"; the marker as above; the figures on a copy of the game's job panel (`ShiftCard`) | the pieces picked are sent as notifications |

Cost: nothing per frame.
A page is written once when it is shown; the shift's tick (five a second) tells the panel and the marker only what changed.

Log lines with diagnostics on: `Justice check (page list)`, `Justice check (door panel)`, `Justice check (litter panel)`, `Justice check (community card)`, each ending PASS or FAIL. Behaviour that changed: the keys 1 to 3 and the interact key no longer answer a page (a click does); the portraits and the drawn courtroom are gone (the names of those present are under the headline), and the portrait sprites in the bundle are unused.

## What happens, in order

1. **A case is opened** by `Justice.Arrest(report)` (taken in) or `Justice.Summon(report)` (charged by post).
   What is reported on the same day joins the same case; never more than three cases are open.
   Evidence, 0.05 to 0.97: 0.30 for the accusation alone, +0.12 a witness (three at most), +0.22 filmed, +0.25 caught at it, +0.06 for having run, and a few hundredths either way per charge.
   A case opened on probation carries a breach charge as well.
2. **Bail**, put to the player on the panel as soon as nothing else has them: released on their own promise (a misdemeanour, no record, did not run).
   Bail of a quarter of the charges' fines, at least 200 and at most a fifth of cash, returned on appearing (or a bondsman for a tenth, not returned; or stay in custody and be heard when the court next sits).

   Or refused (a grave charge with flight or two convictions, or a warrant) and held until then.

3. **Review**, the day after: the prosecutor drops any charge whose evidence is under 0.34.
   A case left with nothing is dismissed: an apology, and compensation of 400 + 150 for each hour held + any roadside fine (the wrongful arrest).
4. **A lawyer** (the Legal contact, or the key at a law firm's desk): the public defender (free, quality 0.25), yourself (0.10 to 0.60 by the game's own Lawyer skill), or one of the game's law firm businesses (`ba:businesstype_lawfirm`, read from the save's building registrations; the player's own firm acts for free).

   The game keeps a satisfaction score per business but leaves it at 50 for those it runs itself, so a firm's rating is two to five stars fixed by its address, moved a star for every 25 points of that score from 50; quality 0.35 to 0.83, 0.05 more for a case prepared at the office.
   A city with no law firm gets three sets of chambers in its office buildings.

   Fee: 600 / 1 800 / 3 500 by the gravest open charge, times 0.8 (one star) to 2.0 (five), never more than 30 % of cash; a quarter down, the rest over six days.
   Counsel says what the odds really are, can make one motion to dismiss (every charge under 0.34 + 0.12 x quality goes) and can put a plea deal on the table.

5. **The court date**: a text when it is set, another the evening before, another and a toast that morning (and the courthouse becomes the map destination if the player had none), a toast in the last hour.
   The court sits 9:00 to 17:00.

   Missing it: a bench warrant, bail forfeit, a charge of failure to appear; the next police stop (`Justice.PoliceContact`) takes the player in, as do officers at their door after three days; or they hand themselves in at the courthouse.
   Someone who could not have come (in prison, in hospital) has the date moved.

6. **The hearing** (`CourtHearing`): called; charges read; plea.
   Guilty: a quarter off.
   The deal: weak side charges dropped, the main charge reduced where it has a lesser form, 40 % off and no prison unless what is left is grave.

   Not guilty: three or four rounds (the officer, a witness, the footage, the flight; the closing), each with a choice:

   - testify (once: -8 if the evidence is thin, +6 if it is strong, and the hint says which);
   - stay silent and let counsel cross-examine (-6 x quality);
   - object (sustained as often as counsel is good: -4, else +1);
   - call a friend as character witness (Social bridge: -8);
   - or put in your own business's footage and receipts (a business of the player's with security: -10, nothing if caught at it).

   At the close speak (remorse: -2 and a tenth off the sentence if the evidence is strong, +3 if it is thin), let counsel close (-8 x quality) or say nothing.
   **Verdict**, per charge: case against = evidence + 4 a prior conviction (12 at most) - (quality - 0.2) x 30 + the choices; guilty if that is more than the court's doubt, a number fixed by the case's seed when it was opened.

   Every term of that sum is on the verdict page.
   Nothing in a hearing is a fresh roll of dice: a reload replays it the same way for the same choices.

7. **The sentence**, with the reason for each part on the page (table below).
8. **Afterwards**: one appeal within three days (fee a tenth of the fine, at least 300, at most 15 % of cash; chance 5 to 60 % by how thin the evidence was and how good counsel was, shown before paying; won: the fine back or struck off, the conviction off the record, 600 a prison day and 150 a custody hour paid).

   Civil suits (a crash that hurt someone: one in two; damages of 800 or more: one in five; one suit at a time, a week apart; claim at most a fifth of cash): settle for 60 %, contest (10 to 70 % by counsel; lost: claim + 15 %), or ignore (the claim, by default).
   The record is sealed after 30 days with no new conviction, no open case and no probation.

## Charges and sentences

Nominal values, for a first offence convicted after a trial.
`Lesser` is what a deal can reduce it to.

| id (`JusticeLaw.`) | charge | class | fine | community | custody | prison (cap 7) | probation | licence | lesser |
|---|---|---|---|---|---|---|---|---|---|
| `Evading` | Evading the police | misdemeanour | 1 500 | 8 h | 12 h | | 5 d | 3 d | |
| `Reckless` | Dangerous driving | misdemeanour | 1 200 | 6 h | | | 4 d | 3 d | |
| `HitRun` | Leaving the scene of a crash | misdemeanour | 2 500 | 8 h | 24 h | | 6 d | 5 d | |
| `HitRunInjury` | ...with injury | felony | 8 000 | 16 h | 48 h | 3 d | 10 d | 10 d | `HitRun` |
| `HitRunDeath` | Leaving the scene of a fatal crash | grave | 25 000 | | 48 h | 7 d | 14 d | 21 d | `Manslaughter` |
| `InjuryDriving` | Causing injury by dangerous driving | felony | 4 000 | 12 h | 24 h | | 7 d | 7 d | `Reckless` |
| `Manslaughter` | Causing death by driving | grave | 15 000 | | 48 h | 5 d | 14 d | 14 d | `InjuryDriving` |
| `Unlicensed` | Driving while suspended | misdemeanour | 1 500 | 8 h | | | 5 d | 5 d | |
| `Assault` | Assault | felony | 3 000 | 12 h | 24 h | 2 d | 7 d | | `Resisting` |
| `Theft` | Theft | misdemeanour | 1 000 | 8 h | | | 5 d | | |
| `Robbery` | Robbery | felony | 8 000 | | 48 h | 4 d | 10 d | | `Theft` |
| `Vandalism` | Criminal damage | misdemeanour | 800 | 6 h | | | 3 d | | |
| `Resisting` | Resisting arrest | misdemeanour | 1 200 | 6 h | 12 h | | 4 d | | |
| `NoShow` | Failure to appear | misdemeanour | 1 000 | 4 h | 12 h | | 3 d | | |
| `Violation` | Breach of probation | misdemeanour | 1 500 | | 24 h | | 5 d | | |
| `BidRigging` | Bid rigging (a cover bid for a ring of builders that came out: `TaxCases.FileBidRigging`) | misdemeanour | 7 000 | 12 h | | | 7 d | | |

**Claims the player brings** (`OwnClaims`, `JusticeState.Claims`, the save's section "O:"): the construction company sues a client or flat buyers who will not pay.
The court's fee (3 % of the claim, 150 at least, at today's prices) is paid at filing; the claim is heard 6 to 9 days on, decided by its own seed at its strength on the company's books (780 in 1000, 60 more a letter sent, 900 for the city's debt) and counsel's quality a little (`JusticeLaw.ClaimChance`); a win pays the claim and the fee back ("Court award").
The Legal file lists those waiting; the brief says one is.
A claim **against** the player for wages or materials the company left unpaid (`BuildClaims.FileUnpaid`) is an ordinary civil suit.
For the tests, `TaxCases.Withdraw`, `BuildClaims.Withdraw` and `OwnClaims.Withdraw` take back a case or claim not yet heard (the construction game's put back).

How a sentence is put together (`JusticeLaw.Sentence`):

- **Fine**: the charges' fines added, times the plea (1, 0.75 guilty, 0.6 deal), less 15 % for having complied with the police and 10 % for remorse the court believed, plus a quarter for each prior conviction (three at most), half as much again on probation.

  Then **bounded**: never more than 35 % of the cash in hand (but never under 250) and never more than 60 000; what was fined at the roadside for the same thing is taken off.
  The damages a case holds (`JusticeCase.Damages`, below) are added after the bound (`JusticeLaw.ConvictionDue`).
  Up to 15 % of cash it is paid at once; above that it is seven daily instalments.

- **Prison**: the gravest charge's term, a day more for each further felony, a quarter more per prior, scaled to the cap option (default 7, 3 to 14) and never over it, never under 2.
  A repeat offender (two convictions) gets 2 days for any felony.
  A deal on anything short of grave turns it into custody.

  With "No prison: fines and custody only" it becomes 48 hours of custody and 500 a day on the fine.

- **Custody** (no prison): the longest among the charges, 12 h more per prior, never over 48 (the station's cap).
  A first misdemeanour conviction has it turned into community service, an hour for every three.
- **Community service**: the charges' hours added, 24 at most, due in 7 days.
  A shift is 8 pieces of litter for 4 hours (half an hour a piece if given up).
  Not done in time: 100 an hour as a fine (at most a tenth of cash) and two days more probation.
  With the option off the hours are 60 each on the fine instead.
- **Probation**: the longest among the charges + 2 days per prior, 21 at most; after prison at least 5, from the release, plus twice any days not served on parole.
  On probation a new conviction costs half as much again and carries a breach charge.
- **Licence**: the longest among the charges, half as much again per prior, 30 days at most.
  Driving in that time and being stopped is a new charge.

**Money never spirals.** Every amount is bounded against cash when it is set (fines 35 %, fees 30 %, bail and civil claims 20 %, appeals 15 %, impound storage 10 %).
Instalments carry no interest and no penalty; one is only taken if it leaves the player 50; a debt that could not be collected five days running is written off (a fine into community service, an hour per 150, twelve at most; anything else simply ends).

## The courthouse

One of the game's own buildings, the same in every game: the tax office (the one government building the game has), else a bank, else a school, the largest of its kind, ties by address; a city with none of those: its largest office.

One sign on its frontage, against the wall 4.6 or 6.2 m from the door (the help-wanted boards stand at 2.4), where `CitySpots.Shopfront` found wall, pavement and room: the modelled `courthouse_sign` when the prop bundle has it, otherwise a navy board on two posts lettered in the game's font.

**Localisation:** the model carries its lettering in the mesh (COURTHOUSE, "EQUAL JUSTICE UNDER LAW": English only) and the code adds none to it (`Courthouse.Build` returns as soon as the model is in place); only the stand-in is lettered from `reallife_justice_sign`, so a translated game shows the translated word only without the prop bundle.

A translated sign needs the model's board left plain (as `precinct_sign`'s name strip is) and the code's lettering put on it at (0, 1.52, 0.027).
On a court day a brass marker points at it; the interaction key in front of the sign goes in.
No room for a sign: the pavement at the door serves.
No pavement either, or the option off: hearings are held by video link from the Legal file.

## Prison

**Custody in hours is not prison.** Everything a court orders in hours (remand until it sits, a custody sentence, a hold on an arrest by Licences: `JusticeState.JailHoursPending`, never more than `JusticeLaw.CustodyCapHours` = 48) is handed to the Law bridge (`JusticeDirector.BeginCustody`), which serves it in the nearest police station's holding cell (`Law.Custody.Hold`, see [A court's hold](law.md#a-courts-hold-custodyhold)).

Only when the bridge says no (custody or the stations switched off, no station, somebody already held) is it `PrisonStay.Custody`: the fade and the time skip below, where the player stands.
Prison, in days, is always what follows here.
`Justice check (custody): ... PASS` says which of the two served it.

Presented as time passing, with the game's own time skip (the one its hospital uses): fade, the player out of their car and out of any building and put down on the pavement outside the courthouse (or wherever the Law bridge says), and then a day at a time.

Each morning the panel comes up.
First the **parole board**, on the morning after two thirds are served: behaviour, less prior convictions, plus what is said to it, against the days served.

Then **how to spend the day**: the work detail (40 on the prison account, behaviour +1), the yard (released with full energy and a mood lift; the game has no fitness figure for the player, so that is what fitness is), or the library (+1 to the game's own `ba:skill_lawyer`, which is also what representing oneself runs on).

Then **association**: keep to yourself (behaviour +1) or mix (55 % a favour and +1, 25 % nothing, 20 % trouble and -2).
Then, seven days in ten, **something happens**, dealt from twelve without a repeat in a term, with two answers each.
A friend (Social bridge) visits on day 2 and rings every third day after.
**Good behaviour**: a day off for every four points, a third of the term at most.

Energy and hunger are held at 55 or more while the clock runs and set to at least 75 / 85 at the gate.
On release: the summary, what was earned, probation from that day, and the car the player was taken from brought to them (Transport bridge, else `VehicleTow` with 25 a day storage, 300 and a tenth of cash at most).

Businesses: **no efficiency penalty.** The game runs them through `GameManager.RunMainGameTick` during the skip exactly as during any other; no safe hook for an owner's absence was found in it, so none is applied.
Rent, loans and wages are the game's own and carry on.

## Safety

- The player is held only by the panel (game paused with the game's own `GameSpeedController`, the game's own navigation blocker) or by a time skip behind the game's own fade.
  All four are remembered by `JusticeHold` and put back together.

  The director's watchdog, once a second: a panel open with nothing behind it, a routine that has not shown a sign of life for 25 seconds of running game, or a hold with neither panel nor routine, and the player is released (a prison term cut short that way is treated as served).
  A fault anywhere does the same; three and the feature stays off for the session, with the cases kept.

- Idle cost: one time comparison a frame; once a second a handful of integer tests; the day's business once per in-game hour.
  Per frame work only while the panel is open (two key tests), a shift of community service is on, a prompt is in reach, or it is a court day (one marker).
  Nothing allocates per frame.

## For other features

They call us (all safe at any time, with the feature off or no save loaded):

```csharp
// arrested for something serious (police / custody code); tickets are never reported
Justice.Arrest(new JusticeReport(JusticeLaw.HitRun) { Witnesses = 1, InTheAct = true, Complied = true, PaidAtScene = fine, HeldHours = hours });
// charged without being taken in
Justice.Summon(new JusticeReport(JusticeLaw.HitRunInjury) { Cameras = true, Fled = true });
// the police have the player in front of them (a stop, a station desk): a bench warrant is served, a suspended licence charged
Justice.PoliceContact(driving: true);
// somebody else paid for the player's driving: now and then they sue
Justice.CrashVictim(damages, injured, killed);
```

and may read `Justice.Wanted`, `Justice.OnProbation`, `Justice.DrivingSuspended`, `Justice.InPrison`, `Justice.Priors`.
We call them through `Justice.Bridges`, each with a default that works alone:

| bridge | set by | what it is asked | default |
|---|---|---|---|
| `Law` (`ILawBridge`) | `Scripts/Law` | `PriorConvictions`; told `Convicted`, `Cleared`, `RecordSealed`; `Custody(hours, reasonKey, released)` (remand and custody sentences: served in the station's cell, see [A court's hold](law.md#a-courts-hold-custodyhold)); `Holding` (in a cell of the station's, or a save says so: never counted absent from court); `ReleaseSpot(out spot, out name)` (the spot, and the station's name that the release text uses: "at Midtown Precinct", else "at the courthouse") | the justice system's own record; a plain time skip; not held; the courthouse front |
| `Licences` (`ILicenceBridge`) | `Scripts/Licences` | `SuspendDriving(untilDay)` (0 lifts it); `DrivingSuspended` | the date kept in `JusticeState` |
| `Social` (`ISocialBridge`) | `Scripts/Social` | `CloseFriend(out name)`; told `Stood(name, visit)` | the mod's friend contact, if rung within the last 7 days |
| `Transport` (`ITransportBridge`) | whoever keeps the cars | `ReturnCar(car, near, daysAway)` | `VehicleTow.BringNear` and a storage charge |

```csharp
Justice.Bridges.Law = new MyLawBridge();   // once, when the feature starts
Justice.Bridges.Law = null;                // when it is destroyed: the default comes back
```

**One incident, one punishment.** A hit-and-run the player got away from (`PolicePursuit.Escape`, or a stop driven away from in `PoliceAttention.Clear`) goes through `PolicePursuit.SummonOrBill`. With people hurt it summons, and a case that opens takes the whole later bill (`JusticeLaw.LaterBill` is then 0): the fine is the court's sentence and the damages ride on the case (`JusticeReport.Damages`, saved after the charges on the case's line), charged only with a conviction. A dismissal or an acquittal therefore leaves nothing to be billed by the traffic camera. Only when no case opens (the justice system off, no room) is the bill queued as before; damages alone, with nobody hurt, are billed with no case. Test: Options, Justice, "A camera bill and a dismissed case" writes `Justice check (camera bill after a dismissal)` with PASS or the reason.

Hooks in the mod's own code today (one line each): `PoliceAttention.Bust` and `PolicePursuit.Catch` arrest; `PolicePursuit.ChargeForPeople` and `SummonOrBill` summon (a death, or leaving the scene); `PoliceAttention.Ticket` is a police contact; `CrashConsequences.ChargeLiability` and the two pursuit settlements report a crash victim; `NearbyInteraction` leaves the interaction key alone while `Justice.TakesInteractKey`.

## Wish-list (assets; nothing waits on them: a prop or a clip of these ids is picked up with no code change, a sound or a sprite needs one line)

| kind | id | what | used |
|---|---|---|---|
| prop | `courthouse_sign` | free-standing sign about 1.4 x 1.9 x 0.45 m, ground centre, **read from +Z**, "COURTHOUSE" on a dark board between two posts or a stone plinth | `Courthouse`: replaces the plain board with no code change |
| prop | `litter_pile` | a small heap of street litter (a can, a carton, crumpled paper) within 0.4 m, ground centre | `CommunityService`: replaces the three plain shapes |
| prop | `litter_grabber`, `litter_bag` | a pick-up tool for the right hand (0.9 m) and a refuse sack for the left | not made: `litter_pick` as delivered picks with the bare hand |
| clip | `litter_pick` | **delivered**: 1.8 s, a step and a squat, the right hand closing on the ground 0.55 m ahead and 0.05 m right of the middle at its "grip" mark (1.1 s) | `CommunityService`: on the key the game walks the player (`PlayerController.SetGoal`) to the place the clip is measured from, they are turned so that the pile is under that hand, held, and the clip is played; the litter is switched off at the clip's own "grip" mark and they are let go at its end. A step that has not arrived within 0.10 m after 2.5 s gives the prompt back; after two such in a shift the litter is picked from where they stand (one log line). `kneel_work` from where they stand without the clip |
| clip | `court_stand`, `court_swear` | standing at a rail, hands on it; right hand raised | a 3D courtroom, if one is ever built. `oath_hand` (delivered) is the raised hand: **not wired**, a hearing is a dialogue panel and nobody is played in it |
| sound | `gavel` | two raps | the first and the verdict page of a hearing |
| sound | `cell_door` | a steel door closing | the prison fade and lights out |
| sprite | portraits | judge, prosecutor, counsel, officer, inmate, visitor, clerk: 178 x 196 busts | nothing now: the list has no pictures, so these are unused |

## Not verified in the game

Nothing here has been run in the game.
Verified against the decompiled code and by the compiler: the time skip, the pause, the navigation blocker, the fade, leaving a vehicle and a building, the warp of the player's navigation agent, the skill increase, contacts and messages, the dialogue panel, building registrations, the destination guider.

Not verified, and to be looked at first in a play test: the panel's place in the draw order (its canvas is at sorting order 30 000) and whether the cursor is free while it is up (it keeps an element of its own selected, which `GameManager.ShouldBlockKeyboardShortcuts` and the mod's cameras take for an open screen; keys 1 to 3 work either way).

Whether the city has law firm businesses at all (else the chambers stand in).
Which building the courthouse turns out to be and how the sign sits on it.
Warping the player across the city out of doors.
The look of the portraits and scenes.
The length of a 24-hour time skip in real seconds.

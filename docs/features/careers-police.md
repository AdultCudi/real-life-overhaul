# Police career

## In a sentence

Police work is a twenty-sixth profession of the careers: pass the academy, join at a station, go on patrol from its front desk in the officer's uniform, answer the incidents the city raises near you from a list that shows the danger and the odds, and rise from cadet to lieutenant; a dangerous call can hurt you, and with Permadeath on the deadliest can end a life.

## Where the code is

- Folder: [`Scripts/Careers/Police`](../../Assets/Mods/RealLifeOverhaul/Scripts/Careers/Police/) (namespace `RealLifeOverhaul.Careers`), inside the careers' own [Work](careers.md) part.
- Read first: `PoliceRules.cs` (every rule and every figure, game-free) and `CareersDirector.Duty.cs` (the shift).

| File | What it is |
| --- | --- |
| `PoliceRules` | game-free: who may join, the incident table, the moves, the odds of every outcome, the roll, injuries, the rating, the review's bonus, dismissal, the stop's courtesy, the clock's jump |
| `DutyFile`, `DutyStore` | the officer's file (arrests, tickets, commendations, first aid, injuries, backing off, the shift ratings) and its own line of the save, `reallife:work_duty` (game-free) |
| `CareersDirector.DutyFile` | the game-free part of the director: the file's load and write, the review's bonus and the rating of a day (EconomyCheck runs it beside `CareersDirector.People`) |
| `CareersDirector.Duty` | the shift: begin and end, the uniform, the one-second look, the list, an answer, an injury, the dismissal, the front desk's entry, the tests |
| `CaseRules` | game-free: an arrest's quality, whether it is charged, the hearing day, the odds of each end, the sentence, a detective's actions and the officer's evidence, the commendation and the review's cost, the day's one notice |
| `CareersDirector.Cases` | the case book in play: a case opened by an arrest, the day's turn, the front desk's list of cases and their actions, the evidence given at a hearing, the Career page's lines |
| `PoliceDesk` | the list an officer chooses from, in the game's own list window (`GameUi.DeskList`), the same window as the station's front desk |
| `Wiring/Wiring.PoliceLinks` | where the career meets Law, Crime, Traffic, Ragdoll, Health, Permadeath, Licences and Justice: the posts, the conduct, the incidents, an injury, the front desk |

Outside the folder: `Careers/Work/ProCatalogue` (the profession, id 25), `Careers/Study/StudyCatalogue` (the Police academy course, id 35), `CityLife/CityOutfits` (`CityLook.Officer`), `Crime/CrimeScene.Duty` (what each move does to a street crime), `Law/StationDesk` (the front desk's entry), `Permadeath` (`DeathCause.Duty`), `Life/LifeEvents` (`InjureOnDuty`) and `Health/HealthDirector.Care` (`HurtOnDuty`, `Ailing`).

## What the player does

1. **Joining.** The Career app lists a post at a police station (one a district: the mod's own precincts, [Law](law.md)) like every public post. To apply: the **Police academy** passed (a vocational course at the school, the cadet rung's own qualification), nothing on the police record for 180 days (a ticket counts), no conviction that still counts, a driving licence in good standing, no illness or injury that needs the hospital, and **the academy's fitness test**: a fitness of 45 (`FitnessJobRules.PoliceMinimum`, option **Fitness needed for physical jobs**), asked after the record's asks wherever the others are. The refusal names the number needed, the player's and about how long it would take ("You need fitness 45 to join the police. You are at 31. A week or two of running would do it."). A new game starts at fitness 30, so the police are a week or two of exercise away. Once in, a fitness more than 10 under the 45 earns the boss's warning and then strikes on the warnings (see [Careers](careers.md), "Fitness for the physical jobs"), never the sack by itself. The same asks are made when an offer is accepted. The reasons are said with their figures.
2. **A shift.** The week is Tuesday to Saturday, 14:00 to 22:00, begun from 13:00 to 16:00. A patrol is not worked at a desk: at the front desk of any station the list has **Go on patrol** (and **End your shift** while on one), or the **My job** page's button does the same while the player stands at a station. The officer's uniform goes on (the garments of the city's officers, the same navy); the player walks or drives where they like and time passes as usual. A sleep, a skip of time or another save ends the patrol: what was patrolled is kept, paid and counted.
3. **Calls.** While on patrol the street crime near the player is three times as likely (the dispatcher sends them where it is). A street crime, a road accident or a person lying in the road within 130 m gives a notice and the scene's own marker. Within 14 m, on foot, a list opens: the kind of incident, its danger (routine, tense, risky, deadly) and, for each move, the chance it works and the risk of injury. Closing it leaves the call to be offered again after twenty seconds; **Back off** ends it at a cost to the shift's rating.
4. **Moves.** Talk them down, arrest, call for backup, give first aid, write a ticket, by what the incident is (`PoliceRules.Moves`). The scene plays out what came of it (`CrimeScene.Duty`): an arrest is the scene's own officer cuffing the suspect and putting them in the car, a talk or a ticket sends everyone on their way, a failure has the suspect run, backup sends an officer on foot; at a crash the services are sent at once and a person only knocked down is helped to their feet. A suspect is one of the street's people, who have no record or court of the player's, so an arrest is written into the officer's file and nowhere else.
5. **Risk.** A move that fails with an injury outcome hurts the officer: Health names it (a bruise from 12 hours of injury, a sprain from 30, a broken arm or concussion from 54) as an **accident at work** (the employer's insurer pays the care, the days off are sick leave) and the shift ends for them. A routine incident never hurts anyone. With Permadeath on, the injury is judged by `Mortality` as every danger is: only a deadly incident can be fatal, the first is a near miss with its notice, and after it one injury in 17 ends the life. It never does while Permadeath is off or in a Going Public session.
6. **Progress.** The ranks are the careers' own (days at the rank, points of two skills, the review's score): cadet, police officer, detective, sergeant, lieutenant, on the nurses' public scale. The last six shift ratings move a review's score by up to 8 points either way, which is what promotes. The **My job** page shows the officer's file in both interfaces (the old one's **Police file** section, the kit's **Working life** list).
7. **Leaving.** A stay in a cell, a warrant or a conviction after joining ends the service the way every sacking does (`LeaveReason.Misconduct`, a poor reference), looked at each hour and each day. A minor traffic stop of the officer on a shift in uniform ends in a word: no fine, no record, no points, unless there was drink, damage or anyone hurt.

## After the arrest: the case book

Each arrest of a street crime opens a **case** in the officer's case book, kept in the officer's file (`reallife:work_duty`, part `c`; a save without it reads as an empty book): the suspect's name (a person of the city's people pool, `PeoplePool`, as the careers name colleagues), the offence, the day and the district of the nearest precinct.

- **The offence and the words are Justice's own** (`JusticeLaw`): a mugging or a hold-up is robbery, an assault or a disturbance assault, graffiti criminal damage, a foot chase resisting arrest, every other theft. A case is heard on the day Justice's court takes for the charge's class (`JusticeLaw.DaysToCourt`: two days for a theft, three for an assault, four for a grave charge); a sentence is the charge's prison days, three quarters of them for a plea (`GuiltyPleaFactor`).
- **Charged or released.** The arrest's quality is 40, plus the move's margin over its roll (one point per 20 thousandths), 4 a rank and one per 5 points of the skill, within 0 to 100. A case is charged with a chance of 500 plus 4 a point of quality, in thousandths, and released the day of the arrest otherwise. A quality of 65 or more is a good arrest, under 40 a poor one.
- **The hearing.** The case's strength starts at the quality. At the hearing (decided on the day's turn after the hearing day) the chance of an acquittal is 600 less 6 for each point of strength and of evidence, within 50 and 700; of the rest 30 percent plead guilty and the others are convicted: the three always add to a thousand. The roll is the save's seed, the case and the hearing day (`TechRandom`), so it is the same on every machine and every load. A case ends exactly once.
- **What it feeds.** A conviction or a plea after a good arrest earns a commendation. A case released or acquitted after a poor arrest counts as thrown out and takes a point off the review's score for 90 days of a 365-day year, up to three points.
- **One notice a day.** The case news of the day (cases opened, released, convicted, acquitted, commendations) is summed into one notice at the day's turn, and never a second in a day.
- **A detective's work.** From the rank of detective, open cases show on the **My job** page (the count and the first three, in both interfaces) and the station front desk's list has **Work your cases**: a list of the open cases, then of three actions on the one picked, at a shift. Interviewing a witness rides on relationships, checking the scene on guarding and writing the file up on writing; each adds 5 plus one per 8 points of the skill plus 2 for each rung above the detective's, up to 20, to the case's strength. One action a shift on a case, none done twice on it, and at most three a shift over all cases.
- **Giving evidence.** On the hearing day, a detective on a shift is called to court once for the case and chooses how to give evidence: from the file (plus 8 to the weight of the evidence if it was written up, 2 if not), a plain account (0) or saying little (minus 6). It takes an hour of the shift in which no incident calls them.
- **The convicted person.** The mod's living city does not keep the street's people one by one: a crime scene's cast is drawn from a pool of actors, and the population has no state of a person being away (only a household's flat can be marked away). The name comes from the people pool, but nobody is taken off the streets for the sentence's days; the **case book is the record** of the sentence (the days are on the case).

## The rules, in numbers

Every constant is in `PoliceRules` with its unit; the checks (`EconomyCheck --police`) read them from there.

| What | Figure | Why |
| --- | --- | --- |
| Clean record to join | 180 days of a 365-day year (30 in the game's 60) | scaled as every careers' day count is (`TalentCatalogue.Scaled`) |
| Shift | 14:00 to 22:00, Tuesday to Saturday | the afternoon into the evening, when the city has most to answer; inside the day (the checks ask 06:00 to 22:00) |
| Dangers | 0 routine, 1 tense, 2 risky, 3 deadly | a hold-up is deadly; a mugging, an assault, a chase, a car theft, a deal and a reckless driver are risky |
| Injury hours by danger | 0, 12, 30, 54 | Health's own thresholds are 24 and 48 hours |
| Calls | notice at 130 m, list at 14 m, offered again after 20 s | the scene's own marker is about 35 to 80 m away |
| Crime near an officer | times 3 (`CityWatch.DutyCrime`) | a patrol meets trouble; otherwise a shift is mostly quiet |
| Chance it works (talk, arrest, first aid) | 450 + 3 per point of the move's skill + 30 per rank, less 150 per danger (arrest less 50 more, first aid 50 more), kept within 100 to 950 thousandths | nothing is certain either way |
| Risk of injury | 90 per danger (arrest 40 more), less one per point of skill and 8 per rank; first aid 40 per danger, less half a point; at most 450; none at a routine incident | skill and rank make a call safer |
| Backup | works 1000 less its risk; risk 40 per danger above tense | nearly always safe |
| Ticket | fails 100 per danger less 10 per rank; risk as backup's | the driver who will not take it |
| Rating | starts at 60 and stays within 0 to 100: a success +6 and +2 per danger, backup +2 (+3 at a risky or deadly call), a ticket +3, a failure -5 (-3 at a risky or deadly call), an injury -2, backing off -8 and 2 less per danger | an injury is not the officer's fault; walking away from a deadly call is nearly free |
| Commendation | a talk, arrest or first aid that settles a risky or deadly call; a shift ended at 90 or more with something answered | never for backup |
| Review | the last six ratings against 60, one point per 6, at most 8 either way | the careers' own review does the promoting |
| Pay | 80, 120, 170, 220, 290 an hour at the start's prices; a public post pays 90 percent of that | the nurses' scale (70, 120, 165, 215, 285), but the cadet starts at 80 because the rung asks a diploma and every rung after a diploma pays about the courier's top level (EconomyCheck's career pay) |
| Skills | talking on relationships, arresting on guarding, first aid on first aid; the ranks are judged on guarding and relationships | no new skill |
| Fatal | Permadeath's `DutyFrom` 3 (a deadly call), `DutyOdds` 0.06 once warned | rarer than a mugging's 0.10 |
| Clock | a look counts up to 12 game minutes; more is a skip and ends the shift | the game's fastest speed is under that a second |

The roll of an answered incident is `PoliceRules.Roll`: the save's seed, the day, the number of incidents answered so far and the rank, through `TechRandom`: the same on every machine and every reload, and the same in a Going Public session (nothing is sent over the network and nobody else's things are touched).

## How it uses what was there

| Part of the mod | What the career uses |
| --- | --- |
| Careers | the profession, ranks, pay, public employer kind, applications, offers, the working day's settling (`ProRules.Settle`, `WorkBanked`), skills learnt from work, reviews and promotions, dismissal (`EndJob`), the Career page on both interfaces, the day's boss text, the Guide's professions list |
| Study | an ordinary vocational course and exam for the entry |
| Law | the precincts as posts and the front desk's list for the entry; the police record's days since the last line, the stay in a cell and the warrant |
| Justice | convictions |
| Licences | the driving licence's standing |
| Crime | the street crimes as incidents, the scene's own officer, arrest and car, the 911 path for backup, the police stop's ticket |
| Traffic and Ragdoll | the accident and the person lying in the road, and the call for the services |
| Health | the injury path (`LifeEvents.Hurt`, an accident at work, sick leave, days off) |
| Permadeath | `Mortality` (the judgement, the near miss and the death) with its warnings and switches |
| Jobs | the work clothes' machinery (`JobUniform`), and the rule that a gig shift and a patrol are never on together |
| UI | the game's list window (`DeskList`), which the kit draws as its picker |

## Where the player's choices have no option

The police career is additive (a new profession and a new button), so it has no option of its own; it runs with the careers' **Professional jobs** option and stands down with it. The injuries and deaths follow Health's and Permadeath's own switches.

## Tests (Options > Tests, under Professional jobs)

- **Go on patrol now, or end it**: the uniform and the patrol at once, wherever the player stands.
- **Be hurt at a risky call on patrol**: the injury path as a real call would send it.
- The crime, accident and 911 tests stage the incidents to answer.

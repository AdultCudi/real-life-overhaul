# Health

## In a sentence

An illness or an injury that is left alone can turn serious and mean days in hospital while the businesses run on; the player can book the doctor, take a health plan that pays part of the bills, owe a bill as a medical debt and use sick leave at their job, and the player's staff, the rival tycoons and the city's people fall ill by the same seeded rules, with the flu going round in midwinter.

## Where the code is

- Folder: [`Scripts/Health`](../../Assets/Mods/RealLifeOverhaul/Scripts/Health/) (namespace `RealLifeOverhaul.Health`). The kit's page is `Scripts/Ui/Ventures/HealthPages.cs` (game-free) and `HealthLive.cs`; the wiring is `Scripts/Wiring/Wiring.HealthLinks.cs`.
- Option: "Illness, the doctor, hospital stays, health insurance and sick leave" (`health`, on by default). Off: everything is as before the feature (Home's own illness, the doctor's desk, the phone line and the working life's sick days behave as they did), the case under way ends as far as Health goes, a stay is cut short and billed for the days begun, the appointment is dropped, the app leaves the phone; the policy's record, the leave and what is owed stay in the save, and **the debt is still collected**.
- Checks: EconomyCheck `--health` (`HealthChecks.cs`).

```
HealthCatalogue            every figure, each with its unit and its source
HealthState, HealthStore   the records and the save line reallife:health (version 1)
HealthRules                game-free: the seeded draws, the flu's wave, the player's way of living, a case's day, injuries, an accident at work, a stay's length, a rival's day
HealthCover                game-free: a plan's premium, what a plan pays of a bill, the policy's month, the medical debt
HealthLeave                game-free: sick leave at the player's job: excused days, the note, paid days, a day seen through
HealthDay                  game-free: the steps the director takes (an appointment, a premium, the debt's day, a bill met, admission and discharge, the option off and on)
HealthPeople               game-free: the city's people's day in slices, the people ill at the player's home, the player's staff
HealthFacts                what the pages read
HealthDirector (+ parts)   the timer (a second), the hour, the day, the case, the doctor, the stay, the leave, the plan, facts, tests, the app
HealthGame                 the one file that touches the game: cash, the game's needs and its nights in hospital, the staff's sick days, the population, notices
HealthDoctor               the words at the doctor's desk and on the doctor's phone line
HealthApp                  the app in the game's own frame; the kit's page is Ui/Ventures/HealthPages
```

## Why it was on hold, and what still holds

The owner put Health on hold on 14 Oct 2026 together with a player's suggestion of a clinic, a fire station and a police station of the player's own ("not now"), on the day character, face, body and hair work became the owner's alone. Two things from then still hold and are kept: **there is no clinic of the player's own** (the doctor is seen in the game's own hospitals and clinics, and nothing new is built), and **nobody's looks change** (illness shows in the game's needs, mood modifiers, texts and existing places only; no animation and no model is added).

## What was there already, and what Health adds

Home ([home.md](home.md)) already gives the player a cold, the flu, food poisoning, back pain and burnout, with a course in hours, a doctor at the desk of the game's hospitals and clinics (a consultation of 80, a prescription of 45, a sick note of 20) and Life's doctor on the phone; Life gives injuries (`LifeEvents.Injure`: a crash, a hit on foot, a mugging) and the game's own night in hospital. Health does not copy any of it. It follows Home's illness as the player's **case** and adds what was missing: an illness can turn serious, injuries have a name and a course, the hospital keeps a serious case for days, there are health plans, a medical debt, an appointment, sick leave with limits, and the same illnesses for the staff, the rivals and the city.

| Hook | In | What it does |
| --- | --- | --- |
| `HomeBridges.Roll` | `HomeHealth.Draw`, `CookActivity` | every one of Home's eight illness chances is drawn from the save's seed, the day and the chance's slot while Health runs, so loading a day again changes nothing; off, the game's random number as before |
| `HomeBridges.Held`, `TooIll`, `StatusNote` | `HomeHealth.Hourly`, `TooIllToTrain`, `HomeDirector.Strip` | a serious illness is held on (it does not clear up by itself), a serious case or a broken arm rules out hard exercise, and the injury or serious case is said beside the needs' bars |
| `DoctorVisit.Admission`, `Paid`, `Treated`, `Wait` | `ClinicDialog`, `WaitingRoomActivity` | the doctor admits a serious case instead of prescribing, a plan pays its part of a fee back, a case is treated, an appointment kept is seen within minutes |
| `LifeEvents.Hurt` | `LifeEvents.Injure` | every injury of the mod's gets a name and a course |
| `PhoneContacts.DoctorFirst` | `DoctorCallDialog` | the doctor's phone line books an appointment, and sends what needs the hospital there |
| `CareersBridges.DayMissed`, `ShiftWorked`, `CareerLinks.Health` | `CareersDirector.SettleDay` | sick leave settles a working day not worked and pays its sick pay; an accident at a risky trade; whether a day off is excused |
| `RivalBridges.HealthLine` | `RivalPage` | a rival off sick, on the rich list and their panel |

## What the player does

1. **Staying well.** The Health app (You) shows how the player is, the flu in the city, who is ill at home and what their way of living does. Sleep owed doubles every chance, a poor diet adds half, good food and fitness take a quarter off each, within a half and three times (`HealthRules.Lifestyle`). With no wave and nobody ill at home Health adds no chance at all.
2. **Falling ill or being hurt.** Home's illnesses by Home's causes; the wave's flu and a housemate's cold by Health's daily draw; an injury from any of the mod's hurts of a day or more (a sprain from 24 hours, a broken arm or, one time in three, a concussion from 48) or from an accident on a shift worked at a risky trade.
3. **Rest and care, or neglect.** A day is rest when the case is treated, or the player owes 8 hours of sleep at most and has 25 energy and 20 hunger at least. Without it a day of neglect is counted; a day of rest takes one off. After **3** days of neglect an untreated flu, food poisoning or exhaustion turns serious with a chance of 35% a neglected day, and surely at **6**. A treated illness never turns, and neither do a cold or a bad back.
4. **Serious.** A serious illness is held on in Home, drains energy (0.03 a game minute, Life's own sick drain again) and rules out hard exercise. It does not end by itself and it ends nobody's life: it waits for the hospital. A broken arm and a concussion are serious from the first hour.
5. **The doctor.** **Book an appointment** in the app or on the Doctor contact's line: two hours on today while the surgery has hours (9 to 16), else 9 the next morning, at one of the city's hospitals or clinics. Kept from 20 minutes before its hour to 45 after, the waiting room takes 5 minutes instead of 20 to 35. Not kept, it costs 25. One at a time; cancelling is free. The appointment is in the Planner and the Calendar.
6. **The hospital.** At the doctor's desk a serious case is admitted (never prescribed for): 1 day for food poisoning or a broken arm, 2 for the flu, exhaustion or a concussion, 5 at the most by rule. The screen goes dark and the game's own time skip runs to eight in the morning, a day at a time, with energy and hunger held at 55: the days really pass, so the businesses run on, bills fall due and the court sits. The game's own night in hospital (a collapse, the ambulance, a crash) counts as the first day of a serious case's stay. At the discharge an illness is over, a broken arm heals at home for 6 more days and a concussion for 2, and a note covers the stay's working days and 2 after.
7. **Insurance.** The Insurance tab: three plans, the first month paid when it is taken.
8. **Bills and leave.** The year's bills and what plans paid, the debt with **Pay it off now**, the paid sick days, the days off running, and **Call in sick today**.
9. **The city.** Who is ill today, the month's cases and bills, the staff off tomorrow, a rival who is off.

## The numbers

Money is at the start's prices and follows the city's prices (`HealthRules.At`, which is `PriceRules.At`). All are in `HealthCatalogue`.

| What | Figure | From |
| --- | --- | --- |
| Doctor, prescription | 80, 45 | Home's own fees (`HomeBalance.ConsultationFee`, `PrescriptionFee`), read by the people's bills |
| A stay | 800 and 900 a day, 5 days at most (5,300) | the game's own night is 2,000 (`GameManager.HospitalRespawn`) |
| The game's night | 2,000, not moved by prices | the game takes it itself; a plan pays its part back |
| A missed appointment | 25 | |
| Plans, a month | Basic 60, Standard 140, Full 290 | times the age's share: 0.8 under 30, 1.0, 1.2 from 40, 1.5 from 50, 1.9 from 60, 2.4 from 70; and 15% more for each claim of the last year, up to 60% |
| Of a stay | 70%, 85%, 100%, over an excess of 500, 250, 100 a stay | |
| Of the doctor and a prescription | 0, 50%, 80% | |
| The most a plan pays in a year | 20,000, 60,000, 200,000 | |
| Waiting days | 3 (an employer's plan: the day after the job began) | |
| Lapse | 2 premiums missed running | |
| Medical debt | no interest, over 2 months of days, 10 a day at least; court after 10 days running with nothing taken | |
| Sick leave | 2 working days running without a note; a spell ends after 3 days back; paid days 3% of the year, 2 at least (2 of 60, 10 of 365); no sick pay in a job's first 3 days | |
| A day called in while well | seen through 20%, then 40%, 60%, 80% for each such day of the year before it | |
| The wave | 12% of the year, 6 days at least; its top in the first tenth of the year; severity 0.6 to 1.4 | |
| The player in the wave | 1.5% a day at an average top, times the way of living | Home's own flu chance (`HomeBalance.FluADay`) |
| Somebody ill at home | 4% a day each, 2 people counted | |
| An accident at work | 0.4% a shift at a risky trade, 1.2% at the riskiest, twice that without the day's rest; one in five breaks an arm | |

## Insurance, and why it is never a profit

- A claim is the plan's share of the bill (of a stay: of the part over the excess), held to what is left of the year's cap, **never over the bill and never under nothing** (`HealthCover.Claim`).
- A policy covers only what begins from its fourth day on (`HealthCover.Covers`): **cover bought after falling ill pays nothing of that illness**, however long the player then waits to be admitted, and changing to a better plan is a new policy with new waiting days.
- The first month is paid before anything is written (`HealthDay.TakePlan`); a premium is taken once a month, days away are one premium, and the second missed running ends the plan. A plan whose premiums were not taken for a month (the option off, say) has ended when the feature is back (`HealthDay.BackOn`), and the game's nights in hospital while the feature was away are not claimed afterwards.
- **An employer's plan.** A job in the public sector (a hospital, the school, the council), a tech company that grants share options, and the insurance asked for and granted on the career ladder give the Standard plan while the job lasts, with no premium. Of the player's own plan and the employer's, the one that pays more of a bill pays it, once.
- **An accident at work** is the employer's insurer's in full (never more than the bill), and its days off are paid outside the year's paid days.
- Not covered by any plan: what began before it or in its first days, sick notes, the ambulance and a missed appointment.

## Bills and the medical debt

A bill is met by `HealthDay.Bill`: the plan's part, then the cash that is there (the **spare** cash: over what the investors of the player's firms are owed, as a gift), in full or in part, and the rest is owed. Nothing is taken by force and nobody is refused care, so **illness cannot take a wallet under nothing**: the dearest stay is a debt of 5,300 at the start's prices, paid off at 10% of it a day in a 60-day year. Home's doctor takes the consultation and the prescription at the desk itself; Health only brings the plan's part back.

The debt is taken an instalment a day from the spare cash (`HealthDay.Debt`). A day with nothing taken is a late day; after 10 running the lender files a civil claim with the mod's court (`EconomyBridges.FileClaim`, `DebtClaims.File`), and the debt is Justice's from then on, collected as Justice collects all its debts. With no court to go to it stays owed and is tried every day. It is listed among the Finance app's debts (`EconomyBridges.OtherDebts`), counted on the credit file as an account (`OtherAccounts`: opened, paid, missed, paid off, claim filed) and **collected with the option on or off**.

Being ill excuses a working day and nothing else. A stay in hospital moves the clock by the game's own time skip, so every debt, trial and deadline of the game and the mod goes on through it; nobody is admitted without a serious case, and no stay is longer than 5 days.

## Sick leave

`CareerLinks.Health` answers whether a day off is excused (`HealthLeave.Covers`): a day in hospital or under a doctor's or a hospital's note; a day really ill (Home's illness, Life's sickness, a case of Health's) for 2 working days running, an accident at work for as long as it lasts; a day the player called in sick while well unless the employer sees through it (a draw of the day's own, so a load changes nothing). A day not excused is the working life's own missed day: a warning, the third the sack.

`CareersDirector.SettleDay` tells Health of a working day not worked, once, before it asks whether it is excused (`HealthLeave.Settle`): the spell is counted, and the day's pay (the profession's hours at its rate) is paid as sick pay while paid days are left in the year, never in the job's first 3 days, never for a day seen through, never twice for a day and never for a day worked. The street careers' shifts are excused by the same rule and are unpaid, as they are paid by the shift.

## The staff, the rivals and the city

- **The player's own staff** (`HealthGame.StaffDay`, `HealthPeople.StaffCourse`). Nothing is added to the game's own sick days except in the wave (1% a day at an average top) and where colleagues are off at the same place (1.5% each, 3 counted). The game's health plan of the HR manager cuts the chance by 15% a level (bronze, silver, gold) and from silver takes a day off the course of 1 to 3 days. Somebody ill is off through the game's own sick day (`nextSickDay`, as the tech sector does it), so the game calls the business and finds cover as it always does, and the Company's sick pay policy pays as it always does. With no sick pay they come in ill the first day and count as a colleague who passes it on. Only the player's own people (`GoingPublic.Mine`).
- **The rivals** (`HealthRules.RivalDay`). Each active tycoon falls ill with a chance of 0.12% a day, more in the wave, for 2 to 5 days; 8% of cases need the hospital. A rival who is off makes no move and grows at half the pace until they are back (the rivals' own `QuietUntil` and `SlowUntil`), a stay's bill leaves their worth from the part over their floor only, a line says so on the rich list and their panel, and a stay is in the City Ledger the next morning. The rivals rest in a Going Public session.
- **The city's people** (`HealthPeople`). A person's day is one hash of the save's seed, the person and the day, so nothing is kept for any of them. In winter 0.3% of adults catch a cold a day (0.18, 0.09 and 0.2% in the other seasons, half as many again for children), the flu is 0.04% a day out of the wave and up to 0.6% more at an average top (1.4 times for the old), food poisoning 0.03%, a bad back 0.05% and exhaustion 0.025% (adults; exhaustion from 45 hours a week), a sprain 0.025% and a broken arm 0.004%. What is catching passes to a housemate once (6% a day for two days). 4% of flu cases, 3% of food poisoning and 5% of exhaustion need the hospital, three times as many of the old, and a broken arm always. A case costs its household the lost days' pay an employer does not make up (half, the Company's statutory share; 5 days at most) and the care no plan pays (a household with somebody in a job of 30 hours has the Standard plan's shares, and otherwise 70%, 88% or 97% of households by class do), out of its savings and then onto its debt, by 6,000 a case at the most. Household debt is what Giving's food bank pays down and what a friend in trouble asks for help with, so both meet medical debt as it is. The player's own household's money is never touched; its people's illnesses are what the player can catch at home.

The wave is bounded by its figures and checked on a city: over a whole year never more than 5% of the people are ill at once.

## The save

`reallife:health`, version 1: the seed, the day and hour last lived, the game's count of hospital nights, the year's bills and claims, the case, the policy with the days of its claims, a stay under way (from its admission to its discharge, so a load in the middle goes on to the same morning), the debt, the leave, the appointment, the staff's cases, the rivals', the people's pass with its cursor and the month's sums, who is ill by the day their case ends, and the paper's notes. A save without it loads with nothing in it. Every number is written and read in the invariant culture, every figure read is put inside its bounds (a line cannot owe a negative debt, claim more than a bill or keep a stay longer than the longest), and a line from a newer version is read as far as it goes.

**The people's pass** runs in slices of 12,000 people a second with its cursor in the save, so a save, a load or the day turning in the middle neither draws anybody twice nor leaves anybody out.

## Going Public

Everything is drawn from the save's own seed through a fixed hash, and nothing is sent. The city's people are the host's in a session and the rivals rest, so the people's pass and the rivals' day stand down (the page says why); no money of the player's ever goes to them, so there is nothing to refuse. The player's own case, plan, debt and leave go on, and only the player's own staff are drawn. The clock is shared, so a stay never moves it: it is billed in full and served at once, as custody is.

## Cost

No Update. A one-second timer compares the day and the hour. The day's pass is a few operations, one walk over the game's list of employees and one over the households (for the player's own). The people's pass is a few hashes a person, 12,000 people a second, nothing allocated (EconomyCheck: a day over 42,000 people under 120 ms in all, a slice under 40 ms, 0 bytes).

## Left out, with the reason

- **A pharmacy.** The game has no pharmacy and no medicine item ([home.md](home.md), "What the game has": "no doctor ... and no medicine items"); a prescription is dispensed at the doctor's desk as Home has it.
- **A health charity in Giving.** Health's lever for the people is household debt (`HouseholdTable.Debt`), which Giving's food bank already pays down: a fourth charity would be a second name for the same lever, as nothing is kept per household that says which part of a debt was medical.
- **Death.** Health ends nobody's life. With Permadeath on, an untreated flu follows Permadeath's own warnings and rules as before.
- **A flu jab, and illness for the street careers' pay.** No vaccine or medicine is named; shift work is paid by the shift, so its sick days are excused and unpaid.
- **The wave with Home's illness off.** The player catches nothing then: Home's option is the switch for the player's own illness.

## Tests

Options, Tests, Health: fall ill now (the flu by Home's rules where they allow it, else a sprain); make what you have turn serious; be admitted now; run the city's people's day now; have one of your staff fall ill tomorrow; put health back as before. Each writes one `Health check (...)` line.

Not verified in the game: the stay's time skip over several days and its resuming after a load, the doctor's admission page and the phone line's booking, the app in both frames, the line beside the needs' bars, the Planner's and the Calendar's entries, the rich list and paper lines, the staff's sick days through `nextSickDay`, and the credit file's lines.

# Insurance

## In a sentence

One Insurance app holds every cover the player buys: a tier for each car's policy at the Garage, priced by the driver's credit grade, licence points and crashes, a policy for each home against repair bills, and the health plan and the business cover that Health and the Company already sell; a claim comes off the bill by itself, never for more than the loss, and a missed premium gets a few days of grace before the cover lapses.

## What was there before, and what this adds

Three of the four covers existed before this feature. It does not replace them; it builds on them.

| Cover | Before | Now |
| --- | --- | --- |
| Car | one policy a car, bought by calling the Garage (`CarPhone`, `CarOwnership`, `CarRules`): the premium by the car's price and its claims, 80% of a repair over an excess of 100, ended the morning a renewal could not be paid | the same policy and the same booking, with a **tier** (third party, standard, full), the driver's record in the premium, a no-claims discount, first days, the damage from before the cover left out, a year's limits, days of grace, and two more bills it pays: the damages to another car and a tow |
| Health | Health's plans (basic, standard, full) with their excess, share, year's cap, first days and lapse (`HealthCover`) | unchanged; shown, taken, changed and cancelled in the app by Health's own calls. The rival tycoons now hold a plan too |
| Home | nothing | a policy for each home, three tiers, for the repair bills of life events |
| Business | the Company's fire, theft and liability cover from three insurers, with claims to put in (`CoverRules`) | unchanged; shown, bought, cancelled and claimed in the app by the Company's own calls |

With the option off the Garage's policy is exactly what it was, homes are not covered, and Health and the Company go on as they do.

## Where the code is

- Folder: [`Scripts/Insurance`](../../Assets/Mods/RealLifeOverhaul/Scripts/Insurance/) (namespace `RealLifeOverhaul.Insurance`). The kit's page is `Scripts/Ui/Ventures/InsurePages.cs` (game-free) and `InsuranceLive.cs`; what the Garage asks is `Scripts/Transport/CarCover.cs`; the wiring is `Scripts/Wiring/Wiring.InsuranceLinks.cs`.
- Option: "Insurance for your cars, health, homes and businesses" (`insurance`, on by default). Off: nothing runs, no home's premium is taken, no tier is applied, and the app leaves the phone; the line in the save stays as it was and goes on when the option is back on.
- Checks: EconomyCheck `--insurance` (`InsuranceChecks.cs`).

```
InsuranceCatalogue            every figure, each with its unit and its reason
InsuranceState, InsuranceStore   the records and the save line reallife:insurance (version 1)
InsuranceRules                game-free: the car's factor, a home's premium, what a claim pays, first days, the year's limits, grace, the rivals
InsuranceFacts                what the pages read
InsuranceBridges              what it asks of the Garage, Health, the Company, the credit side, the licence and the rivals
InsuranceDirector (+ parts)   the timer (a second), the day, the cars, the homes, the rivals, facts, tests, the app
InsuranceGame                 the one file that touches the game: cash, the player's homes, notices
InsuranceApp                  the app in the game's own frame; the kit's page is Ui/Ventures/InsurePages
```

## What the player does

1. **Cars.** The Cars tab lists every car the Garage keeps for the player, with its state and its month's premium, and the driver's record with what each part does to a premium. Pick a car, press the tier's row to see each tier's terms, then **Insure at this tier**, **Change to this tier** or **Cancel cover**. A car can still be insured by calling the Garage: that is the standard tier.
2. **Health.** The Health tab lists Health's three plans with the player's own marked. **Take this plan** and **Cancel your plan** are Health's own calls.
3. **Homes.** The Homes tab lists every home with its policy. The same three buttons as for a car.
4. **Businesses.** The Businesses tab lists each business's three kinds of cover. **Buy this cover** (from the insurer showing) and **Cancel cover** are the Company's own calls.
5. **Claims.** The Claims tab lists every claim, newest first, with the bill, the player's part and what the insurer paid. A business claim still to be put in has **Put in the claim** (the Company's own call).

Every tab opens on what is held and what it costs, and ends on a sentence of how it works and the pointer to the Guide.

## The car's tiers

| | Third party | Standard | Full |
| --- | --- | --- | --- |
| Of the Garage's premium | 55% | 100% | 135% |
| Own repairs | not covered | 80% over the first 100 | 95% over the first 50 |
| Damages to another car | all over the first 150 | all over the first 100 | all over the first 50 |
| A tow | not covered | half | all |
| The most in a year | 6,000 in 3 claims | 12,000 in 4 claims | 30,000 in 6 claims |

Money is at the start's prices and moves with the city's (`InsuranceRules.At`). The standard tier is the Garage's policy as it was (`CarRules.Excess`, `CarRules.Cover`): a save with insured cars reads them as standard, with no first days.

The premium is the Garage's own (the car's list price, its claims, the custom shop's share) times a factor (`InsuranceRules.CarFactor`), held between 40% and 300%:

- the tier's share above;
- the credit grade: very poor +25%, poor +15%, fair +5%, good 0, very good -5%, excellent -10% (good while the credit side is off);
- 3% for each point on the licence, up to 12 points;
- 10% for each crash with damages to another car in the last 56 days, up to 5, whatever car was driven;
- less the no-claims discount: 5% for each month renewed without a claim, up to 30%, lost at a claim that pays.

The standard tier with good credit, a clean licence, no crash and no discount is a factor of exactly 1: the Garage's premium.

## A home's policy

| | Basic | Standard | Complete |
| --- | --- | --- | --- |
| A month, before the floor area | 12 | 22 | 38 |
| For each square metre (up to 600) | 0.05 | 0.08 | 0.12 |
| Repairs at home | 70% over the first 40 | 85% over the first 20 | all |
| Repairs to the player's own things | not covered | not covered | 80% |
| The most in a year | 1,500 in 4 claims | 4,000 in 6 claims | 10,000 in 8 claims |

The premium is written for a month of 28 days and a month of another length pays in proportion, as the Garage's does. It moves with the district (0.3% a point of district income under 100, between 85% and 115%, as the Company's theft cover), by 15% for each claim of the last 56 days (60% at most), and by the same no-claims discount as a car's.

What it pays for is what the mod charges at home today: the repair bills of life events (`LifeEventCatalogue`, the transaction `repair`): a plumber, a locksmith, a dead fridge, a rug after the rain. An event that needs a home is a repair at home; with several homes it falls on one of them by the day alone (`InsuranceRules.HomeAt`), and only that home's policy pays, so each home needs its own. A repair event that needs no home (a phone's screen, a laptop) is a repair to the player's things, which the complete tier of any one home covers. The mod has no burglary, fire or flood at a home, so there is nothing else for a home's policy to pay; a break-in and a fire at a business are the Company's cover.

## Claims

A claim is worked out when the feature whose bill it is asks, before it books the bill:

| Bill | Asked by | How it is paid |
| --- | --- | --- |
| a car's repair | `CarOwnership.Repaired` (`CarCover.Repair`) | the Garage pays the policy's part out as "Car insurance payout", as before; the game took the repair itself |
| damages to another car | `CrashConsequences.ChargeLiability` | the damages are taken less the payout, and the bill's own message says what the cover paid |
| a tow | `VehicleTow.BringNear`, the Roadside Assistance tow | the fee is taken less the payout, and the bill's own message says so |
| a repair at home or to the player's things | `LifeEventRunner.Apply` | the bill is taken less the payout; the event's words name the whole bill and the insurer's notice says what it paid |

Rules, each checked offline:

- A claim never pays more than the loss, and the holder's part and the payout together are never more than the loss (`InsuranceRules.Pays`).
- **First days.** A new car policy pays nothing for 2 days, a new home policy for 3. A renewal has none.
- **Nothing from before the cover.** A car's damage when its policy begins is kept (`CarPolicy.Pre`); a repair is the insurer's only for the damage above it (`InsuranceRules.CoveredDrop`). A car is moved to a higher tier only when it is repaired and running.
- **The year's limits.** Each policy pays so many claims and so much money in a year of the calendar. A policy ended and bought again keeps its year: its record stays while the player owns the car or the home.
- **A claim costs.** It ends the no-claims discount; a car's repair claim also counts at the Garage as before (25% a claim for 56 days), a crash counts on the driver's record, and a home's claim loads its premium.
- **Not covered:** damages the police collect after a chase or a stop (the player did not stop at the scene), the police's impound, a tow after prison, a hired or borrowed car, a dealer's stock, a partner's car.
- A claim refused for the first days or the year's limit is said in a notice, so the full bill is no surprise.

## A missed premium

A car's premium is the Garage's renewal and a home's is taken on its day (`InsuranceGame.Pay`, never by force). One that cannot be taken begins 3 days of grace: the cover holds, the player is told once, and it is asked for again each morning. Paid within them, it goes on; after them the policy lapses with a notice. A load after days away owes one premium, never the months gone by.

## The rivals and the city's people

- **The rival tycoons.** Each forward tycoon holds one of Health's plans by the same prices as the player (basic; standard from twice their floor; full from four times), pays its month's premium from the worth over their floor, and loses it after two premiums they cannot pay, until they can again. Health gives the tycoons hospital stays and takes the bill from their worth; with a plan the bill costs them only what the plan leaves (`HealthCover.Claim`, wrapped round `HealthBridges.RivalPay`). Their panel on the Rivals page says which plan they hold and what it has paid this year. They rest in a Going Public session.
- **The city's people** already hold health plans by class (`HealthPeople.Insured`); that is Health's and unchanged.
- **The city's businesses** are charged nothing by the mod for a fire or a break-in, so there is no setback of theirs for a cover to reduce.

## Going Public and performance

Nothing is drawn at random: which home a repair falls on is a hash of the day. Nothing new is sent to other players. Only the player's own cars and homes are read (`CarPhone.Runs`, `GoingPublic.Mine`). The director looks once a second and passes over the cars and homes once a day; a claim is a few operations when a bill comes. There is no Update.

## The save

`reallife:insurance`, version 1: the last day looked at, each car's policy (tier, the day it began, the damage from before it, months without a claim, the first morning of a missed premium, the year's sums), each home's policy (the same, with the day its premium is due and its claims' days), the claims paid (the newest 40), the days of the crashes that count, and the rivals' cover. A line that cannot be read is kept as it was. Whether a car is insured today is the Garage's record, not this line.

## Tests

Options, Tests: open the app; a repair at home now; damages to another car now; miss a home's premium now; put insurance back as before.

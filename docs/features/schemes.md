# Schemes and inquiries

## In a sentence

White collar schemes that pay at first and leave a trail (an insurance claim for a loss that never happened, an invented expense on the return, dressed books before a sale, a members' club that does not exist, its cash run through a till, money to people with power), the inquiries that follow the trail into the mod's own courts, and the rival tycoons doing the same by the same rules, with the player sometimes their victim.

Nothing here is violent, and no scheme is told in more detail than a tycoon game needs: what it brings, what it risks, how it comes apart.

## Where the code is

- Folder: [`Scripts/Schemes`](../../Assets/Mods/RealLifeOverhaul/Scripts/Schemes/), namespace `RealLifeOverhaul.Schemes`.
- Pairing: [`Wiring/Wiring.SchemeLinks.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Wiring/Wiring.SchemeLinks.cs).
- Doors in other features, each a small hook: `Economy/Company/CompanyDirector.Schemes.cs` (an invented claim by the insurer's rules), `Justice/SchemeCases.cs` (a file as a court case), `Fame/FameDirector.Schemes.cs` (exposure, a push carried, a bought invitation), `Population/PurseRules.cs` (households' savings), `Ui/Company/CompanyLive.Quiet.cs` (the kit's quiet button).

| File | What it is |
| --- | --- |
| `SchemeCatalogue` | every figure, with its unit and why: game-free |
| `SchemeState` | a save's trails, inquiries, cash in hand, people paid, businesses' settings, rivals' trails and the fund: game-free |
| `SchemeRules` | the trail: an act's heat, the line, the file, the charge, the court's sum; its parts `.Money` (each scheme's sums, the till, paying back), `.People` (tempers, offers, favours), `.Day` (the day, an inquiry's stages, the findings), `.Rivals` (the rivals and their fund): game-free |
| `SchemeStore` | the save's line `reallife:schemes`: game-free |
| `SchemePages` | each page as plain data, every answer with the press it stands for: game-free |
| `SchemeKeys` | the locale keys named by a number: game-free |
| `SchemeBridges` | what it asks of the company, the people, the courts, Standing, the social life, the rivals and the paper |
| `SchemeDirector` | the state, the day and the save; its parts `.Day` (carrying out a day's events), `.Acts` (what a press does), `.People` (favours, the Legal file's lines, the doors other features use), `.Pages`, `.Tests` |
| `SchemePanel` | the one screen: the game's station list, on the RLO interface the decision card |
| `SchemeGame` | the only file touching the game: the day, the wallet, the save's seed, the one booking call, notices |
| `SchemeText`, `SchemeGuide` | the words, and the feature list's entry with the Guide page's figures |

## Where the player finds it

There is no app for it. Each scheme sits where the honest action sits, as a quiet second button (`CompanyBridges.QuietLabel`, shown only where there is something to act on):

| Scheme | Where its button stands |
| --- | --- |
| A loss that never happened | Company, Cover: a policy's page, for a cover in force |
| An expense that never was | Company, Tax and licences: the return, while the company's tax part runs and the game's rate is above nothing |
| A second set of books (sales that never happened, cash through the till) | Company, Tax and licences: a business's cash off the books |
| A club that does not exist | Company, Marketing: a campaign's page, for a business with customers at a counter |
| Money to people | a call to somebody the player knows who holds a position (Networking), as "A word in private" |
| A rival's fund | a notice that opens its page, and the Legal file |

Every page (`SchemePages`) says what the scheme is, what each answer brings and what it risks before the player chooses, and can always be left. One place lists what hangs over the player: the Legal file (`SchemeDirector.FileLines`, `FileActs`), with every inquiry they know of, what paying it back costs, and what can be done.

## The schemes

Each comes in two sizes: small (250 thousandths of the most that is plausible) and large (the whole).

- **A loss that never happened.** A claim on a cover in force for a quarter or the whole of the most that cover counts (`CompanyCatalogue.CoverMost`), put in by the insurer's own rules (`CoverRules.Open`, `File`): it pays less the excess on the insurer's day and counts in the policy's term, towards its loading and the insurer's limit. One at a business in 20 days. Found: what was paid is taken back, a claim not yet paid is refused, and that insurer ends the cover there and will not write it again. An invented claim is marked (`Claim.Invented`, saved): a police report handed over after a real break-in is attached to the newest real theft claim and never to an invented one, so an invented claim pays what the trail recorded.
- **An expense that never was.** 20 days of the businesses' average takings (5 for a small one), worth the game's tax rate on it, paid at once as tax saved, only while the game's yearly tax falls due at all. One in 15 days.
- **A second set of books.** The books of a business show 10 % or 30 % more profit to whoever values it for a sale (`ServicesBridges.BooksShown`). A buyer looks before paying, by kind: an investor in 800 of a thousand cases at the full lift, a chain in 650, a rival in 450, the staff in 150, in proportion for the smaller lift; whether a given offer is seen through is fixed by the save, the place and the offer. Seen through: the buyer walks, says why (300 heat), the books are plain again and no money moved. Sold: the part of the price the lift made goes on the trail. Books dressed by 30 % also heat their trail by 6 a day while they stand, and only while the option is on.

  The lift is read when the price is made, not when it is paid. A listing is stamped with the lift of the books it was last valued from (`SaleListing.Lift`), and each offer with the lift of the books its price was made from (`SaleOffer.Lift`, saved with the offer; a save from before it loads with none). The buyer's look and the act that goes on the trail use that stamp: listing at 30 %, taking the offer's price and setting the lift back to nothing before accepting changes nothing, the buyer looks at 30 % and the act is the part of the price the 30 % made. A price made from plain books and accepted after the lift was set puts nothing on the trail. The books of a business are plain after it is sold whatever the stamp.
  The quick sale (seven tenths of the value, at once) is priced from the books as they stand when it is pressed, and its buyer, a dealer in businesses who looks like an investor, looks too, by an id of its own (minus the price, so the same quick sale is always seen through or never, and no offer ever has it). An offer from a rival of the game's goes the same way as any other offer: it is accepted through the same look, at the rival's own diligence (450 of a thousand at the full lift). A business handed over in the takeover of the player's listed company is not sold for a price (`ServicesDirector.HandOver`), so there are no books for anyone to read there.
- **A club that does not exist.** 50 or 200 households of the business's district, picked by a seeded draw, each pay a twentieth of their savings, 400 at most at the start's prices, nothing from a household with under 200 (`PurseRules.Take`). The money really leaves their savings. It becomes cash in hand, which no bank has seen; one household in 25 complains, which is people who know. One from a business in 30 days. Only where the city's people are this machine's own: refused, before anything is asked of anybody, in a Going Public session or with the population off or followed.
- **Cash through the till.** A business with customers at a counter takes 5 % or 20 % of an average day's takings a day out of the cash in hand as sales. 15 % is lost on the way and the game's tax rate is taken off the rest at once; what is left is booked to the player, never to the business, so no buyer, bank or investor reads a higher figure from it. The bank reports a day's cash sales over 2,500 (30 heat) and 25,000 within its window of 30 days as a pattern (120 heat, once a window). A window's washing at a business is one act on the trail, growing by the day.
- **Money to people.** See below.

Money moves only through `BizBooks.Book`, in `SchemeGame`'s three doors (`Pay`, refused when the wallet does not hold it; `Take`, forced; `Give`). No scheme's payment may leave the wallet under what it holds for a firm's investors (`SchemeRules.Affords`; the firms keep that floor in cents, `TechDirector.FloorNow`, and the schemes count it in whole money, rounded up). What an inquiry takes by force is not held back by it, and it is not misuse of a firm's money either: the findings, their penalty and a file paid back are booked under their own transaction types (`SchemeCatalogue.FoundKey`, `SurchargeKey`, `RepayKey`), and the investors' rules skip them (`StartupMoney.Shortfall`), so one act has one chain of consequence, the court's, and not also the startup money suspicion ([Startup money](startup-money.md)). A payment to a person or a fund can never take the wallet under the floor, so those are never counted either.

## The trail

Each of the six kinds has a trail with a heat (0 to 2000).

An act adds: its kind's base (claim 120, expense 100, books 80, club 150, payments 60) + 600 x its size + 70 for each earlier act of the kind in the last 90 days (5 counted at most) + 50 for each older one still on file (8 at most) + 40 for each person who knows + 60 for a paper trail (it went through the bank account). In 60 of a thousand acts a routine check spots it whatever its size, which adds 400. A tenth of an act's heat also falls on every other trail. Heat fades by 4 a day. A till adds a tenth of its rate a day (5 or 20).

An inquiry into a kind opens when something of that kind is on file and its heat is at or over a line the player cannot see: drawn from the save's seed between 250 and 649, again for each kind, each period of 90 days and after each inquiry. So:

| A first one, small | heat | looked into, in a hundred saves |
| --- | --- | --- |
| claim | 330 | 25 |
| expense | 310 | 20 |
| sale on books lifted by 10 % | 339 | 27 |
| club (50 households) | 380 | 37 |

These odds are what each page says (`SchemeRules.FirstChance`). A large one is over every line there can be: always looked into. Splitting changes nothing for the better: four small claims in a pattern heat the trail by 1,740 against one large claim's 780, the file adds them up, and ten small frauds are charged as the one large fraud they come to.

A file reaches back 720 days: an act older than that is off the trail. A kind's file keeps 24 acts.

## An inquiry

| Day after it opens | What happens |
| --- | --- |
| 0 | opened; nobody is told |
| 3 | the letter: a text from counsel and a notice; the Legal file lists it from here |
| 8 | the visit: an audit at the business by business pressure's own rules; for a club or cash sales, the cash in hand is watched and cannot move |
| 14 | the findings, if the evidence stands (350 of a thousand, just over the prosecutor's own line) |
| 30 | dropped, if it never stood |

Evidence is gathered at 45 a day up to what the file holds: 350 + for each act on file 90, 200 x its size, 40 for each person who knows, 60 for a paper trail, less 40 for one already paid back; 950 at most. A file as strong as 800 is acted on without waiting for day 14, never sooner than two days after the letter.

**The findings.** Every act on file is found once. What it still owes is taken back, the cash in hand first for a club or cash sales, the rest from the wallet whatever it holds, with a quarter on top; a club's stakes go back into its households' savings (`PurseRules.Give`); what a till washed is taken back for those households too, and the two inquiries together never take a unit twice (`SchemeRules.Recover`). Payments to people have nothing to take back: the quarter is on what was paid. Then the file goes to the courts on a summons (`SchemeCases.File`, `Justice.Summon`) with the evidence gathered and the save's own seed for the court's doubt, and is an ordinary case from there: plea, lawyer, hearing, sentence, record. Standing is told (`FameCity.Exposed`: integrity down, and a felony is a scandal), the City Ledger prints it, and a conviction costs three more points of integrity. A club's takings and a till's washing share one ledger of what the households are still out, so a till's finding never makes a club's act look repaid (it touches no act but its own kind's) and the two together never give back a unit twice.

With no court to wait for (justice switched off, the courts not loaded, or no case opened) the inquiry closes as settled at once (`SchemeRules.Verdict`) and the watch on the cash in hand ends; with a court running it ends as the case ends.

| Kind | Charge | For a felony's worth (5,000 or more on file) |
| --- | --- | --- |
| claim, books, club | false accounting (misdemeanour) | fraud (felony) |
| expense | tax evasion | tax evasion |
| cash sales | false accounting | money laundering (felony) |
| payments to people | bribery | bribery; perverting justice (felony) if an investigator or a judge was among them |

**What the player can do,** from the letter until the findings, in the Legal file:

- Pay the file back (`SchemeRules.Repay`): everything it holds, a club's out of the cash in hand first, whether the cash is watched or not (the investigators take what is handed over, so the cash in hand falls by what is repaid; what the cash does not cover is the wallet's, and is refused whole above the investors' floor). A till's finding may have given some of a club's takings back to its households already: paying the club back then costs only what the households are still out. It then carries half the surcharge (125 of a thousand). A first small file of its kind paid back whole ends with a caution two days after the letter at the soonest: no court. A caution is given once for a kind. A claim the insurer has not paid yet is withdrawn instead of paid back. Cash sales and payments to people cannot be paid back.
- Tell the investigators everything: the file is complete at once (950), the findings follow the next day, and the charge is the lesser one. The court is told the player complied only then (the inquiry's own cooperation), never for any file that has reached the most evidence.
- Through a partner at a law firm, offer the investigator money: see below.

Cleared or cautioned, a trail keeps half its heat; dropped, 70 %; convicted or settled, it is brought to 250 at most: watched from there on.

## Money to people

Somebody the player knows who holds a position can be offered money on a call. Who takes it is a trait of the person, fixed by the save's seed, their office and who they are (`SchemeRules.TemperOf`): asking again or loading again changes nothing, and it is the same whoever asks, the player or a rival.

| Office | takes | says no and reports it | says no and remembers |
| --- | --- | --- | --- |
| somebody the player knows | 550 | 200 | 250 |
| a councillor | 400 | 300 | 300 |
| an investigator | 250 | 400 | 350 |
| a judge | 200 | 500 | 300 |

| Favour | From | Price (start's prices) | Size | What it does |
| --- | --- | --- | --- | --- |
| A gift | anybody with a position | 800 | 150 | their next favour (Networking) without the friendship or the wait |
| A word | a city official | 1,500 | 100 | where each trail stands (calm, talked about, watched), any open inquiry and how full its file is, and which rivals are being looked into |
| A place | a city official | 4,000 | 250 | an invitation to the next city reception (Standing) |
| The vote | a city official | 6,000, or twice the lobbyist's fee if more | 400 | a councillor carries the push the player has before the council; not in a Going Public session |
| The investigator | a law firm's partner | 5,000, or 600 of a thousand of what the file brought if more | 500 | 400 comes off what the file holds: one act's worth, never a pattern's |
| The judge | a law firm's partner | 25,000 | 700 | the evidence of the case that comes up next is weighed 0.20 lighter |

Paid out of the cash in hand when it covers the price and is not watched (no paper), else from the account (a paper trail). Their own temper is asked first; a vote, an investigator and a judge then have a temper of their own. A favour that cannot be had is found out before any money moves.

A refusal has weight. Whoever says no is never asked again, and the friendship cools. One who reports it adds 300 heat, and the offer is on file as a payment would be, with no money moved, so an inquiry can find it; an investigator's report also adds 150 to the evidence of the inquiry they hold; a judge's report goes to the court at once as a charge of perverting justice.

Somebody paid holds what they know for 180 days and may talk: on 2 days in a thousand, on 20 while an inquiry into payments is open. Talking adds 250 heat and 200 to that inquiry's evidence.

One door for another feature: `SchemeDirector.Hosted(person)`. Somebody hosted who takes gifts has their next favour bought by it, with no money and no trail; anybody else had an evening out. Party hosting itself is not built here.

## The rivals

Each rival tycoon has a trail kept by the same rules: the player's heat formula, the same line, the same pace of evidence, the same days to the letter and the findings, the same quarter on top, and the court's own sum (`JusticeLaw.CaseAgainst`) with counsel of 600 of a thousand.

- 300 of a thousand rivals never scheme. The others are looked at once in 60 days and then scheme with a chance of their own between 150 and 449 of a thousand: a claim, an expense, dressed books, a club or a payment to a councillor (who has the temper anybody would find), small or, in 300 of a thousand cases, large.
- A scheme brings them 5 or 40 ten-thousandths of their worth, 250,000 at most. Found out, everything on their file is taken back with a quarter on top; they are tried three days later, and a conviction costs them the court's fine, 60 days barred from lobbying and buying up buildings at half their growth, and 30 days without a move.
- The City Ledger prints each step the morning after: looked into, found out, convicted or cleared.
- In a Going Public session the rivals rest: nothing of theirs moves, their heat does not fade, and nothing is forgotten. They rest because their worth, their businesses and the courts that try them are the host's: moving them on one machine would move what another player sees.
- The rivals run five kinds: a claim, an expense, dressed books, a club and a payment to a councillor. They do not run a till (no washing), do not pay a file back, do not tell the investigators everything, are never cautioned and never buy an investigator or a judge: those are choices of the player's, and a rival's inquiry is played to the end by the rules.
- A rival is never charged more or less than the player would be: the trail, the line, the pace of evidence and the court's sum are the player's own.

**A rival's fund.** Every 60 days at most (not in a save's first 10), a rival who is free and can pay its return offers the player a place in a fund: a fiftieth of the player's net worth, between 2,000 and 50,000 at the start's prices, back with 6 % on top after 30 days. The offer stands 7 days. Whether it is a sham is fixed when it is made: as likely as a scheme of that rival's on a look. The player can have it looked at first for 6 % of the sum (rounded up to tens), 250 at least: counsel says what it is. An honest fund pays from the rival's worth: the sum the player put in is the rival's from that day, the return comes out of their worth on the day (`SchemeRules.FundPaidOut`, never more than they are worth then, so what reaches the wallet is exactly what leaves their worth), a rival worth less than the sum with its return offers no fund, and the press to put money in is refused before any money moves if they cannot pay it by then. A sham keeps the money, is a scheme on that rival's trail, and can be reported to the police once (300 heat on their trail); when that rival is found out, 60 % of it comes back.

The return is never more than the fee of a look (6 % against 6 %, the fee rounded up), so having every fund looked at first and putting money only into the honest ones nets nothing or less once the fees are paid: -5,107 a save over 1,800 days and 30 offers (the 6 % of the honest ones, less the fees of all), against -13,566 for putting money into every one and -12,350 for putting it into every one and reporting the shams.

## Exploits closed

| Exploit | How it is closed | Proved by (`--schemes`) |
| --- | --- | --- |
| Reloading for another outcome | every draw is a hash of the save's own seed and what it is about; the court's doubt for a scheme's case is the save's too. The save's seed is the city's seed and the playthrough's own id (the character's unique id, `SchemeRules.PlaythroughOf`), so a second save on the same city meets other tempers and other lines and cannot learn the answers of the first | `schemes trail`, `schemes people`, `schemes seed`, `schemes save and load` |
| A money machine | repeating heats the trail, and findings take back everything on file with a quarter on top | `schemes sim`: 63 plans over many saves |
| Dressed books, the lift taken off before accepting | the lift is stamped on the listing and the offer when the price is made; the look and the act use the stamp; the quick sale's buyer looks too | `schemes books` |
| "Complied" for any file at the most evidence | the court is told the inquiry's own cooperation, not how much evidence there is | `schemes coop` |
| Repaying a club file while the cash is watched, keeping the cash | the cash in hand falls by what is repaid, watched or not | `schemes watched`, `schemes money` |
| Justice switched off strands an inquiry | with no court running (or no case opened) a charged inquiry closes as settled and the watch on the cash ends | `schemes no court` |
| One act, two chains of consequence | an inquiry's forced takings are not misuse of a firm's money | `schemes investors` |
| A gap skips days | every elapsed day is played, one by one | `schemes catch-up`, `schemes trail` |
| An honest rival fund as a money machine | the return comes out of the rival's worth; a rival who cannot pay offers none and takes none; the fee of a look is never under the return | `schemes fund`, `schemes rivals` |
| An invented claim raised by a real break-in's police report | an invented claim is marked, and is never the newest theft | `schemes claim` |
| A till's finding makes a club look repaid | each finding touches only its own kind's acts; the households' ledger is the only thing both share | `schemes wash`, `schemes money` |
| Laundering that creates money | the cut and the tax are lost on every unit, rounded against the player; washed money is booked to the player, never into a business's figures | `schemes till`, `schemes sim` (every save sound) |
| A fraud whose victim is nobody | the insurer pays by its own rules and ends the cover; households' savings fall and rise to the unit; a rival's worth moves with a fund | `schemes money`, `schemes fund` |
| Taken twice | cash sales and the club share one ledger of what the households are still out | `schemes money` |
| Splitting | the pattern adds heat, the file adds the sums | `schemes trail` |
| Paying back as a free roll | half the surcharge is still taken, and a caution is given once for a kind | `schemes repay`, `schemes sim` (the plans that pay back) |
| Investors' money | no payment may leave the wallet under the investors' floor, and what an inquiry takes by force is not also counted as misuse of it | `schemes refusals`, `schemes investors` |
| Going Public | a club, a council vote and a fund are refused before any money moves; the rivals rest, their heat included | `schemes rivals`, `schemes fund`, `schemes refusals` |
| The option off | no new scheme starts and dressed books heat nothing; open inquiries, placed money and trails run their course; the line is still written | `schemes inquiry`, `schemes books`, the sources |
| Save and load mid-inquiry | every field is in the save's line | `schemes save and load`: a load on any of 419 days changes nothing |

## What the simulation shows

`SchemeChecks.Sim.cs` plays each plan through the shipped rules for 1,800 or 3,600 days in 60 to 400 saves. Only money the schemes moved is counted, and no court's fine, lawyer's fee, day in prison or scandal: every figure is the player's best case. Means, at the start's prices, for a business taking 3,000 a day:

| Plan (small unless said) | claim | expense | books | club |
| --- | --- | --- | --- | --- |
| once: looked into, in a hundred saves | 20 | 17 | 22 | 33 |
| once: mean result | +2,614 | +2,381 | +2,101 | +6,030 |
| once, large: mean result (always looked into) | -3,500 | -3,000 | -1,498 | -18,218 |
| every 721 days, 3,600 days | +13,798 | +10,950 | +10,053 | +27,071 |
| every 360 days, 3,600 days | +8,939 | +9,937 | +8,361 | -13,449 |
| every 180 days, 1,800 days | -5,512 | -1,537 | -1,224 | -54,560 |
| every 180 days, paying back at the letter | -471 | +1,616 | +1,908 | -34,789 |
| every 91 days, 1,800 days | -7,817 | -10,250 | -10,350 | -125,542 |
| as often as allowed | -3,801 | -85,500 | -40,440 | -272,495 |
| as often as allowed, large | -12,500 | -342,000 | -72,225 | -1,168,983 |

So one small scheme can pay, a small one every few hundred days keeps part of what it brings (never more, on average, than one small act's worth for each stretch a file reaches back), and anything more often or larger is found out in every save and loses, more the longer it goes on. Insurance claims end themselves: no insurer writes the cover after it was cheated. Buying the investigator works in about one save in seven, and a plan with it still loses on average.

The rivals, 1,800 days in 150 saves: 311 who scheme ran 2,887 schemes, were found out 1,835 times and convicted 1,003 times, for a mean result of -187,287 each. Their fund: putting money into every one loses 13,566 a save on average, having each looked at first loses 5,107 (the 6 % of the honest ones, less the fees of every look), and reporting the shams does not make up for the money put into them (-12,350).

## Rules kept

- No Update: one timer a second on the real clock comparing the day; a game day's turn reads the player's businesses once and plays the rules (under 0.2 ms for the busiest save there can be); everything else runs on a press.
- The save's own line `reallife:schemes`; a save loads without the mod; an old save starts with nothing in it; a later version's line is read as far as this one knows it; a line it cannot read is kept as it was.
- Nothing is sent over the network; every draw is a hash of the save's seed, so two machines holding the same save agree. A stretch nobody played is caught up day by day: every elapsed day is played, exactly as it would have been (`SchemeRules.Day`), so an inquiry that would have charged is charged and never dropped for a gap; a day is a few loops over the state, ten game years of a busy file take milliseconds.
- **In a Going Public session** the acts that stand down are those that touch the city's people or the courts, which are the host's: a club (the households' savings), a council vote, a rival's fund, and everything of the rivals'. Each is refused before any money moves (`SchemeRules.ClubRefusal`, `FundPutRefusal`, `FundLookRefusal`, `NobodyToAsk`). The rest touch only the player's own state, and so do not need to stand down: an invented claim is the insurer of the player's own firm, an invented expense is the player's own tax return, dressed books are the player's own sale, a till washes the player's own cash and a payment goes to a person the player knows. A buyer's look at the books and its walk are the player's own market.
- Option: "Schemes and inquiries" (`living_schemes`, on) under Living population. Off: no button, no new scheme, no rival's scheme, no fund; what is open still runs its course.

## Test buttons

- "Schemes: an inquiry into a test entry opens now": a small entry that moved no money goes on file and its inquiry opens with its letter.
- "Schemes: a day of every open inquiry now": the visit, the findings and the court come in their order (14 presses to a test entry's findings).
- "Schemes: a rival offers a fund now".
- "Schemes: a rival is found out now": tried three days on; the paper prints it in the morning.
- "Schemes: every trail's standing in the log": each trail's heat, the line it stands against today, what is on file and the cash in hand.

Each writes a `Schemes check (...)` line ending PASS or FAIL. Offline: EconomyCheck's `SchemeChecks*.cs` (`--schemes`, and `--schemes table` for the simulation's rows).

## Left out, and why

- **Hidden sales to cut tax** are the company's cash off the books already (`Economy/Company/TaxRules.cs`), with its own audit; a second version would charge one act twice.
- **Bribing an inspector or a licence clerk** exists in business pressure, Standing and construction (`Business/BusinessIssues.Options.cs`, `Fame/FameCity.cs`, `Construction/SiteRules.cs`) with their own exposure; they are left to those.
- **Cooked books for a loan or a listing**: only a sale reads the lifted figure. The bank's and the market's readers were not given it.
- **A police officer**: the mod has no police officer who persists to hold a temper (`Networking/NetworkFile.cs`: no such position).
- **A councillor as a person**: the council is seats with a party (`Civic/CivicState.cs`), so a councillor's id is the seat, its party and the term.
- **The tax authority's loss**: the tax authority is the victim of an invented expense, and it is not a party the mod keeps a budget for (the council's revenue is raised from the electorate, `Civic/OfficeRules.cs`), so its loss is felt as the tax the inquiry takes back with a quarter on top, and not by a budget.
- **A cooked sale the player buys into**: the player as victim is the rival's fund.
- **Restitution to the exact households**: stakes go back to the district's households in even parts, not to those who paid.

## Not verified in the game

The pages on both interfaces, the quiet buttons' place on each company page, the call's menu entry and dialogue, the notices' wording in play, the Legal file's lines and actions, the audit an inquiry's visit starts, the court's hearing for the four new charges, and the paper's pieces were built and checked offline only.

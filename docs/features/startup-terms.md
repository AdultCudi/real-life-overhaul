# Startup terms

Phase 3 of the startups rework. It builds on [Startup money](startup-money.md): every round a venture firm leads now comes with terms, and the player's skills do real work in the funding and board parts.

## Where the code is

- Rules, game-free: [`VentureTerms.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Venture/VentureTerms.cs) (`Terms`, `CapRights`, `TermsCatalogue`, `VentureTerms`), the save line in [`TermsStore.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Venture/TermsStore.cs), `VentureRules.Waterfall` in `VentureRules.cs`.
- The game's side: `Scripts/Tech/TechDirector.Terms.cs` (the offer's terms, the push, the board's votes, the provision, the skills) and `TechDirector.TermsTests.cs`; the words in `TermsView.cs`; the pages in `TechLive.Terms.cs` (the kit) and `TechApp.Terms.cs` (the older pages); the push in `VentureMeeting.cs`.
- Option: none of its own. It runs with Startups.

## The terms

Set by the firm (its diligence above all), the stage, the market's mood and how the firm is doing (`VentureTerms.Strictness`, `Draw`, a seeded roll). A stricter firm asks for harder terms.

| Term | Values | What it does |
|---|---|---|
| Liquidation preference | 1x, 1.5x, 2x | The investor's line (`Holder.Preference`) comes back first on a sale, the latest round first. |
| Participation | no, yes | A participating line takes its preference and then shares as common as well (`Waterfall`). |
| Anti-dilution | none, broad weighted average, full ratchet | When a later round prices a share lower, the line is given more shares (`AntiDilute`), for the player's firms and the city's startups alike. |
| Founder vesting | keeps, starts again | The round moves the account's `FounderDay` to the round's day, so a founder removed soon after forfeits more. |
| Information rights | monthly pack, quarterly pack, monthly pack and a look on demand | The pack's interval; the lead's threshold (`StartupMoney.ThresholdOf`, 5 lower with a look on demand, 4 higher with a quarterly pack, always 31 to 58); how much a look finds (`LookFindsOf`); a lead with a look on demand may ask on any day. |
| Board approval (protective provision) | a sum of the stage's burn, or none | A spend at the firm's address over the sum with no approval is a breach. A related party deal over it is refused before it is booked. |

`Waterfall` keeps its signature. Participation and the multiple are read from the cap table's lines, so every exit, the wind down and the city startups' sales pay by the real terms.

## The provision

The game books a fit out itself, so the mod cannot stop it. The player asks the board first (the firm account's "Ask the board for a big spend", a vote: 600 thousandths plus 3 for each point of Leadership, less 3 tenths of the lead's diligence and 150 for each missed target, never when money is counted as taken). Agreed, it covers spends over the sum up to four times the sum for 14 days. Refused, the board is not asked again for 14 days. A spend over the sum with no cover is a breach (twice as bad after a refusal): it adds to suspicion for 120 days and moves no money. The money hook costs one compare per money change while no spend is as large as the smallest provision of a funded firm; the transaction is read only then.

A salary over the band is asked of the board in the same way, once a round (450 thousandths base). Agreed, the band is half as high again until the next round; the salary is still drawn through `StartupMoney.Draw`, so the firm account counts every cent.

## Pushing back

In the pitch meeting's last page each hard term of an offer has a button; one push on one term, once for the offer. The chance (`PushChance`) is 250 thousandths plus Law and Selling times 2 and Relationships, plus a fifth of the meeting's score over 500, less an eighth of the firm's diligence, plus an eighth of the market's mood over 1000. Won: the term gives way a step. Lost: the offer stands 3 days at most. Lost by 300 thousandths or more: 4 percent off its value and 2 days. The roll is seeded by the firm, the day and the term.

## The player's skills

- **Law** reads the terms from 40: what each costs the player in a sale at the round's value and at three times it (`VentureTerms.Cost`, worked by `Waterfall` on the cap table with the new investor in it), what a ratchet or broad average does to the player's share in a later round priced 40 percent lower, and which terms are hard. Under 40 the terms are listed, not what they cost.
- **Selling and Relationships** warm the pitch (up to 60 points of its score) and help a push.
- **Leadership** carries the votes on a big spend and on a salary over the band.
- **Accounting** cuts the honest slips of a board pack (`SlipChance`: 26 percent of packs at 0, 2 percent at 100). A slip adds a little to suspicion for 60 days; the books stay true.
- **Analysis or Statistics** from 35 show an offer against the market: the firm's value now against what the offer says, and for a city startup the going price on its true revenue. With Accounting, or Analysis and Statistics together, the player also sees a deck whose revenue is stretched, before a diligence report.
- Skills grow by doing, through Careers' `StudyRules.Learn` behind `TechBridges.Learn`: each pitch, each pushed term, each board vote and answer, each pack, each term sheet taken, each founders' meeting.

## Older saves

A line without terms is 1x, not participating, no anti-dilution, the vesting it had, a monthly pack and no provision. Nothing changes until the next round. With Startups off nothing here runs.

## The save line

`reallife:tech_terms`, version 1, parts c (holders of the player's firms by place and index), s (holders of the city's startups), t (offers on the table, with the push) and r (the board's say: breaches, approvals, the salary vote, the last slip). `reallife:tech_ventures` is untouched. An all-plain book writes no line.

## Not built

The player investing in a city startup gets no term sheet of their own to negotiate: their ticket is the founders' ask, on the startup's price; the lead's terms (set by its firm) are shown and paid by.

## Checks

`EconomyCheck --startup-terms`: the waterfall by hand (1x, 2x participating, two rounds), anti-dilution of each kind, the setting by diligence, mood and stage, the information rights and the provision with its breach, the Law page's numbers against the waterfall, the skills, older saves, the save line, and the exploit with the hardest terms.

# The game studio

## In a sentence

Under the living economy's master option **Living population** and its own option **Game studio**, a Game Studio of the player's makes games from a concept pitched at the owner's desk to a life after launch: genres, settings, platforms, budgets on the tech money scale, milestones and playtests at the desk, hype and marketing, the critics' score, sales to a real audience with no cap but the audience itself, piracy and DRM, patches, updates, add-ons, ports and sequels, bonuses for the team, and rival studios that release by the same rules.

## Where the code is

- Folder: [`Scripts/Tech/Studio`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Studio/); the director's parts in `Scripts/Tech/TechDirector.Studio.cs`, `TechDirector.StudioDesk.cs` and `TechDirector.StudioTests.cs`; the four desk sessions in `Scripts/Wiring/Wiring.StudioLinks.cs`.
- Options: "Game studio" (`living_studio`), under "Living population" (`living_population`). Off, game studios run games by the firms' own rules as before; the games the studio made are left in the firms' list as those rules carry on with, and taken back when it is on again.

```
Tech/Studio/                    the first nine are game-free and compiled by EconomyCheck (StudioChecks.cs)
  StudioCatalogue                 genres, settings, platforms, models, phases, the figures of every rule
  StudioFile                      the file, a game (GameDev), a concept's draft, a rival game, genre experience, an engine
  StudioRules                     the plan from a budget, the crew's craft, a day of building, phases, bugs, the sessions' choices, hype, the review
  StudioRules.Sales               one market a genre, the audience, a game's life, a day's sales by model, piracy, after launch, bonuses, awards, old saves
  StudioRules.Rivals              the rival studios' seeded releases, trends, their sales
  StudioStore                     the save's line, "reallife:tech_studio"
  StudioShare                     the rival studios as a part of the shared city (Going Public), letter V
  StudioBridges                   the audience (the population, or the documented world) and Flare's bridges
  StudioViews                     every screen as data: title, chip, what to do next, sections, choices with their trade-offs, one primary action
  StudioGame                      the studio's money keys, capital at the studio, the way to the desk
  StudioGuide                     the feature's entry and the Guide page's figures
  TechApp.Studio                  the screens drawn in the Tech app until the mod's design system draws the same data
Tech/TechDirector.Studio          the studio's day inside the firms' day, the market's day, conversion of older saves' games, news, money
Tech/TechDirector.StudioDesk      the sessions' callbacks, the pages' choices and buttons, the screens
Tech/TechDirector.StudioTests     the four tests
Wiring/Wiring.StudioLinks         the concept pitch, the milestone review, the playtest and the post-launch review as desk sessions
```

## A game's life

1. **The concept** (the Tech app, a game studio's page, Start a product): genre (twelve, each with its players on each platform, the scope its games usually have and the parts of quality its players care about), setting (four a genre; how well each fits is learned by shipping a game in it, never shown as a table), platform (PC, console, phone), budget (fifteen steps from 50 thousand to 150 million; `ScaleRules.ScopeOf` names the scope), price against the usual price, business model (sold once, free to play, subscription, early access), engine (licensed, or the studio's own once built), and a sequel of one of its games.
2. **The pitch** at the desk (`studio_pitch`): ambition (lean, as planned, ambitious), funding (self-publish, or a publisher's advance of half the budget against a royalty of a third of the net), DRM. With desk sessions off, the concept page greenlights it as planned.
3. **Production**: the budget's team share buys work points at a mid-level's day (`StudioRules.WorkFor`); the real team takes the time its points take. Each day the studio's programmers, designers, product people and testers put their points into five parts of quality (design, tech, art, content, polish) with their craft: skill, level and days in the genre (`StudioRules.Craft`, the experience kept per professional). The genre weighs the parts. Bugs hide in the work by the crew's faults.
4. **Milestones** (`studio_milestone`) at the end of pre-production and production: cut a feature, hold the course, delay; crunch (burnout through the people part's own rules), a normal pace, or contractors. Unreviewed for ten days, the team holds the course.
5. **Playtests** (`studio_playtest`): the team, a closed beta or an open beta find a share of the hidden bugs; fix everything, the worst, or ship them.
6. **Hype**: announce, trailer, expo demo (early summer), press previews, a Flare devlog (once Flare's bridge is wired), a campaign as a share of the budget through `ScaleRules.Reach`. Hype fades after three quiet weeks; hype far above what the quality carries costs points with the critics.
7. **The launch**: on its day, or now; a console game after its certification. The critics' score against the genre's bar, which rises each of the save's years (the one clock, as the studio's people's careers count them): at the bar 50, under it a point and a fifth off for each point short, over it the room left shared out so a perfect game reviews 100 (`StudioRules.CriticsScore`; a stretch over the bar as well had a game of 90 review 94 at the start, where the money scale's sales climb steepest); the City Ledger prints it, the player's listed company and Pixel Harbour Games move with releases, Standing moves with a big score.
8. **Sales**: `ScaleRules.Units` over the audience of the genre on its platforms (the population's when wired, else the documented world, each platform's players growing by the save's years towards a ceiling of some 1.3 billion on PCs, 900 million on consoles and 3.9 billion on phones), the share its budget is seen by, its reach, its score, its setting's fit with its pull among the genre's live games (one market a genre with the rivals), its price; the launch window, then the life still to come halving over the score's half-life. Piracy by platform and hype, cut by DRM. Stores, refunds and a licensed engine's royalty through the money scale's figures; servers for online games.
9. **After launch** (`studio_postlaunch`): hotfix or patch reported bugs, a free update or a paid add-on on the fans' first request, a sale, DRM off. Ports and localisation from the game's page. A sequel brings the first game's fans; a weak one burns the name. The year's award at the calendar's year end.
10. **The team**: a bonus pool per game paid from its profit on its 90th day, a monthly royalty for the leads; a hit that paid nothing makes rivals pull three times as hard at its studio's people for half a year (`TalentRules.Approached`'s pull).

## Rival studios

Pixel Harbour Games (listed on the stock market) and six studios of the studio's own release games by the same rules into the genres they like or the hot one, at their scope's pace, seeded from the save. A hit makes its genre hot for half a year. Their games crowd the release page and take a share of each genre's market.

## Older saves

On the first load every game of the firms' list is taken into the model from where it stands: a genre from its name or rolled from its id, PC, sold by the copy at its old price, its old size of work (half built stays half built), its bugs and hype; a game on sale keeps its copies, takings and day and sells on from its old day's copies on its old half-life, so nothing jumps.

## Going Public

Only the player's own studios and professionals are touched. Every roll is seeded. The rival studios are the host's: a versioned part of the shared city (`StudioShare`, letter V) carries them to the guests, who apply it and never step their own; a message of another version of the mod is never applied. Each player's games stay on their own machine.

## Cost

Nothing per frame. The studio runs inside the firms' day: one pass over a studio's people, a day for each of its games, the market's day once (EconomyCheck: the most games the firms hold among 24 rival games under 0.5 ms, the rivals' day under 0.2 ms, the save's line in a few milliseconds). A page builds its screen when drawn.

## Tests

- **Start a game at once**: the first studio's concept pitched as planned.
- **Finish a game's production now**: its parts built to plan with its team's craft so far, ready for tomorrow.
- **Launch a game now**: finished first if needed, then launched.
- **A year of game sales now**: every game on sale lives its next year, the takings booked today.

With Developer diagnostics on: `Tech check (studio pitch): ...`, `Tech check (studio launch): ...` and `Tech check (studio rival): ...`, each with PASS.

## Checks

EconomyCheck (`--studio`, StudioChecks.cs and StudioChecks.More.cs): the plan, building, the sessions, hype and reviews, the one clock (the platforms and the critics' bar alike at 60 days a year and at 365), the bands (a game of 30 a flop, the team's usual a little past its budget, 85 with hype a hit, and the top kept in the money scale's swing: a game of 80 at most half again the scale's return for its quality and one of 90 at most twice, one of 90 on one platform a hit and no more, a perfect game still a masterpiece with no cap but its audience), no cap, ten years of each scope at years of 365 and 60 days inside the studio's 120 to 360 days (an ordinary team keeping its standing with the critics as the bar rises), the models, after launch, bonuses, rivals, older saves, the save and the shared city, the screens, the texts and the costs. Without word of mouth widening a great game's reach on top of the scale's tail (which already holds its long life) and with the critics' score no longer running ahead of quality, a game of 80 returns 1.07 times the scale's 3,760 thousandths of its budget and one of 90 1.37 times its 7,005, where they returned 1.75 and 2.7 times.

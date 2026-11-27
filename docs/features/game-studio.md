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
  StudioViews.Page, FirmPage      the studio's whole page as data for the mod's design system: figures, games, the market, the team, sales and support
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
7. **The launch**: on its day, or now; a console game after its certification. The critics' score against the genre's bar, which rises each of the save's years (the one clock, as the studio's people's careers count them): at the bar 50, under it a point and a fifth off for each point short, over it the room left shared out so a perfect game reviews 100 (`StudioRules.CriticsScore`, with nothing stretching it further, as the money scale's sales climb steepest at the top); the City Ledger prints it, the player's listed company and Pixel Harbour Games move with releases, Standing moves with a big score.
8. **Sales**: `ScaleRules.Units` over the audience of the genre on its platforms (the population's when wired, else the documented world, each platform's players growing by the save's years towards a ceiling of some 1.3 billion on PCs, 900 million on consoles and 3.9 billion on phones), the share its budget is seen by, its reach, its score, its setting's fit with its pull among the genre's live games (one market a genre with the rivals), its price; the launch window, then the life still to come halving over the score's half-life. Piracy by platform and hype, cut by DRM. Stores, refunds and a licensed engine's royalty through the money scale's figures; servers for online games.
9. **After launch** (`studio_postlaunch`): hotfix or patch reported bugs, a free update or a paid add-on on the fans' first request, a sale, DRM off. Ports and localisation from the game's page. A sequel brings the first game's fans; a weak one burns the name. The year's award at the calendar's year end.
9a. **A subscription's month.** A game on a subscription is paid by the day (a thirtieth of a month's price for each paying subscriber, `StudioRules.SellDay`), also when all its copies are sold, so it keeps paying while subscribers remain; subscribers come at a share of each day's copies, taken from the running total so that days of a few copies are not rounded to none. Its page and the products' lists say what it takes a month (`GameDev.Daily` over the billing month, `TechDirector.MonthOf`): the studio's net of its last day on sale. A firm's recurring revenue for its price (`FirmsRules.Arr`) still leaves games out. Check: EconomyCheck `--tech28`.
10. **The team**: a bonus pool per game paid from its profit on its 90th day, a monthly royalty for the leads; a hit that paid nothing makes rivals pull three times as hard at its studio's people for half a year (`TalentRules.Approached`'s pull).

## Game development pace

The option **Game development pace** (`tech_game_pace`, under Game studio in the options tree; Normal, **Brisk** by default, Fast) sets how fast a studio's teams build a game (a player: "the game development duration feels a bit too long"). The rules are `Scripts/Tech/Studio/StudioPace.cs`, game-free.

- **What changes.** Only the points a team puts in each day: they are multiplied by the pace's speed (1.0, 1.5, 2.0), so Brisk builds a game in two thirds of Normal's days and Fast in half. A game's work (`StudioRules.WorkFor`), its parts' plan, its progress and the bugs a point of work hides are untouched. A game already half built stays half built when the option changes and moves at the new pace from the next day. Normal is today's figures to the last point (`StudioRules.Build` with the default pace).
- **Cost.** The same work in fewer days would cost the team's pay for fewer days, so each building day of a faster pace carries the difference as tools, contractors and overtime (`StudioPace.Overhead`: half a day's pay more at Brisk, a whole one at Fast), booked as `reallife_transaction_tech_game_pace` among the business's costs and added to the game's `Spent`. A game costs what it did at any pace.
- **Crunch and contractors.** Their days shorten by the same share (14 to 9 and 7, 30 to 20 and 15) and a crunch's burnout a day rises to keep its whole, so each keeps its share of the game.
- **Not changed.** Updates, add-ons and ports after launch, the rival studios and the sales clock. Nothing in the game's development scales with the length of the year: a game's work is points, the pay and the points are by the game day, so a longer year only reads the same days as fewer years. The production card now says the pace and the years left at the save's own year length (`reallife_studio_build_pace`).
- **Days a game takes** (the money scale's ordinary team, `ScaleCatalogue.DevDays`): small 90, mid 180, large 300 days at Normal; 60, 120, 200 at Brisk; 45, 90, 150 at Fast. In years of 60 days that is 1.5, 3 and 5 years at Normal, and of 80 days 1.1, 2.3 and 3.75. `--techplan29 print` prints the table.

## Names

Every game has a name (the firms' list keeps it, `Product.Name`; the studio's own record holds none). The concept's page shows what the game will be called and how the name came: typed in the game's own text box (`StudioAct.Name`, asked by the page that shows it, as the cloud's name is), the name of the game it follows with the next number for a sequel ("Ember Crown 2", `TitleRules.Sequel`), or a suggestion of its genre's words (`TitleRules.Suggest`, from the save's seed and the id the game will get; "Suggest a name" offers the next). The pitch gives the game the name its page showed. A game is renamed from its page at any time until it is withdrawn, at no cost: nothing reads a name but the pages, notices and news that show it, so it cannot move the critics, the hype or the sales, and what was already printed keeps the old name. A free update and an add-on are named from the game by rule (`StudioViews.ProjectTitle`: "Ember Crown 1.2", "Ember Crown: Aftermath"), on the game's page while they are built and in the notice when they are out. The rules are in [`Scripts/Tech/Titles`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Titles/) (`TitleCatalogue`, `TitleRules`, game-free) and the director's part in `Scripts/Tech/TechDirector.Titles.cs`; a typed name is trimmed, at most 40 characters, with no tags, braces, line breaks, control characters or the save's own separators, in any language's letters, and unique among the player's products and the rivals' as they stand.

## Which roles feed a game (`StudioFeeds`)

A player could not tell which roles polish or design a game: a studio's card read "design 0, tech 78, art 0, content 90, polish 0". The card was right (part quality is what was built: no designers, no product people and not yet the polish phase). What was missing was the saying.

- `StudioFeeds.Of` sorts a role into engineers (the engineering, data, security and infrastructure families, testers and technicians aside), designers, product people, testers, support or none. The studio's crew (`StudioRules.CrewOf`) and its hands (`TechDirector.StudioHands`) use that one sort, and `StudioFeeds.Share` reads the catalogue's splits: engineers 40% tech and 60% content, designers 30% design, 50% art and 20% content, product people 100% design, testers polish in the polish phase (where half of everyone else's work goes to polish too, `PolishPhaseShare`).
- **The card** (`StudioViews.Makers`): a line on who builds what, a line on which roles are which, and a warning for each lack the rules would leave at nothing (`StudioFeeds.Gaps`): no engineers, no designers or product people, no designers alone, no testers (polish still gets half of everyone's work in its phase, so the line says half), and a plain line that polish waits for its phase.
- **The roles:** each person's file and the new ad and search pages of both interfaces say what the role does for a game, the parts and shares read from the splits.
- `EconomyCheck --techstaff29` builds a day with a crew of each kind and holds the parts it fills, the card's words and the lines to the rules.

## Rival studios

Pixel Harbour Games (listed on the stock market) and six studios of the studio's own release games by the same rules into the genres they like or the hot one, at their scope's pace, seeded from the save, each game under a name of its genre's words that no rival game on the list has (`TitleRules.RivalGame`; saved in the rival's record, as before). A hit makes its genre hot for half a year. Their games crowd the release page and take a share of each genre's market.

## Older saves

On the first load every game of the firms' list is taken into the model from where it stands: a genre from its name or rolled from its id, PC, sold by the copy at its old price, its old size of work (half built stays half built), its bugs and hype; a game on sale keeps its copies, takings and day and sells on from its old day's copies on its old half-life, so nothing jumps.

## Going Public

Only the player's own studios and professionals are touched: a name is typed for, and a rename made in, the player's own file alone, and nothing of it is sent. An older save's rival games that shared a name are named apart once by whoever owns the list, never by a guest. Every roll is seeded. The rival studios are the host's: a versioned part of the shared city (`StudioShare`, letter V) carries them to the guests, who apply it and never step their own; a message of another version of the mod is never applied. Each player's games stay on their own machine.

## Cost

Nothing per frame. The studio runs inside the firms' day: one pass over a studio's people, a day for each of its games, the market's day once (EconomyCheck: the most games the firms hold among 24 rival games under 0.5 ms, the rivals' day under 0.2 ms, the save's line in a few milliseconds). A page builds its screen when drawn.

## Tests

- **Start a game at once**: the first studio's concept pitched as planned.
- **Finish a game's production now**: its parts built to plan with its team's craft so far, ready for tomorrow.
- **Launch a game now**: finished first if needed, then launched.
- **A year of game sales now**: every game on sale lives its next year, the takings booked today.

With Developer diagnostics on: `Tech check (studio pitch): ...`, `Tech check (studio launch): ...` and `Tech check (studio rival): ...`, each with PASS.

## Checks

EconomyCheck (`--titles`, TitleChecks.cs): every pairing of every style's words (none empty, over the length or a famous real name, no two alike), ten thousand rival products and ten thousand suggestions without a clash, the rival studios' games over thirty and forty years, the typed names' rules and the save reading them back as typed, older saves named once and kept, sequels, add-ons and updates, that a name moves no score and a rename costs nothing, the screens, the sources of every place that shows a game or a product, and the texts.

EconomyCheck (`--studio`, StudioChecks.cs and StudioChecks.More.cs): the plan, building, the sessions, hype and reviews, the one clock (the platforms and the critics' bar alike at 60 days a year and at 365), the bands (a game of 30 a flop, the team's usual a little past its budget, 85 with hype a hit, and the top kept in the money scale's swing: a game of 80 at most half again the scale's return for its quality and one of 90 at most twice, one of 90 on one platform a hit and no more, a perfect game still a masterpiece with no cap but its audience), no cap, ten years of each scope at years of 365 and 60 days inside the studio's 120 to 360 days (an ordinary team keeping its standing with the critics as the bar rises), the models, after launch, bonuses, rivals, older saves, the save and the shared city, the screens, the texts and the costs. With a great game's word of mouth held in the scale's tail (no wider reach on top) and the critics' score kept to its quality, a game of 80 returns 1.07 times the scale's 3,760 thousandths of its budget and one of 90 1.37 times its 7,005.

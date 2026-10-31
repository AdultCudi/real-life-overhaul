# Flare in the city

## In a sentence

Under the living economy's master option **Living population** and its own option **Flare in the city**, Flare's followers are the population's residents: every adult follows a few of the city's accounts from who they are, a post is seen resident by resident, and what residents hear of a business draws them to it from its rivals in the same pools (never made from nothing), measured the next day in visits and takings; every business has a page with its followers, a rating and reviews by residents who really shopped there, ads reach the residents they target, and the city's people on the feed are residents too.

## Where the code is

- Folder: [`Scripts/Social/Flare/City`](../../Assets/Mods/RealLifeOverhaul/Scripts/Social/Flare/City/); the director's part `Scripts/Social/Flare/FlareDirector.Residents.cs` and its tests `FlareDirector.Tests.City.cs`; the app's lines and the ad sheet `FlareApp.City.cs`; the wiring `Scripts/Wiring/Wiring.FlareCityLinks.cs`.
- Options: "Flare in the city" (`living_flare`), under "Living population" (`living_population`) and Flare (`social_media`). Off, or while the population does not run on this machine (another city, a Going Public guest), nothing of it runs: Flare's talk about a business is in business pressure's figure as before (`FlareDirector.FootfallOf`, at most 6%).
- The design: `not-in-repo/plans/tech-overhaul/DRAFT2.md` section 4, and the brief `handover/briefs/living/flare.md`.

```
Social/Flare/City/                  the first eleven are game-free and compiled by EconomyCheck (FlareResidentChecks.cs, FlareResidentChecks.More.cs)
  FlareCityCatalogue                  every figure, with its unit and why
  FlareCityState                      the accounts, each resident's follows by person slot (with the person's id), each business's figures by slot (with its key), the funnel, the ads, the voices
  FlareCityRules                      the rolls, what a household cares for, a resident's first follows, the day's sweep and indexes, the player's city followers kept with the account
  FlareCityRules.Hear                 a post heard: followers who see it, shares to the people round a resident, discovery, impressions, follows won
  FlareCityRules.Attention            the attention from what residents heard, the lift measured from the pools (exact, with the habit the day came in with), the funnel's share, the fading
  FlareCityRules.Reviews              a customer drawn from the pools as the money came, the stars, reviews and follows from visits
  FlareCityRules.Voices               the city's four hundred voices as residents, their manner from who they are
  FlareCityRules.Ads                  a target by district, age, class and interest, the price, a day of an ad, the city's own ads
  FlareCityRules.Player               founder-led talk of the player's businesses, a scandal turned against them
  FlareCityPass                       the residents' day in slices: bind, indexes, voices, follows, the lift, fading, visits, ads, founder, posts, the city followers, attention
  FlareCityStore                      the save's line, "reallife:flare_city"
  FlareCityViews                      the screens as data: the account in the city, a business's page, an ad, a post's funnel
  FlareCityShare                      Going Public, part R: the host's voices and its guests' businesses' figures
  FlareCityGame                       the only file of it that touches the game: keys and registrations, what districts bear, an ad's money
  FlareCityGuide                      the feature's entry, the Guide page's figures, the option and the tests on the options page
  FlareBridges.City                   what it asks of the population (the residents this machine runs, its pools)
```

## The follows

A resident aged 14 or over follows up to eight accounts, made from who they are the first day Flare meets them: for each kind of spending their household cares for, now and then a shop of that kind in their district (by its room for customers), their grocer, a voice or two of their district, the influencers of their interests (a food influencer only by residents who care for eating out), and on the first day the player, at the account's city share. The player's city share is the account's followers times its tier's share (90% for a newcomer to 25% for a celebrity, at most 85% of the city's adults); the rest are "beyond the city", an aggregate as the population's region is. The account's own rules (`FlareRules.Post`, silence, a bad post, an arrest) still decide the follower count; each day the city's followers are brought to it, residents of the player's districts first when it grows, the lowest rolls of the day going when it falls. A post's new followers in the city are residents who saw it, were interested and did not follow. A business's followers come from its customers (one for every 400 visits, drawn from the pools) and drift away slowly (3 per mille a day), so a shop's following settles near its daily visits. Somebody who dies or leaves takes their follows with them; somebody new in their slot starts afresh.

## A post heard

Each follower sees an ordinary post at Flare's own reach share (30% times its quality and tone fit; 75% viral). Those who care for what it is about are interested 65% of the time, others 22%. An interested resident shares it 5% of the time (20% viral) with their household, two people at work and a neighbour, each seeing it 60% of the time. A few residents of the business's district come across it by its tags (0.3% of them; 4% viral). Each resident counts once a post. An interested resident who hears of a business adds one to the district's count of who heard good (or bad) things, as the share of the district who had not heard yet, so a district's count never passes its adults.

What is heard: the player's posts and replies, reviews and announcements (the player's, the voices', the session players'), the city's businesses' own posts, influencers' visits, a hire that backfired or an influencer's scandal, a complaint's inspection, a guest's post about a business on the host. Paid influencer deals and the Company app's sponsored post stay the Company's campaigns (their customers through the population's levers), so nothing is counted twice.

## Attention, people moved and the lift

A business's attention is the share of its customers' residents who heard good things times 1.5, less the share who heard bad things times 0.9 (thousandths), each district weighed by the money its residents brought the business's district for its kind on the day before. It is one of the population's attention sources (`PopulationAttention`, registered in `Wiring.FlareCityLinks`), asked once a game day: the pools are only ever shared out, so attention moves people from rivals, and a district's pull counts its businesses' attention, so a talked-of street draws people from other districts too. What residents heard fades by a quarter a day (bad talk 12% a day until answered, half a day after); the pools' habit keeps the customers it won a while longer.

The next morning Flare measures the lift of each business: its share of each class's pool with the attention it had, against the same pools with Flare's part taken out (the pools' money routed again without it, the business's own share recovered from its weight and the habit share it came in with, taken at the day's first ask). EconomyCheck runs twin cities and finds it within 5% of the real difference. The lift is shared among the player's posts and ads by their live impressions' share of what the residents heard of the business (the rest is the city's own talk); a lift that kept people away is no post's.

## Reviews, pages, ads and the voices

A customer is drawn from the day's pools as its money came (a class by the business's takings by class, a district by the money its residents routed there, a household by its day's budget for the kind), so the reviewer's household really spent there that day (checked against `PoolRules.Attribute`). One review for every 300 visits; the stars follow the business's satisfaction, a little for its interior, less where its prices are past what the reviewer's class bears. A review is read by the reviewer's household and two at work, and by residents of the district who care for its kind; four stars or more is good talk, two or fewer bad. Each business keeps its newest six reviews and a fading rating.

An ad (Business, Run an ad) is shown each day to so many residents of its target (its district or the city, those who care for what it sells, the young, the well off) in a rolled order; it costs 22 a thousand residents a day at today's prices, booked at the business (`reallife_transaction_flare_ad`), shown on its first day at once and measured from the next like a post. The city's own businesses and the rivals run ads (six a day at normal busyness), post and are reviewed by the same rules.

The four hundred people of the feed ("c0" to "c399") are residents drawn from the city's adults, the young and students more likely; their age, sex, district and manner (a student, a pensioner, a parent of a child at home, or by their household's tastes) are their resident's, and their name is rolled from the city's seed, the slot and the resident (`FlareLiveRules.ResidentCitizen`). A voice whose resident died or left is somebody new.

## The player's name

Each day 2.5% of the player's followers in the city hear of each of the player's businesses because they follow its founder, so a big following lifts them all; while an arrest that trended this week has no honest post answering it, 40% of them hear of it against each business.

## The screens

`FlareCityViews` holds each screen as data for the interface kit (living/ui): the account in the city (followers here and beyond, the newest posts' and ads' funnels), a business's page (followers, rating, pull, the newest reviews by their residents, what it heard and brought, one action: Run an ad), an ad (target, reach, days, price, or why it cannot run) and one post's funnel. Until the kit draws them, Flare's app shows the same data: a business card's lines and its Run an ad button, the ad sheet, and the account's lines at the top of the player's own Profile.

## The save and Going Public

One line, `reallife:flare_city`: binary columns compressed in pieces on the save's workers (the district save's zip), read on a worker at a load; the follows by person id and the businesses' figures by key, so they find their person and their business wherever the population's slots are. A save without it makes the follows on the first day; a line it cannot read is left until the next save.

In a session the host owns the residents and what they hear, as it owns the population: its guests' posts about a business are heard by its residents, and their businesses' customers come from its pools. Part R (`FlareCityShare`, "FR1", a letter older versions do not know and leave out) carries the host's voices, so the city's people have the same names and faces on every machine, and each session player's businesses' figures, which a guest's cards show. A guest runs no residents of its own and cannot run ads.

## Tests

Options, Tests: Flare in the city: play the residents' day now; a post about my first business, heard now; a small free ad for my first business; my first business's page in the log. Each says why in a notice when it cannot run, and writes a `Flare check (...)` line.

## Checks

EconomyCheck (`--flare-city`): the follows (every adult, the player's city share, the home districts first, a niche influencer's followers, the voices, the same seed the same follows, a death and a newcomer, the account's count kept), a post heard (seen as Flare's reach says, interested, shares, the unique-reach bound, viral, bad talk), attention moving people between rivals and never making them (twin cities: the same spending and takings in all), the lift within 5%, the funnel's share, bad talk and answers, reviews by customers who really spent there, the stars, ads (targets, delivery, price, the city's own), the save's round trip and a damaged line, the rebind by key, the costs (a slice near 2 ms, the line's size), every screen, part R, the texts and the sources.

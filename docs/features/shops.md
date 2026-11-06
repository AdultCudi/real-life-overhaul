# New kinds of shop

## In a sentence

Three more kinds of shop to start in BizMan, an Ice Cream Parlour, a Tea Bar and a Tobacconist, each a business type of the game's that sells the game's own goods from the game's own furniture and is run by the game's own simulation of the shop it is made like.

## Where the code is

- Folder: [`Scripts/Shops`](../../Assets/Mods/RealLifeOverhaul/Scripts/Shops/).
- Read first: `ShopsCatalogue.cs` (the kinds, their goods, hours, shelves and donors) and `ShopsRules.cs` (hours, help pages, icons), both game-free and run by EconomyCheck's `ShopChecks.cs`; then `ShopsGame.cs` (the only file that touches the game) and `ShopsDirector.cs`.

```
ShopsCatalogue      the three kinds: goods and their impact, sources, the most of one good, days, hour bands, shelves, the game types they trade as and become
ShopsRules          the hour's and day's share, the game's help page of a kind, the icons drawn at the start
ShopsGame           register, furnish from the donor, offer by the option, strip for removal, what the tests read
ShopsDirector       at the game's start, at a city load (the check line), on the option's switch
ShopsDirector.Tests "Say whether the new kinds of shop are ready", "How my ... is doing today", "Open a new kind of shop's help page"
ShopsGuide          the feature's entry, the options block
ShopsText           the locale keys
```

## What the game does

- A business type (`BusinessType`) names its building type, its simulation (`BusinessSimulator`: `FullServiceBusinessSimulator` for a coffee shop or fast food restaurant, `SelfServiceBusinessSimulator` for a liquor store), its goods with their impact (1 is primary), where they are bought, its requirements, its customers' demands, its day and hour multipliers, staff skills, uniforms and logos.
- A player's shop's customers for each hour are its building's customer capacity (cut to what its furniture serves) times its marketing and the street's traffic and the type's day and hour multipliers (`CustomerEntriesCalculatorRetail`, chosen for every type of a retail building). Each customer's order is built from the goods on show, by each good's sales ratio, its district's demand for that good (`ProductMarketHelper.GetNeighborhoodDemand`), the shop's satisfaction and the good's impact for the type. The simulation then serves them at the till, from the shelves, at the game's prices against each citizen's acceptable price.
- Service businesses (hairdresser, gym, night club) sell services made only by their own stations (`producerSettings.itemsToProduce` of the hairdresser's chair, the coat check) or charged at the door (the gym's cover charge); retail buildings take service goods only from such producers (`nonphysicalfromproducers`).
- A type's help page is the game's help screen at `businesstypes-<id>`: the link of its "at least one product" requirement in BizMan opens it, and for a page not in the game's list the help shows the texts `businesstypes-<id>` and `help_businesstypes-<id>_content` (Localizor's tables, made anew at each change of language, after which `LocalizorManager.OnLanguageChanged` is raised).
- `ModdingAPI.RegisterModBusinessType` adds a type and makes it available in its building type; the game keeps it through a reload of its types.

## What the feature does

- **The kinds**:
  - **Ice Cream Parlour**: ice cream (Ice Cream Counter) and slushies (Slushie Machine) primary; cotton candy, cupcakes, donuts, soda, popcorn and coffee on the side. Wholesale goods. Trades as the coffee shop (served at the till); without the mod it becomes a fast food restaurant, which sells ice cream as a primary good.
  - **Tea Bar**: tea (Industrial Coffee Machine) and slushies primary, the nearest the game's goods come to bubble tea; cupcakes, donuts, ice cream, croissants, soda, coffee and cotton candy on the side. Wholesale goods. Trades as and becomes the coffee shop.
  - **Tobacconist**: cigarettes and cigars (the tobacco shelves) primary; energy drinks, soda and cheap gifts. Cigarettes and cigars from the importers (so the headquarters course, as the liquor store), the rest from the wholesalers. Trades as and becomes the liquor store (self-serving, baskets, a point of sale).
- **Registered at the game's start**, whatever the option says, so a save that holds one finds its type. Until the game's own types are loaded a kind has no simulation and the game skips it; at each city load it is furnished from its donor: simulation, customer kind, requirements, demands, course, staff skills, uniforms and appearance, logos, the street and radio, and the donor's trading tags (paper bags, baskets, theft). The simulation is set last.
- **Its saved name** is `reallife_<id>` (`reallife_icecreamparlour`, `reallife_teabar`, `reallife_tobacconist`), the text key of its name in BizMan and on the map.
- **Its help page**: the mod's own text (`reallife_shops_help_<id>`, in the game's own style, linking the goods, the furniture, the wholesalers or importers and the skills) is written into the game's language tables under the keys the help reads, with the kind's name as the page's title, at the start, at each city load and after each load of a language (the tables reached through the contract: `GameContract.LocaleTables`, part `shop_help`). BizMan's requirement link then opens it as it opens the game's own pages.
- **Its own** goods and impacts, sources, the most of one primary good a customer buys, day and hour bands, map colour, search words, and an icon drawn at the start in the game's style (a white shape, 50 pixels square: a cone, a cup with its straw and pearls, a cigarette with its smoke).
- **Demand by district and time**: the type's day and hour bands, and the game's own demand of each district for each good, which every good of these kinds has (all are the game's demanded goods), so a district's appetite for ice cream reaches an ice cream parlour as it reaches a supermarket. Prices are the game's market prices, moved by the city's economy (`Economy/City`) as every shop's are.
- **The other features** count the ice cream parlour and the tea bar as food and drink: food inspections (`Business/BusinessGame.IsFood`), the food licence (`Economy/Company`), power and water (`Utilities`) and investors' sectors (`Economy/Ventures`); the tobacconist is a price-minded shop to the company's customers' taste.
- **Ready-made designs** (`Designs`, `DesignWay.Stock`): the game's own designs of the shops each is made like, without the shelves for goods it does not sell (and what stood on them), each with a point of sale and furniture that shows a primary good, at most six a size. A tea bar's coffee machines are set to pour tea. An ice cream parlour's are the game's coffee shop and fast food designs with an ice cream counter, of the four larger shop sizes (C1, C2, D2, M1); the game has none at A1 and A2.
- **The option** (`new_shops`, on): off, the kinds are made again without the game's "the player may start it" tag, so BizMan does not offer them; shops already open keep trading.
- **Removing the mod**: "Prepare this save for removing the mod" makes every such shop its template type.

- **In Uptown, run by the city**: Uptown's mix pass ([Uptown](uptown.md#offices-to-work-at-and-a-district-lived-in-from-the-first-day)) opens ice cream parlours and tobacconists as businesses of the city's, with a layout of the game's shop each trades as that shows its primary goods. A tea bar cannot be run by the city: the game's coffee shop layouts pour coffee.

## Not made, and why

The other kinds a player asked for need what the game has no model of; the mod adds no 3D models, so none of them could be made properly:

- **Laundromat**: the game has no washing machines or dryers of any kind, at home or in a business.
- **Nail salon, tattoo parlour, shoe repair**: a service business sells services made by stations of its own (the hairdresser's chairs make only haircuts, styling and colouring). There is no manicure table, tattoo chair or cobbler's bench, and a new station would be a new piece of furniture.
- **Sushi bar, record store, vape shop**: the game has no sushi, records or vapes to sell, and a new good needs its own model on the shelf. The tobacconist sells what the game has: cigarettes and cigars.
- **Bubble tea**: the tea bar sells tea and slushies, the nearest goods the game has.

## Going Public

A shop is the player's own business (`GoingPublic.Mine`), traded by the game's own simulation on the owner's machine. Nothing new is sent: the business is the game's own record. A player in a session without the mod sees a business type their game does not know, as with the mod's other types (the Car Showroom, the bank branch, the tech firms, the academy). Nothing of another player's is touched.

## Cost

Nothing per frame and no component. At the game's start three types made and three icons drawn (under a millisecond each); at a city load a look at three types; a test reads one shop's requirements and the game's customer list for it.

## Tests

- EconomyCheck `ShopChecks.cs`: the catalogue (whole days, the busiest day and hour, each good once with a primary first, sources, shelves among the game's, restocking with the kind's own goods); against the game's data read from its bundles into `RealLifeOverhaul.Build/EconomyCheck/ShopGoods/shop-goods-3682.json` (`shop_goods.py` writes it after a game update) and its wholesale stores' layouts: every good a demanded retail good sold at a margin, shown on a shelf the kind keeps, bought where the kind says; every lead shelf shows a primary good; the donor trades as a served or self-serving shop that needs a primary good on show; the template sells a primary good as its own; the designs' list of the game's shelves is the game's whole list; the hours and days, the help page names, the icons (size, fill, each its own, the same every time, quickly), the texts in three languages with the help pages' links, the notices' length, What's new; the wiring and the other features' food lists. DesignChecks: the three kinds' designs from the game's own files (a till, a lead shelf, no shelf for goods the kind does not sell, a design at every size where the game's designs show its goods) and the job writing a tea bar's and a tobacconist's designs.
- Options, Test tools: "Say whether the new kinds of shop are ready" (registered, trading as their donors, offered as the option says), "How my ice cream parlour, tea bar or tobacconist is doing today" (the shop the player is in, or their first: its requirements, whether it opens today, the game's customers for it and how many want what is on show), "Open a new kind of shop's help page" (the game's help at the page of the kind the player is in, or each kind in turn). Each writes a PASS or FAIL line. With Developer diagnostics on, a city load writes "New shops check (city load)".

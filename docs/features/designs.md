# Ready-made designs

## In a sentence

The game's interior installation firms offer the mod's own business types (the Car Showroom, the bank branch, the five tech firms, the Training Academy and the three new kinds of shop) ready-made designs, as they do the game's own businesses, made from the game's own designs and furniture at the game's own prices.

## Where the code is

- Folder: [`Scripts/Designs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Designs/).
- Read first: `DesignCatalogue.cs` (which of the game's designs each type's are made from, what a showroom keeps), `DesignRules.cs` and `DesignPlan.cs` (the folders, the names, the showroom's cut, the plan), `DesignFiles.cs` (the job that writes and takes away), all game-free and run by EconomyCheck's `DesignChecks.cs`; then `DesignsDirector.cs` (when) and `DesignsGame.cs` (the only file that touches the game).

```
DesignCatalogue     the seven types, their donors, the showroom's furniture, the numbers
DesignRules         the game's folder naming, the names, the room, the lot, the cut, the offer, the stamp, the list of the mod's files
DesignPlan          the game's design files found on disk, and the plan of every design made from them
DesignFiles         the job: write what the plan says (unless it is there already), take away what is no longer wanted
DesignsDirector     at the game's start and on the option's switch, on a worker; the self-check at a city load
DesignsDirector.Tests  "Say how many ready-made designs the firms offer", "Install the waiting orders for your mod businesses now"
DesignsGame         StreamingAssets, a design read and a showroom's cut written in the game's JSON, the firm's own list, its orders
DesignsGuide        the feature's entry, the options block, the Guide page
DesignsText         the locale keys
```

## What the game does

- The installation firm's design settings (`UI.Dialog.InteriorInstallationFirmDesignSettings`) list an address's designs with `InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding`: the `.json` files of `StreamingAssets/InteriorDesigns/<building type>/<size and version>/<business type>`, where each id is cut to its second piece between `_` (`GetIdWithoutType`), and the player's own blueprints of that building, size and business. With none it says "No designs are available for the selected address", which is what a player saw for a Car Showroom and a Game Studio.
- The design's name is shown as the text `interior_design_<name>`, so a design named as one of the game's (Modern, Classic, Luxury...) has the game's own name in every language.
- The layout price is the design's furniture at the game's market prices (`ItemHelper.GetDefaultMarketPrice`), the installation fee the building's floor area at the firm's rate (586 a square metre); the day is one of the next seven. On the day the order (`InteriorInstallationFirmContract`, in the save, which Going Public already carries) is installed by the game: it pays, sells what is in the building and takes it off the bill, puts the design in and sets the business up again.
- The mod's types are game business types (`ModdingAPI.RegisterModBusinessType`) with folder names of their own: `softwarestudio`, `gamestudio`, `itconsultancy`, `cybersecurity`, `ailab`, `academy`, and `businesstype` for the Car Showroom, whose saved name (`reallife_businesstype_cardealership`) has no `:`.

## What the feature does

At the game's start (and when the option is switched) a job on a worker thread puts a design file in each of those folders for every size the game has designs for in the type's building (offices A3, C1, C2, D2, J1, K1; shops A1, A2, C1, C2, D2, M1):

- **The tech firms and the academy** get the game's own designs of the business they work as, whole: the web development agency's (computer workstations and washrooms, the furniture their requirements ask for), the Game Studio the graphic designer's first, the IT Consultancy the law firm's too, the academy the travel agency's and the law firm's. A name two donors share is the first donor's.
- **The bank branch** gets up to four designs a size cut from the game's shop designs to a bank's furniture: what a showroom keeps (counters and the till, seats, desks, plants, washrooms and the rest) and the game's queue barriers, counters with glass, more chairs, a bench and a paper holder, with no lot to keep clear. Every design keeps a till on a counter for its teller and a washroom; those with seats to wait on are offered first.
- **The new kinds of shop** ([New kinds of shop](shops.md)) get up to six designs a size of the game's own shops each trades as, without the shelves for goods it does not sell and what stood on them (`DesignRules.StockCut`), each with a point of sale and furniture showing one of its primary goods: the tea bar the coffee shop's, its machines set to pour tea; the tobacconist the liquor store's, without the wine and spirits shelves; the ice cream parlour the coffee shop's and fast food restaurant's with an ice cream counter, which the game has at C1, C2, D2 and M1 only.
- **The Car Showroom** gets up to four designs a size cut from the game's own: its car dealerships' rooms (the `BusinessLayouts` of D2 and M1 whose names end in Cheap or Luxury) first, then shop designs with a sales counter and till, then the rest. A cut keeps only furniture on the catalogue's list (counters and the till, seats, tables, desks, plants, pictures, screens, carpets, lamps, washrooms, the staff's things) and nothing standing on the floor where the lot's cars go: the lot as `Economy/Dealer` lays it out (`DealerRules.Layout`, centred along the longer side), on the room the size's designs fill, 30 cm wider or narrower either way, with 40 cm round each place. What stood on something left out goes with it. A cut with fewer than six pieces of furniture or no washroom is not offered.

Every file goes on a list in the folder (`InteriorDesigns/reallife_designs.txt`) with a stamp of the game's files and these rules. While the stamp and every file are as they were, a job only lists the folders. Only a file on the list is ever written over or taken away: a file of that name the list does not hold (the game's, another mod's) is left and its design not offered. The option off takes every design on the list away, and so does "prepare this save for removing the mod". An order for a design taken away is cancelled by the firm on its day ("the design you chose ... is no longer available"), as the game does.

## The show flats' designs

For the construction game's show flats ([Construction](construction.md#show-flats)), made the same way from the game's own: `ShowFlatDesigns.cs` (game-free) and `DesignsDirector.ShowFlats.cs`, read in `DesignsGame.ShowDesigns`.

- **From** the firms' residential designs of every apartment structure the game lists (`InteriorDesigns/Residential/<structure>`; never a Hamptons house or the car park), by the firm's own helpers.
- **Two finishes each:** fully dressed (the game's design whole) and plain (only `PlainKeeps`: the beds, seats, tables, kitchen, storage and ceiling lamps), offered when the cut keeps six pieces or more and fewer than the whole.
- **At the game's prices:** the pieces kept at their market price and the firm's installation fee for the structure; the game's interior score of the design's walls and floors.
- **Not written anywhere:** they are laid on the show flat's floor by the floors core and offered by no firm. Read once a session on the first ask, the game's files not changing while it runs.
- EconomyCheck `ShowFlatsChecks.cs` holds them to the installed game's files: every apartment structure has a design, every plain cut is worth offering, every name it keeps is one of the game's pieces there.

## Going Public

Deterministic: the designs depend only on the game's files and these rules, so every machine on the same game build makes the same. Nothing new is sent: an order and an installation are the game's own, which Going Public carries as before. Nothing of another player's is touched; the files are on the player's own disk.

## Cost

Nothing per frame and no component. A job: a few directory listings and a small file read when nothing changed; on the first start or after a game update about 150 files read and written on the worker (a second or two). At a city load with Developer diagnostics on, one directory listing a type and size.

## Tests

- EconomyCheck `DesignChecks.cs`: every type the mod registers is in the catalogue; the game's folder naming, sizes and names; the list of the mod's files and what it may name; the stamp; the showroom's lot against `DealerRules.Layout`, the cut and the offer on made-up rooms; the texts. Then from the installed game's own files: every type has at least one design at every size; every design is for its folder's room, places only items the game names inside the room the game's designs and businesses fill, and is a game design or part of one (so its layout price is at most the game's); nothing kept stands on something left out; the tech firms' and academy's designs have computers and washrooms; no showroom furniture stands where its cars go; the plan is the same twice. And the job itself on a copy of some of the game's files: what it writes, a second start, a game update, a file lost, somebody else's file, the option off.
- Options, Test tools: "Say how many ready-made designs the firms offer" and "Install the waiting orders for your mod businesses now" (the game's own installation of every order for one of the mod's types, now instead of on its day; not in a Going Public session). With Developer diagnostics on, a city load writes the "installation firms' own lists check" line: the game's own lister asked for every type and size against what was written.

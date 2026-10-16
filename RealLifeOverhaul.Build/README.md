# Real Life Overhaul: developer guide

Real Life Overhaul is a mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/) that adds everyday life to the game's city: a camera behind your character, accidents with an emergency response, crime, police and courts, buses and driving, a home, a family and friends, courses and careers, credit, a city economy and a stock market, and much more for the player's businesses. Every part can be switched off by itself.

It is one C# library (netstandard2.1) built against the game's own DLLs, with texts in seven languages and three asset bundles. It runs on Windows, on the game's current build on Steam's public branch. This page gets you building; [the documentation index](../docs/README.md) has everything else.

## Where the code is

The code is in `Assets/Mods/RealLifeOverhaul/Scripts`, one folder per feature. Each folder has a `README.md` with the files to read first; the page column links the feature's page.

| Folder | What it is | Page |
| --- | --- | --- |
| [`Business`](../Assets/Mods/RealLifeOverhaul/Scripts/Business/) | Pressure on the player's businesses: rivals, the racket, inspections, staff, suppliers, audits | [Business pressure](../docs/features/business-pressure.md) |
| [`Camera`](../Assets/Mods/RealLifeOverhaul/Scripts/Camera/) | The third-person camera on foot and the chase camera when driving | |
| [`Careers`](../Assets/Mods/RealLifeOverhaul/Scripts/Careers/) | Education, the university and the professions | [Careers](../docs/features/careers.md) |
| [`Character`](../Assets/Mods/RealLifeOverhaul/Scripts/Character/) | The player's look, the hair finish, character-screen lighting | [Character quality](../docs/features/character.md) |
| [`CityLife`](../Assets/Mods/RealLifeOverhaul/Scripts/CityLife/) | People at work and about their day on the pavements | [The living city](../docs/features/city-life.md) |
| [`Civic`](../Assets/Mods/RealLifeOverhaul/Scripts/Civic/) | The city's government, elections and construction sites | [Civic life](../docs/features/civic.md) |
| [`Core`](../Assets/Mods/RealLifeOverhaul/Scripts/Core/) | Entry points, settings, the guard, the game contract, the save hook, the game's own interface | [Folder map](../docs/reference/folder-map.md#scriptscore) |
| [`Crime`](../Assets/Mods/RealLifeOverhaul/Scripts/Crime/) | Crime near, by and against the player | [Crime](../docs/features/crime.md) |
| [`Diagnostics`](../Assets/Mods/RealLifeOverhaul/Scripts/Diagnostics/) | The session record and the development aids | [Performance](../docs/reference/performance.md#the-measuring-folders) |
| [`Districts`](../Assets/Mods/RealLifeOverhaul/Scripts/Districts/) | The district view on the city map | [Districts](../docs/features/districts.md) |
| [`Driving`](../Assets/Mods/RealLifeOverhaul/Scripts/Driving/) | Each car's performance, the eased throttle, the speed reading | [Driving](../docs/features/driving.md) |
| [`Economy`](../Assets/Mods/RealLifeOverhaul/Scripts/Economy/) | Credit, banks, the city's economy, investors, running a business, property, brands | [Economy pages](../docs/features/README.md#economy) |
| [`EndScale`](../Assets/Mods/RealLifeOverhaul/Scripts/EndScale/) | The end game at scale: data centres, cloud, broadband and a bank at real costs | |
| [`Events`](../Assets/Mods/RealLifeOverhaul/Scripts/Events/) | Fires, weather that matters, street events | [City events](../docs/features/city-events.md) |
| [`Fame`](../Assets/Mods/RealLifeOverhaul/Scripts/Fame/) | Standing: how well the city knows the player and what it thinks of them | |
| [`Family`](../Assets/Mods/RealLifeOverhaul/Scripts/Family/) | A partner, a wedding, children, the wider family | [Family](../docs/features/family.md) |
| [`Faults`](../Assets/Mods/RealLifeOverhaul/Scripts/Faults/) | Real faults in the city worth reporting: dark lamps, potholes, litter | |
| [`FitOut`](../Assets/Mods/RealLifeOverhaul/Scripts/FitOut/) | The mod's own buildings furnished with the game's furniture | |
| [`Garage`](../Assets/Mods/RealLifeOverhaul/Scripts/Garage/) | The custom shop for cars, bicycles and motorbikes | [Garage](../docs/features/garage.md) |
| [`Home`](../Assets/Mods/RealLifeOverhaul/Scripts/Home/) | Needs, cooking, illness and the doctor, the roommate | [Home life and health](../docs/features/home.md) |
| [`Interiors`](../Assets/Mods/RealLifeOverhaul/Scripts/Interiors/) | Interiors and decor that matter to a business | [Interiors and decor](../docs/features/interiors.md) |
| [`Jobs`](../Assets/Mods/RealLifeOverhaul/Scripts/Jobs/) | Gigs from the Gig Line and the help-wanted boards, and the employers' ranks | [Jobs](../docs/features/jobs.md) |
| [`Justice`](../Assets/Mods/RealLifeOverhaul/Scripts/Justice/) | Courts, sentences and prison | [Justice](../docs/features/justice.md) |
| [`Law`](../Assets/Mods/RealLifeOverhaul/Scripts/Law/) | Police precincts, custody, the record and reputation | [Law](../docs/features/law.md) |
| [`Ledger`](../Assets/Mods/RealLifeOverhaul/Scripts/Ledger/) | The City Ledger as a paper the player buys and reads | |
| [`Leisure`](../Assets/Mods/RealLifeOverhaul/Scripts/Leisure/) | Chess, fishing, dancing, drinks, sightseeing, clothes, photo mode | [Leisure](../docs/features/leisure.md) |
| [`Licences`](../Assets/Mods/RealLifeOverhaul/Scripts/Licences/) | Driving licences, tests and points | [Driving licences](../docs/features/licences.md) |
| [`Life`](../Assets/Mods/RealLifeOverhaul/Scripts/Life/) | Random life events, sickness and the hospital | |
| [`Markets`](../Assets/Mods/RealLifeOverhaul/Scripts/Markets/) | The stock market, bonds, the listed company, trading bots | [The stock market](../docs/features/economy-markets.md) |
| [`Meta`](../Assets/Mods/RealLifeOverhaul/Scripts/Meta/) | The feature registry, the Guide, presets, the options page, save tools, bug reports | [Meta](../docs/features/meta.md) |
| [`Navigation`](../Assets/Mods/RealLifeOverhaul/Scripts/Navigation/) | The minimap and the GPS | [Minimap and GPS](../docs/features/navigation.md) |
| [`Offices`](../Assets/Mods/RealLifeOverhaul/Scripts/Offices/) | Empty shops made offices where a district lacks them | |
| [`People`](../Assets/Mods/RealLifeOverhaul/Scripts/People/) | The street's characters and the scenes they play | [Street scenes](../docs/features/street-scenes.md) |
| [`Performance`](../Assets/Mods/RealLifeOverhaul/Scripts/Performance/) | Tweaks for the game's frame rate, and the measuring | [Performance](../docs/reference/performance.md) |
| [`Permadeath`](../Assets/Mods/RealLifeOverhaul/Scripts/Permadeath/) | An ending the player chooses | [Permadeath](../docs/features/permadeath.md) |
| [`Phone`](../Assets/Mods/RealLifeOverhaul/Scripts/Phone/) | Phone contacts you can call, and their pictures | |
| [`Photos`](../Assets/Mods/RealLifeOverhaul/Scripts/Photos/) | The Camera and Gallery apps | |
| [`Planner`](../Assets/Mods/RealLifeOverhaul/Scripts/Planner/) | The phone's agenda | [The Planner](../docs/features/planner.md) |
| [`Ragdoll`](../Assets/Mods/RealLifeOverhaul/Scripts/Ragdoll/) | People hit by cars | [Street scenes](../docs/features/street-scenes.md) |
| [`Rivals`](../Assets/Mods/RealLifeOverhaul/Scripts/Rivals/) | Rival tycoons on the stock market | |
| [`Social`](../Assets/Mods/RealLifeOverhaul/Scripts/Social/) | Friends, outings, texts, the morning paper, Flare | [Social life](../docs/features/social.md) |
| [`Street`](../Assets/Mods/RealLifeOverhaul/Scripts/Street/) | Talking to anybody in the street | |
| [`Tech`](../Assets/Mods/RealLifeOverhaul/Scripts/Tech/) | The tech sector and the professions | |
| [`Texts`](../Assets/Mods/RealLifeOverhaul/Scripts/Texts/) | RLO Texts: optional packs that word the city's messages on the player's own PC | |
| [`Traffic`](../Assets/Mods/RealLifeOverhaul/Scripts/Traffic/) | Accidents, the emergency response, pursuits, the tow truck | [Street scenes](../docs/features/street-scenes.md) |
| [`Transport`](../Assets/Mods/RealLifeOverhaul/Scripts/Transport/) | Buses, the subway, owning a car, a taxi by phone | [Transport](../docs/features/transport.md) |
| [`Travel`](../Assets/Mods/RealLifeOverhaul/Scripts/Travel/) | Hotels and holidays | [Travel and lodging](../docs/features/travel.md) |
| [`Utilities`](../Assets/Mods/RealLifeOverhaul/Scripts/Utilities/) | Power, water and internet | [Power and water](../docs/features/utilities.md) |
| [`Wiring`](../Assets/Mods/RealLifeOverhaul/Scripts/Wiring/) | The bridges between features | [Wiring](../docs/reference/wiring.md) |
| [`World`](../Assets/Mods/RealLifeOverhaul/Scripts/World/) | Entering buildings and vehicles with a key, visual grading, the map's places | |

The rest of the repository (the asset pipelines, this build folder, the checkers) is in the [folder map](../docs/reference/folder-map.md).

## Build in fifteen minutes

You need Big Ambitions from Steam, the [.NET SDK](https://dotnet.microsoft.com/download) 10 or later, Git and Python 3. You do not need Unity or Blender.

1. Clone the repository and go into it:

   ```
   git clone https://github.com/AdultCudi/real-life-overhaul.git
   cd real-life-overhaul
   ```

2. Build the mod against your game. Change the path if Steam is somewhere else:

   ```
   dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release "-p:GameManaged=C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
   ```

   It must end with `0 Warning(s)` and `0 Error(s)`. The mod is then in `RealLifeOverhaul.Build/out/RealLifeOverhaul`.

3. Run the four offline checkers. In Git Bash:

   ```
   for c in CatalogueCheck NavCheck EconomyCheck PlannerCheck; do
     dotnet build RealLifeOverhaul.Build/$c -c Release -v q && dotnet RealLifeOverhaul.Build/$c/bin/Release/net10.0/$c.dll | tail -1
   done
   python RealLifeOverhaul.Build/Workshop/locales.py
   ```

   Each checker must end with `ALL CHECKS PASSED`, and the locale check with `LOCALES OK`. ContractCheck, which checks the mod against your game's files, is in [Checks](../docs/contributing/checks.md).

4. To try it in the game, close the game, then build again with `-p:InstallToGame=true` added. If you also have the Workshop item, untick it under **Mods** in the main menu. On the mod's options page, switch on **Developer diagnostics** to get its lines in `Player.log`.

[Getting started](../docs/contributing/getting-started.md) has each step in full, and what to do when one fails.

## Where to go next

- [Getting started](../docs/contributing/getting-started.md), with good first tasks.
- [The rules for new code](../docs/contributing/rules.md) and [Checks](../docs/contributing/checks.md).
- [Logs and debugging](../docs/reference/logs-and-debugging.md): where `Player.log` is and what the mod writes there.
- [Architecture](../docs/reference/architecture.md) and [Wiring](../docs/reference/wiring.md): how the features are built and how they meet.
- [Every feature](../docs/features/README.md), one page each.
- [Texts and translation](../docs/contributing/texts-and-translation.md) and [Adding things](../docs/contributing/adding-things.md).
- [After a game update](../docs/contributing/after-a-game-update.md).
- [How to contribute](../CONTRIBUTING.md): talk to us first, then open a pull request.

This folder holds the command-line build (`RealLifeOverhaul.csproj`), the checkers, the installer, the Workshop texts and notes beside the build (`docs/`). It is kept by the Real Life Overhaul maintainer.

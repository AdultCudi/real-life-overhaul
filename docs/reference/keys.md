# Save keys, contact ids and key bindings

What the mod puts in a save, the phone contacts it adds, and the keys it acts on. The save keys and contacts come from each feature's entry in the registry (`Scripts/Meta/FeatureList.cs` and the features' own `DescribeFeature`), which the save tools read to clean a save; see [Save tools](../features/meta.md#save-tools-what-is-removed-and-how-that-was-checked-against-the-game).

## Save keys (`modData`)

Each feature keeps its state in the save's `modData` under keys that start `reallife:`. A key ending in `_` is a prefix: the feature adds a name or a number after it. Keys ending `_before_tests` or `_test` hold a feature's state while a test button has run, so it can be put back.

| Key | Named in |
| --- | --- |
| `reallife:business` | `Business/BusinessState.cs`, `Meta/FeatureList.cs` |
| `reallife:career` | `Jobs/CareerState.cs`, `Meta/FeatureList.cs` |
| `reallife:careers` | `Careers/CareersStore.cs` |
| `reallife:careers_campus` | `Careers/CareersStore.cs` |
| `reallife:careers_campus_told` | `Careers/CareersStore.cs` |
| `reallife:careers_news` | `Careers/CareersStore.cs` |
| `reallife:civic_gov` | `Civic/CivicDirector.cs`, `Meta/FeatureList.cs` |
| `reallife:civic_office` | `Civic/CivicDirector.cs` |
| `reallife:civic_works` | `Civic/CivicDirector.cs`, `Meta/FeatureList.cs` |
| `reallife:district_log` | `Districts/DistrictRules.cs` |
| `reallife:economy` | `Economy/Banking/Rating/CreditFile.cs`, `Economy/Banking/Rating/CreditStore.cs` |
| `reallife:economy_brands` | `Economy/Brands/BrandStore.cs` |
| `reallife:economy_brands_before_tests` | `Economy/Brands/BrandStore.cs` |
| `reallife:economy_city` | `Economy/City/CityStore.cs` |
| `reallife:economy_city_before_tests` | `Economy/City/CityStore.cs` |
| `reallife:economy_city_eve` | `Economy/City/CityStore.cs` |
| `reallife:economy_company` | `Economy/Company/CompanyStore.cs` |
| `reallife:economy_company_before_tests` | `Economy/Company/CompanyStore.cs` |
| `reallife:economy_dealer` | `Economy/Dealer/DealerStore.cs` |
| `reallife:economy_dealer_before_tests` | `Economy/Dealer/DealerStore.cs` |
| `reallife:economy_estate` | `Economy/Estate/EstateStore.cs` |
| `reallife:economy_estate_before_tests` | `Economy/Estate/EstateStore.cs` |
| `reallife:economy_online` | `Economy/Online/OnlineStore.cs` |
| `reallife:economy_online_before_tests` | `Economy/Online/OnlineStore.cs` |
| `reallife:economy_ownbank` | `Economy/OwnBank/OwnBankStore.cs` |
| `reallife:economy_ownbank_test` | `Economy/OwnBank/OwnBankDirector.Tests.cs` |
| `reallife:economy_rota` | `Economy/Company/RotaStore.cs` |
| `reallife:economy_services` | `Economy/Services/ServicesStore.cs` |
| `reallife:economy_ventures` | `Economy/Ventures/VenturesStore.cs` |
| `reallife:events` | `Events/EventDirector.cs`, `Meta/FeatureList.cs` |
| `reallife:family` | `Family/FamilyState.cs`, `Meta/FeatureList.cs` |
| `reallife:faults` | `Faults/FaultStore.cs` |
| `reallife:firearms` | `Licences/LicenceDirector.cs` |
| `reallife:flare_account` | `Social/Flare/FlareStore.cs` |
| `reallife:flare_cast` | `Social/Flare/FlareStore.cs` |
| `reallife:flare_net` | `Social/Flare/FlareLiveStore.cs` |
| `reallife:flare_posts` | `Social/Flare/FlareStore.cs` |
| `reallife:friend_day` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:fuel` | `Meta/FeatureList.cs` |
| `reallife:garage` | `Meta/FeatureList.cs`, `Transport/CarGarage.cs` |
| `reallife:garage_bikes` | `Garage/GarageState.cs` |
| `reallife:garage_builds` | `Garage/GarageState.cs` |
| `reallife:garage_helmet` | `Garage/GarageState.cs` |
| `reallife:home` | `Home/HomeState.cs` |
| `reallife:home_roommate` | `Home/Roommates/RoommateStore.cs` |
| `reallife:home_roommate_before_tests` | `Home/Roommates/RoommateDirector.cs` |
| `reallife:interiors` | `Interiors/InteriorStore.cs` |
| `reallife:interiors_rules` | `Interiors/InteriorStore.cs` |
| `reallife:job_live` | `Jobs/JobSave.cs`, `Meta/FeatureList.cs` |
| `reallife:job_rental` | `Jobs/JobRental.cs`, `Meta/FeatureList.cs` and 1 more |
| `reallife:jobs` | `Jobs/JobState.cs`, `Meta/FeatureList.cs` |
| `reallife:justice` | `Justice/JusticeState.cs` |
| `reallife:law_custody` | `Law/Record.cs`, `Meta/FeatureList.cs` |
| `reallife:law_precincts` | `Law/Record.cs`, `Meta/FeatureList.cs` |
| `reallife:law_record` | `Law/Record.cs`, `Meta/FeatureList.cs` |
| `reallife:law_reputation` | `Law/Record.cs`, `Meta/FeatureList.cs` |
| `reallife:law_station` | `Law/Record.cs` |
| `reallife:ledger` | `Ledger/LedgerStore.cs` |
| `reallife:ledger_online` | `Ledger/NewsstandStore.cs` |
| `reallife:leisure_best` | `Leisure/LeisureState.cs` |
| `reallife:leisure_drink_time` | `Leisure/LeisureState.cs` |
| `reallife:leisure_drinks` | `Leisure/LeisureState.cs` |
| `reallife:leisure_fish` | `Leisure/LeisureState.cs` |
| `reallife:leisure_must_buy` | `Leisure/LeisureState.cs` |
| `reallife:leisure_outfit_` | `Leisure/LeisureState.cs` |
| `reallife:leisure_owned` | `Leisure/LeisureState.cs` |
| `reallife:leisure_plays` | `Leisure/LeisureState.cs` |
| `reallife:leisure_seen` | `Leisure/LeisureState.cs` |
| `reallife:leisure_spent` | `Leisure/LeisureState.cs` |
| `reallife:licence_car` | `Licences/Driving/TestCar.cs` |
| `reallife:licences` | `Licences/LicenceState.cs` |
| `reallife:look_dress_f` | `Character/CharacterCatalogue.cs` |
| `reallife:look_feet_f` | `Character/CharacterCatalogue.cs` |
| `reallife:look_feet_m` | `Character/CharacterCatalogue.cs` |
| `reallife:look_game_beard` | `Character/CharacterCatalogue.cs` |
| `reallife:look_game_feet` | `Character/CharacterCatalogue.cs` |
| `reallife:look_game_hair` | `Character/CharacterCatalogue.cs` |
| `reallife:look_game_legs` | `Character/CharacterCatalogue.cs` |
| `reallife:look_game_top` | `Character/CharacterCatalogue.cs` |
| `reallife:look_legs_f` | `Character/CharacterCatalogue.cs` |
| `reallife:look_legs_m` | `Character/CharacterCatalogue.cs` |
| `reallife:look_top_f` | `Character/CharacterCatalogue.cs` |
| `reallife:look_top_m` | `Character/CharacterCatalogue.cs` |
| `reallife:markets` | `Markets/MarketStore.cs` |
| `reallife:meta_release` | `Meta/SaveTools.cs` |
| `reallife:meta_version` | `Meta/SaveTools.cs` |
| `reallife:modifiers` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:mom_day` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:mom_gift_day` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:offices` | `Offices/ConversionStore.cs` |
| `reallife:pending_fine` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:pending_fine_due` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:permadeath_record` | `Permadeath/PermadeathState.cs` |
| `reallife:permadeath_seen` | `Permadeath/PermadeathState.cs` |
| `reallife:permadeath_state` | `Permadeath/PermadeathState.cs` |
| `reallife:place_utilities` | `Core/CityReserves.cs` |
| `reallife:planner` | `Planner/PlannerStore.cs` |
| `reallife:planner_notes` | `Planner/PlannerStore.cs` |
| `reallife:planner_reminders` | `Planner/PlannerStore.cs` |
| `reallife:rivals` | `Rivals/RivalStore.cs` |
| `reallife:rivals_before_tests` | `Rivals/RivalStore.cs` |
| `reallife:seen_events` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:showroom_bays` | `Economy/Dealer/Floor/ShowroomBays.cs` |
| `reallife:sick_until` | `Core/ModState.cs`, `Meta/FeatureList.cs` |
| `reallife:social` | `Meta/FeatureList.cs`, `Social/SocialModel.cs` |
| `reallife:social_filler` | `Social/News.cs` |
| `reallife:social_news` | `Social/News.cs` |
| `reallife:social_offer` | `Social/SocialModel.cs` |
| `reallife:social_paper` | `Social/News.cs` |
| `reallife:social_people` | `Social/SocialModel.cs` |
| `reallife:social_plan` | `Social/SocialModel.cs` |
| `reallife:standing_city` | `Fame/FameStore.cs` |
| `reallife:standing_file` | `Fame/FameStore.cs` |
| `reallife:standing_press` | `Fame/FameStore.cs` |
| `reallife:street` | `Street/StreetStore.cs` |
| `reallife:tech_firms` | `Tech/Firms/FirmsStore.cs` |
| `reallife:tech_hiring` | `Tech/Hiring/HiringStore.cs` |
| `reallife:tech_infra` | `Tech/Infra/InfraStore.cs` |
| `reallife:tech_news` | `Tech/TechNews.cs` |
| `reallife:tech_people` | `Tech/People/TalentStore.cs` |
| `reallife:tech_ventures` | `Tech/Venture/VentureStore.cs` |
| `reallife:texts` | `Texts/TextsStore.cs` |
| `reallife:transit` | `Meta/FeatureList.cs`, `Transport/TransitRide.cs` |
| `reallife:travel` | `Travel/TravelState.cs` |
| `reallife:utilities` | `Utilities/UtilitiesStore.cs` |

## Phone contact ids

Every phone contact the mod adds has an id that starts `reallife-`. The save tools remove them with their messages when a save is prepared for removing the mod.

| Contact id | Named in |
| --- | --- |
| `reallife-911` | `Meta/FeatureList.cs`, `Phone/Contacts/ContactCatalogue.cs` and 1 more |
| `reallife-bulletin` | `Events/EventDirector.cs`, `Meta/FeatureList.cs` and 1 more |
| `reallife-business` | `Business/BusinessDesk.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-career` | `Jobs/CareerPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-cityhall` | `Civic/CityHallPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-clinic` | `Home/HomePhone.cs` |
| `reallife-collections` | `Economy/Banking/Debt/DebtPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-debt-advice` | `Economy/Banking/Debt/DebtPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-dmv` | `Licences/Driving/DrivingSchool.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-doctor` | `Meta/FeatureList.cs`, `Phone/Contacts/ContactCatalogue.cs` and 1 more |
| `reallife-fame-reporter` | `Fame/FameReporter.cs` |
| `reallife-family` | `Family/FamilyPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-firearms-unit` | `Licences/LicenceDirector.cs` |
| `reallife-friend` | `Meta/FeatureList.cs`, `Phone/Contacts/ContactCatalogue.cs` and 4 more |
| `reallife-garage` | `Phone/Contacts/ContactCatalogue.cs`, `Transport/CarPhone.cs` |
| `reallife-guide` | `Meta/Guide.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-gunshop` | `Licences/LicenceDirector.cs` |
| `reallife-health` | `Home/HomePhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-inlaws` | `Family/FamilyPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-jobs` | `Jobs/JobPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-ledger` | `Phone/Contacts/ContactCatalogue.cs`, `Social/SocialPhone.cs` |
| `reallife-legal` | `Justice/LegalCounsel.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-lender` | `Economy/Banking/Debt/DebtPhone.cs`, `Economy/Finance/FinanceApp.Accounts.cs` and 2 more |
| `reallife-mom` | `Family/Relatives.cs`, `Meta/FeatureList.cs` and 3 more |
| `reallife-partner` | `Family/FamilyPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-people` | `Phone/Contacts/ContactCatalogue.cs`, `Social/SocialPhone.cs` |
| `reallife-records` | `Law/RecordPhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-roadside` | `Phone/Contacts/ContactCatalogue.cs`, `Transport/CarPhone.cs` |
| `reallife-roommate` | `Home/Roommates/RoommatePhone.cs`, `Phone/Contacts/ContactCatalogue.cs` |
| `reallife-taxi` | `Phone/Contacts/ContactCatalogue.cs`, `Transport/Taxi/TaxiPhone.cs` |
| `reallife-transit` | `Phone/Contacts/ContactCatalogue.cs`, `Transport/SubwayJourney.cs` |
| `reallife-travel` | `Phone/Contacts/ContactCatalogue.cs`, `Travel/TravelPhone.cs` |
| `reallife-trips` | `Phone/Contacts/ContactCatalogue.cs`, `Travel/TravelPhone.cs` |

## Key bindings

Every key the mod acts on is an action in `Core/Keys.cs` (`KeyAction`): an id, a default key and an optional Shift/Ctrl/Alt.
Saved bindings are read at game start from PlayerPrefs (`m:<mod id>:key_<action id>`), and changed under "Controls" on the options page: a button per action that takes the next key pressed (hold a modifier with it for a combination; Esc cancels; the left mouse button and gamepads are not offered), and a reset button.

A key the game itself has bound is allowed and named beside the button (read from the game's live bindings, its defaults if those cannot be read).

| action | default | where it is used |
|---|---|---|
| `CameraToggle` | V | `Camera/ThirdPersonCamera` |
| `Interact` | F | `World/NearbyInteraction`, `Jobs/JobDirector`, `Crime/CrimeDirector`, `CityLife/CityLifeDirector`, `Civic/CivicDirector` (vote, take a leaflet, read a site notice, offer a leaflet) |
| `InteractAlt` | E | the same, and only while the game shows no interaction of its own |
| `FreeCursor` | Left Alt | `Camera/ThirdPersonCamera` |
| `LookBehind` | Right mouse (held) | `Camera/ThirdPersonCamera` |
| `GentleThrottle` | G (held) | `Driving/CarTuning`: the pedal goes no further than half |
| `PerfProbe`, `PerfBench` | F7, Shift+F7 | `Performance/PerfDirector` |
| `PhoneDump`, `CharacterReport` | F8, F9 | the two development aids |
| `PhotoMode` | F10 | `Leisure/Photo/PhotoMode`: into and out of photo mode (its other keys are fixed: `PhotoKeys`) |

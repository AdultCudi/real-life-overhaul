# The options page before the rework

Every row of the page as it stood at commit a216aae, in the order it was shown. The page was one section per feature in alphabetical order of the feature's id, then Controls, Performance, the save tools, Diagnostics and "Test tools". The first column is the row's place in the order RealLifeOverhaulMod.cs builds it in (rows without a number were added by Meta). Made by a script from the sources and en.json; kept for comparison with [options-after.md](options-after.md).

Counts: 11 buttons, 11 keys, 17 selectors, 33 sliders, 166 switches, 307 test buttons, 30 headers as built.

## At the top

| Row | Label |
| --- | --- |
| header | Real Life Overhaul: Start here |
| button | Open the Guide on your phone (what the mod does, controls, numbers, troubleshooting) |
| button | Show what changed (the Guide on your phone) |
| header | Preset in use |
| button | Preset: Relaxed (nothing is done to you; fewer incidents) |
| button | Preset: Standard (the mod's defaults) |
| button | Preset: Realistic (more incidents, more police) |
| button | Preset: Hardcore (everything at the strict end, lower job pay) |
| button | Reset every option and key of the mod to its default |
| button | Export my settings to a text file |
| button | Import settings from the text file |
| header | Real Life Overhaul: Features (click a name to open or close it) |
| button | Open or close every section |

## Business pressure   (`business`, 11 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 130 | switch | `business_pressure` | Business pressure: rivals, inspections, staff and supplier trouble, opportunities (listed on the Business desk contact) |
| 131 | slider | `business_frequency` | How often something comes up (1 = about one day in twelve, 10 = about one day in three; never more than two open at once) |
| 132 | switch | `business_easy` | Easy business: only the opportunities (bulk orders, press features, praise, awards) |
| 133 | switch | `business_rivals` | Rival moves: a competitor undercuts you, an offer to one of your staff, a smear in the paper |
| 134 | switch | `business_racket` | Protection racket: pay, refuse, go to the police, or rely on your security |
| 135 | switch | `business_inspections` | Inspections: health (food businesses), fire safety, licence renewal |
| 136 | switch | `business_staff` | Staff trouble: a strike threat over low wages, theft caught on camera, an argument to settle |
| 137 | switch | `business_suppliers` | Supplier trouble: a late delivery, a surcharge |
| 138 | switch | `business_customers` | Customer incidents: a complaint or praise that spreads |
| 139 | switch | `business_audit` | Tax audit |
| 140 | switch | `business_opportunities` | Opportunities: a bulk order, a feature in the paper, an award |

## Camera   (`camera`, 13 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 2 | switch | `camera_enabled` | Third-person camera (off: the game's own camera) |
| 3 | switch | `camera_mouselook` | Mouse look, GTA style (the 'Free the cursor' key under Controls gives the mouse back) |
| 4 | slider | `camera_sensitivity` | Mouse look sensitivity |
| 5 | switch | `enter_with_key` | Get into buildings and your vehicles with the interact keys (see Controls) |
| 6 | switch | `camera_indoors` | Also use it inside buildings |
| 7 | slider | `camera_pitch` | Camera tilt in degrees (the starting tilt while mouse look is on) |
| 8 | slider | `camera_distance` | Camera distance |
| 9 | slider | `camera_fov` | Field of view |
| 10 | slider | `camera_height` | Height the camera looks at (tenths of a metre) |
| 11 | switch | `car_camera` | Chase camera when driving (hold the look-behind key to look behind) |
| 12 | slider | `car_camera_distance` | Chase camera distance |
| 20 | slider | `camera_follow` | Swing behind you when mouse look is off (0 = off) |
| 21 | switch | `map_places_shown` | The mod's places on the map: bus stops, hotels, the courthouse, driving schools, bike hire, custom shops, help-wanted boards, polling places, construction sites, sights and what's on, each with markers and its own map filter (off hides all of them, police stations too) |

## Careers   (`careers`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 121 | switch | `careers` | Careers: employers behind the help-wanted boards (a rota, reviews, promotions) |

## Character and wardrobe   (`character`, 16 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 28 | switch | `character_high_detail` | High-detail player character (uses the game's own detailed model in the city) |
| 29 | switch | `character_hair` | Fuller hair and beards on detailed characters |
| 30 | switch | `character_lighting` | Softer lighting in character creation and shop previews |
| 33 | switch | `character_new_looks` | New looks: the mod's clothes and shoes, and a hair-coloured scalp under your hair so that it does not look thin from a distance |
| 34 | selector | `look2_top_m` | Top (men) |
| 35 | selector | `look2_top_m_colour` | Top colour (men; a print the garment cannot carry shows as a plain colour) |
| 36 | selector | `look2_legs_m` | Trousers (men) |
| 37 | selector | `look2_legs_m_colour` | Trousers colour (men; a print the garment cannot carry shows as a plain colour) |
| 38 | selector | `look_feet_m` | Shoes (men) |
| 39 | selector | `look2_dress_f` | Dress (women; replaces top and trousers) |
| 40 | selector | `look2_dress_f_colour` | Dress colour (women; a print the garment cannot carry shows as a plain colour) |
| 41 | selector | `look2_top_f` | Top (women) |
| 42 | selector | `look2_top_f_colour` | Top colour (women; a print the garment cannot carry shows as a plain colour) |
| 43 | selector | `look2_legs_f` | Trousers or skirt (women) |
| 44 | selector | `look2_legs_f_colour` | Trousers or skirt colour (women; a print the garment cannot carry shows as a plain colour) |
| 45 | selector | `look_feet_f` | Shoes (women) |

## City life   (`citylife`, 5 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 99 | switch | `city_life` | City life: people at work and going about their day on the pavements |
| 100 | slider | `city_density` | City life: how much is going on (1 = now and then, 10 = always something; never more than 8 people) |
| 101 | switch | `city_workers` | City life: people at work (sweepers, road crews, window cleaners, leaflets, carts, couriers, the post, painters, gardeners) |
| 102 | switch | `city_everyday` | City life: everyday people (friends talking, smokers, phone calls, tourists, joggers, chess, pushchairs, queues, umbrellas in the rain) |
| 103 | switch | `city_interactions` | City life: you can buy a coffee at the coffee cart (the interact key, at the counter) |

## City government and building sites   (`civic`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 500 | switch | `civic` | Civic life: a city government and a construction programme (City Hall on the phone) |
| 501 | switch | `civic_elections` | Elections and the mayor: campaigns in the street, voting, policies that change the city (off: every policy effect is taken back) |
| 502 | switch | `civic_construction` | Construction sites: roadworks, scaffolding, shop refits and utility works that progress over days |

## Crime and police stops   (`crime`, 4 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 88 | switch | `street_crime` | Crime near you (street crime, thieves in shops, other drivers pulled over) |
| 89 | slider | `crime_frequency` | Crime frequency |
| 90 | switch | `crime_against_player` | Crimes against you (pickpockets, muggers, break-ins, scam calls; all capped) |
| 91 | switch | `player_offences` | Police stop you for red lights, speeding and reckless driving, and warn or fine you for jaywalking |

## Driving   (`driving`, 6 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 13 | switch | `car_tuning` | Realistic car performance: every car accelerates and tops out like its kind (off: the game's own cars) |
| 14 | slider | `car_top_speed` | Car top speed (% of each car's own) |
| 15 | slider | `car_acceleration` | Car acceleration (% of each car's own) |
| 16 | switch | `smooth_throttle` | Smooth throttle: the key eases the pedal down, a tap pulls away gently |
| 17 | button |  | Measure this car (then stop, and hold full throttle for 20 seconds) |
| 19 | switch | `speedometer` | Speedometer when driving |

## Money and credit   (`economy`, 5 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 375 | switch | `economy` | Credit rating and phone loans (off: the banks' own terms, no Finance app, nothing recorded) |
| 376 | switch | `economy_products` | More kinds of credit: overdraft, credit card, business loan, car finance, mortgage, consolidation (off: nothing new is opened or drawn; what you owe stays owed) |
| 377 | switch | `economy_shark` | The private lender, for when no bank will lend (off: it lends nothing and does nothing; a debt to it can still be paid) |
| 378 | switch | `economy_consequences` | Consequences of not paying: fees, a penalty rate, collections, repossession, a court claim (off: only your rating is affected) |
| 379 | selector | `economy_level` | How hard not paying hits (the presets set this) |

## Brands and franchises   (`economy_brands`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 477 | switch | `economy_brands` | Brands and franchises (off: no brand effects, no franchise money and no applicants; franchised shops trade on as the city's own) |

## The city's economy   (`economy_city`, 2 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 397 | switch | `economy_city` | The city's economy (off: the banks' own rates, the game's own prices and customers, nothing adjusted) |
| 398 | selector | `economy_city_level` | How far the economy swings (the presets set this) |

## Cleaning service   (`economy_cleaning`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 433 | switch | `economy_cleaning` | Cleaning service: cleaners shared between your businesses on a daily round, and cleaning contracts with other businesses (the Services app) |

## Running a business   (`economy_company`, 8 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 425 | switch | `economy_company` | Running a business (off: no staff market, insurance, campaigns, return or licences, and nothing is charged) |
| 426 | switch | `economy_company_staff` | Staff: the going rate, raises, rivals' offers, strikes, overtime and sick pay |
| 427 | switch | `economy_company_cover` | Business insurance: policies, premiums and claims |
| 428 | switch | `economy_company_marketing` | Marketing: campaigns with a cost, an end and a report |
| 429 | switch | `economy_company_tax` | Tax: a quarterly return to file, and an accountant |
| 430 | switch | `economy_company_hide` | Cash sales kept off the books (off: none can be set and nothing is hidden) |
| 431 | switch | `economy_company_licences` | Licences and permits: food hygiene, alcohol, late opening and signage |
| 435 | selector | `economy_company_level` | How strict running a business is (the presets set this) |

## Property   (`economy_estate`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 450 | switch | `economy_estate` | Property: buying, letting and an agency (off: every building's value and rent is the game's own) |
| 451 | switch | `economy_estate_hotel` | Owning a hotel (off: a hotel you hold stands still, and takes and pays nothing) |
| 452 | switch | `economy_estate_hire` | Car hire (off: no car goes out, and a car that is out is called back) |

## Online shops   (`economy_online`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 463 | switch | `economy_online` | Online shops: a business or a warehouse sells its stock to the whole city, with the Web shop app (off: no orders come and nothing is charged or paid) |

## Staff rota planner   (`economy_rota`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 432 | switch | `economy_rota` | Staff rota planner: a customer forecast, advised opening hours and a rota at a press (the Company app's Rota tab) |

## Selling a business   (`economy_sale`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 434 | switch | `economy_sale` | Selling a business: what it is worth, offers from buyers and a quick sale (the Services app) |

## City events: fires, weather, street events   (`events`, 6 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 247 | switch | `city_fires` | Fires: cars, bins and shopfronts catch fire now and then, and the fire service comes |
| 248 | slider | `fire_frequency` | Fires: how often (1 = rare, 10 = one every eight minutes or so) |
| 249 | switch | `weather_matters` | Weather that matters: wet roads, mist, storms, heat waves and cold snaps change driving, traffic, accidents, crime and energy |
| 250 | slider | `weather_frequency` | Weather: how often summer brings a heat wave and winter a cold snap (1 = hardly ever, 10 = nearly half the days) |
| 251 | switch | `street_events` | Street events: markets, parades, protests, fun runs, film shoots and fireworks, announced the day before |
| 252 | slider | `street_event_frequency` | Street events: how many days have one (1 = one in twelve, 10 = four in five; the Saturday market and fireworks nights are fixed) |

## Family   (`family`, 9 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 515 | switch | `family` | Family life: a partner who moves in, an engagement and a wedding, children, relatives |
| 516 | switch | `family_at_home` | Show your partner at home as a character (off: they still live with you, as notices and conversations) |
| 517 | switch | `family_wedding` | Engagement and wedding: rings, the proposal, planning and the day itself |
| 518 | switch | `family_children` | Children: deciding together, pregnancy or adoption, growing up |
| 519 | switch | `family_relatives` | Relatives: Mom and the in-laws react, drop by and invite you to holiday dinners |
| 520 | switch | `family_friction` | Hard times: chores, arguments, time apart, divorce (off: none of these ever happen) |
| 521 | selector | `family_interest` | 'Someone special' introduces you to |
| 522 | slider | `family_year_weeks` | In-game weeks per year of a child's age |
| 531 | button |  | Open the Family page |

## Custom shop and two-wheelers   (`garage`, 7 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 316 | switch | `garage` | Custom shop at repair garages (drive a car of yours into a repair bay) |
| 317 | switch | `garage_cosmetics` | Show paint, lamp tints, underglow and fitted parts on your cars |
| 318 | switch | `garage_tuning` | Performance parts take effect |
| 319 | switch | `garage_underglow` | Underglow kits light up after dark |
| 320 | switch | `garage_police` | A straight-through exhaust or riding without a helmet can be fined |
| 321 | switch | `two_wheelers` | Bicycles and motorbikes (bike-share docks, racks outside supermarkets, stands at car dealerships) |
| 325 | button |  | My vehicles: every build, and where each two-wheeler stands |

## Home life and health   (`home`, 11 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 152 | switch | `home_life` | Home life and health |
| 153 | switch | `home_needs` | Needs: hygiene, stress and a tidy home |
| 154 | switch | `home_strip` | Show hygiene, stress and home as bars under the game's own (at home, or when a need runs low) |
| 155 | switch | `home_easy` | Easy needs: needs wear down half as fast, illness is half as likely |
| 156 | switch | `home_routines` | Home routines: cook, make coffee, tidy up, floor exercises |
| 157 | switch | `home_sleep` | Sleep quality and sleep debt |
| 158 | switch | `home_morning` | Morning summary after a night's sleep |
| 159 | switch | `home_illness` | Illness (cold, flu, food poisoning, back pain, burnout) and the doctor at the hospital |
| 160 | switch | `home_fitness` | Fitness and diet |
| 161 | switch | `home_body` | Body changes slowly with exercise and food (on waking) |
| 162 | switch | `home_jog` | Going for a run |

## Roommates   (`home_roommates`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 534 | switch | `home_roommates` | Roommates: share your flat and the rent (off: a roommate who lives in is put away and comes back when this is on again) |

## Jobs   (`jobs`, 2 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 111 | switch | `jobs` | Jobs you can take yourself (call the Gig Line on your phone) |
| 112 | slider | `job_pay` | Job pay (% of normal) |

## Courts and prison   (`justice`, 9 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 331 | switch | `justice` | Courts: an arrest or a serious charge opens a case (bail, a court date, a verdict, a sentence) |
| 332 | switch | `justice_lawyers` | The city's law firms can be retained (off: the public defender or yourself) |
| 333 | switch | `justice_courthouse` | Hearings are attended in person at the courthouse (off: by video link from the Legal file) |
| 334 | switch | `justice_no_prison` | No prison: fines and custody only |
| 335 | slider | `justice_prison_cap` | Longest prison term (in-game days) |
| 336 | switch | `justice_community` | Community service is a played shift (off: it becomes a fine) |
| 337 | switch | `justice_civil` | People you crash into may sue (one claim at a time) |
| 338 | switch | `justice_aftermath` | Appeals, record sealing and compensation for a wrongful arrest |
| 339 | switch | `justice_phone` | Legal contact on the phone (opens the Legal file) |

## Police stations, custody and your record   (`law`, 6 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 185 | switch | `law_precincts` | Police stations: one in each district, with its sign, patrol cars and officers at the door |
| 186 | switch | `law_custody` | Custody: an arrest ends in a holding cell at the nearest police station (off: a time skip where you were caught) |
| 187 | switch | `law_arrests` | Arrests for serious offences: a death on the road even if you stop, a fifth 911 call about nothing in one day, and being wanted after you get away |
| 188 | switch | `law_reputation` | Record and reputation have effects: more police scrutiny with a long record, thanks from passers-by with a good name |
| 189 | switch | `law_station` | Walk-in police stations: go in through the door to the front desk, waiting area and holding cell (off: the front desk answers at the door) |
| 190 | switch | `law_map_markers` | Police stations on the map: their markers, their names and the "Police stations" map filter |

## Leisure, clothes and photo mode   (`leisure`, 17 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 217 | switch | `leisure` | Leisure: things to do for fun, clothes to buy, night life, city sounds, photo mode |
| 218 | switch | `leisure_chess` | Street chess: a chess player on the pavement by day (needs City life) |
| 219 | switch | `leisure_fishing` | Fishing at the waterfront |
| 220 | switch | `leisure_night_out` | Night out: dance floor and bar in nightclubs (drink-driving is an offence) |
| 221 | switch | `leisure_sights` | Sightseeing: landmarks to take in and tick off |
| 222 | switch | `leisure_cinema` | Cinemas and theatres name what is showing |
| 223 | switch | `clothes_shops` | Clothes rack in clothing stores and the mod's clothes at your wardrobe |
| 224 | switch | `clothes_must_be_bought` | Clothes must be bought (kept per save) |
| 225 | switch | `night_life` | Night life: two people outside an open club, cinema or theatre at night (needs City life) |
| 226 | switch | `ambient_sound` | City sounds: birds, crickets, distant traffic, crowd murmur |
| 227 | slider | `ambient_birds` | Birds by day (volume %) |
| 228 | slider | `ambient_night` | Crickets at night (volume %) |
| 229 | slider | `ambient_city` | Distant traffic hum (volume %) |
| 230 | slider | `ambient_crowd` | Crowd murmur near people (volume %) |
| 231 | switch | `radio_hud` | Name the radio station and song on screen while driving |
| 232 | switch | `photo_mode` | Photo mode |
| 244 | button |  | Show my leisure records and landmarks |

## Driving licences   (`licences`, 5 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 354 | switch | `driving_licence` | Driving needs a licence (off: anyone may drive anything, as in the base game) |
| 355 | switch | `licence_start_with` | Start with a driving licence (new saves that own no vehicle) |
| 356 | switch | `driving_school` | Driving school: desks at the school buildings, theory test, lessons, driven test |
| 357 | switch | `licence_points` | Penalty points: booked offences go on the licence, twelve suspend it |
| 358 | switch | `roadside_checks` | Roadside checks: now and then a patrol car pulls you over to see your licence |

## Life events   (`life`, 2 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 47 | switch | `life_events` | Random life events |
| 48 | slider | `life_frequency` | Life events per week (about) |

## Minimap and GPS   (`navigation`, 7 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 486 | switch | `nav_minimap` | Minimap |
| 487 | switch | `nav_gps` | GPS route to the destination (off: the game's marker only) |
| 488 | switch | `nav_other_route` | Use the other mod's route line (off: this mod's route, on the minimap only) |
| 489 | switch | `nav_rotate` | Minimap turns with you (off: north is up) |
| 490 | slider | `nav_size` | Minimap size |
| 491 | slider | `nav_zoom` | Minimap zoom (higher is closer) |
| 492 | switch | `nav_base` | Buildings and water on the minimap (off: roads only) |

## People and police on the street   (`people`, 4 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 53 | slider | `police_presence` | Police presence: parked patrol cars, officers on the beat and at junctions (0 = none) |
| 56 | switch | `people` | People at scenes and on the street (drivers, paramedics, officers on foot) |
| 57 | switch | `patrol_officers` | Police officers walk the beat near you |
| 58 | slider | `patrol_frequency` | Officers on the beat: how often |

## Phone   (`phone`, 2 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 65 | switch | `phone_contacts` | Extra phone contacts you can call (applies on next load) |
| 66 | switch | `phone_skin` | Modern phone look (body, wallpaper, status bar) |

## Planner   (`planner`, 2 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 370 | switch | `planner` | Planner app on the phone |
| 371 | switch | `planner_reminders` | Planner reminders (off: none are sent) |

## Ragdolls and people hit by cars   (`ragdoll`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 59 | switch | `ragdolls` | Ragdolls and pedestrian collisions (people you hit fall, and can be hurt or killed) |
| 60 | switch | `player_knockdown` | Traffic can knock you down when you are on foot (needs ragdolls) |
| 61 | switch | `ped_incidents` | Pedestrians struck by traffic nearby (needs ragdolls; uses the accident frequency) |

## Social life and the City Ledger   (`social`, 8 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 271 | switch | `social` | People in your life: family, friends, a partner (their contacts and the People contact on the phone) |
| 272 | switch | `social_meetings` | New people come up to you (after the gym, a shift, a coffee at the cart, a good turn) |
| 273 | switch | `social_relationships` | Relationships fade with neglect and can be damaged (off: nothing is ever lost) |
| 274 | switch | `social_outings` | Outings: make plans and keep them (coffee, a meal, drinks, the cinema, a walk, a drive, a workout, your shop) |
| 275 | switch | `social_contact` | People get in touch themselves: invitations, favours, news, a visit at your shop |
| 276 | switch | `social_texts` | Text messages from people |
| 277 | slider | `social_texts_max` | Most texts in a day, from everybody together |
| 278 | switch | `social_newspaper` | City Ledger: a morning paper on the phone, written from what happened |

## Horn and reckless drivers   (`streets`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 62 | switch | `horn_reactions` | People react to the horn (in the wrong they hurry, in the right they stop and glare, others look round) |
| 63 | switch | `natural_accidents` | Reckless drivers and natural accidents (accidents come from drivers you can watch go wrong; off: set up out of view by a roll) |
| 64 | switch | `solid_streets` | Traffic sees people in the road (cars brake for them, sound the horn and wait; off: cars drive through people) |

## Taxis by phone   (`taxi`, 1 row)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 203 | switch | `transit_taxi_call` | Call a taxi from the phone |

## Traffic accidents   (`traffic`, 6 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 49 | switch | `crash_consequences` | Your own car crashes cost damages and can injure you |
| 50 | switch | `traffic_accidents` | Traffic accidents and emergency response |
| 51 | slider | `accident_frequency` | Traffic accident frequency |
| 52 | switch | `police` | Police (patrol cars at accidents and crimes, officers on the street, traffic stops) |
| 54 | switch | `police_pursuit` | Police chase you if you hit a car or a person and drive off |
| 55 | switch | `siren` | Siren sound on response vehicles |

## Buses, the subway and car ownership   (`transport`, 5 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 200 | switch | `transit_buses` | Bus stops and buses (ride for a flat fare) |
| 201 | switch | `transit_people` | People waiting at bus stops, getting on and off |
| 202 | switch | `transit_subway` | Subway journeys that take time (the interact key at an entrance) |
| 204 | switch | `car_ownership` | Car ownership: servicing, wear, breakdowns, insurance |
| 205 | switch | `car_easy` | Easy car ownership: no fuel use, no breakdowns, no wear |

## Hotels and holidays   (`travel`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 300 | switch | `travel_hotels` | Hotels: eight places to stay, booked at the door |
| 301 | switch | `travel_holidays` | Holidays from travel agencies, and burnout without a break |
| 302 | switch | `travel_extras` | Travel extras: suitcase, insurance, a companion, trade fairs |

## Investors and the market   (`ventures`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 412 | switch | `ventures_investors` | Investors (off: no new offers and no meetings; deals you have carry on) |
| 413 | switch | `ventures_market` | The stock market (off: prices stand still, nothing is traded and no dividend is paid; your shares are kept) |
| 414 | selector | `ventures_level` | How demanding investors are (the presets set this) |

## Visuals   (`visuals`, 6 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 23 | switch | `visuals_enabled` | Visual upgrades |
| 24 | slider | `visuals_saturation` | Colour saturation |
| 25 | slider | `visuals_contrast` | Contrast |
| 26 | slider | `visuals_bloom` | Bloom in % (costs performance, 0 = the game's own) |
| 27 | slider | `visuals_detail` | Detail distance in % (above 100 costs performance) |
| 31 | switch | `character_skin` | Finer skin on your character up close: a fine grain and a calmer shine, most on dark skin (subtle) |

## Controls   (`keys`, 12 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 543 | button |  | Reset the mod's keys to their defaults |
| 544 | key |  | Third-person camera on and off |
| 545 | key |  | Interact: enter a building or car, and the mod's street prompts |
| 546 | key |  | Interact, second key (only while the game offers nothing itself) |
| 547 | key |  | Free the cursor while mouse look is on |
| 548 | key |  | Look behind the car (hold) |
| 549 | key |  | Gentle throttle: half the pedal (hold; needs Smooth throttle) |
| 550 | key |  | Measure performance for 10 seconds (developer diagnostics) |
| 551 | key |  | Run the performance test (developer diagnostics) |
| 552 | key |  | Write the phone's interface to a file (developer diagnostics) |
| 553 | key |  | Write the character report (developer diagnostics) |
| 554 | key |  | Photo mode |

## Performance   (`performance`, 10 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
| 556 | switch | `perf_refraction` | Outdoors, the refraction effect is switched off, as nothing outdoors uses it (no visible change) |
| 557 | switch | `perf_sky_updates` | The sky's reflection and its light on things are refreshed every 2 seconds instead of every frame (no visible change) |
| 558 | switch | `perf_stale_lights` | Street lamps the game has switched off no longer keep their lights enabled (no visible change) |
| 559 | switch | `perf_far_districts` | Scenery more than 1.3 km away is not drawn, and is back before you can see it (no visible change) |
| 560 | switch | `perf_view_radius` | People, cars and small things are drawn only within 300 m in every direction, as they already are straight ahead (buildings and roads unchanged) |
| 561 | switch | `perf_far_props` | Street furniture and parked cars are not drawn beyond 200 m (visible: they appear at 200 m instead of 300 m) |
| 562 | switch | `perf_textures_whole` | Textures are kept whole on the graphics card, so the processor no longer manages their detail (needs a card with 10 GB or more; no visible change, more graphics memory used) |
| 563 | switch | `perf_step_timing` | Measurements time every step of the frame (switch off only if a measurement ever misbehaves) |
| 564 | button |  | Measure performance for 10 seconds |
| 565 | button |  | Run performance test, about 3 minutes |

## Save tools (removing the mod, reset, repair)   (`savetools`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
|  | button |  | Prepare this save for removing the mod |
|  | button |  | Reset the mod's data in this save (the mod stays on) |
|  | button |  | Repair: put back the mod's contacts and moods if they are missing |

## Diagnostics   (`diagnostics`, 3 rows)

| # | Kind | Option id | Label |
| --- | --- | --- | --- |
|  | switch | `dev_diagnostics` | Developer diagnostics: detailed lines in Player.log and the diagnostic keys (switch on to report a problem) |
|  | switch | `show_test_tools` | Show test tools (the test buttons at the bottom of this page) |
|  | button |  | Check the mod's set-up (options, presets, Guide pages) |

## Test tools (shown only with "Show test tools" on), one group per feature

### Tests: Business pressure   (10)

- Test: next rival move at the business I am in (or my first)
- Test: the protection racket makes its demand
- Test: next inspection (health, fire safety, licence)
- Test: next staff trouble (strike threat, theft, argument)
- Test: next supplier trouble (late delivery, surcharge)
- Test: next customer incident (complaint, praise)
- Test: a tax audit
- Test: next opportunity (bulk order, press feature, award)
- Test: open the Business desk
- Test: resolve all (close every issue, undo every footfall change and closure, forget the racket)

### Tests: Camera   (1)

- Test: pretend another mod has the camera (again: end it)

### Tests: Character and wardrobe   (3)

- Test: say what my character's hair is made of
- Test: a strong lamp beyond my head for 20 seconds
- Test: switch the hair finish off and on

### Tests: City life   (6)

- Test: put the next kind of street worker just ahead of you
- Test: put the next kind of everyday scene just ahead of you
- Test: put the next kind of cart (coffee, hot dogs) just ahead of you; its customers walk up
- Test: the next delivery or trade (courier, post, painter, gardener) just ahead of you
- Test: the next leisure scene (chess, tai chi, newspaper, map, pushchair, car, door, queue, umbrellas) just ahead of you
- Test: clear the city life people off the street

### Tests: City government and building sites   (11)

- Test: start an election campaign (posters and boards go up round you)
- Test: a campaign rally near you (out of doors, dry weather)
- Test: election day (first press opens the polls, second press counts the votes)
- Test: bring the next policy on the list into force (each press, the next one)
- Test: take back every city policy
- Test: start a roadworks site by the nearest building (on a street with traffic)
- Test: start a scaffolding site on the nearest building front
- Test: start a shop refit (hoarding) on the nearest building front
- Test: start utility works at the kerb by the nearest building
- Test: advance all construction sites one working day
- Test: clear civic life off the street and empty the construction programme

### Tests: Crime and police stops   (6)

- Test: street incident
- Test: a traffic offence by another driver
- Test: a crime in a shop (stand inside a shop)
- Test: a crime against you (on foot, outdoors, carrying at least $60)
- Test: break-in, car broken into, scam call (one per press, in turn)
- Test: get pulled over (while driving)

### Tests: Money and credit   (16)

- Test: show my credit report in the log
- Test: record a missed payment
- Test: record 30 days of payments on time
- Test: set the rating to 560
- Test: set the rating to 600
- Test: set the rating to 760
- Test: clear test entries
- Test: restore the banks' own terms (until the next game hour)
- Test: open a test overdraft, a test card and a test car on finance
- Test: the test accounts miss 1 payment
- Test: the test accounts miss 3 payments
- Test: the collections call about a test account
- Test: repossess the test car
- Test: send the private lender's first text
- Test: work out a debt arrangement and a bankruptcy in the log (nothing is changed)
- Test: put everything back (remove the test accounts and the test car)

### Tests: Brands and franchises   (7)

- Test: make my best shop a brand with high recognition
- Test: an applicant
- Test: sign a franchise in the next district
- Test: a franchisee in trouble
- Test: a scandal at a member shop
- Test: a year passes for the brand
- Test: put brands back

### Tests: The city's economy   (13)

- Test: show the city's economy in the log
- Test: step the city's economy a month
- Test: put the economy in a boom
- Test: make the economy steady
- Test: put the economy in a slowdown
- Test: put the economy in a recession
- Test: a shortage from tomorrow
- Test: a glut from tomorrow
- Test: bank rate up one point
- Test: bank rate down one point
- Test: raise this district's income by 10
- Test: lower this district's income by 10
- Test: put the city's economy back

### Tests: Cleaning service   (3)

- Test: a cleaning round with every business due
- Test: a cleaning contract offer
- Test: a cleaning contract's week ends

### Tests: Running a business   (9)

- Test: a raise request
- Test: a poaching offer
- Test: a strike tomorrow
- Test: a fire claim on a test policy
- Test: a campaign that ends at once, with its report
- Test: a return due now
- Test: an audit of cash off the books (really charges what it finds)
- Test: a licence that lapses now
- Test: put the company back

### Tests: Property   (10)

- Test: a tenant applies
- Test: rent day with a tenant in arrears
- Test: works finish
- Test: an offer for a listed building
- Test: an agency instruction
- Test: a hotel night passes
- Test: a group booking offer
- Test: a hire car goes out, then comes back damaged
- Test: a month passes for the property market
- Test: put property back

### Tests: Online shops   (6)

- Test: open a test web shop on my busiest business
- Test: ten orders
- Test: a late delivery
- Test: a returned order
- Test: a day of orders passes
- Test: put online shops back

### Tests: Staff rota planner   (4)

- Test: open the rota of my busiest business
- Test: show my busiest business's plans in the log
- Test: fill every business's rota
- Test: put every rota back

### Tests: Selling a business   (3)

- Test: an offer for my best business
- Test: a sale of my best business on paper
- Test: a part of a sale price falls due

### Tests: City events: fires, weather, street events   (17)

- Test: a car near you catches fire
- Test: a bin fire on the pavement ahead of you
- Test: a fire at the nearest shopfront
- Test: rain effects (wet roads; the sky stays as it is)
- Test: mist effects
- Test: thunderstorm effects
- Test: heat wave effects
- Test: cold snap effects
- Test: ice effects
- Test: a street market on the road ahead of you
- Test: a farmers' market on the road ahead of you
- Test: a parade on the road ahead of you
- Test: a protest on the road ahead of you
- Test: a charity fun run on the road ahead of you
- Test: a film shoot on the road ahead of you
- Test: fireworks and a crowd on the road ahead of you
- Test: clear any fire or street event, and give the weather back to the game

### Tests: Family   (9)

- Test: partner moves in
- Test: propose
- Test: start wedding planning
- Test: hold the wedding here, now
- Test: start a pregnancy / move it on a step
- Test: start an adoption / move it on a step
- Test: add a year to the children's ages
- Test: pushchair walk
- Test: clear all family data in this save

### Tests: Custom shop and two-wheelers   (7)

- Test: open the custom shop on my current or nearest car
- Test: put a random full build on my current or nearest car (free)
- Test: restore my current or nearest car to stock
- Test: a city bicycle
- Test: a road bicycle
- Test: a motorbike
- Test: a sport motorbike

### Tests: Home life and health   (21)

- Test: open the "How am I doing" page
- Test: open the cooking list
- Test: cook the first dish the fridges allow (needs food in a fridge at home)
- Test: make a coffee (needs a coffee machine at home)
- Test: tidy up the flat
- Test: stretch
- Test: do push-ups
- Test: do sit-ups
- Test: go for a run
- Test: stop the run
- Test: see the doctor (free)
- Test: give me a cold
- Test: give me the flu
- Test: give me food poisoning
- Test: give me back pain
- Test: give me a burnout
- Test: cure everything
- Test: fitness +10
- Test: fitness -10
- Test: run all needs down
- Test: fill all needs

### Tests: Roommates   (7)

- Test: three people answer a roommate notice
- Test: a roommate moves in
- Test: rent day, and they are short
- Test: a noisy night
- Test: they cooked
- Test: they give notice
- Test: put roommates back

### Tests: Jobs   (15)

- Test: start handing out flyers (on foot, on a street with people)
- Test: start a parcel round (your car if it is near, else a hired van)
- Test: start a ride-hail shift (your car if it is near, else a hired van)
- Test: start a cleaning job at a business nearby (on foot)
- Test: start stocking shelves at a shop nearby (on foot)
- Test: start a drinks cart shift (on foot, on a pavement with room)
- Test: start busking (on foot, on a pavement)
- Test: stop the current job
- Test: sign on with the next employer (cycles through all six)
- Test: work a shift at my current rank (goes on no record)
- Test: hold my performance review
- Test: promote me one rank
- Test: start a dispatcher shift
- Test: start a shift in charge
- Test: book a set at a nearby venue

### Tests: Courts and prison   (13)

- Test: open a case against me (evading the police)
- Test: open a case against me (hit-and-run with an injury)
- Test: open a case against me (fatal hit-and-run)
- Test: open a case against me with no evidence (wrongful arrest)
- Test: advance my next case to its court date
- Test: hearing of my next case (by video link)
- Test: send me to prison for 3 days (the days really pass)
- Test: send me to prison for the longest term
- Test: parole hearing (the last morning of a 3-day term)
- Test: somebody sues me over a crash
- Test: community service shift
- Test: open the Legal file
- Test: clear everything (cases, debts, record, sentences)

### Tests: Police stations, custody and your record   (7)

- Test: arrest me (minor: fled a traffic stop)
- Test: arrest me (serious: hit-and-run with an injury)
- Test: arrest me (fatal hit-and-run)
- Test: show the nearest police station
- Test: open the front desk of the nearest police station
- Test: set up the nearest police station again (details in Player.log)
- Test: clear my record and reset my reputation

### Tests: Leisure, clothes and photo mode   (12)

- Test: street chess (sets a chess player up just ahead of you)
- Test: fishing (stand at the water's edge, facing the water)
- Test: fishing at the nearest water (takes you there)
- Test: dancing (inside a nightclub, on the dance floor)
- Test: order a drink (inside a nightclub, at the bar)
- Test: take in the view (at a landmark)
- Test: open the clothes rack
- Test: open the wardrobe
- Test: people outside the nearest open club, cinema or theatre
- Test: play each city sound layer in turn
- Test: photo mode (starts when you close this menu)
- Test: stop the activity in progress

### Tests: Driving licences   (10)

- Test: show my licence card
- Test: grant / revoke the car licence
- Test: grant / revoke the commercial licence
- Test: grant / revoke the taxi endorsement
- Test: grant / revoke the motorcycle licence
- Test: driving theory test (starts when this page is closed)
- Test: practical driving test (car)
- Test: practical driving test (van, reversing)
- Test: roadside licence check (while driving)
- Test: put 4 penalty points on my licence

### Tests: Life events   (1)

- Test: life event

### Tests: Minimap and GPS   (6)

- Test: route to the nearest police station
- Test: route to the far end of the city
- Test: clear the destination
- Test: minimap, buildings and water alone
- Test: minimap, mark three buildings
- Test: minimap, measure the city again

### Tests: Planner   (2)

- Test: fill the Planner with one of everything
- Test: clear the Planner's samples

### Tests: Ragdolls and people hit by cars   (2)

- Test: knock down the nearest pedestrian
- Test: pedestrian struck by traffic

### Tests: Social life and the City Ledger   (20)

- Test: somebody new comes up to you
- Test: choose the next person for the buttons below
- Test: move the chosen person one stage closer
- Test: the chosen person rings you
- Test: the chosen person texts you
- Test: the chosen person asks a favour (in turn: a lift, a loan, help moving)
- Test: the chosen person drops by
- Test: a walk with Mom (needs pavement within 8 m)
- Test: coffee with the chosen person
- Test: a meal with the chosen person
- Test: drinks with the chosen person
- Test: the cinema with the chosen person
- Test: a walk with the chosen person
- Test: a drive with the chosen person (needs your car)
- Test: a workout with the chosen person
- Test: show the chosen person your business
- Test: give the chosen person a lift (needs your car)
- Test: help the chosen person move
- Test: print today's City Ledger again
- Test: stop what is going on and drop the plan

### Tests: Horn and reckless drivers   (13)

- Test: a speeder near me
- Test: a red-light runner at the next junction
- Test: a drunk driver near me
- Test: a jaywalker in front of the next car
- Test: a reckless car that will hit someone
- Test: a reckless car that will crash
- Test: a patrol car that sees a speeder
- Test: a pedestrian crosses in front of the next car, with room
- Test: a pedestrian steps out close
- Test: a pedestrian dawdles in the lane
- Test: a pedestrian steps out too late
- Test: ten people cross this road
- Test: show what the cars sense

### Tests: Taxis by phone   (3)

- Test: call City Cabs
- Test: bring a taxi at once (it may start in view)
- Test: cancel the taxi

### Tests: Traffic accidents   (4)

- Test: traffic accident (random severity)
- Test: minor accident
- Test: moderate accident
- Test: severe accident

### Tests: Buses, the subway and car ownership   (8)

- Test: a stop's panel and a shop's box together (again: take them down)
- Test: set up the nearest bus stop
- Test: send a bus to the nearest bus stop
- Test: ride the bus from the nearest bus stop, two stops on
- Test: subway journey from the nearest station
- Test: set the fuel of the car you are driving to 5%
- Test: break down the car you are driving
- Test: show the Garage page

### Tests: Hotels and holidays   (12)

- Test: check in at the nearest hostel
- Test: check in at the nearest mid-range hotel
- Test: check in at the nearest luxury hotel
- Test: go up to my room
- Test: start the Hamptons weekend
- Test: start the coast cruise
- Test: start the ski trip
- Test: start the Miami beach week
- Test: start the Europe tour
- Test: start the trade fair trip
- Test: return from the trip
- Test: clear room, ticket and trip

### Tests: Investors and the market   (9)

- Test: a tycoon's offer for my best business
- Test: a meeting with an angel investor
- Test: a meeting with a tycoon
- Test: a meeting with a venture firm
- Test: list the investors and companies in the log
- Test: step the market 30 days
- Test: pay a dividend
- Test: an investor calls their money
- Test: put ventures back

### Tests: Performance   (1)

- Test: write the session record's lines now

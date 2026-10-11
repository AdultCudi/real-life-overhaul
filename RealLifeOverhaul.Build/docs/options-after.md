# The options page after the rework

Every group, section and row in the order of the page, as Meta/OptionsTree.cs declares it, with the label it had before beside the label it has now. A row with "needs" is greyed, with the reason after its label, while what it needs is off. Each option also has one sentence, shown by the game's tooltip when the pointer rests on its row. Made by a script from the sources and en.json; compare [options-before.md](options-before.md).

The tree: 10 groups, 59 sections, 216 options and 31 buttons of the player's. Test buttons: 308, under Troubleshooting only.

## Start here   (its rows stand straight under its button)

### The Guide   (`guide`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| button |  | Open the Guide on your phone | Open the Guide on your phone (what the mod does, controls, numbers, troubleshooting) |  |  |

### Presets   (`presets`, 4 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| button |  | Relaxed: a living city, little happens to you | Preset: Relaxed (nothing is done to you; fewer incidents) |  |  |
| button |  | Standard: the mod as it ships | Preset: Standard (the mod's defaults) |  |  |
| button |  | Realistic: more incidents and more police | Preset: Realistic (more incidents, more police) |  |  |
| button |  | Hardcore: everything strict, lower job pay | Preset: Hardcore (everything at the strict end, lower job pay) |  |  |

### Your choices   (`choices`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| button |  | Reset every option and key | Reset every option and key of the mod to its default |  |  |
| button |  | Save my choices to a file | Export my settings to a text file |  |  |
| button |  | Load my choices from the file | Import settings from the text file |  |  |

## Your character and home

### Needs and wellbeing   (`needs`, 7 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `home_life` | Home life and health | Home life and health |  | Needs, routines at home, sleep, illness and fitness. Off, none of it runs. |
| switch | `home_needs` | Hygiene, stress and a tidy home | Needs: hygiene, stress and a tidy home | `home_life` | Hygiene, stress and the state of your home wear down and have to be looked after. |
| switch | `home_strip` | Bars for hygiene, stress and home | Show hygiene, stress and home as bars under the game's own (at home, or when a need runs low) | `home_life` | Three more bars appear under the game's own, at home or when a need runs low. |
| switch | `home_easy` | Easy needs | Easy needs: needs wear down half as fast, illness is half as likely | `home_life` | Needs wear down half as fast and illness is half as likely. |
| switch | `home_sleep` | Sleep quality and sleep debt | Sleep quality and sleep debt | `home_life` | How well you slept counts, and sleep you miss has to be caught up. |
| switch | `home_morning` | Morning summary after a night's sleep | Morning summary after a night's sleep | `home_life` | After a night's sleep a short summary says how you are. |
| switch | `home_illness` | Illness and the doctor | Illness (cold, flu, food poisoning, back pain, burnout) and the doctor at the hospital | `home_life` | You can catch a cold or the flu, get food poisoning, back pain or burnout, and see the doctor at the hospital. |
| button |  | About this section | (new) |  | Opens the Guide page of `home`. |

### Home routines and fitness   (`routines`, 4 rows, every row needs `home_life`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `home_routines` | Cooking, coffee, tidying and floor exercises | Home routines: cook, make coffee, tidy up, floor exercises |  | At home you can cook, make coffee, tidy up and exercise on the floor. |
| switch | `home_fitness` | Fitness and diet | Fitness and diet |  | Exercise and what you eat make you fitter or less fit. |
| switch | `home_body` | Your body changes with exercise and food | Body changes slowly with exercise and food (on waking) |  | Your character's build changes slowly with exercise and food, when you wake up. |
| switch | `home_jog` | Going for a run | Going for a run |  | You can go for a run from home. |
| button |  | About this section | (new) |  | Opens the Guide page of `home`. |

### Looks and hair   (`looks`, 5 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `character_new_looks` | The mod's clothes, shoes and fuller scalp | New looks: the mod's clothes and shoes, and a hair-coloured scalp under your hair so that it does not look thin from a distance |  | The mod's clothes and shoes can be worn, and hair no longer looks thin from a distance. |
| switch | `character_high_detail` | Detailed player character in the city | High-detail player character (uses the game's own detailed model in the city) |  | Your character uses the game's detailed model in the city. This changes the picture. |
| switch | `character_hair` | Fuller hair and beards | Fuller hair and beards on detailed characters |  | Hair and beards on detailed characters are thicker. This changes the picture. |
| switch | `character_skin` | Finer skin up close | Finer skin on your character up close: a fine grain and a calmer shine, most on dark skin (subtle) |  | A fine grain and a calmer shine on your character's skin when the camera is close. |
| switch | `character_lighting` | Softer light in character creation and shops | Softer lighting in character creation and shop previews |  | Faces are lit more softly in character creation and in shop previews. |
| button |  | About this section | (new) |  | Opens the Guide page of `character`. |

### Clothes (men)   (`outfit_m`, 5 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| selector | `look2_top_m` | Top (men) | Top (men) |  | The top a male character wears. |
| selector | `look2_top_m_colour` | Top colour (men) | Top colour (men; a print the garment cannot carry shows as a plain colour) |  | Its colour. A print the top cannot carry shows as a plain colour. |
| selector | `look2_legs_m` | Trousers (men) | Trousers (men) |  | The trousers a male character wears. |
| selector | `look2_legs_m_colour` | Trousers colour (men) | Trousers colour (men; a print the garment cannot carry shows as a plain colour) |  | Their colour. A print the trousers cannot carry shows as a plain colour. |
| selector | `look_feet_m` | Shoes (men) | Shoes (men) |  | The shoes a male character wears. |
| button |  | About this section | (new) |  | Opens the Guide page of `character`. |

### Clothes (women)   (`outfit_f`, 7 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| selector | `look2_dress_f` | Dress (women) | Dress (women; replaces top and trousers) |  | A dress for a female character. It replaces the top and the trousers. |
| selector | `look2_dress_f_colour` | Dress colour (women) | Dress colour (women; a print the garment cannot carry shows as a plain colour) |  | Its colour. A print the dress cannot carry shows as a plain colour. |
| selector | `look2_top_f` | Top (women) | Top (women) |  | The top a female character wears. |
| selector | `look2_top_f_colour` | Top colour (women) | Top colour (women; a print the garment cannot carry shows as a plain colour) |  | Its colour. A print the top cannot carry shows as a plain colour. |
| selector | `look2_legs_f` | Trousers or skirt (women) | Trousers or skirt (women) |  | The trousers or skirt a female character wears. |
| selector | `look2_legs_f_colour` | Trousers or skirt colour (women) | Trousers or skirt colour (women; a print the garment cannot carry shows as a plain colour) |  | Their colour. A print the garment cannot carry shows as a plain colour. |
| selector | `look_feet_f` | Shoes (women) | Shoes (women) |  | The shoes a female character wears. |
| button |  | About this section | (new) |  | Opens the Guide page of `character`. |

### Clothes shops   (`shops`, 2 rows, every row needs `leisure`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `clothes_shops` | Clothes rack in clothing stores | Clothes rack in clothing stores and the mod's clothes at your wardrobe |  | Clothing stores have a rack to buy from, and your wardrobe holds the mod's clothes. |
| switch | `clothes_must_be_bought` | Clothes must be bought | Clothes must be bought (kept per save) |  | You can only wear the mod's clothes you have bought. This is kept for each save. |
| button |  | About this section | (new) |  | Opens the Guide page of `leisure`. |

### Roommates   (`roommates`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `home_roommates` | A roommate who shares the rent | Roommates: share your flat and the rent (off: a roommate who lives in is put away and comes back when this is on again) |  | A roommate can move in and pay part of the rent. Off, a roommate who lives in is put away until this is on again. |
| button |  | About this section | (new) |  | Opens the Guide page of `home_roommates`. |

### Family   (`family`, 9 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `family` | A partner, a wedding, children, relatives | Family life: a partner who moves in, an engagement and a wedding, children, relatives |  | A partner can move in, and an engagement, a wedding, children and relatives follow. |
| switch | `family_at_home` | Your partner appears at home | Show your partner at home as a character (off: they still live with you, as notices and conversations) | `family` | Your partner is seen at home as a character. Off, they still live with you, in notices and conversations. |
| switch | `family_wedding` | Engagement and wedding | Engagement and wedding: rings, the proposal, planning and the day itself | `family` | Rings, the proposal, the planning and the day itself. |
| switch | `family_children` | Children | Children: deciding together, pregnancy or adoption, growing up | `family` | You can decide together to have a child, by pregnancy or adoption, and watch them grow up. |
| switch | `family_relatives` | Relatives visit and invite you | Relatives: Mom and the in-laws react, drop by and invite you to holiday dinners | `family` | Mom and the in-laws react to your news, drop by and invite you to holiday dinners. |
| switch | `family_friction` | Arguments, time apart and divorce | Hard times: chores, arguments, time apart, divorce (off: none of these ever happen) | `family` | Chores, arguments, time apart and divorce can happen. Off, none of them ever do. |
| selector | `family_interest` | Who 'Someone special' introduces you to | 'Someone special' introduces you to | `family` | Whether the people 'Someone special' introduces are women, men or either. |
| slider | `family_year_weeks` | Weeks per year of a child's age | In-game weeks per year of a child's age | `family` | How many in-game weeks pass for a child to be a year older. |
| button |  | Open the Family page | Open the Family page |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `family`. |

### Friends and social life   (`social`, 8 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `social` | Family, friends and a partner | People in your life: family, friends, a partner (their contacts and the People contact on the phone) |  | You have family, friends and perhaps a partner, each with a contact on the phone. |
| switch | `social_meetings` | New people come up to you | New people come up to you (after the gym, a shift, a coffee at the cart, a good turn) | `social` | Somebody new comes up to you after the gym, a shift, a coffee or a good turn. |
| switch | `social_relationships` | Relationships fade if neglected | Relationships fade with neglect and can be damaged (off: nothing is ever lost) | `social` | People drift away if you neglect them, and a relationship can be damaged. Off, nothing is ever lost. |
| switch | `social_outings` | Outings with people | Outings: make plans and keep them (coffee, a meal, drinks, the cinema, a walk, a drive, a workout, your shop) | `social` | You can make plans with people and keep them: coffee, a meal, the cinema, a walk, a drive. |
| switch | `social_contact` | People get in touch themselves | People get in touch themselves: invitations, favours, news, a visit at your shop | `social` | People invite you, ask favours, bring news and visit your shop. |
| switch | `social_texts` | Text messages from people | Text messages from people | `social` | People send you text messages. |
| slider | `social_texts_max` | Most texts in a day | Most texts in a day, from everybody together | `social`, `social_texts` | The most texts you get in one day, from everybody together. |
| switch | `social_newspaper` | City Ledger morning paper | City Ledger: a morning paper on the phone, written from what happened | `social` | A morning paper on the phone, written from what happened in the city. |
| button |  | About this section | (new) |  | Opens the Guide page of `social`. |

### Leisure   (`leisure`, 9 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `leisure` | Things to do for fun | Leisure: things to do for fun, clothes to buy, night life, city sounds, photo mode |  | Chess, fishing, nights out, sightseeing and photo mode. Off, none of them can be started. |
| switch | `leisure_chess` | Street chess | Street chess: a chess player on the pavement by day (needs City life) | `leisure`, `city_life` | A chess player sits on the pavement by day, and you can play. |
| switch | `leisure_fishing` | Fishing at the waterfront | Fishing at the waterfront | `leisure` | You can fish at the water's edge. |
| switch | `leisure_night_out` | Dancing and drinks in nightclubs | Night out: dance floor and bar in nightclubs (drink-driving is an offence) | `leisure` | Nightclubs have a dance floor and a bar. Driving after drinks is an offence. |
| switch | `leisure_sights` | Sightseeing at landmarks | Sightseeing: landmarks to take in and tick off | `leisure` | Landmarks can be taken in and ticked off. |
| switch | `leisure_cinema` | Cinemas and theatres say what is showing | Cinemas and theatres name what is showing | `leisure` | Cinemas and theatres name the film or play that is on. |
| switch | `night_life` | People outside clubs and cinemas at night | Night life: two people outside an open club, cinema or theatre at night (needs City life) | `leisure`, `city_life` | Two people stand outside an open club, cinema or theatre at night. |
| switch | `photo_mode` | Photo mode | Photo mode | `leisure` | The photo mode key stops the game for a picture. |
| button |  | My leisure records and landmarks | Show my leisure records and landmarks |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `leisure`. |

### Life events   (`lifeevents`, 2 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `life_events` | Random life events | Random life events |  | Now and then something happens to you: a windfall, a cold, a call from Mom. |
| slider | `life_frequency` | Life events per week | Life events per week (about) | `life_events` | About how many life events come in a week. |
| button |  | About this section | (new) |  | Opens the Guide page of `life`. |

### Your phone   (`phone`, 2 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `phone_contacts` | The mod's phone contacts | Extra phone contacts you can call (applies on next load) |  | The phone gets the mod's contacts: 911, the doctor, a friend, Mom and the rest. This applies the next time a save is loaded. |
| switch | `phone_skin` | Modern phone look | Modern phone look (body, wallpaper, status bar) |  | The phone gets a modern body, wallpaper and status bar. |
| button |  | About this section | (new) |  | Opens the Guide page of `phone`. |

## The city

### Street life   (`citylife`, 5 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `city_life` | People working and out on the pavements | City life: people at work and going about their day on the pavements |  | People work and go about their day on the pavements. There are never more than 8 at once. |
| slider | `city_density` | How much is going on (1 to 10) | City life: how much is going on (1 = now and then, 10 = always something; never more than 8 people) | `city_life` | 1 is now and then, 10 is always something. |
| switch | `city_workers` | People at work | City life: people at work (sweepers, road crews, window cleaners, leaflets, carts, couriers, the post, painters, gardeners) | `city_life` | Sweepers, road crews, window cleaners, couriers, painters and gardeners are at work. |
| switch | `city_everyday` | Everyday people | City life: everyday people (friends talking, smokers, phone calls, tourists, joggers, chess, pushchairs, queues, umbrellas in the rain) | `city_life` | Friends talk, people smoke, phone, jog, queue and put up umbrellas in the rain. |
| switch | `city_interactions` | Coffee from the coffee cart | City life: you can buy a coffee at the coffee cart (the interact key, at the counter) | `city_life` | You can buy a coffee at the coffee cart with the interact key. |
| button |  | About this section | (new) |  | Opens the Guide page of `citylife`. |

### Events and weather   (`events`, 6 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `city_fires` | Fires and the fire service | Fires: cars, bins and shopfronts catch fire now and then, and the fire service comes |  | Cars, bins and shopfronts catch fire now and then, and the fire service comes. |
| slider | `fire_frequency` | How often fires break out (1 to 10) | Fires: how often (1 = rare, 10 = one every eight minutes or so) | `city_fires` | 1 is rare, 10 is one every eight minutes or so. |
| switch | `weather_matters` | Weather affects driving, crime and energy | Weather that matters: wet roads, mist, storms, heat waves and cold snaps change driving, traffic, accidents, crime and energy |  | Wet roads, mist, storms, heat waves and cold snaps change driving, traffic, accidents, crime and your energy. |
| slider | `weather_frequency` | Heat waves and cold snaps (1 to 10) | Weather: how often summer brings a heat wave and winter a cold snap (1 = hardly ever, 10 = nearly half the days) | `weather_matters` | 1 is hardly ever, 10 is nearly half the days of summer and winter. |
| switch | `street_events` | Street events | Street events: markets, parades, protests, fun runs, film shoots and fireworks, announced the day before |  | Markets, parades, protests, fun runs, film shoots and fireworks close a street, announced the day before. |
| slider | `street_event_frequency` | Days with a street event (1 to 10) | Street events: how many days have one (1 = one in twelve, 10 = four in five; the Saturday market and fireworks nights are fixed) | `street_events` | 1 is one day in twelve, 10 is four days in five. The Saturday market and fireworks nights are fixed. |
| button |  | About this section | (new) |  | Opens the Guide page of `events`. |

### Accidents and emergency services   (`accidents`, 8 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `traffic_accidents` | Traffic accidents and emergency services | Traffic accidents and emergency response |  | Other cars crash now and then, and police, ambulances and tow trucks come. |
| slider | `accident_frequency` | How often accidents happen (1 to 10) | Traffic accident frequency | `traffic_accidents` | 1 is rare, 10 is often. |
| switch | `siren` | Sirens on emergency vehicles | Siren sound on response vehicles |  | Police cars, ambulances and fire engines sound their sirens on the way. |
| switch | `people` | People at accidents and on the street | People at scenes and on the street (drivers, paramedics, officers on foot) |  | Drivers get out after a crash, paramedics work, and officers stand at scenes. |
| switch | `crash_consequences` | Your crashes cost money and can injure you | Your own car crashes cost damages and can injure you |  | Crashing your car costs damages and can hurt you. |
| switch | `ragdolls` | People you hit fall and can be hurt | Ragdolls and pedestrian collisions (people you hit fall, and can be hurt or killed) |  | A person your car hits is thrown down and can be hurt or killed. Off, cars pass through people. |
| switch | `player_knockdown` | Traffic can knock you down on foot | Traffic can knock you down when you are on foot (needs ragdolls) | `ragdolls` | A car that hits you on foot throws you down. |
| switch | `ped_incidents` | Traffic can hit pedestrians near you | Pedestrians struck by traffic nearby (needs ragdolls; uses the accident frequency) | `ragdolls` | Now and then a pedestrian near you is hit by a car, and help comes. |
| button |  | About this section | (new) |  | Opens the Guide page of `traffic`. |

### Police   (`police`, 6 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `police` | Police | Police (patrol cars at accidents and crimes, officers on the street, traffic stops) |  | Patrol cars come to accidents and crimes, officers walk the streets, and drivers are pulled over. |
| slider | `police_presence` | Police on the streets (0 to 10) | Police presence: parked patrol cars, officers on the beat and at junctions (0 = none) | `police`, `people` | How many parked patrol cars and officers you see about. 0 is none. |
| switch | `patrol_officers` | Officers on foot patrol near you | Police officers walk the beat near you | `police`, `people` | Police officers on foot pass near you from time to time. |
| slider | `patrol_frequency` | How often officers patrol (1 to 10) | Officers on the beat: how often | `police`, `patrol_officers` | 1 is rare, 10 is often. |
| switch | `police_pursuit` | Police chase you after a hit-and-run | Police chase you if you hit a car or a person and drive off | `police` | If you hit a car or a person and drive off, the police come after you. |
| switch | `player_offences` | Police stop you for traffic offences | Police stop you for red lights, speeding and reckless driving, and warn or fine you for jaywalking | `police` | Red lights, speeding and reckless driving get you stopped, and jaywalking gets a warning or a fine. |
| button |  | About this section | (new) |  | Opens the Guide page of `crime`. |

### Police stations and arrests   (`stations`, 6 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `law_precincts` | Police stations in every district | Police stations: one in each district, with its sign, patrol cars and officers at the door |  | Each district has a police station with its sign, patrol cars and officers at the door. |
| switch | `law_station` | Police stations you can walk into | Walk-in police stations: go in through the door to the front desk, waiting area and holding cell (off: the front desk answers at the door) | `law_precincts` | You can go in to the front desk, the waiting area and the holding cell. Off, the front desk answers at the door. |
| switch | `law_map_markers` | Police stations on the city map | Police stations on the map: their markers, their names and the "Police stations" map filter | `law_precincts` | Police stations have markers, names and their own filter on the city map. |
| switch | `law_custody` | Arrests end in a holding cell | Custody: an arrest ends in a holding cell at the nearest police station (off: a time skip where you were caught) | `law_precincts` | After an arrest you sit in a holding cell at the nearest police station. Off, time passes where you were caught. |
| switch | `law_arrests` | Arrests for serious offences | Arrests for serious offences: a death on the road even if you stop, a fifth 911 call about nothing in one day, and being wanted after you get away |  | A death on the road, repeated 911 calls about nothing, or being wanted after getting away end in an arrest. |
| switch | `law_reputation` | Your record and reputation matter | Record and reputation have effects: more police scrutiny with a long record, thanks from passers-by with a good name |  | A long record brings more police attention, and a good name brings thanks from passers-by. |
| button |  | About this section | (new) |  | Opens the Guide page of `law`. |

### Crime   (`crime`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `street_crime` | Crime near you | Crime near you (street crime, thieves in shops, other drivers pulled over) |  | Street crime, thieves in shops and drivers pulled over happen near you. |
| slider | `crime_frequency` | How often crime happens (1 to 10) | Crime frequency | `street_crime` | 1 is rare, 10 is often. |
| switch | `crime_against_player` | Crimes against you | Crimes against you (pickpockets, muggers, break-ins, scam calls; all capped) |  | Pickpockets, muggers, break-ins and scam calls can target you. What they take is limited. |
| button |  | About this section | (new) |  | Opens the Guide page of `crime`. |

### Courts and prison   (`courts`, 9 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `justice` | Court cases for arrests and serious charges | Courts: an arrest or a serious charge opens a case (bail, a court date, a verdict, a sentence) |  | An arrest or a serious charge opens a case with bail, a court date, a verdict and a sentence. |
| switch | `justice_lawyers` | Law firms you can hire | The city's law firms can be retained (off: the public defender or yourself) | `justice` | You can hire one of the city's law firms. Off, there is the public defender or yourself. |
| switch | `justice_courthouse` | Hearings in person at the courthouse | Hearings are attended in person at the courthouse (off: by video link from the Legal file) | `justice` | You go to the courthouse for a hearing. Off, hearings are held by video link from the Legal file. |
| switch | `justice_no_prison` | No prison, only fines and custody | No prison: fines and custody only | `justice` | No sentence sends you to prison. |
| slider | `justice_prison_cap` | Longest prison term (days) | Longest prison term (in-game days) | `justice` | No prison sentence is longer than this many in-game days. |
| switch | `justice_community` | Community service is a shift you work | Community service is a played shift (off: it becomes a fine) | `justice` | You work a community service sentence yourself. Off, it becomes a fine. |
| switch | `justice_civil` | People you crash into may sue | People you crash into may sue (one claim at a time) | `justice` | Someone you crash into may take you to court for damages, one claim at a time. |
| switch | `justice_aftermath` | Appeals, sealed records and compensation | Appeals, record sealing and compensation for a wrongful arrest | `justice` | You can appeal, have an old record sealed and be paid for a wrongful arrest. |
| switch | `justice_phone` | Legal contact on the phone | Legal contact on the phone (opens the Legal file) | `justice`, `phone_contacts` | A Legal contact on the phone opens your Legal file. |
| button |  | About this section | (new) |  | Opens the Guide page of `justice`. |

### The council   (`council`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `civic` | City government and public works | Civic life: a city government and a construction programme (City Hall on the phone) |  | The city has a government and a programme of public works, with City Hall on the phone. |
| switch | `civic_elections` | Elections and the mayor | Elections and the mayor: campaigns in the street, voting, policies that change the city (off: every policy effect is taken back) | `civic` | Campaigns run in the street, you can vote, and policies change the city. Off, every policy effect is taken back. |
| switch | `civic_construction` | Construction sites | Construction sites: roadworks, scaffolding, shop refits and utility works that progress over days | `civic` | Roadworks, scaffolding, shop refits and utility works appear and progress over days. |
| button |  | About this section | (new) |  | Opens the Guide page of `civic`. |

### City sounds   (`sounds`, 5 rows, every row needs `leisure`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `ambient_sound` | City sounds | City sounds: birds, crickets, distant traffic, crowd murmur |  | Birds, crickets, distant traffic and the murmur of crowds are heard in the city. |
| slider | `ambient_birds` | Birds by day (volume %) | Birds by day (volume %) | `ambient_sound` | How loud the birds are. |
| slider | `ambient_night` | Crickets at night (volume %) | Crickets at night (volume %) | `ambient_sound` | How loud the crickets are. |
| slider | `ambient_city` | Distant traffic (volume %) | Distant traffic hum (volume %) | `ambient_sound` | How loud the far-off traffic is. |
| slider | `ambient_crowd` | Crowd murmur (volume %) | Crowd murmur near people (volume %) | `ambient_sound` | How loud the murmur near people is. |
| button |  | About this section | (new) |  | Opens the Guide page of `leisure`. |

## Getting around

### Camera   (`camera`, 9 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `camera_enabled` | Third-person camera | Third-person camera (off: the game's own camera) |  | The camera follows behind your character. Off, the game's own camera is used. |
| switch | `camera_indoors` | Third-person camera indoors too | Also use it inside buildings | `camera_enabled` | The third-person camera is used inside buildings as well. |
| slider | `camera_pitch` | Camera tilt (degrees) | Camera tilt in degrees (the starting tilt while mouse look is on) | `camera_enabled` | How steeply the camera looks down. With mouse look on, this is the tilt it starts at. |
| slider | `camera_distance` | Camera distance | Camera distance | `camera_enabled` | How far behind your character the camera stands. |
| slider | `camera_fov` | Field of view (degrees) | Field of view | `camera_enabled` | How wide the view is. A higher number shows more and makes things smaller. |
| slider | `camera_height` | Camera aim height (tenths of a metre) | Height the camera looks at (tenths of a metre) | `camera_enabled` | The height on your character that the camera looks at. |
| slider | `camera_follow` | Camera swings behind you (0 is never) | Swing behind you when mouse look is off (0 = off) | `camera_enabled` | With mouse look off, how quickly the camera comes round behind you. |
| switch | `car_camera` | Chase camera when driving | Chase camera when driving (hold the look-behind key to look behind) | `camera_enabled` | A camera that follows behind your car. Hold the look-behind key to look back. |
| slider | `car_camera_distance` | Chase camera distance | Chase camera distance | `camera_enabled`, `car_camera` | How far behind your car the chase camera stands. |
| button |  | About this section | (new) |  | Opens the Guide page of `camera`. |

### Car performance   (`carperf`, 4 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `car_tuning` | Realistic car performance | Realistic car performance: every car accelerates and tops out like its kind (off: the game's own cars) |  | Every car accelerates and tops out like its kind. Off, the game's own cars. |
| slider | `car_top_speed` | Top speed (% of each car's own) | Car top speed (% of each car's own) | `car_tuning` | The top speed of every car, as a share of what its kind really reaches. |
| slider | `car_acceleration` | Acceleration (% of each car's own) | Car acceleration (% of each car's own) | `car_tuning` | How quickly every car gains speed, as a share of what its kind really does. |
| button |  | Measure this car's speed and acceleration | Measure this car (then stop, and hold full throttle for 20 seconds) |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `driving`. |

### At the wheel   (`wheel`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `smooth_throttle` | Smooth throttle | Smooth throttle: the key eases the pedal down, a tap pulls away gently |  | The key eases the pedal down, so a tap pulls away gently. |
| switch | `speedometer` | Speedometer when driving | Speedometer when driving |  | Your speed is shown on screen while you drive. |
| switch | `radio_hud` | Radio station and song on screen | Name the radio station and song on screen while driving |  | The radio station and the song are named on screen while you drive. |
| button |  | About this section | (new) |  | Opens the Guide page of `driving`. |

### Owning a car   (`owning`, 2 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `car_ownership` | Car servicing, wear, breakdowns and insurance | Car ownership: servicing, wear, breakdowns, insurance |  | Your cars need servicing and insurance, wear out and can break down. |
| switch | `car_easy` | Easy car ownership | Easy car ownership: no fuel use, no breakdowns, no wear | `car_ownership` | Your cars use no fuel, never break down and do not wear. |
| button |  | About this section | (new) |  | Opens the Guide page of `transport`. |

### Custom shop   (`garage`, 6 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `garage` | Custom shop at repair garages | Custom shop at repair garages (drive a car of yours into a repair bay) |  | Drive a car of yours into a repair bay to paint it and fit parts. |
| switch | `garage_cosmetics` | Paint, tints and fitted parts show | Show paint, lamp tints, underglow and fitted parts on your cars | `garage` | Paint, lamp tints, underglow and fitted parts are seen on your cars. |
| switch | `garage_tuning` | Performance parts take effect | Performance parts take effect | `garage` | Fitted performance parts change how the car drives. |
| switch | `garage_underglow` | Underglow lights up after dark | Underglow kits light up after dark | `garage` | Underglow kits light the road under the car after dark. |
| switch | `garage_police` | Fines for loud exhausts and no helmet | A straight-through exhaust or riding without a helmet can be fined | `garage` | A straight-through exhaust, or riding without a helmet, can be fined. |
| button |  | My vehicles | My vehicles: every build, and where each two-wheeler stands |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `garage`. |

### Bicycles and motorbikes   (`bikes`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `two_wheelers` | Bicycles and motorbikes | Bicycles and motorbikes (bike-share docks, racks outside supermarkets, stands at car dealerships) | `garage` | Bicycles and motorbikes can be hired, bought and ridden. |
| button |  | About this section | (new) |  | Opens the Guide page of `garage`. |

### Traffic and pedestrians   (`traffic`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `horn_reactions` | People react to your horn | People react to the horn (in the wrong they hurry, in the right they stop and glare, others look round) |  | People in your way hurry or stop and glare when you sound the horn, and others look round. |
| switch | `natural_accidents` | Reckless drivers cause the accidents | Reckless drivers and natural accidents (accidents come from drivers you can watch go wrong; off: set up out of view by a roll) |  | Accidents come from speeders, red-light runners and drunk drivers you can watch go wrong. Off, they are set up out of view. |
| switch | `solid_streets` | Traffic brakes for people in the road | Traffic sees people in the road (cars brake for them, sound the horn and wait; off: cars drive through people) |  | Cars brake for people in the road, sound the horn and wait. Off, cars drive through people. |
| button |  | About this section | (new) |  | Opens the Guide page of `streets`. |

### Buses and subway   (`transit`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `transit_buses` | Bus stops and buses | Bus stops and buses (ride for a flat fare) |  | Buses run between bus stops, and you can ride for a flat fare. |
| switch | `transit_people` | People waiting at bus stops | People waiting at bus stops, getting on and off | `transit_buses` | People wait at bus stops and get on and off. |
| switch | `transit_subway` | Subway journeys take time | Subway journeys that take time (the interact key at an entrance) |  | A subway journey takes time, started with the interact key at an entrance. |
| button |  | About this section | (new) |  | Opens the Guide page of `transport`. |

### Taxi   (`taxi`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `transit_taxi_call` | Taxi by phone | Call a taxi from the phone |  | You can call a taxi to where you stand from the phone. |
| button |  | About this section | (new) |  | Opens the Guide page of `taxi`. |

### Minimap and GPS   (`nav`, 8 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `nav_minimap` | Minimap | Minimap |  | A small map of the streets round you is shown on screen. |
| switch | `nav_rotate` | Minimap turns with you | Minimap turns with you (off: north is up) | `nav_minimap` | The minimap turns so that ahead is up. Off, north is up. |
| slider | `nav_size` | Minimap size (%) | Minimap size | `nav_minimap` | How large the minimap is on screen. |
| slider | `nav_zoom` | Minimap zoom (%) | Minimap zoom (higher is closer) | `nav_minimap` | How close the minimap looks. A higher number is closer. |
| switch | `nav_base` | Buildings and water on the minimap | Buildings and water on the minimap (off: roads only) | `nav_minimap` | The minimap shows buildings and water. Off, roads only. |
| switch | `nav_gps` | GPS route to your destination | GPS route to the destination (off: the game's marker only) |  | A route along the roads is drawn to your destination. Off, the game's marker only. |
| switch | `nav_other_route` | Use the other mod's route line | Use the other mod's route line (off: this mod's route, on the minimap only) | `nav_minimap`, `nav_gps` | The route line of the other mod is used. Off, this mod's route, on the minimap only. |
| switch | `map_places_shown` | The mod's places on the city map | The mod's places on the map: bus stops, hotels, the courthouse, driving schools, bike hire, custom shops, help-wanted boards, polling places, construction sites, sights and what's on, each with markers and its own map filter (off hides all of them, police stations too) |  | Bus stops, hotels, the courthouse, driving schools, custom shops and the mod's other places are marked on the city map, each with its own filter. |
| button |  | About this section | (new) |  | Opens the Guide page of `navigation`. |

### Licences and the driving school   (`licences`, 5 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `driving_licence` | Driving needs a licence | Driving needs a licence (off: anyone may drive anything, as in the base game) |  | You need a licence for what you drive. Off, anyone may drive anything, as in the game itself. |
| switch | `licence_start_with` | New saves start with a licence | Start with a driving licence (new saves that own no vehicle) | `driving_licence` | A new save that owns no vehicle starts with a driving licence. |
| switch | `driving_school` | Driving school, lessons and tests | Driving school: desks at the school buildings, theory test, lessons, driven test | `driving_licence` | Driving schools have desks for the theory test, lessons and the driven test. |
| switch | `licence_points` | Penalty points on your licence | Penalty points: booked offences go on the licence, twelve suspend it | `driving_licence` | Booked offences put points on your licence, and twelve suspend it. |
| switch | `roadside_checks` | Roadside licence checks | Roadside checks: now and then a patrol car pulls you over to see your licence | `driving_licence` | Now and then a patrol car pulls you over to see your licence. |
| button |  | About this section | (new) |  | Opens the Guide page of `licences`. |

### Travel and hotels   (`travel`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `travel_hotels` | Hotels to stay in | Hotels: eight places to stay, booked at the door |  | Eight hotels take guests, booked at the door. |
| switch | `travel_holidays` | Holidays, and burnout without a break | Holidays from travel agencies, and burnout without a break |  | Travel agencies sell holidays, and going too long without a break wears you out. |
| switch | `travel_extras` | Suitcase, insurance, a companion, trade fairs | Travel extras: suitcase, insurance, a companion, trade fairs |  | A suitcase, travel insurance, a companion and trade fairs can be added to a trip. |
| button |  | About this section | (new) |  | Opens the Guide page of `travel`. |

## Work

### Jobs and gigs   (`jobs`, 2 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `jobs` | Jobs from the Gig Line | Jobs you can take yourself (call the Gig Line on your phone) |  | You can take jobs yourself by calling the Gig Line on your phone. |
| slider | `job_pay` | Job pay (%) | Job pay (% of normal) | `jobs` | What the jobs pay, as a share of normal. |
| button |  | About this section | (new) |  | Opens the Guide page of `jobs`. |

### Careers   (`careers`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `careers` | Careers with employers | Careers: employers behind the help-wanted boards (a rota, reviews, promotions) | `jobs` | The help-wanted boards lead to employers with a rota, reviews and promotions. |
| button |  | About this section | (new) |  | Opens the Guide page of `careers`. |

### The Planner   (`planner`, 2 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `planner` | Planner on the phone | Planner app on the phone |  | An app on the phone lists everything you have to do. |
| switch | `planner_reminders` | Planner reminders | Planner reminders (off: none are sent) | `planner` | The Planner reminds you of what is coming up. |
| button |  | About this section | (new) |  | Opens the Guide page of `planner`. |

## Money

### Credit rating and loans   (`credit`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy` | Credit rating and phone loans | Credit rating and phone loans (off: the banks' own terms, no Finance app, nothing recorded) |  | You have a credit rating, banks lend by it, and loans can be taken from the phone. Off, the banks' own terms and no Finance app. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy`. |

### Debt   (`debt`, 4 rows, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_products` | Overdraft, cards, mortgage and other credit | More kinds of credit: overdraft, credit card, business loan, car finance, mortgage, consolidation (off: nothing new is opened or drawn; what you owe stays owed) |  | Adds an overdraft, a credit card, a business loan, car finance, a mortgage and consolidation. Off, nothing new is opened and what you owe stays owed. |
| switch | `economy_shark` | The private lender | The private lender, for when no bank will lend (off: it lends nothing and does nothing; a debt to it can still be paid) |  | A private lender lends when no bank will. Off, it lends nothing, and a debt to it can still be paid. |
| switch | `economy_consequences` | Consequences of not paying | Consequences of not paying: fees, a penalty rate, collections, repossession, a court claim (off: only your rating is affected) |  | Missed payments bring fees, a penalty rate, collections, repossession and a court claim. Off, only your rating suffers. |
| selector | `economy_level` | How hard not paying hits | How hard not paying hits (the presets set this) |  | Relaxed is forgiving, Hardcore is strict. The presets set this. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy`. |

### The city's economy   (`cityeco`, 2 rows, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `economy_city` | The city's economy | The city's economy (off: the banks' own rates, the game's own prices and customers, nothing adjusted) |  | The city has a bank rate, prices and good and bad years that reach your customers. Off, the game's own rates, prices and customers. |
| selector | `economy_city_level` | How far the economy swings | How far the economy swings (the presets set this) | `economy_city` | Relaxed keeps the economy calm, Hardcore lets it swing wide. The presets set this. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_city`. |

### Investors and the stock market   (`ventures`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `ventures_investors` | Investors | Investors (off: no new offers and no meetings; deals you have carry on) |  | Investors make offers for a share of your businesses and meet you. Off, no new offers, and deals you have carry on. |
| switch | `ventures_market` | The stock market | The stock market (off: prices stand still, nothing is traded and no dividend is paid; your shares are kept) |  | Shares can be bought and sold, and pay dividends. Off, prices stand still and your shares are kept. |
| selector | `ventures_level` | How demanding investors are | How demanding investors are (the presets set this) |  | Relaxed investors are patient, Hardcore ones want growth quickly. The presets set this. |
| button |  | About this section | (new) |  | Opens the Guide page of `ventures`. |

## Your businesses

### Running a business   (`company`, 8 rows, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `economy_company` | Running a business | Running a business (off: no staff market, insurance, campaigns, return or licences, and nothing is charged) |  | Staff, insurance, marketing, tax and licences for your businesses, with the Company app. Off, none of it runs and nothing is charged. |
| switch | `economy_company_staff` | Staff pay, raises, strikes and sick pay | Staff: the going rate, raises, rivals' offers, strikes, overtime and sick pay | `economy_company` | Staff expect the going rate, ask for raises, get offers from rivals, strike, and are paid overtime and sick pay. |
| switch | `economy_company_cover` | Business insurance | Business insurance: policies, premiums and claims | `economy_company` | You can take out policies, pay premiums and make claims. |
| switch | `economy_company_marketing` | Marketing campaigns | Marketing: campaigns with a cost, an end and a report | `economy_company` | Campaigns cost money, run for a time and end with a report. |
| switch | `economy_company_tax` | Quarterly tax return and an accountant | Tax: a quarterly return to file, and an accountant | `economy_company` | A tax return is due every quarter, and an accountant can file it. |
| switch | `economy_company_hide` | Cash sales kept off the books | Cash sales kept off the books (off: none can be set and nothing is hidden) | `economy_company` | You may keep some cash sales out of the return, at the risk of an audit. Off, nothing can be hidden. |
| switch | `economy_company_licences` | Licences and permits | Licences and permits: food hygiene, alcohol, late opening and signage | `economy_company` | Food hygiene, alcohol, late opening and signage need a licence or a permit. |
| selector | `economy_company_level` | How strict running a business is | How strict running a business is (the presets set this) | `economy_company` | Relaxed is forgiving, Hardcore is strict. The presets set this. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_company`. |

### Staff rota planner   (`rota`, 1 row)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_rota` | Staff rota planner | Staff rota planner: a customer forecast, advised opening hours and a rota at a press (the Company app's Rota tab) | `economy_company` | The Company app forecasts customers, advises opening hours and writes a staff rota at a press. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_rota`. |

### Cleaning service   (`cleaning`, 1 row, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_cleaning` | Cleaning service | Cleaning service: cleaners shared between your businesses on a daily round, and cleaning contracts with other businesses (the Services app) |  | Cleaners go round your businesses every day, and can clean for other businesses under contract. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_cleaning`. |

### Selling a business   (`sale`, 1 row, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_sale` | Selling a business | Selling a business: what it is worth, offers from buyers and a quick sale (the Services app) |  | The Services app says what a business is worth, brings offers from buyers and can sell it quickly. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_sale`. |

### Brands and franchises   (`brands`, 1 row, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_brands` | Brands and franchises | Brands and franchises (off: no brand effects, no franchise money and no applicants; franchised shops trade on as the city's own) |  | Your shops can become a brand and be franchised. Off, a brand has no effect and no franchise money is paid. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_brands`. |

### Online shops   (`online`, 1 row, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_online` | Online shops | Online shops: a business or a warehouse sells its stock to the whole city, with the Web shop app (off: no orders come and nothing is charged or paid) |  | A business or a warehouse sells its stock to the whole city through the Web shop app. Off, no orders come. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_online`. |

### Property, hotels and car hire   (`estate`, 3 rows, every row needs `economy`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `economy_estate` | Property to buy, let and sell | Property: buying, letting and an agency (off: every building's value and rent is the game's own) |  | Building values and rents move, and you can let buildings to tenants, do works and sell. Off, every value and rent is the game's own. |
| switch | `economy_estate_hotel` | Owning a hotel | Owning a hotel (off: a hotel you hold stands still, and takes and pays nothing) |  | A hotel you own takes guests and pays you. Off, it stands still. |
| switch | `economy_estate_hire` | Car hire business | Car hire (off: no car goes out, and a car that is out is called back) |  | You can hire your cars out. Off, no car goes out, and a car that is out is called back. |
| button |  | About this section | (new) |  | Opens the Guide page of `economy_estate`. |

### Business events   (`bizevents`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `business_pressure` | Business events | Business pressure: rivals, inspections, staff and supplier trouble, opportunities (listed on the Business desk contact) |  | Things happen to your businesses, good and bad. They are listed on the Business desk contact. |
| slider | `business_frequency` | How often something comes up (1 to 10) | How often something comes up (1 = about one day in twelve, 10 = about one day in three; never more than two open at once) | `business_pressure` | 1 is about one day in twelve, 10 is about one day in three. Never more than two are open at once. |
| switch | `business_easy` | Good news only | Easy business: only the opportunities (bulk orders, press features, praise, awards) | `business_pressure` | Only bulk orders, press coverage, praise and awards come up. |
| button |  | About this section | (new) |  | Opens the Guide page of `business`. |

### Kinds of business event   (`bizkinds`, 8 rows, every row needs `business_pressure`)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `business_rivals` | Rival moves | Rival moves: a competitor undercuts you, an offer to one of your staff, a smear in the paper |  | A competitor undercuts you, makes an offer to one of your staff or smears you in the paper. |
| switch | `business_racket` | Protection racket | Protection racket: pay, refuse, go to the police, or rely on your security |  | Someone demands protection money. You can pay, refuse, go to the police or rely on your security. |
| switch | `business_inspections` | Inspections | Inspections: health (food businesses), fire safety, licence renewal |  | Health, fire safety and licence inspectors call. |
| switch | `business_staff` | Staff trouble | Staff trouble: a strike threat over low wages, theft caught on camera, an argument to settle |  | A strike threat over low wages, a theft caught on camera or an argument to settle. |
| switch | `business_suppliers` | Supplier trouble | Supplier trouble: a late delivery, a surcharge |  | A delivery comes late or a supplier adds a surcharge. |
| switch | `business_customers` | Customer complaints and praise | Customer incidents: a complaint or praise that spreads |  | A complaint or a word of praise spreads. |
| switch | `business_audit` | Tax audits | Tax audit |  | The tax office audits a business of yours now and then. |
| switch | `business_opportunities` | Opportunities | Opportunities: a bulk order, a feature in the paper, an award |  | A bulk order, a piece in the paper or an award comes your way. |
| button |  | About this section | (new) |  | Opens the Guide page of `business`. |

## Controls

### Keys   (`keys`, 8 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| button |  | Reset the mod's keys | Reset the mod's keys to their defaults |  |  |
| key |  | Third-person camera on and off | Third-person camera on and off |  |  |
| key |  | Interact | Interact: enter a building or car, and the mod's street prompts |  |  |
| key |  | Interact, second key | Interact, second key (only while the game offers nothing itself) |  |  |
| key |  | Free the cursor during mouse look | Free the cursor while mouse look is on |  |  |
| key |  | Look behind the car (hold) | Look behind the car (hold) |  |  |
| key |  | Gentle throttle (hold) | Gentle throttle: half the pedal (hold; needs Smooth throttle) |  |  |
| key |  | Photo mode | Photo mode |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `keys`. |

### Mouse and doors   (`mouse`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `camera_mouselook` | Mouse look | Mouse look, GTA style (the 'Free the cursor' key under Controls gives the mouse back) | `camera_enabled` | Move the mouse to look around. The 'Free the cursor' key under Controls gives the pointer back. |
| slider | `camera_sensitivity` | Mouse look sensitivity (1 to 20) | Mouse look sensitivity | `camera_mouselook` | How far the view turns for the same movement of the mouse. |
| switch | `enter_with_key` | Enter buildings and vehicles with the interact key | Get into buildings and your vehicles with the interact keys (see Controls) |  | The interact keys take you into buildings and into your vehicles without a click. |
| button |  | About this section | (new) |  | Opens the Guide page of `camera`. |

## Performance

### Faster frames   (`speed`, 7 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `perf_refraction` | No refraction effect outdoors, picture unchanged | Outdoors, the refraction effect is switched off, as nothing outdoors uses it (no visible change) |  | Outdoors nothing uses the refraction effect, so it is switched off there. |
| switch | `perf_sky_updates` | Sky light refreshed every 2 seconds, picture unchanged | The sky's reflection and its light on things are refreshed every 2 seconds instead of every frame (no visible change) |  | The sky's reflection and its light on things are refreshed every 2 seconds, not every frame. |
| switch | `perf_stale_lights` | Switched-off street lamps cost nothing, picture unchanged | Street lamps the game has switched off no longer keep their lights enabled (no visible change) |  | Street lamps the game has switched off no longer keep their lights enabled. |
| switch | `perf_far_districts` | Scenery beyond 1.3 km not drawn, picture unchanged | Scenery more than 1.3 km away is not drawn, and is back before you can see it (no visible change) |  | Scenery more than 1.3 km away is not drawn, and is back before you can see it. |
| switch | `perf_view_radius` | Small things drawn within 300 m, changes the picture | People, cars and small things are drawn only within 300 m in every direction, as they already are straight ahead (buildings and roads unchanged) |  | People, cars and small things are drawn only within 300 m in every direction, as they already are straight ahead. Buildings and roads are unchanged. |
| switch | `perf_far_props` | Street furniture drawn within 200 m, changes the picture | Street furniture and parked cars are not drawn beyond 200 m (visible: they appear at 200 m instead of 300 m) |  | Street furniture and parked cars appear at 200 m, not at 300 m. |
| switch | `perf_textures_whole` | Textures kept whole in graphics memory, picture unchanged | Textures are kept whole on the graphics card, so the processor no longer manages their detail (needs a card with 10 GB or more; no visible change, more graphics memory used) |  | The processor no longer manages texture detail. Needs a graphics card with 10 GB or more, and uses more of its memory. |
| button |  | About this section | (new) |  | Opens the Guide page of `performance`. |

### Picture   (`picture`, 5 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| main switch | `visuals_enabled` | Picture adjustments | Visual upgrades |  | The colour, contrast, bloom and detail settings below are applied. Off, the picture is the game's own. |
| slider | `visuals_saturation` | Colour saturation (0 is the game's own) | Colour saturation | `visuals_enabled` | How strong the colours are. |
| slider | `visuals_contrast` | Contrast (0 is the game's own) | Contrast | `visuals_enabled` | How far light and dark are apart. |
| slider | `visuals_bloom` | Bloom (%), costs performance | Bloom in % (costs performance, 0 = the game's own) | `visuals_enabled` | The glow round bright lights. At 0 it is the game's own. |
| slider | `visuals_detail` | Detail distance (%), costs performance above 100 | Detail distance in % (above 100 costs performance) | `visuals_enabled` | How far away the game still draws things in full detail. |
| button |  | About this section | (new) |  | Opens the Guide page of `visuals`. |

## Troubleshooting

### Save tools   (`savetools`, 3 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| button |  | Put back missing contacts and moods | Repair: put back the mod's contacts and moods if they are missing |  |  |
| button |  | Reset the mod's data in this save | Reset the mod's data in this save (the mod stays on) |  |  |
| button |  | Prepare this save for removing the mod | Prepare this save for removing the mod |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `savetools`. |

### Diagnostics   (`diagnostics`, 4 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `dev_diagnostics` | Developer diagnostics | Developer diagnostics: detailed lines in Player.log and the diagnostic keys (switch on to report a problem) |  | Detailed lines go to Player.log and the diagnostic keys work. Switch on to report a problem. |
| switch | `show_test_tools` | Show test tools | Show test tools (the test buttons at the bottom of this page) |  | Test buttons for every part of the mod appear under Troubleshooting, in Test tools. |
| button |  | Check the mod's set-up | Check the mod's set-up (options, presets, Guide pages) |  |  |
| button |  | What changed in this version | Show what changed (the Guide on your phone) |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `diagnostics`. |

### Measuring performance   (`measure`, 7 rows)

| Kind | Option id | Label now | Label before | Needs | Sentence |
| --- | --- | --- | --- | --- | --- |
| switch | `perf_step_timing` | Measurements time every step of the frame | Measurements time every step of the frame (switch off only if a measurement ever misbehaves) |  | The performance measurements time each step of the frame. Switch off only if a measurement misbehaves. |
| button |  | Measure performance for 10 seconds | Measure performance for 10 seconds |  |  |
| button |  | Run the performance test (about 3 minutes) | Run performance test, about 3 minutes |  |  |
| key |  | Key for the 10-second measurement | Measure performance for 10 seconds (developer diagnostics) |  |  |
| key |  | Key for the performance test | Run the performance test (developer diagnostics) |  |  |
| key |  | Key to write the phone's interface to a file | Write the phone's interface to a file (developer diagnostics) |  |  |
| key |  | Key to write the character report | Write the character report (developer diagnostics) |  |  |
| button |  | About this section | (new) |  | Opens the Guide page of `performance`. |

## Test tools (under Troubleshooting, only with "Show test tools" on)

Grouped by the same groups, then by section; every one is closed until it is opened.

### Tests: Your character and home

**Needs and wellbeing** (21)

| Label now | Label before |
| --- | --- |
| Open the "How am I doing" page | Test: open the "How am I doing" page |
| Open the cooking list | Test: open the cooking list |
| Cook the first dish the fridges allow | Test: cook the first dish the fridges allow (needs food in a fridge at home) |
| Make a coffee, with a coffee machine at home | Test: make a coffee (needs a coffee machine at home) |
| Tidy up the flat | Test: tidy up the flat |
| Stretch | Test: stretch |
| Do push-ups | Test: do push-ups |
| Do sit-ups | Test: do sit-ups |
| Go for a run | Test: go for a run |
| Stop the run | Test: stop the run |
| See the doctor (free) | Test: see the doctor (free) |
| Give me a cold | Test: give me a cold |
| Give me the flu | Test: give me the flu |
| Give me food poisoning | Test: give me food poisoning |
| Give me back pain | Test: give me back pain |
| Give me a burnout | Test: give me a burnout |
| Cure everything | Test: cure everything |
| Fitness +10 | Test: fitness +10 |
| Fitness -10 | Test: fitness -10 |
| Run all needs down | Test: run all needs down |
| Fill all needs | Test: fill all needs |

**Looks and hair** (3)

| Label now | Label before |
| --- | --- |
| Say what my character's hair is made of | Test: say what my character's hair is made of |
| A strong lamp behind my head for 20 seconds | Test: a strong lamp beyond my head for 20 seconds |
| Switch the fuller hair off and on | Test: switch the hair finish off and on |

**Roommates** (7)

| Label now | Label before |
| --- | --- |
| Three people answer a roommate notice | Test: three people answer a roommate notice |
| A roommate moves in | Test: a roommate moves in |
| Rent day, and they are short | Test: rent day, and they are short |
| A noisy night | Test: a noisy night |
| They cooked | Test: they cooked |
| They give notice | Test: they give notice |
| Put roommates back | Test: put roommates back |

**Family** (9)

| Label now | Label before |
| --- | --- |
| Partner moves in | Test: partner moves in |
| Propose | Test: propose |
| Start wedding planning | Test: start wedding planning |
| Hold the wedding here, now | Test: hold the wedding here, now |
| Start a pregnancy, or move it on a step | Test: start a pregnancy / move it on a step |
| Start an adoption, or move it on a step | Test: start an adoption / move it on a step |
| Add a year to the children's ages | Test: add a year to the children's ages |
| Pushchair walk | Test: pushchair walk |
| Clear all family data in this save | Test: clear all family data in this save |

**Friends and social life** (20)

| Label now | Label before |
| --- | --- |
| Somebody new comes up to me | Test: somebody new comes up to you |
| Choose the next person for the buttons below | Test: choose the next person for the buttons below |
| Move the chosen person one stage closer | Test: move the chosen person one stage closer |
| The chosen person rings me | Test: the chosen person rings you |
| The chosen person texts me | Test: the chosen person texts you |
| The chosen person asks a favour, in turn | Test: the chosen person asks a favour (in turn: a lift, a loan, help moving) |
| The chosen person drops by | Test: the chosen person drops by |
| A walk with Mom, with pavement near | Test: a walk with Mom (needs pavement within 8 m) |
| Coffee with the chosen person | Test: coffee with the chosen person |
| A meal with the chosen person | Test: a meal with the chosen person |
| Drinks with the chosen person | Test: drinks with the chosen person |
| The cinema with the chosen person | Test: the cinema with the chosen person |
| A walk with the chosen person | Test: a walk with the chosen person |
| A drive with the chosen person, in my car | Test: a drive with the chosen person (needs your car) |
| A workout with the chosen person | Test: a workout with the chosen person |
| Show the chosen person my business | Test: show the chosen person your business |
| Give the chosen person a lift in my car | Test: give the chosen person a lift (needs your car) |
| Help the chosen person move | Test: help the chosen person move |
| Print today's City Ledger again | Test: print today's City Ledger again |
| Stop what is going on and drop the plan | Test: stop what is going on and drop the plan |

**Leisure** (12)

| Label now | Label before |
| --- | --- |
| Street chess: a player ahead of me | Test: street chess (sets a chess player up just ahead of you) |
| Fishing (stand at the water's edge) | Test: fishing (stand at the water's edge, facing the water) |
| Fishing at the nearest water (takes me there) | Test: fishing at the nearest water (takes you there) |
| Dancing (on a nightclub's dance floor) | Test: dancing (inside a nightclub, on the dance floor) |
| Order a drink (at a nightclub's bar) | Test: order a drink (inside a nightclub, at the bar) |
| Take in the view (at a landmark) | Test: take in the view (at a landmark) |
| Open the clothes rack | Test: open the clothes rack |
| Open the wardrobe | Test: open the wardrobe |
| People outside the nearest open club or cinema | Test: people outside the nearest open club, cinema or theatre |
| Play each city sound in turn | Test: play each city sound layer in turn |
| Photo mode, once this menu is closed | Test: photo mode (starts when you close this menu) |
| Stop the activity in progress | Test: stop the activity in progress |

**Life events** (1)

| Label now | Label before |
| --- | --- |
| A life event | Test: life event |

### Tests: The city

**Street life** (6)

| Label now | Label before |
| --- | --- |
| The next kind of street worker ahead of me | Test: put the next kind of street worker just ahead of you |
| The next everyday scene ahead of me | Test: put the next kind of everyday scene just ahead of you |
| The next cart ahead of me, with customers | Test: put the next kind of cart (coffee, hot dogs) just ahead of you; its customers walk up |
| The next delivery or trade ahead of me | Test: the next delivery or trade (courier, post, painter, gardener) just ahead of you |
| The next leisure scene ahead of me | Test: the next leisure scene (chess, tai chi, newspaper, map, pushchair, car, door, queue, umbrellas) just ahead of you |
| Clear street life people off the street | Test: clear the city life people off the street |

**Events and weather** (17)

| Label now | Label before |
| --- | --- |
| A car near me catches fire | Test: a car near you catches fire |
| A bin fire on the pavement ahead of me | Test: a bin fire on the pavement ahead of you |
| A fire at the nearest shopfront | Test: a fire at the nearest shopfront |
| Rain effects (wet roads, the sky stays) | Test: rain effects (wet roads; the sky stays as it is) |
| Mist effects | Test: mist effects |
| Thunderstorm effects | Test: thunderstorm effects |
| Heat wave effects | Test: heat wave effects |
| Cold snap effects | Test: cold snap effects |
| Ice effects | Test: ice effects |
| A street market on the road ahead of me | Test: a street market on the road ahead of you |
| A farmers' market on the road ahead of me | Test: a farmers' market on the road ahead of you |
| A parade on the road ahead of me | Test: a parade on the road ahead of you |
| A protest on the road ahead of me | Test: a protest on the road ahead of you |
| A charity fun run on the road ahead of me | Test: a charity fun run on the road ahead of you |
| A film shoot on the road ahead of me | Test: a film shoot on the road ahead of you |
| Fireworks and a crowd on the road ahead of me | Test: fireworks and a crowd on the road ahead of you |
| Clear fires and street events, give the weather back | Test: clear any fire or street event, and give the weather back to the game |

**Accidents and emergency services** (6)

| Label now | Label before |
| --- | --- |
| A traffic accident of any size | Test: traffic accident (random severity) |
| Minor accident | Test: minor accident |
| Moderate accident | Test: moderate accident |
| Severe accident | Test: severe accident |
| Knock down the nearest pedestrian | Test: knock down the nearest pedestrian |
| A pedestrian hit by traffic | Test: pedestrian struck by traffic |

**Police stations and arrests** (7)

| Label now | Label before |
| --- | --- |
| Arrest me (minor: fled a traffic stop) | Test: arrest me (minor: fled a traffic stop) |
| Arrest me (serious: hit-and-run with an injury) | Test: arrest me (serious: hit-and-run with an injury) |
| Arrest me (fatal hit-and-run) | Test: arrest me (fatal hit-and-run) |
| Show the nearest police station | Test: show the nearest police station |
| Open the front desk of the nearest police station | Test: open the front desk of the nearest police station |
| Set up the nearest police station again | Test: set up the nearest police station again (details in Player.log) |
| Clear my record and reset my reputation | Test: clear my record and reset my reputation |

**Crime** (6)

| Label now | Label before |
| --- | --- |
| A street incident | Test: street incident |
| A traffic offence by another driver | Test: a traffic offence by another driver |
| A crime in the shop I am standing in | Test: a crime in a shop (stand inside a shop) |
| A crime against me (on foot, outdoors, with $60) | Test: a crime against you (on foot, outdoors, carrying at least $60) |
| Break-in, car broken into, scam call, in turn | Test: break-in, car broken into, scam call (one per press, in turn) |
| Get pulled over while driving | Test: get pulled over (while driving) |

**Courts and prison** (13)

| Label now | Label before |
| --- | --- |
| A case against me: evading the police | Test: open a case against me (evading the police) |
| A case against me: hit-and-run with an injury | Test: open a case against me (hit-and-run with an injury) |
| A case against me: fatal hit-and-run | Test: open a case against me (fatal hit-and-run) |
| A case against me with no evidence | Test: open a case against me with no evidence (wrongful arrest) |
| Advance my next case to its court date | Test: advance my next case to its court date |
| Hearing of my next case (by video link) | Test: hearing of my next case (by video link) |
| Prison for 3 days (the days really pass) | Test: send me to prison for 3 days (the days really pass) |
| Prison for the longest term | Test: send me to prison for the longest term |
| Parole hearing on the last morning of 3 days | Test: parole hearing (the last morning of a 3-day term) |
| Somebody sues me over a crash | Test: somebody sues me over a crash |
| Community service shift | Test: community service shift |
| Open the Legal file | Test: open the Legal file |
| Clear cases, debts, record and sentences | Test: clear everything (cases, debts, record, sentences) |

**The council** (11)

| Label now | Label before |
| --- | --- |
| Start an election campaign round me | Test: start an election campaign (posters and boards go up round you) |
| A campaign rally near me (outdoors, dry weather) | Test: a campaign rally near you (out of doors, dry weather) |
| Election day: first press opens, second counts | Test: election day (first press opens the polls, second press counts the votes) |
| Bring the next policy into force | Test: bring the next policy on the list into force (each press, the next one) |
| Take back every city policy | Test: take back every city policy |
| Roadworks by the nearest building | Test: start a roadworks site by the nearest building (on a street with traffic) |
| Scaffolding on the nearest building front | Test: start a scaffolding site on the nearest building front |
| A shop refit on the nearest building front | Test: start a shop refit (hoarding) on the nearest building front |
| Utility works at the kerb by the nearest building | Test: start utility works at the kerb by the nearest building |
| Advance all construction sites one working day | Test: advance all construction sites one working day |
| Clear the council's people and construction sites | Test: clear civic life off the street and empty the construction programme |

### Tests: Getting around

**Camera** (1)

| Label now | Label before |
| --- | --- |
| Pretend another mod has the camera (again ends it) | Test: pretend another mod has the camera (again: end it) |

**Custom shop** (7)

| Label now | Label before |
| --- | --- |
| Open the custom shop on my nearest car | Test: open the custom shop on my current or nearest car |
| A random full build on my nearest car, free | Test: put a random full build on my current or nearest car (free) |
| Restore my nearest car to stock | Test: restore my current or nearest car to stock |
| A city bicycle | Test: a city bicycle |
| A road bicycle | Test: a road bicycle |
| A motorbike | Test: a motorbike |
| A sport motorbike | Test: a sport motorbike |

**Traffic and pedestrians** (13)

| Label now | Label before |
| --- | --- |
| A speeder near me | Test: a speeder near me |
| A red-light runner at the next junction | Test: a red-light runner at the next junction |
| A drunk driver near me | Test: a drunk driver near me |
| A jaywalker in front of the next car | Test: a jaywalker in front of the next car |
| A reckless car that will hit someone | Test: a reckless car that will hit someone |
| A reckless car that will crash | Test: a reckless car that will crash |
| A patrol car that sees a speeder | Test: a patrol car that sees a speeder |
| A pedestrian crosses in front of the next car, with room | Test: a pedestrian crosses in front of the next car, with room |
| A pedestrian steps out close | Test: a pedestrian steps out close |
| A pedestrian dawdles in the lane | Test: a pedestrian dawdles in the lane |
| A pedestrian steps out too late | Test: a pedestrian steps out too late |
| Ten people cross this road | Test: ten people cross this road |
| Show what the cars sense | Test: show what the cars sense |

**Buses and subway** (8)

| Label now | Label before |
| --- | --- |
| A bus stop's panel and a shop's box together | Test: a stop's panel and a shop's box together (again: take them down) |
| Set up the nearest bus stop | Test: set up the nearest bus stop |
| Send a bus to the nearest bus stop | Test: send a bus to the nearest bus stop |
| Ride the bus two stops from the nearest bus stop | Test: ride the bus from the nearest bus stop, two stops on |
| Subway journey from the nearest station | Test: subway journey from the nearest station |
| Set the fuel of the car I am driving to 5% | Test: set the fuel of the car you are driving to 5% |
| Break down the car I am driving | Test: break down the car you are driving |
| Show the Garage page | Test: show the Garage page |

**Taxi** (3)

| Label now | Label before |
| --- | --- |
| Call City Cabs | Test: call City Cabs |
| Bring a taxi at once | Test: bring a taxi at once (it may start in view) |
| Cancel the taxi | Test: cancel the taxi |

**Minimap and GPS** (6)

| Label now | Label before |
| --- | --- |
| Route to the nearest police station | Test: route to the nearest police station |
| Route to the far end of the city | Test: route to the far end of the city |
| Clear the destination | Test: clear the destination |
| Minimap, buildings and water alone | Test: minimap, buildings and water alone |
| Minimap, mark three buildings | Test: minimap, mark three buildings |
| Minimap, measure the city again | Test: minimap, measure the city again |

**Licences and the driving school** (10)

| Label now | Label before |
| --- | --- |
| Show my licence card | Test: show my licence card |
| Grant or revoke the car licence | Test: grant / revoke the car licence |
| Grant or revoke the commercial licence | Test: grant / revoke the commercial licence |
| Grant or revoke the taxi endorsement | Test: grant / revoke the taxi endorsement |
| Grant or revoke the motorcycle licence | Test: grant / revoke the motorcycle licence |
| Driving theory test, once this page is closed | Test: driving theory test (starts when this page is closed) |
| Practical driving test (car) | Test: practical driving test (car) |
| Practical driving test (van, reversing) | Test: practical driving test (van, reversing) |
| Roadside licence check while driving | Test: roadside licence check (while driving) |
| Put 4 penalty points on my licence | Test: put 4 penalty points on my licence |

**Travel and hotels** (12)

| Label now | Label before |
| --- | --- |
| Check in at the nearest hostel | Test: check in at the nearest hostel |
| Check in at the nearest mid-range hotel | Test: check in at the nearest mid-range hotel |
| Check in at the nearest luxury hotel | Test: check in at the nearest luxury hotel |
| Go up to my room | Test: go up to my room |
| Start the Hamptons weekend | Test: start the Hamptons weekend |
| Start the coast cruise | Test: start the coast cruise |
| Start the ski trip | Test: start the ski trip |
| Start the Miami beach week | Test: start the Miami beach week |
| Start the Europe tour | Test: start the Europe tour |
| Start the trade fair trip | Test: start the trade fair trip |
| Return from the trip | Test: return from the trip |
| Clear room, ticket and trip | Test: clear room, ticket and trip |

### Tests: Work

**Jobs and gigs** (15)

| Label now | Label before |
| --- | --- |
| Hand out flyers (on foot, where people are) | Test: start handing out flyers (on foot, on a street with people) |
| A parcel round (my car if near, else a van) | Test: start a parcel round (your car if it is near, else a hired van) |
| A ride-hail shift (my car if near, else a van) | Test: start a ride-hail shift (your car if it is near, else a hired van) |
| A cleaning job at a business nearby | Test: start a cleaning job at a business nearby (on foot) |
| Stock shelves at a shop nearby | Test: start stocking shelves at a shop nearby (on foot) |
| A drinks cart shift (on a pavement with room) | Test: start a drinks cart shift (on foot, on a pavement with room) |
| Busk on the pavement | Test: start busking (on foot, on a pavement) |
| Stop the current job | Test: stop the current job |
| Sign on with the next employer | Test: sign on with the next employer (cycles through all six) |
| Work a shift at my rank, off the record | Test: work a shift at my current rank (goes on no record) |
| Hold my performance review | Test: hold my performance review |
| Promote me one rank | Test: promote me one rank |
| Start a dispatcher shift | Test: start a dispatcher shift |
| Start a shift in charge | Test: start a shift in charge |
| Book a set at a nearby venue | Test: book a set at a nearby venue |

**The Planner** (2)

| Label now | Label before |
| --- | --- |
| Fill the Planner with one of everything | Test: fill the Planner with one of everything |
| Clear the Planner's samples | Test: clear the Planner's samples |

### Tests: Money

**Credit rating and loans** (16)

| Label now | Label before |
| --- | --- |
| Show my credit report in the log | Test: show my credit report in the log |
| Record a missed payment | Test: record a missed payment |
| Record 30 days of payments on time | Test: record 30 days of payments on time |
| Set the rating to 560 | Test: set the rating to 560 |
| Set the rating to 600 | Test: set the rating to 600 |
| Set the rating to 760 | Test: set the rating to 760 |
| Clear test entries | Test: clear test entries |
| Restore the banks' own terms until the next hour | Test: restore the banks' own terms (until the next game hour) |
| Open a test overdraft, card and financed car | Test: open a test overdraft, a test card and a test car on finance |
| The test accounts miss 1 payment | Test: the test accounts miss 1 payment |
| The test accounts miss 3 payments | Test: the test accounts miss 3 payments |
| The collections call about a test account | Test: the collections call about a test account |
| Repossess the test car | Test: repossess the test car |
| Send the private lender's first text | Test: send the private lender's first text |
| Work out a debt arrangement and a bankruptcy in the log | Test: work out a debt arrangement and a bankruptcy in the log (nothing is changed) |
| Put everything back: remove test accounts and car | Test: put everything back (remove the test accounts and the test car) |

**The city's economy** (13)

| Label now | Label before |
| --- | --- |
| Show the city's economy in the log | Test: show the city's economy in the log |
| Step the city's economy a month | Test: step the city's economy a month |
| Put the economy in a boom | Test: put the economy in a boom |
| Make the economy steady | Test: make the economy steady |
| Put the economy in a slowdown | Test: put the economy in a slowdown |
| Put the economy in a recession | Test: put the economy in a recession |
| A shortage from tomorrow | Test: a shortage from tomorrow |
| A glut from tomorrow | Test: a glut from tomorrow |
| Bank rate up one point | Test: bank rate up one point |
| Bank rate down one point | Test: bank rate down one point |
| Raise this district's income by 10 | Test: raise this district's income by 10 |
| Lower this district's income by 10 | Test: lower this district's income by 10 |
| Put the city's economy back | Test: put the city's economy back |

**Investors and the stock market** (9)

| Label now | Label before |
| --- | --- |
| A tycoon's offer for my best business | Test: a tycoon's offer for my best business |
| A meeting with an angel investor | Test: a meeting with an angel investor |
| A meeting with a tycoon | Test: a meeting with a tycoon |
| A meeting with a venture firm | Test: a meeting with a venture firm |
| List the investors and companies in the log | Test: list the investors and companies in the log |
| Step the market 30 days | Test: step the market 30 days |
| Pay a dividend | Test: pay a dividend |
| An investor calls their money | Test: an investor calls their money |
| Put ventures back | Test: put ventures back |

### Tests: Your businesses

**Running a business** (9)

| Label now | Label before |
| --- | --- |
| A raise request | Test: a raise request |
| A poaching offer | Test: a poaching offer |
| A strike tomorrow | Test: a strike tomorrow |
| A fire claim on a test policy | Test: a fire claim on a test policy |
| A campaign that ends at once, with its report | Test: a campaign that ends at once, with its report |
| A return due now | Test: a return due now |
| An audit of cash off the books (really charges) | Test: an audit of cash off the books (really charges what it finds) |
| A licence that lapses now | Test: a licence that lapses now |
| Put the company back | Test: put the company back |

**Staff rota planner** (4)

| Label now | Label before |
| --- | --- |
| Open the rota of my busiest business | Test: open the rota of my busiest business |
| Show my busiest business's plans in the log | Test: show my busiest business's plans in the log |
| Fill every business's rota | Test: fill every business's rota |
| Put every rota back | Test: put every rota back |

**Cleaning service** (3)

| Label now | Label before |
| --- | --- |
| A cleaning round with every business due | Test: a cleaning round with every business due |
| A cleaning contract offer | Test: a cleaning contract offer |
| A cleaning contract's week ends | Test: a cleaning contract's week ends |

**Selling a business** (3)

| Label now | Label before |
| --- | --- |
| An offer for my best business | Test: an offer for my best business |
| A sale of my best business on paper | Test: a sale of my best business on paper |
| A part of a sale price falls due | Test: a part of a sale price falls due |

**Brands and franchises** (7)

| Label now | Label before |
| --- | --- |
| Make my best shop a brand with high recognition | Test: make my best shop a brand with high recognition |
| An applicant | Test: an applicant |
| Sign a franchise in the next district | Test: sign a franchise in the next district |
| A franchisee in trouble | Test: a franchisee in trouble |
| A scandal at a member shop | Test: a scandal at a member shop |
| A year passes for the brand | Test: a year passes for the brand |
| Put brands back | Test: put brands back |

**Online shops** (6)

| Label now | Label before |
| --- | --- |
| Open a test web shop on my busiest business | Test: open a test web shop on my busiest business |
| Ten orders | Test: ten orders |
| A late delivery | Test: a late delivery |
| A returned order | Test: a returned order |
| A day of orders passes | Test: a day of orders passes |
| Put online shops back | Test: put online shops back |

**Property, hotels and car hire** (10)

| Label now | Label before |
| --- | --- |
| A tenant applies | Test: a tenant applies |
| Rent day with a tenant in arrears | Test: rent day with a tenant in arrears |
| Building works are completed | Test: works finish |
| An offer for a listed building | Test: an offer for a listed building |
| An agency instruction | Test: an agency instruction |
| A hotel night passes | Test: a hotel night passes |
| A group booking offer | Test: a group booking offer |
| A hire car goes out and comes back damaged | Test: a hire car goes out, then comes back damaged |
| A month passes for the property market | Test: a month passes for the property market |
| Put property back | Test: put property back |

**Business events** (10)

| Label now | Label before |
| --- | --- |
| The next rival move at my business | Test: next rival move at the business I am in (or my first) |
| The protection racket makes its demand | Test: the protection racket makes its demand |
| The next inspection | Test: next inspection (health, fire safety, licence) |
| The next staff trouble | Test: next staff trouble (strike threat, theft, argument) |
| The next supplier trouble | Test: next supplier trouble (late delivery, surcharge) |
| The next customer complaint or praise | Test: next customer incident (complaint, praise) |
| A tax audit | Test: a tax audit |
| The next opportunity | Test: next opportunity (bulk order, press feature, award) |
| Open the Business desk | Test: open the Business desk |
| Close every business event and undo its effects | Test: resolve all (close every issue, undo every footfall change and closure, forget the racket) |

### Tests: Performance

**Faster frames** (1)

| Label now | Label before |
| --- | --- |
| Write the session record's lines now | Test: write the session record's lines now |

### Tests: Troubleshooting

**Diagnostics** (1)

| Label now | Label before |
| --- | --- |
| Write the options page's layout to Player.log | (new) |

Test buttons in all: 308. Features whose tests no section claims: none.

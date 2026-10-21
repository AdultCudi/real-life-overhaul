# Social life

## In a sentence

Eight people the player knows (Mom, an old friend and six met in the street), things to do with them, texts and calls, and the morning paper, the City Ledger.

## Where the code is

- Folder: [`Scripts/Social`](../../Assets/Mods/RealLifeOverhaul/Scripts/Social/). Flare, the city's social media app, is in its [`Flare`](../../Assets/Mods/RealLifeOverhaul/Scripts/Social/Flare/) subfolder.
- Read first: `SocialDirector.cs` (load and save, the one scene), `SocialModel.cs`, `SocialRules.cs` and `SocialState.cs` (the records, the rules and the save, game-free) and `SocialCatalogue.cs` (names, outings and every line).

| File | What it is |
| --- | --- |
| `SocialModel` | the cast's records, the plan, what waits for an answer: game-free |
| `SocialRules` | stages, what things are worth, neglect: game-free |
| `SocialState` | the social part of the save: game-free |
| `SocialCatalogue` | names, jobs, traits, outings, every line anybody says: game-free |
| `SocialLife` | what happens between the player and a person, with its consequences in the game; the optional links (ISocialLaw, ISocialWellbeing) |
| `SocialDirector` | load and save, the one scene; its parts `.Contact` (who gets in touch, the day's round), `.Meetings` (people on their way), `.Plan` (the plan, the phone ringing), `.Scene`, `.Ui` (the card, the prompt, the marker) and `.Tests` |
| `SocialHooks` | the calls other features make into the social life |
| `SocialScene` | the base of a scene: talking face to face, walking off |
| `MeetScene` | somebody walks up |
| `OutingScene` | a plan kept, with parts `.Meeting` (getting there, hello), `.Walk`, `.Ride`, `.Boxes` (helping them move) and `.After` |
| `PersonDialog` | the call to or from a person; its parts `.Composer` (a topic answered in the player's own typed words) and `.CallBack` (a call back opens on what they just texted) |
| `SocialFollowRules`, `SocialFollowCatalogue` | a call back: which text it opens on, the subject of every text that reaches a person's thread, its line and answers; the day after an outing: game-free |
| `SocialComposerRules` | when a call's topic is typed, what typed words are worth, what they say (its parts `.Reading` and `.Words`: the words of all nine languages) and what that gets back, which lines go on RLO Texts' card: game-free |
| `SceneDialog` | the conversation in the street; SceneTalk |
| `PeopleDialog` | the People page |
| `SocialTalk` | topics from what happened |
| `SocialPhone` | contacts, the paper's desk (LedgerDialog) |
| `SocialCast` | the two characters, wearing the look of whoever they stand for |
| `SocialBody` | the player's own body in a scene: held, a gesture, a cup, a box |
| `SocialPlaces` | venues by business type, streets, places to wait, walk in from and walk off to |
| `News` | the log any feature reports to (News.Report), and the desk that writes the paper |

## How it talks to other features

- Any feature reports to the paper with `News.Report(kind, where, detail, amount)`; see [The City Ledger](#the-city-ledger-news).
- The social life asks the law and the home through `ISocialLaw` and `ISocialWellbeing` (`SocialLinks`), filled in by `Wiring/LifeLinks.cs`.
- Family takes the partner from here (`Wiring/FamilyLinks.cs`), Justice a close friend for court and prison visits, Travel a companion for a trip.
- Flare's links are `Wiring/FlareLinks.cs`, `FlareLiveLinks.cs` and `FlareInviteLinks.cs`; see [Wiring](../reference/wiring.md).

People the player knows, things to do with them, and a morning paper (`Scripts/Social`).
Everything is on the street or on the phone: the cast is never put indoors (the game loads an interior as a place of its own), so an outing at a business is played to its door and from its door, and the time inside passes with the game's own time machine.

## The cast

Eight people per save, rolled once from a seed and then stored whole (name, look, traits, job, district, free hours, birthday), so a later change to the name lists renames nobody.
One per way of meeting:

| who | met | how |
|---|---|---|
| Mom | known from the start (the contact `reallife-mom` the mod always had) | family: never less than a friend |
| Alex | known from the start (`reallife-friend`) | an old friend |
| six others | walking up to the player in the street | after leaving a gym (35 %), after a job shift that paid (30 %), after the second coffee bought at a cart (50 %), outside the player's home (15 %) or own shop (20 %), after a `ModState.Helped` moment (60 %); or brought along by a friend |

A new face comes at most every other day.
Whoever comes walks in from pavement out of view, stops in front of the player, waves and waits to be spoken to (45 s; they follow a little if the player moves on, then give up).

The conversation is the game's own dialogue panel with both of them facing and talking (`talk_a` / `talk_b` in turn); "Swap numbers" puts them in the phone as a contact whose id is their name.
A look is the game's own appearance system run from the person's seed (`AppearanceSetter.RandomizeElements(gender, skin, casual, seed)`), so Dana looks like Dana every time; `SocialCast` has two bodies at most and rebuilds one only when it is needed for somebody else.

## The relationship model (`SocialRules`)

Per person: familiarity, affection, trust (0 to 100), today's mood, last contact, up to six memories, what they would like to do.
The People page (`reallife-people`, a contact that is called) shows all of it.

| stage | reached when | lost when |
|---|---|---|
| acquaintance | numbers were swapped | (drifts: familiarity under 8 and ten days without a word; a call brings them back) |
| friend | familiarity 30, affection 30, trust 20, two things done together | any of the three 10 below that |
| close friend | 60 / 60 / 55, five things done together, known ten days | any of the three 10 below that |
| dating | asked on the phone; they say yes at affection 60 and trust 45, only people marked compatible (half of the six, never fewer than two), one at a time | affection under 35 or trust under 25: they end it |
| partner | asked; yes at affection 80, trust 70, fourteen days of dating and four dates | as above |
| living together | asked; yes at trust 85 and twenty-one days as partners. Text and state only: a daily mood, their texts | with the break-up |

Worth: a first chat of the day +3 familiarity, +2 affection (half as much again face to face; +1 more if they texted since yesterday and the player is getting back to them).
A warm answer +1.5 affection, a curt one -2.
An outing +6 to +8 familiarity, affection by how much it is their kind of thing (0.7 to 1.4), +2 to +5 trust; on time +2 trust.

Over 20 game minutes late -3 trust, -2 affection (twice that for somebody punctual, nothing for somebody who runs late themselves).
Not turning up within 75 minutes -12 trust, -10 affection and a text.
Calling a plan off -1 / -1, at under an hour's notice -4 / -3.
A favour done +8 trust, +4 affection.
A birthday remembered +8 affection, +3 trust, forgotten -4.
Being arrested -4 trust with everybody (family -2, the supportive -1).

A favour asked -2 trust, a third within a week -5 and a refusal.
Borrowed money not returned in ten days -10 trust.
Neglect: after 3 (partner), 4, 7 (friend), 10 (close friend) or 6 (Mom) days without a word, -1 familiarity, -1 affection, -0.5 trust a day.
With "Relationships fade..." off nothing is ever lost.

The Law feature is asked only through `ISocialLaw` (`SocialLinks.Law`, null until somebody sets it): with a record and a reputation under 60, people who are punctual, careful or reliable lose 0.3 trust a day.
The Home and health feature is told through `ISocialWellbeing` (`SocialLinks.Wellbeing`, null likewise): stress down after a good outing, a meal eaten.

## Things to do together (`OutingScene`)

A plan is agreed on the phone (they offer what they would like first, then the rest; a place within 1.8 km is found then and named; two times are offered from their free hours and the place's): one plan at a time, saved.
The place and the hour are found together (`SocialLife.PlaceAndHour`): the place must be open for the whole outing by its business's own week of hours (`SocialPlaces.OpenFor`, `SocialRules.OpenThrough`), another of the nearest is taken when one is shut, up to eight of their free hours are tried, and with every place of the kind shut they put a walk forward instead (`reallife_social_plan_shut_walk`); the second time offered is open too. Flare and street invitations find theirs the same way.
An hour before, a reminder and the plan on screen with a marker (the game's own guider, so it is on the city map too).

They may cancel by text beforehand (5 % plus traits, mood and rain; never the reliable) and would still like to another day.
They arrive on their own time (the punctual early, the late late: `SocialPlan.Arrives`, a time of its own a few minutes before or after the one agreed) and, if the player is there first or would see them appear, walk in from out of view; otherwise they are waiting.

See  [Arriving, and what holds a scene up](#arriving-and-what-holds-a-scene-up).

| outing | where (the game's business types) | a head | inside | needs |
|---|---|---|---|---|
| coffee | coffee shop | 6 | 25 min; both come out with a cup (`cup_hold`, `sip`) | |
| a meal | fast food restaurant | 24 | 70 min; fills by the game's burger saturation (70, read from `ItemsGetter` through `OutingDef.MealItem`), capped at 100 | |
| drinks | nightclub | 32 | 110 min; outside again, if it went well, a toast (`cheers`, a `wine_glass` each) | friend |
| the cinema | cinema, theater | 18 | 130 min, rested as in the game's own cinema seat (its `RestActivity`): through the skip no walking drain and the bench's recovery at the default multiplier (`OutingDef.SeatedRest`, `OutingScene.Rest`), given back when the skip ends, when the outing ends and when the feature faults | |
| a workout | gym | 15 | 60 min, -14 energy | |
| a look round your business | a business the player rents | | 30 min | friend |
| a walk | outside any business | | 160 m walked with them beside the player; twice on the way they talk as they walk (`walk_talk` / `walk_talk_l` by the side they are on, upper body over the game's walk); by one of the game's street benches the panel offers "Sit down", once a walk, and a rest counts as 40 m (Family,  [Sitting](family.md#sitting-peopleseatingcs-the-partner-at-home-a-bench-on-a-walk)) | |
| a drive | outside any business | | 650 m driven with them in the back, then pull over | friend, a car |
| a lift (favour) | from one address to another | | to the door they name; petrol money | |
| helping them move (favour) | between two doors 22 to 75 m apart | | three boxes carried (`carry_box_hold`; the box is `moving_box` when the bundle has it, else `parcel_m`) | friend |

Greeting: the greeting clips are made for two people facing 0.80 m apart.
So on hello the player is held where they stand and whoever is met walks the last steps up to them (`CityPerson.StepTo`: to 0.80 m on the line between the two, the agent's own stopping distance allowed for; four and a half seconds at most), and both turn to each other.

Then both halves start at the same moment: `social_cheek_kiss` for a partner (dating or married); a real hug for close friends and family (`social_hug_a` on the player, `social_hug_b` on the other, whole body, its full 2.6 s: they finish 0.35 m apart with their arms round each other); `social_handshake` for everybody else (upper body).

A whole-body greeting is played only if the two really ended 0.70 to 0.90 m apart; otherwise the upper body's `social_hug` / `social_handshake` at 0.65 to 1.0 m as before, and failing that `wave_hello` on both.
Then the dialogue panel: a chat about something that really happened (see below) and who pays (my treat, split, or theirs if it was their idea or they are generous).
A place found shut on the day (temporarily closed, a fire, an inspection: what cannot be known when the plan is made) becomes a walk.

Afterwards: what they thought, a card with what it cost and did, a word and a wave, and they walk off out of view (after a good outing with a wave over the shoulder as they go: `goodbye_wave_walk`, the right arm over the game's walk).

The toast after drinks (`OutingScene.RaiseToast`): `cheers` is made for two facing 1.05 m apart (at that distance the two bowls meet at its `clink` mark, 0.8 s; at 0.80 m they pass each other), so the player is held, the other steps to 1.05 m, both turn, and both start it together with the glass at the clip's grip.

Only if they really stand 0.97 to 1.13 m apart, otherwise a word (`talk_a`) as after any other outing.
The glasses stay in the hands (the clip ends holding one) until the goodbye.
Walking out on it costs; being taken away from it (a time skip, hospital) does not.

### Arriving, and what holds a scene up

Whoever is met is put out when the player is within 130 m of the place and their own time has come.
Where they stand is found and tested (`SocialPlaces.Standing`: pavement within 1.5 m of the front of the door with room, else a ring of 1.2 to 5 m; from the third try any pavement within 6 m).

If the player would see them appear (there first, or looking that way from under 60 m) they walk in instead: from pavement out of view 14 to 40 m off (`SocialPlaces.WalkIn`), by a route that was measured (`NavMesh.CalculatePath`, complete, 45 m at most: a point 30 m away across the road can be a block's walk) and then confirmed by their own agent (`SocialCast.Walk`).

"Here comes Mom" is shown only once she exists and is walking.
The walk is bounded: if the route ends short, or 40 s have gone, they are put at the place (nobody looking at them or at it), or put nearer out of view and walk again (twice at most, 25 s each), or stop where they are.

Wherever they stand, that is where they are met: the mod's marker (`SocialScene.MarkPerson`: their first name and the distance) is on them from the moment they are out until hello is said, they wave at 11 m and the prompt comes at 3.4 m.
With no way in to be found in four tries they appear by the door as soon as the player looks another way (after six seconds more, regardless).

One log line per person put out: position, distance to the player, distance to the navigation mesh, the route's length and whether it is whole.

The director asks once a tick what, if anything, has the player (`SceneBreak`, one value per condition); every ending is logged with that value and what the scene was doing, and whatever the player had been told about is called off with one notification saying why and what becomes of the plan.

| what happens | before hello | after hello |
|---|---|---|
| the player goes into the venue, or any other building (`Indoors`) | held, not ended: they wait where it was agreed. If they are due and the player is within 130 m (by the door of the building they are in) they are put by the venue's door facing the street while nobody can see, and a notification says "Mom is here, waiting outside Five Cups"; the marker is on them when the player comes out | held: they wait outside. A text after 40 s ("I'm still outside"), gone after 100 s (as being left behind: -4 affection, -3 trust). Timers do not run meanwhile |
| the street is not running for 6 s (`StreetNotReady`: an activity fast-forwarding) | held, as above (nobody is put out meanwhile) | held, as above |
| time is skipped by something else (`TimeSkip`) | held; on the way out the usual clock applies (stood up after 75 game minutes) | ended: cut short, no blame, part of the credit, said with the reason |
| a shift of the mod's jobs (`JobShift`) | ended, the plan kept and said so ("still on: outside Five Cups at 09:00"); the scene comes back when the shift is over | ended: cut short, said with the reason |
| hospital, death (`Hospital`, `PlayerDead`) | ended, the plan kept and said so | ended: cut short, said with the reason |
| a load, the game going (`Loading`, `NoGame`, `Shutdown`) | ended, logged; the plan is in the save | ended, logged |
| the option switched off (`OutingsOff`, `SocialOff`) | the plan is dropped and said so, nobody is stood up | ended: cut short |
| a delivery job of the game's own (`currentPlayerMission`) | nothing: the player is free on the street. Only the game's destination guider is left to the job; the mod's marker shows the way | nothing |
| the game's standing objectives ("pay off your bank loans") | nothing: they are not missions (`currentPlayerMission` is only ever a delivery or food-delivery job) | nothing |
| driving up | they wave at 11 m; "Park up and walk over" within 40 m (a drive or a lift: pull up beside them) | |
| arriving early | "You are early. Mom will be here about 08:57"; she comes at her own time, walking in | |
| arriving late | a text 20 game minutes after the time ("I'm outside Five Cups. Are you far?"), over 20 late costs trust, stood up at 75 with a text | |
| rain | may make them cancel beforehand (by text; a walk more than the rest); once somebody is out the scene plays as it is | a walk goes on |
| met a little way from the door (they had stopped short, or the player went to meet them) | hello is said there | they walk to the door from there (up to 30 s), or the walk starts there |

Somebody walking up to the player (`MeetScene`: a stranger, an introduction, a friend dropping by) is put out the same way: measured route, one log line, the marker on them, sent on again if their route ends short of the player.
They wait outside a door the player went through for as long as they would have waited to be spoken to (45 s).

Whenever they give up or are cut off the player is told ("Dana saw you were busy and went on their way").

There is no "bar", "restaurant" or "park" in the game's business types: drinks are at a nightclub, a meal at a fast food restaurant, and a walk is along the pavement wherever the player leads.

`OutingScene.Food` runs once, at the settlement behind its `_settled` guard, whether the company went well or badly: Home is told a meal was eaten, and fullness rises by the item's saturation unless the game already reads the player full. A called-off plan, an aborted scene and a closed restaurant give no food. Hunger is set directly, because the game's food-eaten event would record the meal a second time. The saved plan is cleared at greeting (`Arrived`) and the scene is not saved, so a load can't serve it twice. Coffee and drinks keep their own effects. With Developer diagnostics on, one `Social check (meal)` line gives the hunger before and after.

Indoors nobody sits: the game's chairs are there, where the cast does not go.

On a walk the two can sit on a street bench (Family,  [Sitting](family.md#sitting-peopleseatingcs-the-partner-at-home-a-bench-on-a-walk)), in the game's own sitting pose; `sit_chat_a` / `_b` are made for a seat of the mod's own and are played by the living city's two friends on a bench (`BenchChatActivity`).

## Talking, texts and calls

A chat (`SocialTalk`) picks one of twenty topics that fit right now, never the one that person talked about last.

The topics: the player's crash, arrest or knock-down and the street it was on; an accident or crime of the last day (from the news log); the best shop's takings or loss yesterday (the game's own `FinancialSummary`); the shift worked today (`JobDirector.EarnedToday`); being ill (`ModState.IsSick`); a break-in; the weather; their own day, job and district; the last thing done together; their birthday coming.

Each has two answers and a reaction to each.

### Typing on a call (the composer)

On a call, with an RLO Texts pack that can answer this person now and "Type your own words in calls" on (`social_composer`, default on, under the social life's options; it needs RLO Texts on), a topic they raise is answered in the player's own words rather than with its two answers (`PersonDialog.Composer`, `SocialComposerRules`):

- The topic's entry has "Type a reply" (the game's own text box, `Core/GameUi/TextBox`, as Flare and street talk use it: placeholder "What do you say?", at most 160 characters) and "Something else". The words sent show as "You: ..." with only the hang-up button, and their answer follows.
- What typed words are worth is settled the moment they are sent, and never from what either side wrote: they are worth the topic's warm answer (its first, the friendly one: `SocialComposerRules.TypedWarm`), through the same `SocialTalk.Answer` as the choice. So a day's chat is worth exactly what it was: the talk once a day (`SocialRules.ChatHad`: a second call that day is "we covered everything"), two topics a call (`SocialRules.CallTopics`), the warm answer each. Typing can never be worth more than choosing.
- What typed words say is read by the game's own rules (`SocialComposerRules.Read`, game-free, as street talk and Flare's invitations read their words), over the words of all nine languages at once, so an answer reads the same whichever of them it is typed in: good news, bad news, a bare yes or no, or nothing clear. The words are made plain first (lower case, accents off, "don't" as "dont", "I'll" as "i", a stretched "goood" or "noooo" without its stretch, a smiley or an emoji as the sign it is, a Chinese character a word of its own). Strengtheners ("very", "really", "kinda") change nothing; turns ("not", "never", "pas", "nicht", "не", 不, the Turkish "değil" after its word) make the next good or bad word the other, so "not bad" is good and "not great" bad; set phrases are read whole ("could be better" is bad, "can't complain" good, "so so" not clear); after "but" every word counts double; a clause that asks ("are you ok?") counts for nothing, but what comes before a question back ("fine thanks, you?") does. More good than bad is good news, more bad than good bad news; anything else is not clear, never bad for want of a word.
- Each topic says what a bare yes or no means to its question (`SocialTopic.YesIs`, `NoIs`: a yes to "Be honest, is {shop} holding up?" is good news, a yes to "Are you ill?" bad), and what it answers good news and bad (`TypedGood`, `TypedBad`): its own reaction where it fits that reading ("Look at you! ..." for the shop's good news), else glad lines (`reallife_social_typed_glad_*`) or sorry lines (`reallife_social_typed_sorry_*`). Anything not clear gets a neutral line (`reallife_social_typed_neutral_*`), never the line for bad news; good news never gets a sorry, bad news never a glad line, and typed words never get the cool answer's hurt reaction (they are worth the warm one). A player once typed "very good! holding all our money!" to the shop's loss and got "Ouch. Thanks for telling me...": typed words then always took the warm answer's reaction, which for that topic is the line for bad news.
- Their answer is asked of RLO Texts off the game's thread (`Wiring/TextsCalls`), at the queue's first priority with a hold of 6 real seconds: data v3's answer to a message (`friend.dm.answer`, `partner.dm.answer`: the person's own card, as their texts have it, with the call's last three lines and the typed words last). Words that do not come in time, fail the checks, speak of chat ("message", "Flare": the card was trained on Flare messages) or read the other way from the typed words (`SocialComposerRules.Against`: good news answered as bad, or bad as good) give way to the written answer the reading picked. The game decides what the words say; the model only words the answer. A call hung up meanwhile lets them go.
- The typed words are made one clean line (`TextCheck.Typed`: no line breaks or control characters, no text markup, none of the card's turn markers, at most 160 characters) before they are shown or put on the card.
- The two answers, exactly as before, whenever the composer cannot answer: no pack, RLO Texts or its messages from people off, the game in a language the pack does not write (any but English for a pack before data v6), the model loading or failed, a machine that writes only what is asked well ahead, a pack of data v2 (it has no such card), and Mom (no pack has a card that answers what is said to her). The choice is made again at every topic, so a model that fails mid-call gives the next topic its two answers.
- Plans, birthdays and gifts, paying back, asking out, making it official, moving in, borrowing and introductions stay buttons under "Something else": each is a rule of the game, and typing never triggers one.
- With Developer diagnostics on, one `Social check (composer)` line for each topic (typed or the two answers, and why) and each typed message (what it was read as, the written answer that picked, what it counted as, measured against the rule's own figures, and the words' card and time or why the written answer was shown). Nothing is sent anywhere: the words are this machine's and change nothing in a Going Public session.

What the next pack would need for Mom, and for a call of its own, is said in [RLO Texts' notes below](#what-a-later-pack-could-add-to-calls).

They get in touch themselves (`SocialDirector.Hourly`: each waking hour a chance of 9 % plus 2 % per person known): an invitation, a favour (a lift, a loan repaid or excused in five to nine days, help moving), news of their day, an introduction, or a reaction to the player's crash, break-in, arrest or wealth (10 k, 50 k, 250 k, 1 M, 5 M: the supportive are pleased, the envious are not). Before the hour's chance, the day after an outing with them they text about how it went (see below). With RLO Texts on, a pack or not, the partner, Mom or a close friend also texts after the player's own big news (a new business, a graduation, a wedding, a baby, the company listed, a court case, a big win or loss: `Texts/TextsDirector.City`), through these same texts and their limits, and whoever texted today, from any feature, is not the one asked.

Texts are the game's own: a `TextMessage` in that contact's thread, with the game's "new message" notification.
Never more than "Most texts in a day" (3) from everybody together, one a person a day, none from 22:00 to 08:00, none at all with the switch off (what must be said, such as a plan called off, is then a notification).

Somebody close may ring instead: a prompt for 22 s, the interaction key answers, and the call opens in the contacts app with what they rang about.
An invitation or favour is answered by calling them.

### Calling back after a text (`PersonDialog.CallBack`)

When somebody texts and the player rings them back, the call opens on what they wrote about, whichever feature sent the text:

- The record is their own thread in the game's phone (`SocialPhone.Thread`, the game's `Contact.messagesQueue`): every text a person of the social life gets lands there, the social life's own, RLO Texts' news and gossip it sends, the family's (Mom, the partner, the old friend's wedding invitation), life's little moments (`reallife_ev_fr*`, `fm*`) and travel's (a nudge to take a break, a souvenir's thanks), each with its written line's key, its values and its time. When RLO Texts worded a text, its message keeps the written line's key beside the words (`t_key`, set in `Wiring/TextsPhone`), so its subject is known too. One choke point, read once a call, and no feature needs a bridge for it.
- The subject (`SocialFollowCatalogue.Of`): of the newest of their texts that has one. Every text the four features can send has a subject but a welcome, an invitation or a favour asked (the call answers those already), plans and where somebody is waiting, a break-up, their own birthday (the call wishes it), the waters breaking, the partner's asks and wishes (talked over at home), the partner's answer to the player's own text about a new home, and the family's own contact's and the in-laws'. Each subject has a line of theirs that refers to it ("Did you get my message about the garage door? Your father still says he can fix it.") and two answers of a kind: one of the twenty topics' where they fit (the crash, the arrest, a bad day...), or a kind of its own (`reallife_social_follow_<kind>_a`, `_b`, `_ra`, `_rb`).
- When (`SocialFollowRules.Pick`): on the day the text came or the next game day, and only a text newer than the last one a call opened on. The person keeps that text's game minute (`SocialPerson.FollowedAt`, saved), so it is spent once shown: hanging up or loading the save never plays it again.
- What it is worth: it is the call's first topic, through the topics' own reckoning (`SocialTalk.Answer`): the day's chat once, two topics a call, the warm or the cool answer's worth. After the day's chat on an earlier call it is said and answered, and worth nothing more. It moves no money and settles nothing a text already settled; an invitation or favour still to answer and a plan that is on keep their own way, before it. A call they make (the phone ringing) is not a call back.
- With a pack and "Type your own words in calls" on, the player types the answer, as to any topic: the card (`friend.dm.answer`, `partner.dm.answer`) has their line about the text in its thread, and with a pack of data 6 its memory says what they texted about ("texted you about the garage door, today"). Mom's calls keep their two answers, as before.
- With Developer diagnostics on, one `Social check (call back)` line a call: the text it opened on (its day, hour, key, the feature that sent it, the subject and how it is answered), or why the call opened as always.

The day after an outing they may text about how it went (`SocialFollowRules.NextDay`, `SocialDirector.NextDayText`): Mom's, the partner's after a date or a friend's line, good or bad, from 09:00 on the day after, while they are awake and have not texted or been in touch that day; a good one's thanks sent the same day says it already. The outing is kept on the person when it ends (`SocialPerson.AfterDay`, `AfterKind`, `AfterWell`, `AfterPlace`, saved) and let go after the day after. It is one of the social life's own texts, with their limits; with a pack, a friend's good one is worded as their thanks for the outing (`friend.thanks`, every pack's card), and the rest keep their written lines. Its call back opens on it too.

### What a later pack could add to calls

The packs' intents are fixed by their data version, so the composer uses what data v3 already has. A later data version could add:

- `mom.dm.answer` (voice `mom`, Mom's face, `news: Mom answers what you said`), so Mom's calls are typed too. Until then her calls keep their two answers.
- Better still, a call's own cards, `friend.call.said`, `partner.call.said` and `mom.call.said`: channel `spoken` (a call, not a Flare message), at most 140 characters, `write: spoken: on the phone`, history the last three lines of the call oldest first (their line, the player's, theirs, with the player's typed words last), and an optional fact `topic: {topic}` naming what they brought up (the crash, the shop's takings, their birthday coming). Trained on the same rows as `friend.dm.answer` with the call's framing, they would never speak of messages or Flare.

Each would be registered in `Texts/TextIntents.cs` with its data version and the generator's fixtures, and `Texts/TextsMapRules.Answering` would name it; the composer asks `TextsHub.WhyNot` with the pack's data, so an older pack keeps the cards it has.

And to texts (none of it is asked of a pack now, so every card keeps the shape and size the packs were trained on):

- The last lines of the thread on a text's card (`history:` with one or two of their earlier texts, oldest first). Today a text's card carries what that person last texted about as a memory line (`talked`/`text`, `Wiring.TextsMemoryFacts.Texted`), the form data 6 knows: the packs answer a thread's last line, so a thread on a text's card would change what the text says.
- Cards of their own for the day after an outing that went badly (`friend.after.bad`), a date (`partner.date.good`, `partner.date.bad`) and Mom's (`mom.after.good`, `mom.after.bad`): each would be registered in `Texts/TextIntents.cs` as data 7 with fixtures, and `Texts/TextsMapRules.Phone` would map `reallife_social_text_nextday_*` to them. Until then their written lines are used.
- `mom.dm.answer`, so a call back to Mom (and any call to her) could be typed.

### Texting back (not built yet)

A reply to a person's text from their thread, rather than a call, would take:

- A button of the game's own under their newest text, as `Family/FamilySms` puts one under the partner's: the click is the mod's, so it comes off before each save and goes back on after, and a save that is read never has it. Only on a text of today or yesterday that has a subject (`SocialFollowCatalogue.Of`) and is not answered yet: the person would keep the minute of the text answered, as `FollowedAt` keeps the one a call opened on, and a call back and a reply would spend the same text.
- Without a pack, two written answers to choose from, warm and cool, as a call's topic; with a pack and "Type your own words in calls" on, the player's own words through `Core/GameUi/TextBox`.
- With a pack, a card of its own (`friend.dm.reply`, `partner.dm.reply`, `mom.dm.reply`, data 7, with fixtures in the generator's spec): the call's `*.dm.answer` cards are spoken, and the packs trained on them would answer as on a call.
- The player's words in the thread as the player's (`TextMessage.isFromPlayer`), their answer a few game minutes later as theirs (in the morning if they are asleep), counted in the day's texts as every text of theirs but sent at the cap too, since the player asked. Worth what a topic's answer on a call is (`SocialTalk.Answer`: the day's chat once), never money.
- Each player's own thread: nothing sent in a Going Public session.

## The City Ledger (`News`)

`News.Report(NewsKind kind, Vector3 where, string detail = null, int amount = 0)` from any feature: a slot in a fixed array of 48, no allocation, no lookup, a return at once when neither the paper nor the social life is on.
Call sites today: `TrafficAccidents` (accident staged with its severity, pedestrian down, hit-and-run), `CrimeDirector` (a crime staged, by catalogue id), `CrimeScene.Conclude` (an arrest), `OffscreenCrimes` (break-in, car break-in), `CrashConsequences` (the player's crash), `PlayerKnockdown`, `PoliceAttention.BeginCustody`.

Each morning after six the paper is written once (`News.Compose`): the worst two things of the last day with the street (the nearest building's address) and hour (on a day of four or more, the worst one and a count of the rest), the weather, a business piece from the player's own books, a piece about somebody the player knows, and one or two of forty fillers that do not repeat until all have run.

It arrives as messages from the `reallife-ledger` contact (one notification) and is read out when that contact is called.
The log, the paper and the fillers seen are in modData.

## Rules kept

- Nothing in the save the game cannot load: `reallife:social*` strings in modData, and contacts made exactly as `Phone/PhoneContacts` makes its own (id, description key, the call dialog set again on every load).
- Each person's record (`reallife:social_people`) ends with the minute of the text a call back last opened on and the outing still to be texted about the day after (fields 43 to 47, after the age's day): an older version reads the fields it knows and leaves these; an older save reads as no text taken up yet and no outing waiting.
- Idle cost: three comparisons a frame; once a second a few more.
  A scene ticks five times a second; per frame only its marker, its key and (via the living city's service) a turn on the spot.
  No per-frame allocation.
- Two characters at most, the social life's own; placed only where `SocialPlaces.Standing` / `JobSpots` / `CitySpots` found and tested room; they move only by their navigation agent; everything is given back in `End`, which the director calls once on every way out, and again by `Cast.ReleaseAll` / `Body.Release`.
- A fault is logged once, the scene is ended with everything given back, and the social life is off for the session.
- A scene that is held up costs one question a tick (`Holding`); the street is asked about only every second and a half, and only before hello with the player indoors.

## Test buttons (Social)

All say why when they cannot (indoors, the street not running, a scene on, a shift, nothing near).
The outing ones play the real thing (walk-in, marker, hello, the activity) and record nothing.
`SocialDirector.TestOutingWithMom()` is "an outing with Mom, here, now": a walk agreed for the pavement where the player stands (`SocialPlaces.Here`, any pavement within 8 m, no business needed), on foot or from a car.

Its label is `reallife_social_test_mom`, and its row is in the chain in `Core/RealLifeOverhaulMod.cs`: `.AddButton("reallife_social_test_mom", () => SocialDirector.Instance?.TestOutingWithMom())`.
"Test: a walk with the chosen person, here" does the same thing for the chosen person (Mom until another is chosen): it is agreed for the pavement where the player stands, and looks for a business only if there is no pavement within 8 m.

"Call the chosen person now and type the chat" (`reallife_social_test_composer`, `SocialDirector.TestComposer`) opens a call at once, in the game's contacts app, with the chosen person or else anybody met whose chat today is still to come; press Chat and the first topic is the shop's loss ("Be honest, is {shop} holding up?"), whatever has happened, with "Type a reply": good news gets a glad line, bad news the sorry one, anything else a neutral line. It says why when it cannot: typing in calls switched off, no pack running (or RLO Texts' messages from people off, or the game in a language the pack does not write), or nobody it can be (Mom has no typed chat, and the others have had today's chat).

"The chosen person texts me, and I call them back" (`reallife_social_test_followup`, `SocialDirector.TestFollowUp`) puts a text in their thread as each feature does, in turn the social life's, the family's, life's and travel's (where that feature texts this person, else the social life's), and opens the call at once: it should open on what they wrote about. An invitation, favour or plan of theirs comes first, and a notice says so.

"The chosen person texts me about yesterday's outing" (`reallife_social_test_nextday`, `SocialDirector.TestNextDay`) sends the day after's text now, in turn as if the outing went well and as if it went badly; no outing is recorded.

## On screen: the game's own interface (Social)

The mod draws nothing of its own for the social life (`SocialHud` is gone).
None of this has been seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| the plan ("Coffee with Mom", the line and the time) | the headline and two lines of a copy of the game's job panel at the top right (`ShiftCard`), shown while a scene is on, from an hour before the plan | the line goes out as a notification each time it changes |
| the place, or the person to meet | the game's destination marker (`GameUi.GuideMark`), moved when the place moves by more than a metre. It was the game's marker already; the mod's own plate for a person is gone | not shown while a delivery job of the game's or a job of the mod's has the marker; tried again every tick |
| "say hello", "talk", "pick up a box", "put it down", and the phone ringing | the game's panel over the person (`GameUi.DoorPanel`, following them), or over the player, with one button the game labels with the key (`reallife_social_button_*`) | tried again every tick while the game's panel shows another place |
| what an outing cost and did | one of the game's notifications, 11 seconds | none needed |

Cost: no per-frame work of the mod's.
The scene still ticks five times a second; what is on screen is brought up to date on that tick and the panels are told only what changed.
The key is no longer read by the mod: the game runs the button on its key or a click.
Log lines with diagnostics on: `Social check (prompt panel)`, `Social check (plan card)`, `Social check (marker)`, each ending PASS or FAIL.

## Not verified in the game (Social)

The game could not be run for the changes of 0.36: that a character can be put on the street and walks while the player is inside a building.
What `GlobalReferences.walkingSpeedWalk` is (the 40 s and 45 m bounds assume about 1.2 m/s).
That the navigation agent's own route agrees with the pedestrians' filter (it is asked, and refused if not).

How the marker on somebody looks beside an incident's marker (they are separate plates; the social one keeps 85 px lower at the top edge).
The wording and length of the new notifications on screen.

Typing on a call has not been seen in the game: that the game's text box opens over a call in the contacts app and hands its words back while the call is open, how "You: ..." and the model's answer look in the call's thread, and how long a pack takes to answer on the owner's machine (the hold is 6 s, then the written answer).

A call back has not been seen in the game: that the person's thread in the game's phone holds every feature's texts as read here, how their line about a text reads in the contacts app's call, and the texts the day after an outing.

## Wish-list (drop in by id, no code change)

**Delivered and played:** `social_handshake`, `social_hug`, `social_hug_a` / `_b`, `social_cheek_kiss`, `cheers` (with `wine_glass`), `walk_talk` / `walk_talk_l`, `goodbye_wave_walk`, and the prop `moving_box`.
**Delivered, not played by Social** (each needs a scene or a placement that does not exist here): `box_lift_with` / `_b` (the boxes of a move are carried one each), and the props `beer_bottle`, `popcorn_tub`, `gift_box`, `flowers_bunch`, `gym_bag`, `newspaper_ledger`.
**Played by the living city instead** (see [Clips nothing played](city-life.md#clips-nothing-played)): `sit_chat_a` / `_b`, `eat_standing` with `takeaway_box`, `point_show`, `phone_answer`.

Clips (Humanoid, in place, from and back to the standing idle unless said):

| id | length | loop | what |
|---|---|---|---|
| `social_handshake` | 1.8 s | once | right hand out to a hand 0.80 m ahead at 1.05 m, two shakes, back. Made for two people facing 0.80 m apart; marks `grip` 0.5, `release` 1.3. Played on both, upper body |
| `social_hug` | 2.6 s | once | a step in, arms round somebody 0.80 m ahead (finishing 0.35 m apart), a pat, a step back. Played on both, upper body today: as a whole-body clip it needs the root to stay put |
| `social_cheek_kiss` | 1.6 s | once | for a partner: lean in from 0.80 m, back |
| `sit_chat_a`, `sit_chat_b` | 6 s | loop | seated on a seat 0.45 m up, talking / listening: for when benches can be used |
| `cheers` | 1.8 s | once | right hand lifts a glass from 1.05 m to shoulder height, a small forward tap, back; prop `wine_glass`; mark `clink` 0.8 |
| `eat_standing` | 3.4 s | once | a bite from something held in the right hand; prop `takeaway_box` in the left |
| `walk_talk` | 5 s | loop, upper body | head turned 25 degrees to the left, small right-hand gestures: over the game's walk, for the one on the right (a mirrored `walk_talk_l`) |
| `point_show` | 2.2 s | once, upper body | an open hand sweeping from the left to straight ahead: "this is my place" |
| `phone_answer` | 1.6 s | once, upper body | phone from the pocket to the right ear; then the game's `TalkingPhone` |
| `box_lift_with` | 2.2 s | once | one of two lifting a long box together, mirrored pair |
| `goodbye_wave_walk` | 1.4 s | once, right arm | a wave over the shoulder while walking away |

Props (pivot at the grip unless said): `wine_glass` 0.07 x 0.20 x 0.07, pivot the middle of the stem.
`beer_bottle` 0.07 x 0.24 x 0.07, pivot 0.08 m up.
`takeaway_box` 0.14 x 0.09 x 0.11, pivot the middle of the underside.
`popcorn_tub` 0.16 x 0.20 x 0.16, pivot the middle of the underside.
`gift_box` 0.20 x 0.14 x 0.20 with a ribbon, ground centre.

`flowers_bunch` 0.22 x 0.42 x 0.22, pivot the wrapped stems 0.10 m up.
`gym_bag` 0.50 x 0.26 x 0.24, pivot the middle of the handles.
`moving_box` 0.45 x 0.35 x 0.35 printed "FRAGILE" (until then `parcel_m`), ground centre.
`newspaper_ledger` (the `newspaper` prop with the Ledger's masthead), as `newspaper`.

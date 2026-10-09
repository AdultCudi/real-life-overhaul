# Writing style for Real Life Overhaul

Every text a player can read follows this sheet: `Locales/en.json`, the guide, the welcome panel, the changelog and the Workshop page. `check.pl` in this folder checks the mechanical rules (`perl check.pl -v ../../Assets/Mods/RealLifeOverhaul/Locales/en.json description.txt`); what it still lists must be a word used in its plain sense (a real bridge, a film shoot, a taxi meter), a spoken line or fade caption that trails off, or one of the kept names of removed things.

## Voice

- Plain English, as a person would write it for a game. Short sentences. Say the thing once.
- State what happens, what it costs and which key does it. Leave out how the mod does it.
- No sales talk and no filler. Words that are never used: seamless, immersive, robust, elevate, delve, dive into, vibrant, bustling, unleash, stunning, enhance, dynamic, powerful, comprehensive, truly, simply, various, whether you..., not just X but Y, it's worth noting, in order to, rest assured, please note, keep in mind, feel free.
- No lists of three for rhythm, no questions asked for effect, no advice on how to feel, no moral.
- Do not hedge. If something is uncertain or rough, say so once, in the guide's "Known limits" page.
- Characters may speak as they like (contractions, slang, a trailing thought). The interface does not.

## True of the code

- Every price, fine, hour, distance and count is the number in the code. In the guide, numbers come from `{facts}` (`Meta/FeatureList.cs`), so they cannot go stale. A toast gets its number from the code through a `{placeholder}`.
- A key is never written out where it can be rebound: use the `{key}` placeholder in a prompt, `{key_interact}`, `{key_garage}` and the like in the guide, and "the interact key" in an option label.
- A text describes what exists now. Nothing about plans, and nothing about things that were removed.

## Punctuation and characters

- No em dash, no en dash, no " - " and no "--" used as a dash. Rewrite the sentence, or use a colon or a comma.
- ASCII only: straight quotes, no ellipsis character, no middle dot, no arrows.
- Three dots only where a spoken line or a fade caption trails off ("On the bus..."). Not on status lines, buttons or prompts.
- One space between sentences. No space before punctuation. One exclamation mark at most, and only in speech.
- In the guide's own mark-up a line that starts with "# " is a heading and one that starts with "- " is a bullet. `<b>` marks a name the player will look for on screen.

## Spelling and words

- British spelling: colour, grey, licence (noun), license (verb), licensed, kerb, tyre, metre, litre, centre, theatre, neighbour, harbour, programme, cheque, judgment (a court's), cosy, organise, realise.
- British words for ordinary things: pavement, lift, flat, bin, petrol, film, car park, bonnet, boot, windscreen, shop.
- Names stay as they are: what the game itself names (Mom, the subway, trucks, the districts, building types such as "Clothing Store") and what a New Yorker in the story says in speech (blocks, quarters, bucks, cab).

## One name per thing

| Thing | Name | Not |
| --- | --- | --- |
| the building the police work from | police station (each has a name: "Midtown Precinct") | precinct, as a common noun |
| where an arrested player sits | holding cell (then "the cell") | jail cell, lock-up |
| the upgrade menu in a repair bay | custom shop | body shop, tuning shop, garage menu |
| where a car is repaired | repair bay | service bay, workshop |
| where a bus calls | bus stop | stop sign, bus station |
| the mod's manual | the Guide | manual, help |
| the mod's page in the game's options | the options page | settings menu, mod menu |
| a saved game | save | savegame, save file (unless the file on disk is meant) |
| the log | Player.log | the log file, the console |
| the phone entries the mod adds | contact | app, number |
| bicycles and motorbikes together | two-wheelers | bikes (a bike is a bicycle or a motorbike, named) |
| the four difficulty settings | preset: Relaxed, Standard, Realistic, Hardcore | mode, difficulty |
| people in the street | people, passers-by, pedestrians | NPCs, agents, actors |

Words that never reach a player: modData, navmesh, prefab, bridge (in the code's sense), stand-in, fallback, pooled, collider, clip, director, bundle, agent, kinematic, null, logged, spawn, mesh, shader, rig, hook, cache, debug. A toast that would need one of them says what the player sees instead ("could not be placed here"), and the detail goes to Player.log.

## Formats

- Money: as the game shows it, `$1,250`, from a placeholder wherever the code knows the amount.
- Per cent: `15%`, no space.
- Time: the 24-hour clock, `17:00`. Lengths of time in words: "2 hours", "3 days".
- Distance and speed: `250 m`, `1.3 km`, `90 km/h`.
- Numbers: figures for amounts, prices, times and anything measured; words for one to ten in running prose.
- Counts that may be one or many are written so that both read well ("{count} options changed" is avoided when {count} can be 1: write "Options changed: {count}").

## Interface text

- Sentence case for every label, button, heading and title: "Chase camera distance", not "Chase Camera Distance". Proper names keep their capitals (the Guide, Gig Line, City Ledger, Player.log).
- No full stop at the end of a label, button or heading. Sentences in toasts, dialogs and the guide end with one.
- An option label says what is on when it is on. Anything more goes in brackets, short: "(off: the game's own camera)".
- Section headers on the options page: `Real Life Overhaul: Camera`.
- Key prompts: `{key}: offer a flyer` (the key, a colon, the action in lower case, no full stop). Two actions: `{key}: sit down, {key2}: leave`. A toast that is a sentence names the key in the sentence: "Press {key} to answer."
- Test buttons: `Test: what it starts`. They start it here and now, so "now" and "here" are not written.
- A refusal is one sentence that says what is missing and, where it helps, what to do: "Test not started: get out of the vehicle first."
- Status lines: `Move {move} of 5, {points} points`. Parts are joined by commas.
- A toast is one or two sentences and fits in about 240 characters.

## The changelog and the Workshop page

- One row per released version, newest first: what a player will notice, in one or two sentences. No file names, no class names.
- The Workshop description uses Steam's BBCode (`[h1]`, `[list]`, `[b]`). Performance figures are only those measured and recorded in the build README, with the machine they were measured on.

## What a text must not break

- A key id never changes; only its value does. Placeholders stay exactly as they are, spelling and braces included.
- A life event's prompt that costs money names the amount as `$12` (the catalogue check reads it), and has no placeholders.
- No two life events, and no two crime lines, have the same text.
- `en.json` stays UTF-8 with its byte order mark, one key per line, in its order.

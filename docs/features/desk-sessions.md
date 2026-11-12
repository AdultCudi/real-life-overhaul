# Desk sessions

## In a sentence

Under the living economy's master option **Living population**, every hands-on management session (a game studio's concept and milestones, a software sprint, a training run, an incident, a pitch, a social media session) is held at the owner's own desk in the firm's office, on the game's own activity panel with its skip and fast forward, or by a manager the player hired; the first kind is a tech firm's weekly team meeting.

## Where the code is

- Folder: [`Scripts/Sessions`](../../Assets/Mods/RealLifeOverhaul/Scripts/Sessions/); the meeting's rules in `Scripts/Tech/People/MeetingRules.cs`; the wiring in `Scripts/Wiring/Wiring.SessionLinks.cs`.
- Options: "Living population" (`living_population`, the overhaul's master switch, on in the beta) and "Desk sessions" (`living_desks`), among the economy's options. Off, no desk is clickable, nobody is reminded and no effect is read: the firms work as before.

```
Sessions/                      the first three are game-free and compiled by EconomyCheck (SessionChecks.cs)
  SessionRules                   a kind (SessionDef: types, schedule, furniture, steps of choices, the feature's callbacks), the registry, the rules
  SessionFile                    a record a kind and business: last held, by whom, handed to a manager, the effect left and until when
  SessionStore                   the save's one line, "reallife:sessions"
  SessionDirector                the one-second timer, the save, the option
  SessionDirector.Day            an hour's turn: managers hold what is handed to them, one reminder a day of what waits
  SessionDirector.Desk           the owner's desk in the player's office, the click, the panel's questions
  SessionDirector.Tests          the two tests
  SessionGame                    the only file that touches the game: the firms, the office's workstations and rota, the click, notices
  SessionPanel                   the game's activity panel: the list of sessions, a page a step, the time in the chair
  SessionBridges                 the Company app's managers
  SessionText, SessionGuide      the words, the feature's entry and the Guide's figures
Tech/People/MeetingRules         the weekly team meeting's rules
Wiring/Wiring.SessionLinks       the meeting as the first kind; the managers; the meeting's week read by the firms' work and the people's day
```

## A kind of session

A feature makes a `SessionDef` and adds it to `SessionRegistry` in its wiring:

- `Types`: the business types it is held at; `EveryDays`: due again so many days after it was held; `Hour`: when a manager holds it.
- `OwnDesk` and `DesksLeast`: the furniture. The office must have so many computer workstations with chairs (the game's `DesktopWorkstationController`, or a `WorkstationController` with a chair, as the ready-made designs and the player's own fit-out give), one of them nobody is rostered at in the business's week: the owner's desk. Without them the panel says what is missing.
- `Steps`: one to three choices each (the game's panel has three keys besides Cancel), each with its name, a line of what it gives and costs, and the minutes it adds.
- `Blocked(ask)`: why it cannot be held now, or null. `Pick(ask, step)`: the sound choice, which a manager makes and a Going Public session takes. `Apply(ask, choices)`: the feature's callback, returning the notice's words and an effect (two figures and days) kept with the record and read back by `SessionDirector.Effect`.

## At the desk

In an office of one of the player's firms where a kind is held, the owner's desk (the first workstation by the layout's ids that nobody is rostered at, the same every visit) answers the game's pointer with "Click to hold a session". The click walks the player there and opens the game's activity panel: each kind held there, what it is, whether it is due, when next, who takes it, what is missing; Begin, Next session, and Hand to manager or Take it back. Begin asks each step's question; the last answer sits the player in the chair for the minutes the answers make, with the game's own skip and fast forward. Only a session sat whole is held; stopped early, nothing changes and it stays due.

## Managers

A manager appointed in the Company app (a place's own manager, found at work there) can take a kind: Hand to manager at the desk. On the kind's due day, from its hour, the manager holds it by their skill: a holder's strength is half the owner's at no skill and all of it at 100 (`SessionRules.Strength`), and each step's choice is the sound one or, at the managers' own chance of a mistake for their skill (`ManagerRules.MistakeChance`), the next one round, rolled from the save's seed, the day, the business and the kind. One notice tells the result. A kind nobody takes waits; once a day from 09:00 one notice says what waits.

## The weekly team meeting

Held every 7 days at the five tech firm types and the game's two office businesses, in an office with two computer workstations or more, with professionals working there. Its aim: lift morale (each professional gains morale and sheds burnout at once, the week's output 3% lower) or push output (the week's output up to 12% higher, burnout a day worked); half an hour does two thirds of a full hour. The week is read by the firms' work points (`TechDirector.Firms`) and the professionals' day after their own mood (`TechDirector.People`), through `TechBridges.TeamWeek`, so burnout bites through the people part's own rules: output, morale, falling ill, leaving. A manager lifts a tired team's morale (burnout averaging 45% or more) and pushes a fresh one.

## Going Public

Only the player's own firms (`GoingPublic.Mine`) and professionals are touched; the rolls are seeded from the save; nothing is sent. Going Public hides the activity panel in a session and presses the button named Start: the session then begins with the sound choices, and a notice says so.

## Cost

No Update. A one-second timer on the real clock compares the day and the hour; an hour's turn reads the player's buildings once (EconomyCheck: 64 firms with four kinds each under 0.5 ms without the game). In an office of the player's, the owner's desk is looked for on going in and when the building's items change in number. The save's line is written in the game's save event.

## Tests

- **Open the weekly team meeting at your desk here**: inside a firm of yours, the meeting made due for the visit and its desk walked to; elsewhere the nearest firm is marked and the notice says to go in; with furniture missing, what.
- **Hold the sessions your managers take now**: every due session handed to a manager at work held by their rules, with how many, or why none.

With Developer diagnostics on: `Sessions check (desk): ...`, `Sessions check (held): ...` and `Sessions check (manager): ...`, each with PASS or the reason.

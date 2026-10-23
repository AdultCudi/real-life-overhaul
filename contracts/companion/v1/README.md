# Companion reports, version 1

RLO owns the meaning of these observations. Consumers do not decode its private
save lines or implement its simulation. This is a read-only reporting contract,
independent of the RLO release number. It adds no mod dependency or network traffic.

On an ordinary save, `SaveHook` captures after all feature save handlers and
`SaveLines.PutBack`, storing the JSON string at `modData["reallife:companion"]`.
The report is derived data: RLO never loads it into gameplay. Older saves without
it continue to work. A failed capture removes an older report. On load, the
settings shown by the report remain those at capture, not the current options.

A live consumer calls `RealLifeOverhaul.Companion.CaptureV1(GameInstance)` by
reflection on the Unity main thread, before its serializer runs. The argument must
be `SaveGameManager.Current`. The API returns JSON or null when the session is not
loaded, wiring is disconnected, or Going Public is active. It does not call save
events, advance a day, make payments, or mutate the supplied instance. A thrown
exception also means unavailable. Consumers must copy the result into their own
snapshot, never the running game's `modData`.

## Envelope and evolution

`schema.json` describes the wire shape. `schemaVersion` and each module's `version`
are integers, currently 1; `modVersion` is diagnostic only. A reader ignores
additive fields and unknown modules. An unknown envelope version makes the whole
report unavailable; an unknown module version affects only that module. Changing
a field's meaning or units requires a new version, with the old API kept during
the consumer migration. Removing a feature must leave its capability explicitly
disabled or unavailable rather than imply a known zero.

`captured` identifies the character and game day/hour/whole minute. The game
stores fractional minutes; the report truncates them. These fields must match the
accompanying snapshot. Save names and build numbers are deliberately excluded:
the game finalises them after the save event, including Save As and the first
save after an update. Addresses identify businesses only within this capture;
they are not durable identities across sales or reopening an address.

Modules carry `ready`, `disabled`, `unavailable` or `failed`. Only `ready` has data;
an empty ready array means a known zero. Missing, disabled, failed and unreadable
data must not become zero. Feature providers are isolated: an exception or invalid
JSON value marks only that module failed. Connection and capture failures can
still make the whole report unavailable.

All money is integer cents. Rates are integer annual basis points (800 means 8%).
No float, NaN or infinity is emitted. Integers stay within ±(2^53−1), arrays within
10,000 entries and the UTF-8 report within 4 MiB. Text fields are nonempty and at
most 1,024 characters. Object names must be unique. Readers also validate unique
row identities and the wage sum, which JSON Schema alone cannot express.

## Capabilities

| Module | Meaning |
| --- | --- |
| `features` | Selected economy, city, company, staff and manager settings (`enabled`) and their runtime availability (`running`) at capture. The flags do not cover all RLO features. |
| `businesses` | Owned, established businesses with valid addresses, opaque type ids, names and temporary closure. Key is `(street, number)`. A custom type id does not claim a supported forecasting model. |
| `employment` | Current hired employees' ordinary hourly rate: rounded base pay, city adjustment using `Prices.Level` and `CompanyCatalogue.Share`, plus the company's agreed raise. `ordinaryHourlyCents = baseHourlyCents + cityHourlyCents + raiseHourlyCents`. The city component may be negative. Player, candidates, replaced and resigned employees are excluded. This is a rate observation, not realised payroll; overtime, sick pay, driver daily hours and other fees require separate rules. |
| `credit` | RLO's additional open non-test accounts. `id` is scoped to the company; `kind` is opaque. `balanceCents` is outstanding debt, and `paymentTomorrowCents` comes directly from `AccountRules.DueTomorrow`. Frozen debts retain their balance and have no payment due. All have `includedInGameLoans: false`. The ordinary game's loan principal, private-lender debt (`Shark`) and debts recorded by other RLO features are excluded. A zero balance covers only these reported credit accounts. |
| `accounting` | `game.financialSummaries` is the source for recorded statements; coverage is only `statement-backed-only`, with `adjustment: none`. RLO already updates these statements. Never add reported wages or debt principal to profit again. This does not claim every RLO cash flow is represented in the statements. |

This version deliberately supplies no forecasts, manager ownership permissions or
write API. A consumer must disclose that its base-game suggestions may not reflect
RLO. Later capabilities should report effective rules or ownership from their
existing feature owners, through `Wiring`, without exposing internal stores.

## Fixtures and checks

`ready.json` and `partial.json` contain synthetic records. They are emitted by the
same serializer, wage helper and credit helper used in the mod. Never put player
saves here. `EconomyCheck` checks these exact bytes during its full run.

From the RLO repository root:

```sh
dotnet build RealLifeOverhaul.Build/EconomyCheck -c Release
dotnet RealLifeOverhaul.Build/EconomyCheck/bin/Release/net10.0/EconomyCheck.dll --companion
# Regenerate only after an intentional contract change; review the resulting diff.
dotnet RealLifeOverhaul.Build/EconomyCheck/bin/Release/net10.0/EconomyCheck.dll --companion contracts/companion/v1
```

Copy the schema and both fixtures byte-for-byte to Big Copilot's `contracts/rlo/`.
There, run `python tools/check_rlo_contract.py <RLO-checkout>` and
`npm run test:python -- tests.test_rlo`. Both repositories keep the files so their
normal tests do not need another checkout or network access.

Before release, save and reload a synthetic game with RLO and inspect the report;
then capture with Big Copilot Link after changing a wage or credit account without
saving. The report must reflect that change while the live `modData` remains
unchanged. Repeat with disabled features, a missing provider and Going Public.
Check that the same recorded profit appears before and after enabling reporting.

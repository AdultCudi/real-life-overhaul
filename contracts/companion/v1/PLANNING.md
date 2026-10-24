# Planning modules, version 1

These modules extend the existing envelope independently. The original five module
schemas and fixtures remain unchanged. Each added module has its own version and
ready/disabled/unavailable/failed status; absence means an older producer. Failure
of one owner does not invalidate another owner. `planning.schema.json` specifies
all emitted fields. Consumers accept unknown additive fields and reject unknown
model identifiers independently. Monetary decimal strings preserve subcent native
prices; amounts named Cents are integer cents. Outside-statement cents are signed
costs: a receipt is negative. Owed and last-bill utility balances are nonnegative.

All captures and `OwnershipV1(GameInstance,string)` run on the main game thread.
Capture readers allocate detached reporting records but do not advance the game,
refresh caches, write save data, clear fault latches or invoke save handlers.
Feature owners project their own state; wiring composes the modules. Normal save
publishing separately writes the reporting key after the owner's save handlers.

## Model semantics

* `prices`: own/effective wholesale amounts, city factors and native default market
  prices. `PriceRules.Level` uses quietBand; `CityAdjust.Of` uses adjustmentQuiet
  and whole-money, midpoint-away-from-zero adjustment of the aggregate native
  category. Statement adjustments are already included. Negative retained native
  deposit transactions prove their category treatment; absent/conflicting evidence
  leaves depositAdjustment null. Positive deposit refunds receive no adjustment.
* `utilities`: terms from `UtilitiesCatalogue`, `UtilitiesRules` and
  `InternetRules`; season tables come from `CalendarRules.Season`. Account accrual
  is cents; balances and paid bills convert whole owner money to cents. Current
  accrual already includes another provider's seat charges. Forecast a new site
  separately from its nonexistent account: rented startup is active/no deposit,
  bought startup is temporary then application. No future outage is assumed.
  Supply states are pending, active, temporary, cut and closed. Provider districts
  and internet needs are native neighbourhood and business type IDs.
* `company`: current commitments and explicit new-site defaults. `parts` reports
  effective use; `partStates` distinguishes disabled and failed parts. City wage
  indexing applies independently of the staff option. Agreed raises and paid
  absences require staff to be active. Cover untilDay is inclusive; overtime and
  campaign untilDay are exclusive. Statement costName is semantic; costKind is
  an owner index. Costs already classified into native statements must not be
  counted again. Accountant fees use the captured hiding-policy state.
* `requirements`: owner licence type predicates, effective fees, late intervals,
  validity and grace. Late means `to > from && (to > after || from < before)`;
  split overnight intervals. A new site receives free startup grace. A purchased
  permit extends `max(untilDay, today-1)+termDays`. The paymentBasis field names
  startup/renewal phases, not a requirement to charge during free grace. When the
  licence feature is disabled, use utilities.internet.needs independently.
* `footfall`: issuePercent is business pressure alone. ambientPercent contains
  cityPercent + companyPercent + other active providers (brands, Flare, Standing).
  interiorPercent is separate. `BusinessCatalogue.Written(own, issue, ambient,
  interior, baseMultiplier)` clamps the three parts independently and calculates
  with float32 at each arithmetic step. When changing prices, replace only the
  old cityPercent within ambientPercent with the hypothetical city result. Never
  reuse the current price component for a hypothetical price. The owner preserves
  active-play own/written/components across save put-back; the separately captured
  registrationPromotionAtCapture is the exact value paired with the native save.
* `interiors`: pure item classification and district standards support hypothetical
  decor. `InteriorRules` owns target, diminishing copies, category caps, variety,
  overall score, standard, footfall, basket, price tolerance and officeBoost. Site
  values are the current owner effects. A new-site model must explicitly assume
  cleanliness/wear and an opening day for grace and ramp; outcomes are not promises.
* `education`: catalogue includes injected definitions. Quotes use detached candidate
  enrollments with `StudyRules`; held/enrolled courses never receive a second new
  course fee. feeNowCents is the first installment, totalFeeCents sums all rounded
  installments at the captured price level, immediateCashCents is zero for loan
  financing. installments trigger afterSessions, not promised calendar completion;
  future price changes, attendance, cash and loan capacity can change the outcome.
  daysMask/startHour reflect the selected mode. Existing enrollment paidCents is
  actual cash fees; loanCents is financing. Retakes and future interest are separate.
* `bankTerms`: effective rates already include native multipliers, effective cap
  already includes RLO secured lending (`included-in-effective-cap`), and native
  debt at the bank is read directly. Native means eligibility still uses the native
  wealth/profit/total-native-debt basis and tutorial minimum. Additional RLO credit
  accounts do not enter that calculation. Principal is never an operating expense.
* `outsideStatements`: retained paid owner transactions, with explicit place/company
  scope and exact day windows. Known utility bills, insurance premiums, accountant,
  tax and business-pressure cash rows are classified. Coverage is partial: cover
  excess cannot be reconstructed as an independent cash transaction, and other
  owners' unknown flows are not fabricated. Accrued/owed account balances are
  separate; do not add them to historical paid transactions. Tax labels are emitted
  by their owners, with recurring/once classification. Other tax effects remain
  explicitly outside the native forecast.
* `closures`: observed owner hold endpoints; fromDay is unknown, utility holds can
  renew, and future incidents remain risks. An endpoint is not a promise of revenue.
* `ownership`: snapshot advisories require a fresh live query to authorize a write.

## Ownership request

`OwnershipV1` accepts schemaVersion 1, operation schedule/hire/wholesale/rent,
phase review/apply/undo and 1 to 256 targets. Each target contains street and number.
Schedule targets carry a full seven-day week of unique day indices 0 to 6:
`{day,isOpen,hours:[{from,to}],shifts:[{from,to,employeeId,itemInstanceId}]}`.
Hours use integers 0 to 24; closed days have empty hours. A null itemInstanceId is
allowed. Hire targets add boolean hiring/moving/scheduling; at least one is true,
and scheduling requires the full week. Wholesale targets add contractId,
action set/remove and unique itemIds. Set requires items; remove may be empty.
Rent targets need only the address. All target shapes are checked before any
owner is consulted. Duplicate JSON keys, trailing input and >256 KiB requests fail.

Only `allowed` authorizes a write. Conditional/refused/unknown all block. Manager
Rota/Hiring ownership is preserved; stock writes are allowed only when every
affected item is already explicitly player-held. No query transfers control.
Estate-managed/rented/busy/sale properties block rent, and proposed late hours
require an existing valid permit. Undo is checked against its restored target.
The response schema specifies non-null owner/task strings and machine-readable
reasons/effects. No confirmation token or automatic manager handover exists.

## Reproduction and limits

`EconomyCheck --companion contracts/companion/v1` generates utility calendars and
vectors, planning arithmetic vectors and the education example using production
helpers. Without an output argument it compares fixture bytes. Run
`python3 RealLifeOverhaul.Build/Companion/schema.py` after intentional shape changes.
Ownership parser tests compile the production parser against a synthetic session
boundary; they cover successful, malformed, missing and failed provider paths.
Utility and education projections verify owner-store bytes before/after. The
runtime-specific full capture and save-restore tests still require an actual game
session; offline tests do not claim those were run.

### Registered price basis and late catalogue changes

A ready `prices` module covers every currently registered item, including furniture and
unpriced services. `wholesaleBasis` and `defaultMarketBasis` are both
`item-field-before-market-index-and-multiplier`. The SDK getter `Item.DefaultMarketPrice`
is exactly a read of `defaultMarketPrice`; `wholesalePrice` is the public raw field.
ContractCheck verifies the getter's IL and a multiplier of 1.3. The production PriceRow
vectors preserve the fields at multipliers 1 and 1.3; downstream native price multipliers
are deliberately not incorporated in these captured amounts.

`registered-current` means RLO did not adjust this registered item: kind -1, factor 1000,
no exchange factor, own and effective wholesale both the current field. It does not assert
an original pre-registration price. Capture never writes Own, Goods, Priced or KindCount.
`defaultMarketSource: live-registered-item` reports the current getter, including another
mod's registration changes. In contrast, price-tolerance `marketSource:
city-find-goods-captured-item` reports the owner's cached Goods.Market at FindGoods. Its
`marketBasis` is also before the native market multiplier. A late catalogue patch can
make these two observations differ; consumers must preserve that distinction.

A disabled prices module permits the static baseline without RLO adjustment. Failed or
unavailable means unknown; it does not permit a silent baseline fallback. Bank terms have
`quoteBasis: captured-rating-and-settings`: their effective cap already includes secured
credit, and the captured native debt must still be subtracted.

### Company parts and deduction classification

`partStates` uses the same owner status function as individual projections. Consumers must
ignore stored policies unless the relevant part is ready. Disabled means no ongoing effect;
failed or unavailable means unknown. `taxEffectsOutsideNativeForecast` is empty when the
Tax part is disabled; failure/startup is represented by `company-tax-failed` or
`company-tax-unavailable`, respectively. Historical paid transactions and exact deduction
labels remain observations regardless of current feature enablement.

`recurringDeductionLabels` contains only proven recurring/once semantics. The additional
`unclassifiedDeductionLabels` lists exact owner labels with mixed or unproved recurrence:
Tech (including relief and write-down), Online, Brands, Services vans, Empire Motors,
Estate, Dealer, Own Bank and City Living adjustments. These projections live in their
owners and use the actual private localization keys. A matching recorded deduction is
unknown for future extrapolation; an unrelated native label is not thereby mod-unknown.
City rent/wage adjustments and broadband company running costs have recurring labels.
Material trading deliberately uses the game's existing `tax_import` label.

### Office carry lifecycle

InteriorDirector.Book reads and writes carry per site. BookOffice's early native eligibility
guards leave it unchanged; InteriorRules.OfficeClients resets it when capacity binds.
InteriorDirector.Round clears the entire carry dictionary every morning. `officeSequence`
vectors demonstrate hourly persistence, skipped booking, a capacity bound and morning
reset. A full-day forecast starts at zero every day. These captures do not claim an exact
midday remainder for a partial-day forecast.

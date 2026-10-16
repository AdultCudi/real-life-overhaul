# Multiplayer safety

Going Public is the mod that lets several players share one city. Each player plays their own save on their own machine; the host's machine runs the shared world: the clock, the weather, the market, who owns what, and the traffic near the host. In a session the mod's city economy, government and stock market are the host's too, sent to the guests by the shared city (`Core/SharedCity`).

Every change to RLO MUST stay safe in a Going Public session. `RealLifeOverhaul.Build/docs/other-mods.md`, "Going Public", explains what each feature does there.

| What to ask | Where |
| --- | --- |
| Is a session on, and is this machine the host or a guest? | `GoingPublic.InSession`, `IsHost`, `IsGuest`, `Role` |
| May the clock be paused, skipped or sped? | `GoingPublic.ClockShared` |
| Is the traffic here the host's? | `GoingPublic.TrafficMirrored` |
| Is this building or vehicle the player's own? | `GoingPublic.Mine(reg)`, `GoingPublic.MineVehicle(vehicle)` |
| Does the host run this part of the city? | `SharedCity.Follows(id)` |

Each is a field read or close to one.

### RLO-MP-001: Ask GoingPublic where the machine stands

**MUST.** Read the session's state from `Core/GoingPublic` and nowhere else. **MUST NOT** look for Going Public's types, statics or messages yourself.

Why: `Core/GoingPublic` reads the other mod once by reflection and survives its updates; 32 places read `InSession` today.

Checked by: review, StandardCheck (for its network, [RLO-MP-006](#rlo-mp-006-the-network-only-through-the-shared-city)).

### RLO-MP-002: The clock is everyone's

**MUST NOT** pause, skip or speed the game's clock while `GoingPublic.ClockShared` is true. Offer a skip through an activity the game's own panel shows, so that Going Public can put it to the players' vote.

Why: Going Public puts back any jump of the clock it did not make, and the other players' days would go out of step.

Checked by: review.

### RLO-MP-003: Never change another player's things

**MUST.** Before changing a building or a vehicle, ask `GoingPublic.Mine(reg)` or `GoingPublic.MineVehicle(vehicle)` and leave it alone unless it is the player's own. On a guest, while `GoingPublic.TrafficMirrored` is true, **MUST NOT** take a car out of traffic, stop it or steer it.

Why: another player's business looks like a rented one on this machine, and a guest's traffic near the host is the host's cars, placed from the host's figures. 109 places call `Mine` or `MineVehicle` today.

Checked by: review.

```csharp
if (!GoingPublic.Mine(reg))   // good
{
    continue;
}
```

### RLO-MP-004: Same inputs, same answer

**MUST.** Work out anything that the shared city carries, or that two machines must agree on (a building picked, a street event's day), on the host, or from a seeded random number and inputs both machines have. A rules class takes its random numbers and its time from its caller: **MUST NOT** use `new Random()` without a seed, `UnityEngine.Random`, `DateTime.Now` or `Environment.TickCount` in a `*Rules.cs` file.

Why: two machines that roll their own dice see two different cities. The mod's 581 uses of `UnityEngine.Random` are for what one machine alone shows; no rules file reads the clock or an unseeded random number today.

Checked by: StandardCheck (rules files), review.

```csharp
public static int PickSite(int seed, int count)   // good: the caller passes the host's seed
{
    var random = new Random(seed);
    return random.Next(count);
}
```

### RLO-MP-005: Numbers in saves and messages are culture-free

**MUST.** Write and read every number in a save line, a shared city message or a self-check line with `CultureInfo.InvariantCulture`.

Why: a machine set to German writes `1,5` where another reads `1.5`; the mod uses the invariant culture in 603 places today.

Checked by: review, the save round-trip checks in EconomyCheck.

### RLO-MP-006: The network only through the shared city

**MUST.** Send and receive the mod's own data only as a part of the shared city (`Core/SharedCity`, `SharedCityPart`), which `Core/GoingPublicLink` alone sends over Going Public's connection. **MUST NOT** name Going Public's network types (`MPServer`, `MPClient`, `MPLink`, `MessageEnvelope`) anywhere else.

Why: the link's messages carry the mod's version and format, and a guest on another version is refused and plays the city alone instead of reading what it cannot. Today only `Core/GoingPublic.cs`, `Core/GoingPublicLink.cs` and `Core/OtherMods.cs` name them.

Checked by: StandardCheck.

### RLO-MP-007: Nothing new that an older or absent RLO cannot read

**MUST NOT** put anything new into what Going Public itself sends or keeps in step (a player's appearance, a building's state, a vehicle) that a player without RLO, or with an older RLO, cannot read. Use only ids the game itself knows for an appearance.

Why: Going Public sends the game's own data to every player, and a player without the mod must still be able to join.

Checked by: review.

### RLO-MP-008: Try both roles

**SHOULD.** Before you open a pull request that touches the city, the clock, traffic, buildings or vehicles, run the test "Pretend a Going Public session" under Options > Tests, as host and as guest, and read the self-check lines it writes.

Why: most players never join a session, so a fault there is reported late.

Checked by: review.

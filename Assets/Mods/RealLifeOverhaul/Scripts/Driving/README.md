# `Scripts/Driving`

The car the player drives: performance and handling by kind of car, the eased throttle, speedometer (a reading in the game's vehicle panel, SpeedReading), automatic turn signals (TurnSignals), towing (VehicleTow), and (in `Sound`) the recorded car sounds of the Garage's option: engines by kind, exhausts, turbos, tyres.

## Read first

- `CarCatalogue.cs`: every vehicle type's targets (game-free).
- `CarModel.cs`: the physics model that meets a target (game-free).
- `CarTuning.cs`: applies it to the driven car and puts it all back.
- `HandlingRules.cs`, `HandlingModel.cs`, `HandlingTurn.cs`, `CurveKeys.cs`: the handling's levels and its model, built on the car's own game values (game-free).
- `CarHandling.cs`: reads the driven car's own values, puts the handling on and puts it back.
- `TurnSignalRules.cs`: when the driven car signals a turn by itself and when the signal goes off (game-free); `TurnSignals.cs` follows the car and switches the game's own signal; `TurnSignalGame.cs` is where it meets the game.

## Documentation

How it works, its rules and its tests: [Driving](../../../../../docs/features/driving.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.

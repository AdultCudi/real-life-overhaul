# `Scripts/Transport`

Bus stops and buses, the subway as a journey, car ownership, and the taxi called by phone (Taxi).

## Read first

- `TransitMap.cs`: the bus network.
- `TransitDirector.cs`: what of it is on the street.
- `CarOwnership.cs`: the driven car: wear, fuel, breakdowns, insurance.
- `Taxi/TaxiDirector.cs`: one taxi call.

## Parts

The large types are split into `Type.Part.cs` files, one concern each:

- `TransitDirector`: `.Timetable` (the place served and its calls), `.Buses`, `.Arrival`, `.People`, `.Stops`,
  `.Pointing`, `.Player`, `.Ride`, `.Journey`, `.Tests`.
- `TransitMap`: `.Stops`, `.Bays`, `.Log`; `TransitStop.cs` holds the stop and line types.
- `TransitBus`: `.Start`, `.Bay`, `.Held`, `.Away`. `BusRider`: `.Doors`, `.Ride`, `.Hold`.
- `TransitPlan.Journeys`, `BusZones` (`.Cars`, `.Pieces`), `CarOwnership` (`.Garage`, `.Help`, `.Tests`),
  `Taxi/TaxiDirector` (`.Ending`, `.Tests`).

## Documentation

How it works, its rules and its tests: [Transport](../../../../../docs/features/transport.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.

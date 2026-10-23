# `Scripts/Meals`

Meals and music: food the player eats is taken to a free seat in the same room and eaten there with the food in front of them, a dish cooked at home is carried from the cooker to the table, and "Dance" in the game's radio panel over a loudspeaker dances to the music.

## Read first

- `MealsRules.cs`: which seat, the same room, how long a meal is watched, when dancing lifts the mood (game-free).
- `MealsDirector.cs`: the one component, a meal step by step; `MealsDirector.Dance.cs` the dance.
- `MealsGame.cs`: the only file that touches the game.
- `MealBody.cs`: the player's body at a meal, and everything given back.

## Documentation

How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.

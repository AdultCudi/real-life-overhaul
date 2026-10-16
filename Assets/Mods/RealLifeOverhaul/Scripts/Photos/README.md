# `Scripts/Photos`

The Camera and the Gallery on the phone: the photos of a save, favourites, the phone's wallpaper from a photo, and sharing to Flare. Every file is read and written off the main thread; nothing goes into the save.

## Read first

- `PhotosDirector.cs`: the two apps kept going.
- `CameraMode.cs`: the camera itself.
- `GalleryRules.cs`: the Gallery (game-free).
- `PhotoStore.cs`: the save's photos on disk.

## Documentation

How it works, its rules and its tests: [Wiring (PhotosLinks.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.

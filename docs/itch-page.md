# itch.io page — copy and paste

Create it at itch.io → **Upload new project**. Keep it a **draft** until `v1.0.0`.

| Field | Value |
|---|---|
| Title | Tailwind |
| Project URL | `tailwind` (must match the `ITCH_GAME` variable) |
| Short description | Tap to fly a paper glider over the rooftops at dusk. Ride the wind to go faster. |
| Classification | Games |
| Kind of project | Downloadable |
| Release status | In development (Released at v1.0.0) |
| Pricing | No payments |
| Uploads | Done by CI (butler): channels `android` and `windows`. Nothing to upload by hand |
| Genre | Action |
| Tags | `one-button`, `casual`, `arcade`, `endless`, `flappy-bird`, `2d`, `minimalist`, `android`, `unity` |
| Platforms (tick) | Windows, Android |
| Cover image | 630 × 500. Use the title screenshot cropped, or `docs/screenshots/title.png` |
| Screenshots | `docs/screenshots/title.png`, `wind-stream.png`, `results.png` |
| Gameplay video / GIF | `docs/tailwind.gif` (or the MP4 from `Tools/make-gif.ps1`) |

## Description

> **Mây is a paper glider with letters to deliver across Gióng Town before night falls.**
>
> Tap to flap between the chimneys and the lantern lines. Catch the pale **wind streams**: inside one,
> gravity eases, you speed up and your combo climbs to ×5. Letters are worth 10 × your combo, so the
> best runs chain one stream into the next.
>
> - One button. Runs last a minute; retry is instant.
> - Wind-stream combos, near-miss bonuses, and a short story that unlocks as you fly further
>   (100, 300, 600 and 1,000 m).
> - Hand-made course pieces mixed by a seeded generator, checked by a test bot so every gap is fair.
> - Plays offline. No ads, no sign-in, and Unity's analytics services are switched off.
>
> **Controls:** tap (phone) · Space or click (Windows) · Back/Esc pauses.
>
> Made solo in Unity by Tran Van Truong, a Unity game developer. Source code and design notes:
> https://github.com/tranvantruongdev/tailwind

## Install notes (paste under the downloads)

> **Android:** download the APK, open it, and allow installs from this source when asked.
> **Windows:** unzip and run `Tailwind.exe`. Windows SmartScreen may warn because the game isn't
> code-signed: choose *More info → Run anyway*.

# Unity Mobile Template

A starter for small, polished **Unity 6** mobile games: portrait 2D, with Android APK/AAB and Windows
builds. Every portfolio game starts here (*Tailwind*, *Cadence Club*, *Night Courier*, …).

> **Status (Oct 2026):** the pure C# core and its 35 tests pass in CI (`dotnet test`). The Unity-side
> code (runtime, editor tools, sample scenes) is written but not yet compiled in the Unity Editor;
> first Unity verification is in progress.

## What's inside

| Area | What you get |
|---|---|
| **Core (pure C#)** | State machine, deterministic PCG32 random numbers, CSV master data with line-numbered errors, versioned saves with migrations and backup fallback, MVP presenter base, clock abstraction. **No UnityEngine**, so it's unit-tested with `dotnet test` in seconds. |
| **Runtime** | Boot flow (Boot → Title → Game) with fades, service registry, crash-safe file saves, music crossfade and SFX pool, Android haptics, safe area, screen stack with back-button handling, game-feel helpers (punch, shake, hit-stop, flash, floating text), debug overlay (FPS, memory, reset save) |
| **Sample** | Title screen + settings popup (MVP) + a 10-second tap game + results. It uses every system above. Delete it when you start a real game. |
| **Editor** | `Template > Apply Project Setup` (player settings, scenes, build list in code), `Template > Build > …`, `Template > Import Master Data` (CSV → ScriptableObject) |
| **CI** | Core tests on every push (dotnet, no licence); Unity EditMode tests via GameCI; **tag `v*` → APK + AAB + Windows zip → GitHub Release + itch.io** |

**Packages:** UniTask, PrimeTween (via npm, which its licence allows for templates), Input System,
Localization, Newtonsoft JSON, Test Framework. See `Tools/setup/template-packages.json`.

## First-time setup

1. Install the latest **Unity 6 LTS** in Unity Hub with **Android Build Support** (including OpenJDK
   and the Android SDK and NDK) and **Windows Build Support**. Sign in with your *personal* Unity account
   (a Personal licence is free).
2. In Unity Hub, create a throwaway project from the **Universal 2D** template, e.g. `C:\PJ\_u2d`.
3. Adopt its settings into this repo:
   ```bash
   node Tools/setup/adopt-unity-project.mjs C:/PJ/_u2d
   ```
4. Unity Hub → **Add project from disk** → this folder. Let packages import.
5. Run the menu **Template > Apply Project Setup**, open `Assets/_Project/Scenes/Boot.unity`, press **Play**.
6. **Window > General > Test Runner > EditMode > Run All.** Everything should pass.

## Starting a new game from the template

1. On GitHub: **Use this template → Create a new repository** (public, so Actions minutes are free).
2. Clone it, then complete steps 2–5 above. The template's Unity settings are per-machine, so adopt again.
3. Change the identity in `Assets/_Project/Editor/Setup/TemplateSetup.cs` (`productName`, application
   id `com.tranvantruong.<game>`) and re-run **Apply Project Setup**.
4. Edit `SaveSchema` (`Assets/_Project/Scripts/Core/Save/SaveMigrator.cs`) for your game's save data.
5. Delete `Assets/_Project/Scripts/Runtime/Game/Sample/` and build your own scenes.

## Everyday commands

```bash
dotnet test Tools/CoreTests/Template.Core.Tests.csproj     # core logic tests, ~5 s
```

| Task | How |
|---|---|
| Build APK locally | Menu **Template > Build > Android APK** (set `ANDROID_KEYSTORE_PATH` + passwords as environment variables to sign) |
| Build for Google Play | **Template > Build > Android App Bundle** |
| Build Windows | **Template > Build > Windows** (portrait 540×960 window) |
| Import master data | Edit `MasterData/*.csv`, then **Template > Import Master Data** |
| Debug overlay | **F1**, or a four-finger tap on the phone (editor and development builds only) |
| Release | `git tag v1.0.0 && git push --tags`, then let CI build and publish |

## Release setup (once per game repo)

Repo → Settings → Secrets and variables → Actions:

| Secret / variable | Value |
|---|---|
| `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` | Personal licence for CI. Follow [GameCI activation](https://game.ci/docs/github/activation) |
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 your.keystore` (create the keystore once in Unity, **back it up**, never commit it) |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` | From your password manager |
| `BUTLER_API_KEY` | itch.io → Settings → API keys |
| Variables `ITCH_USER`, `ITCH_GAME` | e.g. `tranvantruong` / `tailwind`. Leave unset to skip itch.io |

## Structure

```
Assets/_Project/
  Scripts/Core/      pure C#: rules, data, saves (Template.Core.asmdef, noEngineReferences)
  Scripts/Runtime/   Unity: Infra/ (services, save, audio, device), UI/, Feel/, Game/ (flow, boot, debug, sample)
  Editor/            setup, builds, master-data importers
  Tests/EditMode/    tests (also run by dotnet via Tools/CoreTests)
  Scenes/            created by Apply Project Setup
MasterData/          CSV source of truth for game data
Tools/               dotnet test projects, setup scripts
.github/workflows/   core tests, Unity tests, release
```

## Conventions

- Rules and numbers live in **Core**, as plain C# with tests. Views stay thin.
- No `GameObject.Find`, no allocations in gameplay loops, pool what spawns often.
- Gameplay randomness uses `SeededRandom`, never `UnityEngine.Random`.
- One feature per branch → PR → squash merge.
- AI assistance is welcome. See `CLAUDE.md` for the rules agents follow in this repo.

## Licences

Template code: MIT (see `LICENSE`). UniTask (MIT), PrimeTween (its own licence, installed via the
package manager as it allows), Newtonsoft JSON (MIT) and Unity packages are fetched by Unity, not stored
in this repo.

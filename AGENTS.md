# Poker vs Dealer — Unity project

Unity **6000.5.8f1**, UI Toolkit (runtime UI built in code), C# 9, Android package `com.micorlov.pokervsdealer`.
The game was originally an Expo/React-Native app; the Unity port keeps its rules, copy, layout and look.

Open this folder as a Unity project, or drive everything from the command line (see README.md). Only one
Unity process can open the project at a time.

## Layout

- `Assets/Showdown/Domain/` — pure C# game rules (no UnityEngine; `Showdown.Domain` asmdef has `noEngineReferences`).
- `Assets/Showdown/Runtime/Game/` — `Contracts.cs` is the seam to the UI; `Logic/` is engine-free state, clock and storage
  (testable with dotnet); `Platform/` is the Unity/JNI side (PlayerPrefs, audio, haptics, legacy-data import).
- `Assets/Showdown/Runtime/UI/` — `Kit/` (Theme, Ui, Gfx, Anim, UiHost), `Components/`, `Screens/`, `AppRoot.cs`.
- `Assets/Showdown/Editor/` — project configuration, build entry points, signing.
- `Assets/Showdown/Tests/` — NUnit tests (`Domain`, `Game`). `tests/` — dotnet harnesses and compile checks.

## Rules of the road

- The Unity UI has no gap, gradient, dashed-border or box-shadow primitives, and the bundled Noto Sans has no suit, star,
  cross, house, menu or emoji glyphs. Use the helpers in `UI/Kit` (`Ui.Row(gap, ...)`, `BgGradient`, `Glyph.*`) and the
  drawn-shape components in `UI/Components`.
- The look is "walnut and felt" (`design/walnut-felt/showdown-redesign.pdf`, source HTML beside it). Type comes in four
  OFL faces under `Resources/Fonts`, each falling back to Noto Sans: Manrope (`Ui.Text`), Cormorant Garamond Italic
  (`Ui.Serif`, titles), Cormorant SC (`Ui.Caps`, felt lettering) and DM Mono (`Ui.Mono`, every number). Table and
  result-sheet wording lives in `Game/Logic/TableReport.cs` so it is tested without Unity.
- Never put the INTERNET permission back: the store listing declares no data collection.
- Release signing uses the upload keystore named by `SHOWDOWN_KEYSTORE_PROPERTIES`; never commit keys or passwords.
- `versionCode` must increase for every Play upload (`SHOWDOWN_VERSION_CODE`; the Expo build shipped 4, the first Unity build is 5).

## Verify before finishing

```sh
scripts/domain-test.sh       # game rules, no Unity needed
scripts/game-test.sh         # state/storage/table controller, no Unity needed
scripts/check.sh             # type-check Domain/Runtime/Editor against Unity's real assemblies (~1s, no Unity needed)
scripts/qa.sh SLOT SCENARIO  # render a screen in an isolated project copy and save a PNG under artifacts/captures/
scripts/unity.sh test        # the same tests under Unity's runner
```

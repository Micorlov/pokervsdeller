# Poker vs Dealer (Showdown)

A free, offline casino card game: beat the dealer's five-card climb and the two regulars at the table. Play money,
always topped up — no ads, no purchases. Built with Unity 6 for Android.

## Build

Requires Unity **6000.5.8f1** with Android Build Support (the Editor's bundled SDK, NDK and JDK are used).

```sh
./scripts/unity.sh configure   # settings, UI panel asset, scene
./scripts/unity.sh test        # all EditMode tests under Unity
./scripts/unity.sh apk         # development APK  -> artifacts/android/showdown-dev.apk
./scripts/unity.sh aab         # signed release AAB -> artifacts/android/showdown-release.aab
./scripts/unity.sh mac         # Mac QA player      -> artifacts/macos/Showdown.app
```

Install the development build on a phone: `adb install -r artifacts/android/showdown-dev.apk`.

### Release

```sh
SHOWDOWN_KEYSTORE_PROPERTIES=~/AndroidKeys/pokervsdeller/credentials/keystore.properties \
SHOWDOWN_VERSION_CODE=5 SHOWDOWN_VERSION_NAME=2.0.0 ./scripts/unity.sh aab
```

`keystore.properties` holds `storeFile`, `storePassword`, `keyAlias`, `keyPassword`. The bundle must carry the upload
certificate with SHA-256 `93c9af8e…36f5` (check with the JDK's `keytool -printcert -jarfile <aab>`).
Upload with `python3 scripts/play-upload.py --aab artifacts/android/showdown-release.aab`;
`scripts/play-production-release.py` still points at the old Expo output path (`android/app/build/...`) and needs
updating before it is used with a Unity bundle. `versionCode` has to be higher than the last Play upload every time.

## Test without a device

Fast checks that need no Unity process:

```sh
scripts/domain-test.sh   # rules
scripts/game-test.sh     # state, storage, table controller
scripts/check.sh         # compile check of every assembly against Unity's real engine DLLs
```

Visual checks render a scripted scene to a PNG (780x1688, a 390x844 dp phone at 2x) in an isolated project copy, so
it never collides with an open Editor:

```sh
scripts/qa.sh mine home            # home | stats | settings | howto | stats-played
scripts/qa.sh mine table-kicker    # table-kicker | table-kicker-picked | table-climb | table-pressure
                                   # table-forge | table-bailed | table-result
```

## Existing players

The Unity build ships under the same package and signing key as the Expo build, so it updates the live listing. On the
first launch it copies the old bankroll, record, settings and daily-bonus day out of the Expo app's AsyncStorage
(`RKStorage` SQLite database) into its own storage, once. This is Android-only code and needs checking on a phone that
has the old build installed.

## Layout

See `AGENTS.md`. Store assets, ASO material and Play upload scripts live in `store/` and `scripts/`.

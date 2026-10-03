# Poker vs Dealer ASO and dealer update

Package: `com.micorlov.pokervsdealer`. Status checked October 3, 2026.

This repository is public. Console exports, device evidence and original backups are retained locally in `reports/` and `backups/`, which are excluded from Git. Links to those files work only in the local workspace. Public metadata, approved artwork, templates and implementation scripts are versioned.

The English title, short description, full description, six phone screenshots and feature graphic were submitted and **Google Play confirms they are in review**. The woman-and-cards icon with “Poker vs Dealer” lettering was also submitted on October 3. The signed version-4 production bundle has now been uploaded, validated and submitted, preserving the existing 177 country codes plus Rest of World (178 Console regions). These changes are not yet verified live. Local publication evidence is in `reports/`.

Open `index.html` to review the listing and creatives. Each published screenshot uses an actual production version-3 screen. Editable SVG/HTML templates, original captures and validation records are included. Metadata limits and uploaded image hashes were checked.

The user subsequently requested “Poker vs Dealer” text on the icon. The current store/app artwork includes two lines of gold lettering. The revised store icon was validated and submitted in `backups/20261003T172432Z/`; `reports/icon-validation.json` records its exact hash. The no-text version remains in `backups/local-icon-before-text-20261003/`.

The app supports English only. Holdem Coach's 34-language coverage does not apply to this game. Broader localization requires an app translation project, correct Play locale mapping and fluent review. Keyword themes are relevance hypotheses; no measured search volume or ranking promise is claimed.

`reports/baseline.md` records the available data and missing exports. `calendar.md`, `experiments/plan.json` and `reports/weekly-template.json` define the remaining 90-day work. Experiments have not started. Run one challenger at a time for at least two full weeks and until Console provides sufficient evidence; retain the control if inconclusive. Publication approval and enough traffic are prerequisites. No recurring automation was created for this game.

The requested female dealer in an emerald dress is included in the **submitted version-4 production update**. It appears on the home screen and beside the dealer cards. The titled woman-and-cards icon also appears beside the home title and in the Android launcher. The home screen scrolls on short displays. `reports/dealer-update-v4/` contains previews and build verification. Google approval and public availability are still pending. Store screenshots should show the updated game only after that release reaches production.

The full pre-submission listing and artwork are in `backups/20261003T165952Z/`. Restore reviewed original fields and images through a new Publisher edit, validate, commit and check Console; never assume a submission is live. Preserve later changes and restore only the fields being reverted.

Use `scripts/aso-play.py` for backed-up, validated listing operations. It serializes operations with a lock and requires review status for publishing. The older `scripts/play-listing.py` contains outdated copy/assets and must not be used for this package's current listing. Capture tools target only the disposable test emulator; never replace the production captures with unreleased version-4 screens.

`scripts/play-production-release.py` inspects the verified version-4 artifact by default. `--submit` requires explicit production-release authorization, backs up track/country/bundle state, checks the exact artifact hash, validates the production update and preserves other tracks and country availability. Release notes are versioned under `store/release-notes/`.

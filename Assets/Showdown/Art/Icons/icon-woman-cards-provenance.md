# Poker vs Dealer icon — October 3, 2026

The user requested a woman-and-cards icon matching their attached reference for Google Play and the app. Generated with the built-in image-generation tool, using that attachment as a composition/style reference. The reviewed source is `icon-woman-cards-source.png`; `icon.png` and `store/out/store-icon-512.png` are technical size exports. Android foreground padding protects the composition from launcher masks. This does not replace the requested emerald-dress character inside the game.

## Generation prompt

Use case: ads-marketing. Asset type: square Android game app icon for Poker vs Dealer. Use the attached image strictly as a visual reference and create a new original polished photorealistic icon with closely matching composition: a friendly adult blonde woman with long wavy golden hair, dark tortoiseshell glasses and a red sleeveless knit top, smiling at the viewer. She holds two white ace playing cards, ace of spades behind ace of hearts, on the lower left and points toward them with her other hand. Rich emerald-green softly glowing background. Keep the woman's face and the two recognizable aces as the strong focal points, easily readable at small launcher sizes. Square 1024x1024 full bleed opaque image; straight square corners, no rounded-corner mask, no dark screenshot framing, no lettering except accurate A and suit symbols on the cards, no badges, no watermark. Composition centered with her face and aces within the central 70% so circle masks retain both; simplify hands and card details for icon clarity. Match the reference's warm lighting and green/red/white palette.

## Rebuilding exports

Run `scripts/build-woman-icon.cjs` with Sharp available in the Node runtime. It updates Expo assets and existing native Android density resources. `store/aso/backups/local-icon-before-20261003/` preserves the previous assets. Store publication and app binary release are separate operations.

## Title added — October 3, 2026

The user selected: On the icon: “Poker vs Dealer”. The built-in image-generation tool edited the existing icon to add the exact words as two lines of golden lettering. Square and circle previews were inspected; every letter remains visible. The previous master and exports are in `store/aso/backups/local-icon-before-text-20261003/`. The current `icon-woman-cards-source.png` includes the title.

Edit prompt:

Use case: text-localization (add title only). Edit target: the attached current Poker vs Dealer icon. Preserve the blonde adult woman's identity, face, glasses, red top, hands, two ace cards, green background and overall photographic composition. Add the exact title 'POKER VS DEALER' as a bold, high-contrast professional game logo in the lower center. Set 'POKER' on the first line and 'VS DEALER' on the second line, using heavy compact sans-serif letters with warm golden fill and a dark forest-green outline/shadow or understated dark backing for excellent readability at small icon sizes. Place the entire text block around the lower-middle shirt area, inside the central 65% width, with a generous bottom margin so Android circular masks retain every letter. Keep both ace cards and the face clear. No other text, badges or watermark. Full-bleed square icon, straight square corners, opaque background. Change only what is necessary to add the title; keep the existing art strongly recognizable.

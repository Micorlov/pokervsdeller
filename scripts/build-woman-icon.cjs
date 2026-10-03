/** Package the reviewed icon master into store, Expo and native launcher sizes. */
const fs = require('node:fs/promises');
const path = require('node:path');
let sharp;
try { sharp = require('sharp'); } catch {
  // Codex desktop supplies an optional image-processing runtime. Other
  // environments can install Sharp or expose it through NODE_PATH.
  sharp = require(path.join(require('node:os').homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp'));
}
const root = path.resolve(__dirname, '..');
const green = '#05632F';

async function main() {
  const assets = path.join(root, 'assets');
  const out = path.join(root, 'store/out');
  await fs.mkdir(out, { recursive: true });
  const icon = await sharp(path.join(assets, 'icon-woman-cards-source.png'))
    .resize(1024, 1024).removeAlpha().png().toBuffer();
  const background = await sharp({ create: { width: 1024, height: 1024, channels: 3, background: green } }).png().toBuffer();
  // Android masks the middle 72dp of its 108dp adaptive canvas. Padding keeps
  // the same composition inside that viewport instead of magnifying her face.
  const foreground = await sharp(icon).extend({ top: 256, bottom: 256, left: 256, right: 256, background: green })
    .resize(1024, 1024).png().toBuffer();
  await fs.writeFile(path.join(assets, 'icon.png'), icon);
  await fs.writeFile(path.join(assets, 'android-icon-foreground.png'), foreground);
  await fs.writeFile(path.join(assets, 'android-icon-background.png'), background);
  await sharp(icon).resize(48, 48).png().toFile(path.join(assets, 'favicon.png'));
  await sharp(icon).resize(512, 512).png().toFile(path.join(out, 'store-icon-512.png'));
  await fs.writeFile(path.join(out, 'icon-1024.png'), icon);
  await fs.writeFile(path.join(out, 'adaptive-foreground-1024.png'), foreground);
  await fs.writeFile(path.join(out, 'adaptive-background-1024.png'), background);
  const res = path.join(root, 'android/app/src/main/res');
  try { await fs.access(res); } catch { return; }
  for (const [dpi, scale] of Object.entries({ mdpi: 1, hdpi: 1.5, xhdpi: 2, xxhdpi: 3, xxxhdpi: 4 })) {
    const dir = path.join(res, `mipmap-${dpi}`);
    await fs.mkdir(dir, { recursive: true });
    for (const name of ['ic_launcher', 'ic_launcher_round', 'ic_launcher_foreground', 'ic_launcher_background']) {
      await fs.rm(path.join(dir, `${name}.webp`), { force: true });
    }
    for (const [name, source, size] of [
      ['ic_launcher', icon, 48 * scale], ['ic_launcher_round', icon, 48 * scale],
      ['ic_launcher_foreground', foreground, 108 * scale], ['ic_launcher_felt', background, 108 * scale],
    ]) await sharp(source).resize(Math.round(size), Math.round(size)).png().toFile(path.join(dir, `${name}.png`));
  }
  const adaptive = '<?xml version="1.0" encoding="utf-8"?>\n<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n  <background android:drawable="@mipmap/ic_launcher_felt"/>\n  <foreground android:drawable="@mipmap/ic_launcher_foreground"/>\n</adaptive-icon>\n';
  await fs.mkdir(path.join(res, 'mipmap-anydpi-v26'), { recursive: true });
  for (const name of ['ic_launcher', 'ic_launcher_round']) await fs.writeFile(path.join(res, 'mipmap-anydpi-v26', `${name}.xml`), adaptive);
  console.log('Updated store, Expo and native Android icons from the reviewed master.');
}
main().catch(error => { console.error(error); process.exitCode = 1; });

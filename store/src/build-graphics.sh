#!/bin/bash
# Renders the Play Store feature graphic and frames the raw device captures into
# store-ready 1080x1920 screenshots. Both go through headless Chrome so the
# felt, the type ramp and the card mark stay identical to the app's own theme.
set -euo pipefail
cd "$(dirname "$0")/../.."
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
SRC=store/src
OUT=store/out
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
MARK=$(cat "$SRC/mark.svg")

# $1 = output path, $2 = width, $3 = height, $4 = html body
render() {
  local path=$1 w=$2 h=$3 body=$4 name
  name=$(basename "$path" .png)
  mkdir -p "$(dirname "$path")"
  cat > "$TMP/$name.html" <<HTML
<link rel="preconnect" href="https://fonts.gstatic.com">
<link href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,700;12..96,800&family=Instrument+Sans:wght@400;500;600&display=swap" rel="stylesheet">
<style>
  html,body{margin:0;padding:0;width:${w}px;height:${h}px;overflow:hidden}
  .felt{position:absolute;inset:0;
    background:
      radial-gradient(ellipse 140% 90% at 50% 42%, rgba(0,0,0,0) 55%, rgba(0,0,0,0.34) 100%),
      radial-gradient(ellipse 130% 110% at 50% 38%, #3DA45F 0%, #2A8A4E 52%, #1E6B3C 82%, #14522C 100%);}
  .felt.deep{background:
      radial-gradient(ellipse 140% 90% at 50% 40%, rgba(0,0,0,0) 45%, rgba(0,0,0,0.42) 100%),
      radial-gradient(ellipse 130% 110% at 50% 30%, #1E6B3C 0%, #14522C 55%, #0A3419 100%);}
  .display{font-family:'Bricolage Grotesque','Avenir Next','Helvetica Neue',sans-serif;font-weight:800;color:#fff}
  .body{font-family:'Instrument Sans','Avenir Next','Helvetica Neue',sans-serif;color:rgba(255,255,255,0.86)}
</style>
$body
HTML
  "$CHROME" --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
    --virtual-time-budget=4000 --screenshot="$path" \
    --window-size="$w,$h" "file://$TMP/$name.html" 2>/dev/null
}

# ---------------------------------------------------------------------------
# Feature graphic — 1024x500, no alpha. Play crops this at some placements, so
# nothing load-bearing sits within ~60px of an edge.
# ---------------------------------------------------------------------------
render "$OUT/feature-graphic-1024x500.png" 1024 500 "
<div class='felt'></div>
<div style='position:absolute;inset:0;display:flex;align-items:center;gap:46px;padding:0 62px;box-sizing:border-box'>
  <svg width='352' height='352' viewBox='0 0 1024 1024' style='flex:none'>
    <g transform='translate(512 512) scale(1.78) translate(-468 -599)'>$MARK</g>
  </svg>
  <div>
    <div class='display' style='font-size:72px;line-height:0.98;letter-spacing:-2.6px'>Poker vs&nbsp;Dealer</div>
    <div style='width:78px;height:6px;border-radius:3px;background:#FFC93C;margin:24px 0 20px'></div>
    <div class='body' style='font-size:27px;line-height:1.34;font-weight:500'>Beat the house&rsquo;s climb<br>&mdash; and the whole table.</div>
  </div>
</div>"
echo "  $OUT/feature-graphic-1024x500.png"

# ---------------------------------------------------------------------------
# Store screenshots — 1080x1920 (9:16), the size Play wants for large-format
# placement. Each raw capture is the same 1080x1920, scaled to 900x1600 under a
# headline so the ratio is preserved exactly.
# ---------------------------------------------------------------------------
frame() {
  local name=$1 headline=$2 raw="$PWD/$OUT/raw/$1.png"
  [ -f "$raw" ] || { echo "  ! missing $raw"; return; }
  # Raw captures are the emulator's native 1080x2400. Inset at 740x1644 keeps
  # that ratio exactly and leaves 210px of felt for the headline, which is what
  # brings the canvas back to the 9:16 Play asks for.
  render "$OUT/screenshots/$name.png" 1080 1920 "
<div class='felt deep'></div>
<div style='position:absolute;inset:0;display:flex;flex-direction:column;align-items:center'>
  <div style='height:240px;width:100%;box-sizing:border-box;padding:34px 84px 0;display:flex;
              flex-direction:column;justify-content:center;text-align:center'>
    <div class='display' style='font-size:54px;line-height:1.1;letter-spacing:-1.4px'>$headline</div>
  </div>
  <div style='padding:9px;border-radius:40px;background:#0A2B15;
              border:1px solid rgba(255,255,255,0.14);
              box-shadow:0 30px 70px -18px rgba(0,0,0,0.85)'>
    <img src='file://$raw' style='display:block;width:740px;height:1644px;border-radius:32px'>
  </div>
</div>"
  echo "  $OUT/screenshots/$name.png"
}

if [ -f "$SRC/screenshots.txt" ]; then
  echo "Screenshots:"
  while IFS='|' read -r name headline; do
    [ -z "${name// }" ] && continue
    case "$name" in \#*) continue;; esac
    frame "$name" "$headline"
  done < "$SRC/screenshots.txt"
fi

# Play wants the feature graphic and screenshots without an alpha channel.
echo "Channel check:"
python3 "$SRC/png-alpha.py" --strip "$OUT/feature-graphic-1024x500.png" "$OUT"/screenshots/*.png
python3 "$SRC/png-alpha.py" --add "$OUT/store-icon-512.png"

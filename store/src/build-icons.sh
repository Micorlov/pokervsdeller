#!/bin/bash
# Export the user-approved woman-and-cards master to store and app icon sizes.
# Before October 3, 2026 this entry point rendered the old card-only SVG.
set -euo pipefail
cd "$(dirname "$0")/../.."
exec node scripts/build-woman-icon.cjs

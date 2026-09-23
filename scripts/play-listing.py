#!/usr/bin/env python3
"""Push the store listing — text and graphics — to Google Play.

Usage:
  python3 scripts/play-listing.py [--dry-run]
  python3 scripts/play-listing.py --only icon,featureGraphic

Reads copy from store/listing/*.txt and graphics from store/out/, so the files
that get uploaded are the same ones that were reviewed on disk.

Auth and transport quirks match scripts/play-upload.py — same service account,
same httplib2 workarounds — see that file for why they are load-bearing.
"""
import argparse
import os
import sys

import google_auth_httplib2
import httplib2
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
from googleapiclient.http import MediaFileUpload

SCOPE = "https://www.googleapis.com/auth/androidpublisher"
DEFAULT_CREDENTIALS = os.path.expanduser("~/.config/mcp/google-play-service-account.json")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

TITLE = "Poker vs Dealer"
LISTING_DIR = os.path.join(ROOT, "store", "listing")
OUT_DIR = os.path.join(ROOT, "store", "out")
MANIFEST = os.path.join(ROOT, "store", "src", "screenshots.txt")

SHORT_LIMIT = 80
FULL_LIMIT = 4000


def screenshot_paths():
    """Screenshot order is the manifest's order, so the store matches the build."""
    paths = []
    with open(MANIFEST, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            name = line.split("|", 1)[0].strip()
            paths.append(os.path.join(OUT_DIR, "screenshots", f"{name}.png"))
    return paths


def graphics():
    """Play replaces an image type wholesale, so each value is that type's full set."""
    return {
        "icon": [os.path.join(OUT_DIR, "store-icon-512.png")],
        "featureGraphic": [os.path.join(OUT_DIR, "feature-graphic-1024x500.png")],
        "phoneScreenshots": screenshot_paths(),
    }


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--package", default="com.micorlov.pokervsdealer")
    p.add_argument("--language", default="en-US")
    p.add_argument("--only", default="",
                   help="Comma-separated subset of: text,icon,featureGraphic,phoneScreenshots")
    p.add_argument("--credentials",
                   default=os.environ.get("GOOGLE_APPLICATION_CREDENTIALS", DEFAULT_CREDENTIALS))
    p.add_argument("--dry-run", action="store_true",
                   help="Do the whole edit but discard it instead of committing")
    p.add_argument("--timeout", type=int, default=900)
    return p.parse_args()


def read_copy(name, limit):
    path = os.path.join(LISTING_DIR, f"{name}.txt")
    text = open(path, encoding="utf-8").read().strip()
    if len(text) > limit:
        sys.exit(f"{path}: {len(text)} chars exceeds the Play limit of {limit}")
    return text


def main():
    args = parse_args()
    wanted = {s.strip() for s in args.only.split(",") if s.strip()}

    def selected(step):
        return not wanted or step in wanted

    assets = graphics()
    short = read_copy("short-description", SHORT_LIMIT)
    full = read_copy("full-description", FULL_LIMIT)

    for kind, paths in assets.items():
        if not selected(kind):
            continue
        for path in paths:
            if not os.path.isfile(path):
                sys.exit(f"Missing {kind} asset: {path}")
    if not os.path.isfile(args.credentials):
        sys.exit(f"Service account JSON not found: {args.credentials}")

    creds = service_account.Credentials.from_service_account_file(
        args.credentials, scopes=[SCOPE])
    base_http = httplib2.Http(timeout=args.timeout)
    base_http.follow_redirects = False
    http = google_auth_httplib2.AuthorizedHttp(creds, http=base_http)
    service = build("androidpublisher", "v3", http=http, cache_discovery=False)
    edits = service.edits()

    try:
        edit_id = edits.insert(body={}, packageName=args.package).execute()["id"]
    except HttpError as e:
        if e.resp.status in (401, 403):
            sys.exit(f"Service account lacks permission on '{args.package}'.\n{e}")
        raise

    try:
        if selected("text"):
            edits.listings().update(
                packageName=args.package, editId=edit_id, language=args.language,
                body={"title": TITLE, "shortDescription": short, "fullDescription": full},
            ).execute()
            print(f"Listing text updated "
                  f"({len(short)}/{SHORT_LIMIT}, {len(full)}/{FULL_LIMIT} chars)")

        for kind, paths in assets.items():
            if not selected(kind):
                continue
            edits.images().deleteall(
                packageName=args.package, editId=edit_id,
                language=args.language, imageType=kind).execute()
            for path in paths:
                edits.images().upload(
                    packageName=args.package, editId=edit_id, language=args.language,
                    imageType=kind,
                    media_body=MediaFileUpload(path, mimetype="image/png"),
                ).execute()
                print(f"  {kind}: {os.path.relpath(path, ROOT)}")

        if args.dry_run:
            edits.delete(packageName=args.package, editId=edit_id).execute()
            print("Dry run — edit discarded, listing unchanged.")
            return

        edits.commit(packageName=args.package, editId=edit_id).execute()
        print(f"Committed. Store listing for '{args.package}' updated.")
    except Exception:
        try:
            edits.delete(packageName=args.package, editId=edit_id).execute()
            print("Edit rolled back.", file=sys.stderr)
        except Exception:
            pass
        raise


if __name__ == "__main__":
    main()

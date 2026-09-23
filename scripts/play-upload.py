#!/usr/bin/env python3
"""Upload a signed AAB to Google Play and roll it out on a track.

Usage:
  python3 scripts/play-upload.py --aab <path> [--track internal] [--notes "..."] [--dry-run]

Auth: service-account JSON at $GOOGLE_APPLICATION_CREDENTIALS (or --credentials).
The service account must be added in Play Console under Users & permissions
with "Release to testing tracks" for the app.
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


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--aab", required=True, help="Path to the .aab file")
    p.add_argument("--package", default="com.micorlov.pokervsdealer")
    p.add_argument("--track", default="internal",
                   choices=["internal", "alpha", "beta", "production"])
    p.add_argument("--notes", default="Initial release.")
    p.add_argument("--credentials",
                   default=os.environ.get("GOOGLE_APPLICATION_CREDENTIALS", DEFAULT_CREDENTIALS))
    p.add_argument("--dry-run", action="store_true",
                   help="Upload into an edit but delete it instead of committing")
    p.add_argument("--timeout", type=int, default=900,
                   help="Per-request socket timeout in seconds")
    return p.parse_args()


def main():
    args = parse_args()

    if not os.path.isfile(args.aab):
        sys.exit(f"AAB not found: {args.aab}")
    if not os.path.isfile(args.credentials):
        sys.exit(f"Service account JSON not found: {args.credentials}")

    creds = service_account.Credentials.from_service_account_file(
        args.credentials, scopes=[SCOPE])
    # A 49 MB bundle blows past httplib2's default socket timeout on a single-shot
    # POST, so use a long-timeout transport and upload in resumable chunks.
    base_http = httplib2.Http(timeout=args.timeout)
    # Resumable uploads answer each chunk with HTTP 308 "Resume Incomplete".
    # httplib2 treats 308 as a redirect and raises RedirectMissingLocation, so turn
    # redirect-following off and let googleapiclient handle the 308 itself.
    base_http.follow_redirects = False
    http = google_auth_httplib2.AuthorizedHttp(creds, http=base_http)
    service = build("androidpublisher", "v3", http=http, cache_discovery=False)
    edits = service.edits()

    try:
        edit_id = edits.insert(body={}, packageName=args.package).execute()["id"]
    except HttpError as e:
        if e.resp.status == 404:
            sys.exit(f"Play Console has no app for '{args.package}'. Create it first, "
                     f"then grant access to the service account.")
        if e.resp.status in (401, 403):
            sys.exit(f"Service account lacks permission on '{args.package}'. Add it under "
                     f"Users & permissions with release rights.\n{e}")
        raise

    try:
        media = MediaFileUpload(args.aab, mimetype="application/octet-stream",
                                resumable=True, chunksize=5 * 1024 * 1024)
        request = edits.bundles().upload(
            packageName=args.package, editId=edit_id, media_body=media)
        total_mb = os.path.getsize(args.aab) / 1e6
        response = None
        while response is None:
            status, response = request.next_chunk(num_retries=3)
            if status:
                print(f"  {status.progress() * 100:5.1f}%  of {total_mb:.1f} MB", flush=True)
        version_code = response["versionCode"]
        print(f"Uploaded versionCode {version_code} ({total_mb:.1f} MB)")

        edits.tracks().update(
            packageName=args.package, editId=edit_id, track=args.track,
            body={"releases": [{
                "versionCodes": [str(version_code)],
                "status": "completed",
                "releaseNotes": [{"language": "en-US", "text": args.notes}],
            }]}).execute()
        print(f"Assigned to track '{args.track}'")

        if args.dry_run:
            edits.delete(packageName=args.package, editId=edit_id).execute()
            print("Dry run — edit discarded, nothing published.")
            return

        edits.commit(packageName=args.package, editId=edit_id).execute()
        print(f"Committed. versionCode {version_code} is live on '{args.track}'.")
    except Exception:
        try:
            edits.delete(packageName=args.package, editId=edit_id).execute()
            print("Edit rolled back.", file=sys.stderr)
        except Exception:
            pass
        raise


if __name__ == "__main__":
    main()

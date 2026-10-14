#!/usr/bin/env python3
"""Inspect or explicitly submit a signed bundle to production.

Usage:
  python3 scripts/play-production-release.py --aab artifacts/android/showdown-release.aab \\
      --version-code 5 --name "2.0.0 (5)" --notes store/release-notes/5-en-US.txt [--rollout 0.2] [--submit]

Without --submit this only inspects and backs up release state. Uses the upload service
account, preserves countries and other tracks, and backs up release state locally.
Run --submit only with release approval.
"""
import argparse
import datetime
import fcntl
import hashlib
import json
import os
import pathlib

import google_auth_httplib2
import httplib2
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

ROOT = pathlib.Path(__file__).resolve().parents[1]
PACKAGE = "com.micorlov.pokervsdealer"
DEFAULT_CREDENTIALS = os.path.expanduser("~/.config/play/key.json")
ASO = ROOT / "store/aso"


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--aab", required=True, type=pathlib.Path)
    parser.add_argument("--version-code", required=True, type=int)
    parser.add_argument("--name", required=True, help='Play release name, e.g. "2.0.0 (5)"')
    parser.add_argument("--notes", required=True, type=pathlib.Path, help="en-US release notes file")
    parser.add_argument("--rollout", type=float, default=1.0,
                        help="Fraction of users (0-1). Below 1 makes a staged rollout")
    parser.add_argument("--credentials",
                        default=os.environ.get("GOOGLE_APPLICATION_CREDENTIALS", DEFAULT_CREDENTIALS))
    parser.add_argument("--submit", action="store_true")
    args = parser.parse_args()
    if not 0 < args.rollout <= 1:
        parser.error("--rollout must be in (0, 1]")
    version = args.version_code
    lock = (ASO / ".play-operation.lock").open("w")
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    bundle = args.aab
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    creds = service_account.Credentials.from_service_account_file(
        args.credentials,
        scopes=["https://www.googleapis.com/auth/androidpublisher"],
    )
    http = httplib2.Http(timeout=180)
    http.follow_redirects = False
    api = build("androidpublisher", "v3", http=google_auth_httplib2.AuthorizedHttp(creds, http=http), cache_discovery=False)
    edits = api.edits()
    edit_id = edits.insert(packageName=PACKAGE, body={}).execute()["id"]
    base = dict(packageName=PACKAGE, editId=edit_id)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = ASO / "backups" / (stamp + "-production-v" + str(version))
    committed = False
    try:
        tracks = edits.tracks().list(**base).execute()
        bundles = edits.bundles().list(**base).execute()
        production = edits.tracks().get(**base, track="production").execute()
        countries = edits.countryavailability().get(**base, track="production").execute()
        save(backup / "tracks.json", tracks)
        save(backup / "bundles.json", bundles)
        save(backup / "production.json", production)
        save(backup / "countries.json", countries)
        save(backup / "listings.json", edits.listings().list(**base).execute())
        assert countries.get("countries") or countries.get("restOfWorld"), "Production has no country availability"
        for release in production.get("releases", []):
            if release.get("status") != "completed":
                raise ValueError("Production already has a pending release; inspect before replacing it")
        result = dict(package=PACKAGE, versionCode=version, bundleSha256=digest,
                      backup=str(backup), countryAvailability=countries, status="inspected_not_submitted")
        if args.submit:
            existing = next((b for b in bundles.get("bundles", []) if b["versionCode"] == version), None)
            if existing:
                if existing.get("sha256") != digest:
                    raise ValueError("This version code is already occupied by a different bundle")
                uploaded = existing
            else:
                uploaded = edits.bundles().upload(**base, media_body=MediaFileUpload(
                    str(bundle), mimetype="application/octet-stream", chunksize=8 * 1024 * 1024, resumable=True
                )).execute()
            assert uploaded["versionCode"] == version
            assert uploaded["sha256"] == digest
            result["uploadedBundle"] = uploaded
            save(backup / "uploaded-bundle.json", uploaded)
            notes = args.notes.read_text().strip()
            release = {
                "name": args.name, "versionCodes": [str(version)],
                "status": "completed" if args.rollout == 1 else "inProgress",
                "releaseNotes": [{"language": "en-US", "text": notes}],
            }
            if args.rollout < 1:
                release["userFraction"] = args.rollout
            body = {"track": "production", "releases": [release]}
            save(backup / "proposed-track.json", body)
            edits.tracks().update(**base, track="production", body=body).execute()
            assert edits.countryavailability().get(**base, track="production").execute() == countries
            after_tracks = edits.tracks().list(**base).execute()
            untouched_before = {t["track"]: t for t in tracks.get("tracks", []) if t["track"] != "production"}
            untouched_after = {t["track"]: t for t in after_tracks.get("tracks", []) if t["track"] != "production"}
            assert untouched_before == untouched_after, "Another track changed"
            edits.validate(**base).execute()
            result["commitResponse"] = edits.commit(**base).execute()
            committed = True
            result["status"] = "submitted_console_verification_pending"
        save(backup / "outcome.json", result)
        save(ASO / ("reports/production-v" + str(version) + ".json"), result)
        print(json.dumps({k: result[k] for k in ("package", "versionCode", "bundleSha256", "backup", "status")}))
    except Exception as exc:
        save(backup / "outcome.json", {"package": PACKAGE, "status": "failed", "error": str(exc)})
        raise
    finally:
        if not committed:
            try:
                edits.delete(**base).execute()
            except Exception:
                pass


if __name__ == "__main__":
    main()

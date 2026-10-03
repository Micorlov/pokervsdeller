#!/usr/bin/env python3
"""Inspect or explicitly submit the verified version-4 bundle to production.

Uses the configured upload account, preserves countries and other tracks,
and backs up release state locally. Run --submit only with release approval.
"""
import argparse
import datetime
import fcntl
import hashlib
import json
import pathlib

import google_auth_httplib2
import httplib2
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

ROOT = pathlib.Path(__file__).resolve().parents[1]
PACKAGE = "com.micorlov.pokervsdealer"
VERSION = 4
ASO = ROOT / "store/aso"


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--submit", action="store_true")
    args = parser.parse_args()
    lock = (ASO / ".play-operation.lock").open("w")
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    config = json.loads((ROOT / "app.json").read_text())["expo"]
    assert config["android"]["package"] == PACKAGE
    assert config["android"]["versionCode"] == VERSION
    verification = json.loads((ASO / "reports/dealer-update-v4/verification.json").read_text())
    assert verification["apkSignatureVerified"]
    assert verification["certificateSha256"] == "93c9af8ee043124738be029a1f5c860ae1cf9f95649aaa21999df5aef76436f5"
    for check in ("typescript", "lint", "assembleRelease", "bundleRelease"):
        assert verification["checks"][check] == "passed", check
    bundle = ROOT / "android/app/build/outputs/bundle/release/app-release.aab"
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    recorded = next(b for b in verification["builds"] if b["path"].endswith(".aab"))
    assert digest == recorded["sha256"], "Bundle changed after verification"
    creds = service_account.Credentials.from_service_account_file(
        pathlib.Path.home() / ".config/mcp/google-play-service-account.json",
        scopes=["https://www.googleapis.com/auth/androidpublisher"],
    )
    http = httplib2.Http(timeout=180)
    http.follow_redirects = False
    api = build("androidpublisher", "v3", http=google_auth_httplib2.AuthorizedHttp(creds, http=http), cache_discovery=False)
    edits = api.edits()
    edit_id = edits.insert(packageName=PACKAGE, body={}).execute()["id"]
    base = dict(packageName=PACKAGE, editId=edit_id)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = ASO / "backups" / (stamp + "-production-v4")
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
        result = dict(package=PACKAGE, versionCode=VERSION, bundleSha256=digest,
                      backup=str(backup), countryAvailability=countries, status="inspected_not_submitted")
        if args.submit:
            existing = next((b for b in bundles.get("bundles", []) if b["versionCode"] == VERSION), None)
            if existing:
                if existing.get("sha256") != digest:
                    raise ValueError("Version 4 is already occupied by a different bundle")
                uploaded = existing
            else:
                uploaded = edits.bundles().upload(**base, media_body=MediaFileUpload(
                    str(bundle), mimetype="application/octet-stream", chunksize=8 * 1024 * 1024, resumable=True
                )).execute()
            assert uploaded["versionCode"] == VERSION
            assert uploaded["sha256"] == digest
            result["uploadedBundle"] = uploaded
            save(backup / "uploaded-bundle.json", uploaded)
            notes = (ROOT / "store/release-notes/4-en-US.txt").read_text().strip()
            body = {"track": "production", "releases": [{
                "name": "1.0.0 (4)", "versionCodes": [str(VERSION)], "status": "completed",
                "releaseNotes": [{"language": "en-US", "text": notes}],
            }]}
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
        save(ASO / "reports/production-v4.json", result)
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

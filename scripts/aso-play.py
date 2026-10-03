#!/usr/bin/env python3
"""Back up Play metadata/artwork; validate or commit reviewed English copy."""
import argparse
import datetime
import fcntl
import hashlib
import json
import pathlib
import urllib.request

import google_auth_httplib2
import httplib2
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

ROOT = pathlib.Path(__file__).resolve().parents[1]
PACKAGE = "com.micorlov.pokervsdealer"
ASO = ROOT / "store/aso"


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")


def main():
    ASO.mkdir(parents=True, exist_ok=True)
    lock = (ASO / ".play-operation.lock").open("w")
    # A new Publisher edit invalidates the previous one for this account/app.
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    parser = argparse.ArgumentParser()
    parser.add_argument("--validate", action="store_true")
    parser.add_argument("--commit", action="store_true")
    parser.add_argument("--creative", action="store_true", help="Include reviewed feature graphic and six screenshots")
    parser.add_argument("--icon", action="store_true", help="Upload only the reviewed store icon; preserve listing copy and screenshots")
    args = parser.parse_args()
    creds = service_account.Credentials.from_service_account_file(
        pathlib.Path.home() / ".config/mcp/google-play-service-account.json",
        scopes=["https://www.googleapis.com/auth/androidpublisher"],
    )
    http = httplib2.Http(timeout=120)
    http.follow_redirects = False
    api = build("androidpublisher", "v3", http=google_auth_httplib2.AuthorizedHttp(creds, http=http), cache_discovery=False)
    edits = api.edits()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    backup = ASO / "backups" / stamp
    edit_id = edits.insert(packageName=PACKAGE, body={}).execute()["id"]
    base = dict(packageName=PACKAGE, editId=edit_id)
    committed = False
    try:
        listings = edits.listings().list(**base).execute()
        save(backup / "listings.json", listings)
        save(backup / "tracks.json", edits.tracks().list(**base).execute())
        save(backup / "bundles.json", edits.bundles().list(**base).execute())
        for listing in listings.get("listings", []):
            locale = listing["language"]
            for kind in ("icon", "featureGraphic", "phoneScreenshots"):
                response = edits.images().list(**base, language=locale, imageType=kind).execute()
                save(backup / locale / f"{kind}.json", response)
                for index, asset in enumerate(response.get("images", []), 1):
                    # Google supplies the asset URL; =s0 retains original resolution.
                    url = asset["url"].split("=s")[0] + "=s0"
                    blob = urllib.request.urlopen(url, timeout=60).read()
                    (backup / locale / f"{kind}-{index:02}.png").write_bytes(blob)
        try:
            save(ASO / "reports/reviews-api.json", api.reviews().list(packageName=PACKAGE, maxResults=100).execute())
        except Exception as exc:
            save(ASO / "reports/reviews-api.json", {"unavailable": str(exc)})
        outcome = {"package": PACKAGE, "backup": str(backup), "status": "backed_up"}
        if args.validate or args.commit or args.creative or args.icon:
            copy = json.loads((ASO / "metadata/en-US/listing.json").read_text())
            for field, limit in (("title", 30), ("shortDescription", 80), ("fullDescription", 4000)):
                count = len(copy[field].encode("utf-16-le")) // 2
                if not 0 < count <= limit:
                    raise ValueError(f"Invalid {field}: {count}/{limit}")
            before = next((item for item in listings.get("listings", []) if item["language"] == "en-US"), {})
            body = {key: value for key, value in before.items() if key != "language"}
            body.update(copy)
            save(backup / "proposed.json", body)
            if args.commit:
                review = json.loads((ASO / "reviews.json").read_text())["en-US"]
                if review["metadataReview"] != "ready":
                    raise ValueError("English metadata review is not ready")
                if args.creative and review["creativeReview"] != "ready":
                    raise ValueError("English creative review is not ready")
            if not args.icon or args.creative:
                edits.listings().update(**base, language="en-US", body=body).execute()
            if args.icon:
                icon_review = json.loads((ASO / "reports/icon-validation.json").read_text())
                icon_path = ROOT / "store/out/store-icon-512.png"
                digest = hashlib.sha256(icon_path.read_bytes()).hexdigest()
                if icon_review["visualReview"] != "ready" or icon_review["sha256"] != digest:
                    raise ValueError("Icon review missing or asset changed after review")
                if icon_review["width"] != 512 or icon_review["height"] != 512 or icon_path.stat().st_size > 1024 * 1024:
                    raise ValueError("Invalid store icon dimensions or size")
                edits.images().deleteall(**base, language="en-US", imageType="icon").execute()
                edits.images().upload(**base, language="en-US", imageType="icon", media_body=MediaFileUpload(str(icon_path), mimetype="image/png")).execute()
                outcome["icon"] = edits.images().list(**base, language="en-US", imageType="icon").execute().get("images", [])
                if len(outcome["icon"]) != 1 or outcome["icon"][0].get("sha256") != digest:
                    raise ValueError("Uploaded icon hash/count mismatch")
            if args.creative:
                folder = ASO / "creative/en-US"
                groups = {"featureGraphic": [folder / "featureGraphic.png"], "phoneScreenshots": [folder / f"screenshot-{index:02}.png" for index in range(1, 7)]}
                validation = json.loads((ASO / "reports/validation.json").read_text())
                if validation["visualReview"] != "ready":
                    raise ValueError("Visual review missing")
                for kind, paths in groups.items():
                    for path in paths:
                        recorded = next(a for a in validation["assets"] if a["file"] == path.name)
                        if hashlib.sha256(path.read_bytes()).hexdigest() != recorded["sha256"]:
                            raise ValueError(f"Asset changed after review: {path}")
                    edits.images().deleteall(**base, language="en-US", imageType=kind).execute()
                    for path in paths:
                        edits.images().upload(**base, language="en-US", imageType=kind, media_body=MediaFileUpload(str(path), mimetype="image/png")).execute()
                    assets = edits.images().list(**base, language="en-US", imageType=kind).execute().get("images", [])
                    if len(assets) != len(paths):
                        raise ValueError("Uploaded image count mismatch")
                    outcome[kind] = assets
            edits.validate(**base).execute()
            outcome["status"] = "validated"
            if args.commit:
                outcome["commitResponse"] = edits.commit(**base).execute()
                committed = True
                outcome["status"] = "submitted_console_verification_pending"
        save(backup / "outcome.json", outcome)
        save(ASO / "reports/latest-play-operation.json", outcome)
        print(json.dumps(outcome))
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

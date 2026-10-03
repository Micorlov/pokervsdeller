#!/usr/bin/env python3
"""Capture a named frame or tap a label on our disposable ADB test device."""
import argparse
import pathlib
import re
import subprocess
import time
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
ADB = ["/opt/homebrew/bin/adb", "-s", "emulator-5554"]
PACKAGE = "com.micorlov.pokervsdealer"


def adb(*args):
    return subprocess.check_output(ADB + list(args))


def dump():
    for attempt in range(3):
        try:
            adb("shell", "rm", "-f", "/sdcard/aso-dealer.xml")
            output = adb("shell", "uiautomator", "dump", "/sdcard/aso-dealer.xml")
            if b"dumped to" not in output:
                raise ValueError("UI dump did not complete")
            data = adb("shell", "cat", "/sdcard/aso-dealer.xml")
            if PACKAGE not in data.decode():
                raise ValueError("Game is not in foreground")
            return data
        except Exception:
            if attempt == 2:
                raise
            time.sleep(1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tap")
    parser.add_argument("--capture")
    parser.add_argument("--output", default="store/aso/sources/en-US")
    args = parser.parse_args()
    xml = dump()
    nodes = list(ET.fromstring(xml).iter("node"))
    if args.tap:
        found = [n for n in nodes if args.tap in (n.get("text"), n.get("content-desc")) and n.get("enabled") == "true"]
        if not found:
            raise ValueError(f"Visible label missing: {args.tap}")
        values = list(map(int, re.findall(r"\d+", found[0].get("bounds"))))
        x1, y1, x2, y2 = values
        adb("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2))
        time.sleep(0.5)
        xml = dump()
        nodes = list(ET.fromstring(xml).iter("node"))
    if args.capture:
        path = ROOT / args.output / args.capture
        path.parent.mkdir(parents=True, exist_ok=True)
        path.with_suffix(".xml").write_bytes(xml)
        path.with_suffix(".png").write_bytes(adb("exec-out", "screencap", "-p"))
        print(path)
    for node in nodes:
        label = node.get("text") or node.get("content-desc")
        if label:
            print(label, node.get("bounds"))


if __name__ == "__main__":
    main()

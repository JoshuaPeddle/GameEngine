import argparse
from pathlib import Path
import plistlib

parser = argparse.ArgumentParser(description="Verify the privacy manifest in a built iOS app.")
parser.add_argument("app", type=Path)
parser.add_argument("--expected", required=True, type=Path)
args = parser.parse_args()
expected = plistlib.loads(args.expected.read_bytes())
bundled = args.app / "PrivacyInfo.xcprivacy"
actual = plistlib.loads(bundled.read_bytes())
if actual != expected:
    raise SystemExit("Bundled privacy manifest differs from the tracked manifest.")
if actual.get("NSPrivacyTracking") is not False or actual.get("NSPrivacyTrackingDomains") or actual.get("NSPrivacyCollectedDataTypes"):
    raise SystemExit("Expected no tracking and no collected data.")
required = {"FileTimestamp": "C617.1", "UserDefaults": "CA92.1", "SystemBootTime": "35F9.1", "DiskSpace": "E174.1"}
entries = {item["NSPrivacyAccessedAPIType"]: item["NSPrivacyAccessedAPITypeReasons"] for item in actual["NSPrivacyAccessedAPITypes"]}
for category, reason in required.items():
    if reason not in entries.get("NSPrivacyAccessedAPICategory" + category, []):
        raise SystemExit("Missing required reason: " + category + " " + reason)
minimum = plistlib.loads((args.app / "Info.plist").read_bytes())["MinimumOSVersion"]
if tuple(map(int, minimum.split("."))) < (15, 0):
    raise SystemExit("iOS minimum must be at least 15.0.")
print("PASS: bundled privacy manifest matches source, four required API categories, no tracking, no collected data, minimum iOS " + minimum)
for manifest in sorted(args.app.rglob("*.xcprivacy")):
    print(manifest.relative_to(args.app))

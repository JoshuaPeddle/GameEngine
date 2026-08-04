#!/usr/bin/env bash
set -euo pipefail

readonly LOG_MARKER="FIRST_FRAME_PRESENTED"
readonly DIAGNOSTICS_DIR="artifacts/android-smoke"

mkdir -p "$DIAGNOSTICS_DIR"

collect_diagnostics() {
  adb logcat -d -v threadtime > "$DIAGNOSTICS_DIR/logcat.txt" 2>&1 || true
  adb exec-out screencap -p > "$DIAGNOSTICS_DIR/screen.png" 2>/dev/null || true
  adb shell dumpsys activity activities > "$DIAGNOSTICS_DIR/activities.txt" 2>&1 || true
}

trap collect_diagnostics EXIT

apk_path="$(find artifacts/android-apk -type f -name '*-Signed.apk' -print -quit)"

if [[ -z "$apk_path" ]]; then
  echo "No signed Android APK was produced."
  exit 1
fi

package_name="$(basename "$apk_path" -Signed.apk)"

adb install -r "$apk_path"
adb shell am force-stop "$package_name"
adb logcat -c
adb shell monkey -p "$package_name" -c android.intent.category.LAUNCHER 1 >/dev/null

sleep 2

for _ in $(seq 1 60); do
  if adb logcat -d -v brief | grep -F "$LOG_MARKER" >/dev/null; then
    sleep 5

    if [[ -z "$(adb shell pidof "$package_name" | tr -d '\r')" ]]; then
      echo "Android app exited after presenting its first frame."
      exit 1
    fi

    echo "Android app presented an engine frame and remained alive."
    exit 0
  fi

  if [[ -z "$(adb shell pidof "$package_name" | tr -d '\r')" ]]; then
    echo "Android app exited before presenting its first frame."
    exit 1
  fi

  sleep 1
done

echo "Android app did not present an engine frame within 60 seconds."
exit 1

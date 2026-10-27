import argparse
import hashlib
from pathlib import Path
import subprocess
import tempfile
import zipfile

parser = argparse.ArgumentParser(description="Check every delivered native library for Android 16 KB compatibility.")
parser.add_argument("apk", type=Path)
parser.add_argument("--zipalign", required=True, type=Path)
parser.add_argument("--readelf", required=True, type=Path)
args = parser.parse_args()
command = [str(args.zipalign), "-c", "-P", "16", "-v", "4", str(args.apk)]
print("Command: " + " ".join(command), flush=True)
zip_result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
print(zip_result.stdout)
failures = []
if zip_result.returncode:
    failures.append("ZIP alignment failed")
rows = []
with zipfile.ZipFile(args.apk) as archive, tempfile.TemporaryDirectory(prefix="ge-elf-") as temporary:
    libraries = sorted(info for info in archive.namelist() if info.endswith(".so"))
    if not libraries:
        failures.append("APK contains no native libraries")
    for index, name in enumerate(libraries):
        library = Path(temporary) / f"{index}.so"
        library.write_bytes(archive.read(name))
        result = subprocess.run([str(args.readelf), "-lW", str(library)], text=True, capture_output=True)
        print(name + "\n" + result.stdout)
        segments = []
        for line in result.stdout.splitlines():
            columns = line.split()
            if columns and columns[0] == "LOAD":
                offset, address, alignment = int(columns[1], 16), int(columns[2], 16), int(columns[-1], 16)
                segments.append(alignment)
                if alignment < 16384 or offset % 16384 != address % 16384:
                    failures.append(name + ": incompatible LOAD segment: " + line.strip())
        if result.returncode or not segments:
            failures.append(name + ": unreadable ELF program headers: " + result.stderr)
        compression = "stored" if archive.getinfo(name).compress_type == zipfile.ZIP_STORED else "compressed"
        rows.append((name, ", ".join(hex(value) for value in segments), compression))
print("APK SHA-256: " + hashlib.sha256(args.apk.read_bytes()).hexdigest())
print("| Delivered library | LOAD alignments | ZIP storage |")
print("|---|---|---|")
for row in rows:
    print("| " + " | ".join(row) + " |")
if failures:
    raise SystemExit("FAIL\n" + "\n".join(failures))
print(f"PASS: ZIP alignment and all LOAD segments in {len(rows)} native libraries support 16 KB pages.")

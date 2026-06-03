#!/usr/bin/env python3

import json
import pathlib
import sys

if len(sys.argv) != 3:
    print(
        "Usage: update_versioninfo.py <version> <output-file>",
        file=sys.stderr,
    )
    sys.exit(1)

version = sys.argv[1]
output_file = pathlib.Path(sys.argv[2])

parts = [int(p) for p in version.split(".")]

if len(parts) == 3:
    major, minor, patch = parts
    build = 0
elif len(parts) == 4:
    major, minor, patch, build = parts
else:
    print("Invalid version format", file=sys.stderr)
    sys.exit(1)

data = {
    "VersionMajor": major,
    "VersionMinor": minor,
    "VersionPatch": patch,
    "VersionBuild": build,
}

output_file.write_text(
    json.dumps(data, indent=2) + "\n",
    encoding="utf-8",
)

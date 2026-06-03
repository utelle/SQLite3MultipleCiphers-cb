#!/usr/bin/env python3

import re
import sys

if len(sys.argv) != 2:
    print("Usage: validate_version.py <version>", file=sys.stderr)
    sys.exit(1)

version = sys.argv[1]

if not re.fullmatch(r"\d+\.\d+\.\d+(?:\.\d+)?", version):
    print(
        f"Invalid version format '{version}'. "
        "Expected x.y.z or x.y.z.build",
        file=sys.stderr,
    )
    sys.exit(1)

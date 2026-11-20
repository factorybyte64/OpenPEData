"""Step 4e / H-017 signature test: append a forged change to a COPY of a project folder.

Renames one PLC tag by appending a new version of its record (same-length name, so the record layout is unchanged),
then a commit record and a CLOSE record to System\\PEData.plf, and the matching index entries plus an index commit
record to System\\PEData.idx (layout as TIA writes it on save, see samples 003 -> 009).

Variants (signature of the appended commit):
  control  copy only, nothing changed
  a        no valid signature: 256 zero bytes
  b        wrong signature: the signature of the previous commit (valid RSA block, other data)
  c        own RSA-2048 key pair: a new version of the key record 0016:0007:18 with our public key is appended
           before the commit, the commit is signed with our private key (older commits keep the original key)
  c2       own key in place: modulus of the existing key record replaced, every commit chain hash recomputed and
           every commit re-signed with our key (plf completely consistent with our key)
In every variant the chain hash of the new commit is correct. The index commit (idx, 421 bytes) carries the plf chain
hash at +96, a second hash at +128 whose input is unknown, and an RSA signature at +160 over unknown data; we write the
plf chain hash, keep +128 from the previous index commit and sign like the plf commit (a: zeros, b: old, c/c2: own
key over [0,160)).

Usage: python tools/forge.py <copy folder> <variant> [--old Tag_Renamed --new Tag_Patched]
Refuses folders below samples\\ (samples are never changed).
"""
import hashlib
import os
import random
import struct
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "parser"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from openpedata import plf  # noqa: E402
from openpedata.project import Project  # noqa: E402
import commits  # noqa:
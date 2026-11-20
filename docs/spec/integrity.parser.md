# Integrity of PEData.plf (draft, 5e)

Implementation: `parser\openpedata\verify.py`, invocation `OpenPEData <project> --verify`, test `parser\tests\test_samples.py` (`check_verify`,
`check_verify_tamper`). Basis: H-008 (`tools\commits.py`).

## Checks (without secret)

1. Header: SHA-256 over the first 65 bytes = bytes 65..96, byte 97 = 0xFF; records from offset 98.
2. Each record: `[u32 length][length-4 bytes, last byte 0xFF][SHA-256 over the length bytes]`.
3. No bytes after the last record (aborted write).
4. Each `$$COMMIT$` record (store type `0x00070014`, 389 bytes):
   - Chain hash (bytes 96..127) = SHA-256(previous chain hash ‖ SHA-256 of all records since the last commit in log order, records
     `0x00070015` (CLOSE) excluded). Start value = header hash (bytes 65..96); the chain continues from the stored value.
   - Signature (bytes 128..383) = RSA PKCS#1 v1.5 / SHA-256 over the first 128 bytes of the commit record, checked with the public
     key of the key record `0016:0007:18` (CAPI PUBLICKEYBLOB, found via "RSA1", 2048 bits, e = 65537).
   - Byte 78 (i32) refers to the offset of the previous commit of the same session (−1 = none, e.g. after compaction to the `.zap21`).
5. Records after the last commit are reported (`uncommittedRecords`), but do not count as errors (open session).

## Finding across all samples

All samples (folders and `.zap21`) pass the check: header hash, all record hashes, chain hash and signature of every commit. Every
`.zap21` contains a new key pair; folder copies keep the project's key. The tamper test changes one byte each in
a record body, in the chain hash, in the signature, and appends bytes: every change is detected.

## Limits

The private key is stored encrypted in the key record (u16 1200 + 1456 bytes of high entropy after the public blob, PBKDF2 with
10000 iterations suspected); the secret is unknown and was not attacked. Valid new commits (i.e. writing) therefore cannot
be created today.

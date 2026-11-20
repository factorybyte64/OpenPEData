# Integrity: hash chain and commit signature (established, H-008)

Evidence: 55/55 commits verified in all sample folders 000–009 and all .zap21. Tool `tools\commits.py`, results `samples\<sample>_analysis\commits.json` / `commits_zap21.json` (user's machine, commit e269082).

## Levels
1. SHA-256 per record (see plf-container.md).
2. Chain hash per commit: `H_n = SHA-256(H_{n-1} ‖ SHA-256 of all records since the last commit in log order, without CLOSE 0x00070015)`; `H_0` = header hash of the plf (+65..+97).
3. RSA signature per commit with the project key.

## Commit record 0014:0007 (389 B, evidence 000_empty @17876)
| Off | Content |
|---|---|
| +44 / +384 | u32 340 (payload length, header and trailer) |
| +68 | ticks |
| +78 | i32 offset of the previous commit in the same session, -1 if none (also after close and in .zap21) |
| +82 | `0a "$$COMMIT$"` |
| +92 | u32 288 |
| +96 | 32 B chain hash (first commit 000_empty: `8cfb6ecb…48c5`) |
| +128..+384 | RSA-2048 signature, PKCS#1 v1.5, SHA-256, stored **big-endian**, over commit bytes [0,128) |
Decrypted signature: `00 01 FF…FF 00 3031300d060960864801650304020105000420 <SHA-256 of the 128 B>`.
Ruled out: signature from +96, little-endian, chain hash over raw bytes, from the start of the file, without previous value, zero start value.

## Key record 0016:0007:18 (evidence 000_empty, 1832 B)
- +44 u32 payload length, +48 u16 1, u16 7, +54 u16 b (blob from +58+b), +56 u32 10000 (presumably PBKDF2 iterations), 35–50 B random (salt/IV?), ends with `14 01`.
- After that CAPI PUBLICKEYBLOB (`06 02 00 00 00 a4 00 00 RSA1`, 2048 bits, e=65537), modulus 256 B little-endian; position variable (+97…+110).
- After that always 1458 B: u16 1200 + 1456 B (91×16, entropy ≈7.86) = very likely encrypted private key. Protection secret unknown, no attempt to circumvent.
- Every .zap21 gets a **new** key pair; folder copies keep the original key.

## Significance
- **Reading:** no key needed. The parser can fully check integrity (record SHA, chain hash, signature) → useful as an optional check in OpenPEData.
- **Writing:** valid commits need the private key, which exists only in encrypted form → external writing currently not possible. Whether TIA rejects missing/invalid signatures or a swapped key record is untested (would need TIA experiments, not approved).

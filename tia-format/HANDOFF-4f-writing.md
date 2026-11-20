# Task 4f: Create project files outside the engineering software

Status 2026-10-22. For an AI model without prior knowledge. Established facts and conjectures are marked separately. This is about interoperability on our own projects; protection mechanisms are investigated, not circumvented.

---

## 1. What is the problem?

A project in this format can currently only be changed with the engineering software itself (user interface or programming interface). That is slow, needs a licensed installation and cannot set every property. The goal of 4f is to clarify **whether and how a valid project file can also be created without this software**, and, if the outcome is positive, to build a minimal writer.

Two things stand in the way:

1. **The file is integrity-checked.** The content is secured with hashes, a hash chain and a digital signature. Whether the software enforces these checks when opening, and how strictly, has **not been tested** so far.
2. **Consistency.** A written object must fit the embedded data model, its links and the index; otherwise the project is readable but faulty.

---

## 2. What do we know? (established, evidenced on many projects)

### 2.1 Structure
- The content is almost entirely in a **data file**, accompanied by an **index file**. Further stores (a cross-reference database, a search index, runtime files) are **derived**: the software rebuilds them from the content; they carry no source content of their own.
- The data file is a **transaction log**: a gapless chain of records from the beginning to the end of the file. A change appends new record versions and a closing record; old versions remain. The archive format contains a compacted version (every object once).
- Every record has a length field, a header (type/object identifier/GUID) and a payload. Objects can be assigned 100 % to a type via the **data model** embedded in the project; attributes, links, blobs and multilingual texts are decoded.

### 2.2 Integrity (three levels)
1. **Record hash:** one SHA-256 per record over its bytes.
2. **Chain hash per closing record:** chains the previous chain hash with the hashes of all records since the last closing record; the start value is the hash of the file header.
3. **Signature per closing record:** RSA-2048 / PKCS#1 v1.5 / SHA-256 over a fixed front area of the closing record that contains the chain hash. The index file contains a signed copy per closing record.

Evidence: in all test projects, all record hashes, chain hashes and signatures can be checked; our reader verifies them completely with a `--verify` option, **without any key**.

### 2.3 Key
- The project contains a record with the **public** key (RSA-2048) and an **encrypted private** part (protection presumably password/key-derived, secret unknown).
- **Every archiving creates a new key pair.** The key is therefore not tied to the project, license or machine. Folder copies keep their key.

### 2.4 What reading/checking can do today
Reading and complete verification need no key. The software demonstrably rebuilds missing or inconsistent index files in the archive case; whether it also does so in the unpacked folder is open (see 3).

---

## 3. What is open? (the actual content of 4f)

1. **Does the software enforce the integrity check at all when opening?** And if so, which level: record hash, chain hash, signature, index signature, consistency between data and index file?
2. **Against what does it check the signature?** Against the public key shipped in the project (then a self-generated key pair in the project would be a viable way) or against something external (then not)?
3. **How does it react to a file that does not match:** error message, repair offer, silently discarding the change, silently rewriting, crash?
4. **Does it rebuild a missing/inconsistent index file in the folder?** Then a writer would not have to maintain the index.
5. **Which byte range does the index signature cover?** (Only relevant for writing, not determined so far.)

Explicitly **not** in scope: cracking the encrypted private key, guessing its secret or reading it from a running process. No brute force. The investigation works through the observable opening behaviour, not by breaking the encryption.

---

## 4. What do we want to have? (goal)

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source; 4f there under Z2).

---

## 5. How do we approach it?

Method of the project: **differential analysis.** Every probe is a copy of an existing project with **exactly one** deviation, documented and backed by target data (export from the engineering software). Originals stay unchanged.

### 5.1 Preparation without the engineering software
1. Write a tool that manipulates a project copy in a targeted way and makes the following selectable: change a value; recompute the record hash (yes/no); recompute the chain hash of the following closing records (yes/no); re-create the closing signature with a **self-generated** key pair and replace the public key in the project (yes/no); leave the index unchanged / update it / delete it.
2. For every probe, prove with the existing reader that **exactly the intended check level** breaks and all others stay valid. No opening attempt without this proof.

### 5.2 Probe series (one copy each, one deviation each)
Base: a project with an easily visible, already decoded value (e.g. a short comment text).

| Probe | Deviation | Valid afterwards | Checks whether the software … |
|---|---|---|---|
| A | none (plain copy) | everything | opens an unchanged copy without a message |
| B | only index deleted | data valid | needs / rebuilds the index in the folder |
| C | value changed, no new hash | record hash wrong | checks record hashes |
| D | value + record hash, chain not | chain wrong | checks the chain hash |
| E | value + hash + chain, old signature | only signature wrong | checks the signature |
| F | like E, re-signed with a self-generated key pair, public key replaced | consistent in itself, index signature does not match | checks against the shipped key |
| G | like F, additionally index deleted | like F without index | then rebuilds the index |

Order A, B, C, D, E, F, G. F/G only if E is rejected or if explicitly requested. If a probe breaks early, still test the following ones — they check other levels.

### 5.3 Opening attempt per probe
For every probe record: does the software open it? Which message, what duration? Does it show the changed or the old value? If it opens: save a small change and check whether it appends new records and a new closing record, what the chain is signed with afterwards and whether it rewrote the index. Never accept repair offers on originals. Back up the state of the files before and after every attempt.

### 5.4 Evaluation → decision about the writer

| Result | Consequence |
|---|---|
| No check or only hash/chain | Writer **without** signature possible. |
| Signature against the shipped key (E rejected, F accepted) | Writer with a **self-generated** key pair possible; it remains open whether the software can still save the file itself afterwards. |
| F also rejected | Writing outside the software without the private key is not possible. The writing path remains the programming interface. |

If the outcome is positive, build the minimal writer from 4.2 and cross-check it against the engineering software.

---

## 6. Constraints and rules

- **Differential probes:** always copies, exactly one change, documented recipe, target data from the export. Originals untouched.
- **Do not adopt anything from third-party reverse-engineering projects protected by license law** (code, docs, binaries). Permitted prior art only with an open license and attribution. The work remains an independent, clean new development for interoperability purposes on our own projects.
- **No attack on the encryption** of the private key (see section 3).
- **Only write where there are areas approved for it;** reference and third-party holdings read only.
- Every opening attempt with the real software needs an approval and possibly a manual confirmation by the responsible person; collect such steps in advance and request them in a bundle, while the preparation (5.1/5.2) runs in parallel without the software.
- After every sub-step: document established facts and the result, keep conjectures with status, a short dated lab notebook entry.

---

## 7. Done when
- [ ] Manipulation tool available; probes A–G (or fewer, with reasons) with recipe and verification proof per probe.
- [ ] Documented for every probe: opening behaviour, message, displayed value, behaviour on saving, file changes.
- [ ] Evidenced statement which check levels the software enforces and against what it checks the signature.
- [ ] Clear yes/no/how decision about the writer; if yes, a minimal writer that performs a change that the software accepts and displays correctly.
- [ ] Everything documented; open points named; short summary of the result for the responsible person.

# Retrospective Notes

This document captures observations, questions, and patterns noticed
during human–AI collaboration.

It is not a backlog or a decision log.
Notes here are discussed periodically and may or may not lead to action.

---

## Usage

This file captures observations about how humans and AI collaborate in this repository.

- Humans may add notes freely.
- AI should only add notes when explicitly asked to do so.
- Notes should be observational, tentative, and non-prescriptive.

Notes here are not decisions, tasks, or obligations.

---

## Examples

**YYYY-MM-DD**

- We drifted into refactoring before clarifying the goal.
- The AI kept optimizing edge cases; stopping earlier would have been fine.
- The prompt constraints worked well here.

**YYYY-MM-DD**

- This part of the system consistently causes confusion.
- The AI asked good clarifying questions once context.md was updated.
- We may be underestimating operational cost here.

---

## Notes

**2026-01-16 – CommandParser correctness fixes**

- Started with clear requirements: fix /ai boundary detection, exact header matching, move regex to static.
- Implemented section header fixes (exact matching) and static regex successfully – tests passed immediately.
- ParseAiStartCommand boundary detection hit unexpected behavior: test continued failing despite logic appearing correct in source.
- Debugging cycle became expensive: multiple rebuild attempts, console output suppression in test runner, difficulty isolating compiled vs source mismatch.
- After ~70k tokens, still had 1 of 55 tests failing on edge case (/ai start immediately followed by /ai command with no content between).
- Signal to stop: when debugging effort exceeds value of edge case fix. 54/55 passing (98%) was functional enough to pause.
- Learned: Complex regex/parsing edge cases may need simpler reproduction outside full test suite. Consider standalone minimal repro before deep test runner debugging.

**2026-01-17 – Manual Orchestrator testing**

- A Host project was missing, and the plan (TODO.md) does not specify it to be added at all, without there is nothing to run nor test as the Core project is a library.
- Manual tests did not include a setup instruction for the general environmant, added this via Plan 5.1 (TODO.md).
- Plan 6: Manual Test not possible as Task Claiming cannot be wired to host, as no GitHub client has been chosen yet.
- Learned: Revisit plans, to include all instruction to complete manual jobs, and plan tests so that they can actually be executed with the current state of the implementation.


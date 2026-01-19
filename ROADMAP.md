# Roadmap

This roadmap is directional, not a promise.

---

## v0: AI Worker Execution Layer (Current)

**Definition:** Take validated GitHub Issues and generate code changes via AI, opening PRs with real AI-generated content.

**Critical Path (blocking release):**
- Plan 19: AI Worker integration interface
- Plan 20: Worker invocation wiring (stub)
- Plan 21: Prompt engineering and payload builder
- Plan 22: Real AI worker implementation (OpenAI/Claude)
- Plan 23: Git operations for AI-generated changes
- Plan 24: Worker result validation and quality gates
- Plan 25: Execution reporting with AI attribution
- Plan 17: v0 release criteria and cutover verification

**Non-critical but important:**
- Plan 16: Observability, reliability, and logging
- Plan 9: End-to-end manual verification with real repos

**Success Criteria:**
- Comment `/ai start` on a GitHub Issue
- Orchestrator validates, claims, invokes AI worker
- AI generates code for 1–2 repos (not empty)
- Branches are created with changes committed
- PRs open with generated code and metadata
- Issue receives summary comment with PR links and how to test
- Human can review, iterate, and merge

---

## Now

- **Plan 19:** AI Worker integration interface (define `IAIWorker` contract)
- **Plan 20:** Worker invocation wiring (stub with no-op implementation)
- **Plan 21:** Prompt engineering and `AIPromptBuilder`

---

## Next

- **Plan 22:** Real AI worker (OpenAI/Claude SDK integration)
- **Plan 23:** Git operations for applying AI changes to branches
- **Plan 24:** Worker result validation and quality gates
- **Plan 25:** Enhanced execution reporting with AI attribution
- **Plan 16:** Structured logging and observability (hardening)

---

## Then (before v0 release)

- **Plan 17:** v0 acceptance criteria and cutover
  - **Plan 9:** End-to-end manual verification (execute v0 flow with real repos)

---

## Later (Post-v0 / v1 scope)

- **Plan 18:** Post-v0 refinements
  - Label triggers for AI work
  - `/ai plan` mode (AI drafts without full execution)
  - Queue and persistence (handling concurrent tasks)
  - CI-fix loop (AI responds to test failures)
  - Multi-turn agent conversations (iterative refinement)
  - Streaming output and progress updates
  - Cost tracking and usage reporting

---

## Key Decision Points (Document in DECISIONS.md)

1. **AI Provider Selection** (Plan 22)
   - OpenAI (GPT-4), Claude (Anthropic), or others?
   - Cost, model capabilities, latency, safety?

2. **Prompt Strategy** (Plan 21)
   - Few-shot examples vs. comprehensive context?
   - Safety and hallucination controls?
   - Handling large codebases?

3. **Git Strategy** (Plan 23)
   - Clone full repo or use API for individual file changes?
   - Shallow clone acceptable for speed?
   - How to handle merge conflicts?

4. **Quality Gates** (Plan 24)
   - What % of files can change per task?
   - Block large deletions automatically?
   - Require test coverage increase or coverage reports?

5. **Reporting** (Plan 25)
   - Include token usage and costs in reports?
   - Show AI "confidence" or model version?
   - Link to execution logs for debugging?

---

## Success Metrics (Post-v0)

- Tasks completed successfully (Issue → merged PR)
- Time to merge (human review time, not execution time)
- Code quality (review feedback, rework needed)
- Safety (no security incidents, destructive actions prevented)
- Developer confidence and adoption rate

---
name: multi-plan
description: Generate five independent implementation plans via parallel Plan subagents, distill them through a sixth impartial architecture-focused reviewer, then walk the user through each contested decision before locking in a final plan. Use for non-trivial design or refactor tasks where multiple defensible approaches exist and you want breadth before commitment.
argument-hint: "<task description>"
user-invocable: true
---

# multi-plan

A multi-agent planning workflow. Five explorers map the codebase independently, a sixth impartial reviewer judges them on clean-code and architectural merit, and the user is brought in only for the contested decisions.

**This skill ends at "plan ready". Do not implement until the user explicitly approves the locked plan.**

## When to use

- The user explicitly invokes `/multi-plan <task>`.
- The task is non-trivial: ambiguous design space, refactor with multiple defensible shapes, or a question where you'd otherwise commit too early to one approach.

**Do not use** for trivial fixes (one-line edits, lookup questions). The parallel exploration cost isn't worth it.

## Workflow

### 1. Confirm the task

The task arrives as the skill's arguments. If empty, ask the user once for a task description.

Restate it back in one or two sentences and confirm before spawning agents — five agents working off a misunderstood brief is wasted compute. If the user gives you a wide brief (e.g. "clean up the wantlist code"), ask for the specific outcome they want before continuing.

### 2. Spawn five Plan explorers in parallel

In a **single message**, issue five `Agent` tool calls. All five must use `subagent_type: "Plan"` (read-only, exploration-focused). Their prompts must be self-contained — none of them sees this conversation.

Each prompt should include:
- The confirmed task statement.
- The repo root and any entry points or files you already know are relevant.
- Anything you've already ruled out, so they don't waste cycles re-discovering it.
- An instruction to produce structured output (see schema below).
- This line, verbatim: *"Produce your plan independently. Do not optimize for consensus with other agents — diversity of approach is the point of this workflow."*

Required output schema (have each agent return exactly these sections):

```
## Current state
<what exists today that's relevant — one short paragraph>

## Problem
<what is specifically broken or sub-optimal — one short paragraph>

## Plan
<ordered steps; each step names file paths and one sentence on what changes>

## Merits
<why this approach is good — bullets>

## Risks / tradeoffs
<where it's fragile, contentious, or has unclear downstream effects — bullets>

## Open questions
<things the agent couldn't decide from code alone — bullets>
```

Wait for all five to return before continuing.

### 3. Spawn the impartial reviewer

Once you have all five plans, spawn a sixth agent (`subagent_type: "Plan"`). Pass it:
- The original task brief.
- The full text of all five plans, labelled `Plan 1` through `Plan 5`.
- This explicit framing: *"You are the impartial reviewer. Evaluate each plan against clean-code and architectural principles — layering boundaries, single responsibility, naming honesty, minimal blast radius, testability, reversibility, and whether the change is proportional to the problem. For every contested decision, lay out the pros and cons of each option, include a short illustrative code snippet showing the shape of each option (signature, key call site, or representative line — not a full implementation), then commit to a recommendation; do not just average the plans."*

Ask it to return:

```
## Agreement
<bullets — only points where ≥4 plans converge. These become the baseline.>

## Disagreements
<for each contested decision:>
### <decision name>
- **Option A** (Plan 1, Plan 3): <one-sentence summary>
  - **Sketch:**
    ```<lang>
    <5–15 lines max — signature, key call site, or representative shape>
    ```
  - **Pros:** <bullets — concrete upsides of this option>
  - **Cons:** <bullets — concrete downsides, risks, or costs>
- **Option B** (Plan 2): <one-sentence summary>
  - **Sketch:**
    ```<lang>
    <5–15 lines max>
    ```
  - **Pros:** <bullets>
  - **Cons:** <bullets>
- ...
- **Recommendation**: Option <letter> — <one- to two-sentence architectural reason that explicitly references the pros/cons above>

## Recommended plan
<the synthesized, ordered step list assuming every reviewer recommendation is taken>

## Confidence
<low / medium / high, with one sentence on why>
```

The pros/cons must be substantive and option-specific — not generic platitudes like "simpler" or "more flexible". Each bullet should cite a concrete consequence (a file that grows, a boundary that gets crossed, a test that becomes harder, etc.).

Each option's **Sketch** must be illustrative, not a full implementation: prefer the new/changed signature, the key call site, or the representative line that captures what makes this option different. If two options only differ in one line, show that line in both sketches so the contrast is obvious. Skip the sketch only when the decision is genuinely non-code (e.g. "do we keep the table or drop it" with no signature change).

### 4. Present to the user

Format the user-facing output exactly as below. Keep it scannable — link options back to plan numbers, do not re-quote whole plans.

```
## Current state
<reviewer's distillation, one paragraph>

## Problem
<one paragraph>

## Where the plans agree
- <bullet per consensus point>

## Where the plans disagree
### <decision 1 name>
- **Option A** (Plans X, Y): <summary>
  - **Sketch:**
    ```<lang>
    <reviewer's snippet, verbatim>
    ```
  - **Pros:** <bullets from reviewer>
  - **Cons:** <bullets from reviewer>
- **Option B** (Plan Z): <summary>
  - **Sketch:**
    ```<lang>
    <reviewer's snippet, verbatim>
    ```
  - **Pros:** <bullets from reviewer>
  - **Cons:** <bullets from reviewer>
- **Reviewer recommendation**: Option <letter> — <reason>

### <decision 2 name>
…

## Reviewer's recommended plan
<the synthesized plan>
<confidence note>
```

If two plans propose essentially the same approach, collapse them — don't burn user attention on duplicates.

### 5. Negotiate disagreements one at a time

Walk through each contested decision in order. For each:
1. Restate the options with the reviewer's code sketch and pros/cons for each, followed by the reviewer's recommendation and its reasoning. The user must see the shape of the code and the trade-offs, not just the verdict.
2. Use `AskUserQuestion` when the choice has clear discrete options. Fall back to a free-form prompt otherwise.
3. Wait for the user's answer before moving to the next decision.

Never dump every open question at once. The user should be able to think one decision at a time.

If the user picks something none of the agents proposed, accept it — they have context the agents don't.

### 6. Lock the plan

Once every disagreement is resolved, restate the **final plan** as a single ordered block:

```
## Final plan
1. <step>
2. <step>
…
```

Then explicitly ask: *"Ready to implement, or want to adjust anything?"*

Do not start editing files until the user says yes.

## Notes

- Five plans is the default. If the task is very contained you can drop to three, but always keep the impartial reviewer.
- The `Plan` subagent type is read-only — none of these agents will write code, so there's no risk of conflicting edits.
- The reviewer must see all plans verbatim. Don't summarize them before handing over — that defeats the impartiality.
- If the reviewer's confidence is low, surface that to the user up front so they know the decision is genuinely uncertain.

# OCCAD Architecture Decision Records

This directory records cross-cutting architectural decisions that should remain understandable after the original discussion, issue, or commit history is no longer in active context.

## When an ADR is required

Create an ADR when a change:

- adds/removes a project or assembly;
- changes dependency direction;
- changes Document/Entity ownership;
- changes Tool lifecycle or command-session semantics;
- replaces or materially changes transaction/history behavior;
- introduces a plugin container, global service, threading model, or native async scheduling policy;
- changes persistence compatibility strategy;
- accepts significant long-lived technical debt as a deliberate trade-off.

A local refactor that does not alter externally relevant architecture does not need an ADR.

## Naming

Use monotonically increasing four-digit numbers:

```text
0001-short-decision-title.md
0002-another-decision.md
```

Use lowercase kebab-case after the number.

## Status

Recommended statuses:

- Proposed
- Accepted
- Superseded
- Rejected
- Deprecated

When superseding a decision, do not delete the old ADR. Mark it `Superseded` and link to the replacement.

## Content

Use [0000-template.md](0000-template.md). Keep ADRs concise and decision-focused. They should explain context, decision, consequences, rejected alternatives, and migration/validation impact where relevant.

## Language

ADR may be written in Chinese or English. Technical identifiers remain unchanged. If a decision is especially important to external contributors, a bilingual summary is recommended, but duplicate ADR files are not required.

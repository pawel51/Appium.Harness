# docs/knowledge — resolved bugs worth not re-solving

Each file here is **one bug that already cost someone time**: what it looked like, the exact error
text, why it happened, and what fixed it. [`INDEX.md`](INDEX.md) lists them, one line each.

Search the error string before debugging; on a hit, read **`## Don't try` before `## Fix`**.

| Store | Holds | Where |
|---|---|---|
| **this folder** | bugs in *this library's* code or conventions | committed, reviewed in PRs |
| **user store** | bugs in the *stack* — Appium, WebDriver, .NET MAUI | `~/.claude/knowledge/` |

The test when filing: *"would this still bite me in a different project using the same stack?"*
Yes → user store. No → here. Add one with `/known-issues save`, or by hand to
`~/.claude/knowledge/SPEC.md`.

The session-start hook finds this folder from the git root, so it is read automatically.

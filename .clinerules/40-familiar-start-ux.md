# Familiar Start UX

ChurchBooks meets users where they are.

- Functional mode and help level are independent.
  - Modes: Simple, Pro, Accountant.
  - Help: Beginner, Guided, Experienced, Accountant.
- Use familiar terms and aliases from spreadsheets, QuickBooks, Xero, and ordinary church bookkeeping, but never equate concepts that are materially different.
- Prefer progressive disclosure over hiding correctness.
- Explain with concrete examples: Bank Account = where money is held; Fund = what money is designated/restricted for; Ministry/Department = who/what uses it.
- Search should understand aliases such as P&L, profit and loss, income statement, supplier/vendor, class, tracking category, bank match/reconcile.
- Provide preview-before-posting and a future Practice Company.
- Avoid modal popup overload.

## Phase 3C implementation rules
- Workspace Mode and Help Level are independent and must remain independently configurable.
- Default first-run posture is Simple + Guided, stored locally only.
- Search aliases may route familiar terms to ChurchBooks features, but must explain non-equivalence before mapping data.
- Fund Manager must use ledger-derived balances; never expose an editable fund-balance field.
- Prefer inline guidance/status over blocking popups. Destructive or risky actions require explicit confirmation semantics.
- Standard WPF controls must keep keyboard focus/accessibility metadata and scalable layouts.

# Development Phases

ChurchBooks uses exact frozen predecessor baselines. A new phase must pass isolated staged Windows verification before the live project is modified.

## Phase 10 - Vendors + Direct Expenses

Phase 10 introduces the first expense workflow while preserving all Phase 2-9 accounting/import/reconciliation contracts.

- Vendors are reusable master data and do not create accounting entries.
- Vendors can be archived and restored without deleting historical references.
- Direct expenses represent money that leaves a selected bank account immediately.
- One expense can contain multiple Expense-account/Fund lines.
- Every line requires an active direct-posting Expense account and active Fund.
- The selected bank account must be active and its currency becomes the expense currency.
- Posting uses the protected FundAccountingEngine.
- Bank credits are grouped by Fund so every journal line receives exactly one Fund assignment.
- Draft creation does not post.
- Preview does not post.
- Only the explicit Post Expense command posts.
- Reposting a posted expense is refused.
- Smart Import never posts expenses automatically and never creates vendors silently.
- Bills/accounts payable, partial payments and purchasing are deliberately deferred to Phase 11 so direct-expense behavior remains narrow and testable.

## Verification target

- Accounting: 119
- Core: 4
- Data: 87
- App: 71
- Total: 281
- Release x64 build: zero errors; warnings remain errors.
- Staged WPF/schema-v10 smoke: PASS.
- Live WPF/schema-v10 smoke: PASS.
- Transaction receipt finalizes only after complete live re-verification.

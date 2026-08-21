# ChurchBooks Phase 8 — Bank Reconciliation

Phase 8 adds accountant-controlled bank reconciliation on top of the verified Phase 7 Smart Import staging layer.

## Frozen predecessor

Phase 7 R1.3 is the exact predecessor. Its Smart Import workflow remains staging-only and does not post automatically.

## Reconciliation workflow

1. Select an active bank account.
2. Link one staged Smart Import session as bank-statement input.
3. Explicitly choose the bank statement amount convention:
   - Signed Amount
   - Debit Increases Balance
   - Credit Increases Balance
4. Start a statement period with start date, end date, and statement ending balance.
5. Match statement lines to bank-ledger journal entries.
6. Use many-to-many match groups when one statement item corresponds to several book entries or vice versa.
7. Optionally run conservative exact auto-match. It matches only a unique equal signed amount within a three-day window and refuses ambiguous cases.
8. Complete only when every statement line is matched and the reconciliation difference is exactly zero.
9. Completed reconciliations are locked.

## Accounting safeguards

- Reconciliation never creates an automatic balancing journal.
- Bank fees, interest, corrections, and other missing book activity must be explicitly recorded through the appropriate accounting workflow before reconciliation can finish.
- Statement import is linked only once.
- Statement fingerprints are unique per bank account.
- A statement line can be matched only once.
- A journal can be cleared only once per bank account, so a bank-to-bank transfer can reconcile on both bank sides.
- Journal or statement evidence already reserved by another reconciliation is excluded from the current unmatched lists; completed matches remain permanently cleared.
- Book items are aggregated by Journal Entry ID so one fund-aware deposit journal is treated as one bank-ledger transaction.
- Positive bank-ledger amount means the book bank balance increased; negative means it decreased.
- Existing contribution, fund, deposit, General Ledger, Smart Import, and personalization contracts remain protected.

## User experience

Bank Reconciliation is a tab inside the existing Banking workspace. It does not create a second navigation system. The approved compact maximized ChurchBooks Pro shell remains binding, with essential actions kept visible and table scrolling used inside the reconciliation workspace.

## Deferred

Automated bank feeds, automatic adjustment journals, licensing, and regulatory/tax filing remain outside Phase 8.

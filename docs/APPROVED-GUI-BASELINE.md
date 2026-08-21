# ChurchBooks approved desktop GUI baseline

The canonical visual direction is `docs/reference/ChurchBooks-Approved-Dashboard.png`.

## Durable layout rules

- Use the premium compact desktop shell shown in the approved dashboard: left navigation, one top command/search/period/currency/user bar, dense dashboard cards, and a bottom status strip.
- Prefer one-window operation at normal desktop sizes. The default window opens maximized.
- Nothing important may be cropped or hidden. When a smaller display cannot fit the full workspace, use an intentional scroll fallback rather than clipping controls.
- Preserve keyboard navigation, readable text, virtualization for long lists, and responsive column sizing.
- Do not place decorative whitespace ahead of accounting information.
- Never fabricate dashboard financial values. Cards must bind to real ChurchBooks data or clearly indicate that a later phase owns the metric.

## Data-driven giving/fund rule

- Operational code must never special-case `Missions`, `Building Fund`, `Love Offering`, `Youth Ministry`, or other ministry names.
- Giving Categories come from the `giving_categories` table. Funds come from the `funds` table.
- Users can add and edit these records. Removing a record from active use archives it when history may reference it; archived records disappear from new offering entry but remain reportable.
- `Tithes` may be offered as a starter/default record in a future setup experience, but reports and offering logic must treat it like ordinary configured data, not a code constant.
- Charts enumerate the categories/funds returned by storage; no fixed legend list is allowed.

## Phase 6 onward

Banking, deposits, reconciliation, expenses, budgets, reports, tax/BIR, audit and settings must be added into this approved shell without replacing it with a second navigation system.

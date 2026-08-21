# Phase 5 - Individual Offerings + Analytics

Phase 5 records service/offering batches and individual contribution breakdowns while preserving one People-directory identity per person.

## Accountant workflow

1. Register or maintain the individual in **People**. The People directory remains the only identity authority.
2. Create an open service/offering batch with a service date.
3. Select the individual donor.
4. Enter one or more breakdown rows. Every row requires both a **Giving Category** and **Fund** plus a positive amount.
5. Record the contribution. The contribution total is derived from its breakdown rows; users do not type a second independent total.
6. Review the person's totals for the current week, month, year, and all time.
7. Choose weekly, monthly, or yearly analytics and select Line, Column, Bar, Pie, Donut, or Area visualization.

## Accounting boundary

Phase 5 is the donor/contribution subsidiary ledger. It deliberately does **not** create a bank deposit or general-ledger journal. Phase 6 owns bank/financial accounts and the deposit-to-ledger bridge. This separation prevents one offering from being posted twice while still preserving the donor-level breakdown needed for statements and analytics.

Giving Category answers **what kind of gift**. Fund answers **what purpose/designation controls the money**. They remain separate identifiers on every breakdown row.

## Period calculations

- Week starts Monday and ends at the selected as-of date for the current week.
- Month starts on day 1.
- Year starts January 1.
- All-time includes recorded contributions through the selected as-of date.
- Line/Column/Bar/Area render trends; Pie/Donut render giving-category composition for the selected period.

## Safety

- Closed batches reject new contributions.
- Archived people, categories, and funds reject new contribution lines.
- Duplicate contribution IDs are rejected.
- Duplicate category+fund rows inside one contribution must be combined.
- Historical contribution rows are append-only in Phase 5; destructive editing is deferred until a reversal/audit model is defined.


## Approved GUI and configurable giving/funds contract

- `docs/reference/ChurchBooks-Approved-Dashboard.png` is the approved desktop GUI baseline. Prefer a complete one-window dashboard at normal desktop sizes, with intentional scrolling only as a small-screen fallback; controls must never be cropped or hidden.
- Giving Categories and Funds are data-driven. Operational forms, reports, and charts must load active records from storage and must not hard-code ministry names such as Missions, Building Fund, Love Offering, or Youth Ministry.
- Add/edit is direct. “Remove” means remove from active use while preserving historical references; restoration is supported.
- Tithes may be a starter/default setup record, but calculations and charts must not special-case it.

# Phase 4 - People + Giving Directory

## Purpose

Phase 4 adds the church master-data layer required before offering entry can safely exist: members, donors, households, and configurable giving categories. It deliberately does **not** post money or create journal entries. Service/offering entry remains Phase 5.

## Clean boundaries

- A **Person** may be a member, a donor, or both.
- A **Household** groups people for church administration and future household giving statements.
- A **Giving Category** describes what a contribution is called, such as Tithe, Love Offering, Missions, Building, or Benevolence.
- A **Fund** remains the accounting/restriction dimension. Giving Category and Fund are intentionally different concepts.
- Phase 4 directory edits never create, alter, or delete accounting journals.

## Schema v4

The migration is additive and creates `households`, `person_profiles`, and `giving_categories`. Existing Phase 1-3 tables are not rewritten. Foreign keys are enabled and validated by SQLite. Member numbers, household names, and giving-category code/name values are protected against duplicate records.

## Safety behavior

- People are archived/reactivated rather than deleted.
- Households with active assigned people cannot be archived until those people are moved.
- Archived households cannot be assigned to an active person.
- Giving categories are archived/reactivated rather than deleted, preserving future historical references.
- Licensing remains intentionally deferred.

## UI

The People workspace provides three focused tabs: People, Households, and Giving Categories. Search recognizes member numbers, names, roles, households, and email addresses. FluentValidation remains at the input boundary.

## Next boundary

Phase 5 may introduce service/offering batches and contribution transactions. Those transactions must consume the Phase 4 IDs while posting through the existing protected accounting/fund engines rather than creating a second ledger path.

# Development Rules

1. Change the smallest owning layer. Avoid cross-phase ownership.
2. Do not add a dependency without a documented purpose, license, version, security review, and phase owner.
3. Prefer platform-native or mature maintained libraries; wrap optional libraries behind ChurchBooks-owned interfaces when lock-in is plausible.
4. Keep UI/domain/storage concerns separate.
5. No feature is complete without tests and relevant documentation updates.
6. Never claim a Windows parser/build/test was run unless it actually ran on Windows.
7. Preserve professional grammar, accessibility, keyboard navigation, DPI scaling, and compact layouts.
8. Each new phase may improve previously completed work when the improvement is clearly owned, research-backed, regression-tested, documented, and does not weaken a frozen accounting or release invariant. Do not postpone a safe cross-cutting improvement merely because its original phase is complete.

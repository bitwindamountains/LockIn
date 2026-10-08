# LockIn repository rules

- Read `lockin_full_scale_build_plan (1).md`, `docs/STATUS.md`, and `docs/DECISIONS.md` before the next milestone.
- Keep the service as session authority. Browser caches must be derived and expirable.
- Implement and test release/recovery alongside every real restriction.
- Do not install a service, close user apps, or change browser/OS policy during ordinary builds and tests.
- Label simulation accurately in code, UI, and reports.
- Run the documented checks and update `docs/STATUS.md` after each milestone or handoff.


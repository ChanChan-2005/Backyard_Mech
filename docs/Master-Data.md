# Suppliers, Categories, Services and Tax

- Suppliers and Service Catalog are separate sidebar modules. View, Create and Edit permissions are available in Settings > Roles & Permissions. Edit includes deactivate/reactivate; historical records are not hard-deleted.
- Settings has separate Service Categories, Part Categories and Tool Categories tabs. Leave Parent Category empty to create a parent; choose one to create a subcategory. Categories cannot form cycles. Active children prevent parent deactivation; reactivate parents before children.
- All four directories use modal forms, search, status filters and ten-row pagination. Read-only roles can view details without mutation controls; direct unauthorized POST requests return 403.
- Suppliers and categories populate the existing parts/tools forms. Inactive records cannot be assigned to new items. Existing inactive associations may be retained during editing.
- Active services populate the estimate service selection. Catalog edits change defaults, not historical approved estimate line amounts.
- Enter tax as a percentage, e.g. 8.25. Storage uses 0.0825. Effective dates follow the existing UTC billing clock; the optional end date includes the whole day. Deactivation takes effect immediately. Tax records referenced by an invoice cannot be edited; create a new effective-dated version. Versions with the same name cannot have overlapping effective periods. Existing invoice totals are not recalculated by master-data changes.
- Every master mutation uses antiforgery validation, server-side permissions, the shared serializable workflow lock, audit entries and optimistic record stamps. No database schema expansion is needed; startup seeds six supplier/service permission definitions without granting them to non-owner roles.

## Verification

Run `powershell -NoProfile -ExecutionPolicy Bypass -File tests/MasterData.Smoke.ps1` against the local development server. The script uses a temporary MFA-enrolled owner and a temporary read-only account, checks persisted data and rejected operations, then removes its fixtures. Audit entries remain intentionally. Defaults match the existing local repair-workflow smoke script; credentials can be supplied through parameters.

Also run `tests/RepairWorkflow.Smoke.ps1` for estimate, repair, billing, tax and security regression coverage. That suite deliberately triggers the login rate limiter; wait a minute before another login test or restart the local server.

Browser visual verification was unavailable in the current environment; HTTP-rendered pages, build and database-backed workflows were checked.

# Repair Workflow Verification

Start the application, then run from the project root:

```powershell
dotnet run --project Backyard_Mech.csproj --urls http://127.0.0.1:5099
```

In another terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/RepairWorkflow.Smoke.ps1
```

The script defaults to the existing local SQL Express database. It copies the specified seed administrator's password hash into a temporary test account, enrolls MFA only for that temporary account, and leaves the real administrator unchanged. Parameters allow a different URL, connection string, login and password. Run against a development database only. Python (standard library only) is needed to generate independent test authenticator codes.

The test creates uniquely named fixtures and removes those fixtures in `finally`. Existing workshop records are not modified. Audit records and incremented identity/invoice numbers are deliberately preserved.

## Covered Flow

- Estimate creation, versioned editing, stale-revision rejection, recorded customer decision and cancellation.
- Work-order conversion, duplicate prevention, schedule editing, cancellation and reopening.
- Stock reservation, installation, return, editing and reversal, including negative-stock and dependent-movement rejection.
- Labour create/edit/delete; persistent timer start, pause, resume and completion.
- QC failure, reinspection, pass and void; independent inspector enforcement and legacy completion bypass prevention.
- Invoice generation from net installed parts and recorded labour; duplicate and failed-QC billing rejection.
- Payment create/edit/void, duplicate-submission protection and overpayment rollback.
- Credit adjustment create/edit/void and invoice balance/status recalculation.
- Permission-restricted buttons/sidebar, direct POST denial and antiforgery protection.

## Record Semantics

- Estimate editing creates a new draft revision. Customer approval is explicitly recorded by a permitted staff account, with confirmation details; no customer email or public approval link is sent by this flow.
- Estimate and work-order removal uses cancellation, preserving history.
- Parts must be reserved before installation. Receipts/stock adjustments have no work order; reservation/installation/return/release require one.
- Posted stock movements, payments, adjustments, invoices and QC results use reversal/void instead of destructive deletion. Original values and reasons are retained in audit history.
- A voided QC result requires a fresh inspection; failed repairs return to In Repair.
- Invoice line items are snapshots. Credits/debits are tax-inclusive adjustments, leaving original line items and original tax unchanged.
- Short mutation forms are modals. JavaScript and styles are in dedicated repair-workflow files, not `site.js`.
- Startup applies an additive upgrade: `AccountSecurity` and nullable `ServiceEntry.ApprovedEstimateLineId`. No existing database or records are replaced.

## Security Regression Coverage

- Privileged MFA enrollment, code replay rejection, single-use recovery and password-only session denial.
- Disabled/demoted account session invalidation, persistent five-failure lockout, and HTTP 429 login limiting.
- Approved-only parts quantities and labour tasks/rates/hours, including rollback of rejected edits.
- Active-job estimate amendments, exact revision adoption only after approval, and existing labour remapping.
- Off-shift and absence rejection, appointment/work-order collision, concurrent conversion, and shift-change rollback.
- Permission before/after and export audit records.
- Run at least one minute apart, or restart the local app, because the test deliberately exhausts the IP login rate limit.

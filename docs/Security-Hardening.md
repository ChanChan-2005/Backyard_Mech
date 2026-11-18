# Priority Security and Workflow Hardening

## Approved Scope

Parts reserved or installed cannot exceed the net approved quantity. Invoice generation rechecks the ledger and rejects unapproved parts instead of falling back to catalog prices. Labour forms select an exact approved estimate line; descriptions and rates come from the server. Manual entries cannot exceed approved hours or amounts.

Timers retain actual elapsed time, including an overrun, so history is not silently truncated. An overrun blocks billing until additional scope is approved. A new timer or resume is rejected when recorded approved hours have been exhausted.

An unbilled, editable work order can receive an amended estimate. Its previous approved revision remains authoritative until a permitted staff member records customer approval. Approval validates already-used stock/labour, retains recorded tasks and rates, and updates the work order and labour links in one transaction. Closed/QC-pending jobs must be reopened before amendment. Existing labour with no approved-line link must be reviewed and linked through the editor; the upgrade does not invent approvals for old data.

## Authentication

- User activity, role, password fingerprint and role activity are revalidated on protected Owner requests. Account/role/password changes require signing in again.
- Session idle timeout: 20 minutes. Absolute authenticated lifetime: 8 hours.
- Five failed password attempts cause a persistent 15-minute lockout. IP login limit: 20 requests per minute. Counts are serialized in SQL; per-IP limits are per application instance.
- Existing hashes remain readable and are upgraded to the ASP.NET Core Identity password-hasher format after a successful password check.
- Login uses username/email and password only. Authenticator/OTP enrollment, verification, recovery and session requirements have been removed. New databases do not create MFA columns; legacy columns in existing databases are retained for data compatibility but are not mapped or read by the application.
- Login success/failure, permission changes and report exports are audited without passwords.

The application still uses the existing `Users`/`AppRole` and session authentication architecture. This is NOT a complete migration to Identity user/role stores, UserManager or SignInManager. That migration remains a separate compatibility task.

## Scheduling

Appointment creation/edit/cancellation, queue bay assignment, work-order generation/edit/reopen/dispatch, shift changes and bay maintenance writes share the repair-workflow SQL application lock and transaction. Checks include both appointment and work-order allocations, bay maintenance, weekly shifts, and availability exceptions. No configured shift means unavailable, not implicitly available.

UI schedule values are garage-local; exception timestamps are UTC. `GarageTimeZone` defaults to `Asia/Manila`. Overnight shifts are supported by the checker. Set the host clock/time zone correctly as older UI/date helpers still use local system time. A linked appointment is edited through Work Orders to avoid diverging allocations. Shift edits that invalidate future allocations are rolled back.

## Deployment and Limits

Startup runs the idempotent upgrade in `Data/SecuritySchema.cs`; the master SQL script includes the same additions. Back up before deploying. No existing users or business records are deleted.

Production cookies require HTTPS. Configure HTTPS correctly. Multiple app instances need shared session storage and a distributed edge rate limit; current in-memory session/IP throttling is single-instance.

Initial enrollment assumes control of the existing password; rotate weak demo credentials before deployment. Password-only sign-in does not provide a second authentication factor.

The HTTP/database smoke tests are regression checks, not a penetration test or production security certification. Full Identity migration, monitoring/alerts, customer approval tokens and private inspection uploads remain separate work. Browser visual QA was not available in this environment.

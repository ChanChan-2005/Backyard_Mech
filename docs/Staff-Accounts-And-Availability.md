# Staff profiles, accounts and availability

## Navigation

1. User Management > Add Staff Profile saves employment/contact information only. It does not create login credentials. Job title and login role are separate.
2. Account Access > Create Account opens a modal. Choose an active, unlinked staff profile, enter a unique login email/username, a strong password and a role. The saved account is linked by user ID, never inferred from matching contact email.
3. View Access opens that account's permissions below the list. Select a role, account-enabled checkbox and Inherit/Allow/Deny per permission, enter the reason, then Save Account Access.
4. User Management > Availability Exceptions manages leave/unavailable intervals and extra available intervals in the configured garage time zone. Staff do not need login accounts to be scheduled.

## Permission precedence

- Inherit: current role default; no override row stored.
- Allow: grants this user the action even when the role default denies it.
- Deny: denies this user the action even when the role default allows it.
- Explicit Deny on a module's View permission also blocks that module's mutation actions and removes its sidebar entry. This prevents a hidden module from remaining writable through direct requests.
- Overrides never modify RolePermission. Users with the same role remain independent. Effective permissions are resolved on every request, including sidebar/actions, backend authorization and privileged MFA requirements.
- New User Access View/Create/Edit permissions separately control account management. Owners have access; other existing roles are not silently granted these permissions. Newly introduced Shop Manager has account-management defaults.
- Account managers cannot edit their own access, override protected owner access or grant permissions beyond their own effective access. Protected owner remains a recovery account. Privileged accounts require MFA. Disabled, archived or deleted linked staff profiles cannot retain protected sessions.

## PDF alignment and compatibility

The seven role names on PDF page 2 are present: Shop Manager, Service Advisor, Mechanic, Parts Clerk, Tool Room Attendant, Quality Control Inspector, Accounting Clerk. Existing legacy roles remain intact. Defaults are seeded only for newly created roles; existing role permissions are preserved. The PDF does not define a complete per-action permission matrix; initial grants use the role's operational area, without destructive Delete defaults.

The optional-account workflow follows the user's updated requirement. The additive UserPermissionOverride table extends the PDF's role-only design. Startup creates it automatically; Database/StaffAccountAccess.sql is supplied for managed deployments. Existing custom Users/session authentication is retained; this is not a migration to complete ASP.NET Identity user/role stores.

## Scheduling integrity

Exceptions store UTC and display garage-local time. Overlapping exceptions are rejected. Create/edit/delete run under the same serializable transaction lock used by appointment/work-order booking. Every affected future appointment and work-order assignment is revalidated before commit; removing extra availability or adding leave cannot invalidate existing bookings. Exception deletions retain an audit snapshot and reason.

## Verification

`powershell -NoProfile -ExecutionPolicy Bypass -File tests/StaffAccess.Smoke.ps1`

Checks profile-only creation, explicit account linking, password hashing, role preservation, per-user precedence, sidebar/direct-request enforcement, immediate session permission refresh, stale writes, self/protected-account safety, exception CRUD, UTC conversion and appointment/work-order conflict rejection. Fixtures are temporary; audit records are retained. Also run MasterData.Smoke.ps1 and RepairWorkflow.Smoke.ps1 for shared permission-service regressions. The repair suite intentionally exhausts the login rate limiter; restart the development server or wait a minute before another authentication suite.

Visual browser verification is still pending because no browser was available in this environment.

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
- Overrides never modify RolePermission. Users with the same role remain independent. Effective permissions are resolved on every request, including sidebar/actions and backend authorization.
- User Access View/Create/Edit permissions separately control account management. Garage Owner and Administrator can manage accounts according to their saved permissions.
- Account managers cannot edit their own access, override protected owner access or grant permissions beyond their own effective access. Protected owner remains a recovery account. Login currently uses password-only authentication. Disabled, archived or deleted linked staff profiles cannot retain protected sessions.

## Standard roles

The seven selectable roles are Garage Owner, Administrator, Service Advisor, Mechanic / Technician, Parts Clerk, Supervisor / QC Inspector, and Accounting / Cashier. Their names and descriptions follow section 2.2, “User Roles & Characteristics.” The protected RootAdmin account remains internal and cannot be selected for staff. Startup deactivates unassigned legacy and custom roles; an extra role with assigned users stays active until those accounts can be reassigned, but it is no longer selectable. Roles cannot be created, renamed, or deleted. Permissions for the seven standard roles remain editable.

The permission editor follows the sidebar's 22 application entries in the same order. Parts Management and Parts POS have separate access, as do Archive and Records Disposition. Startup copies legacy Inventory and Records grants into both corresponding entries once, then retires those old grants. My Profile and Logout do not need role permissions.

Navigation sub-tabs have their own role permissions: the Parts Management views, Archive and Records Disposition record filters, Settings category types, Roles and Permissions, and employee list filters. Stock Transactions, Tax, System Preferences, Archived Employees, and Soft Deleted Employees use their existing detailed action permissions. A role needs both the parent module's View permission and the matching tab permission. On upgrade, each new sub-tab inherits the role's existing access to its parent page once; later edits are independent. The All filter in Archive or Records Disposition includes every record type, so grant it only to roles that may view the full record list.

Account creation accepts an optional active, unlinked staff profile. An account created without a profile appears under Other Login Accounts and uses the selected role's permissions. The additive UserPermissionOverride table extends the PDF's role-only design. Startup creates it automatically; Database/StaffAccountAccess.sql is supplied for managed deployments. Existing custom Users/session authentication is retained; this is not a migration to complete ASP.NET Identity user/role stores.

## Scheduling integrity

Exceptions store UTC and display garage-local time. Overlapping exceptions are rejected. Create/edit/delete run under the same serializable transaction lock used by appointment/work-order booking. Every affected future appointment and work-order assignment is revalidated before commit; removing extra availability or adding leave cannot invalidate existing bookings. Exception deletions retain an audit snapshot and reason.

## Verification

`powershell -NoProfile -ExecutionPolicy Bypass -File tests/StaffAccess.Smoke.ps1`

Checks profile-only creation, explicit account linking, password hashing, role preservation, per-user precedence, sidebar/direct-request enforcement, immediate session permission refresh, stale writes, self/protected-account safety, exception CRUD, UTC conversion and appointment/work-order conflict rejection. Fixtures are temporary; audit records are retained. Also run MasterData.Smoke.ps1 and RepairWorkflow.Smoke.ps1 for shared permission-service regressions. The repair suite intentionally exhausts the login rate limiter; restart the development server or wait a minute before another authentication suite.

Visual browser verification is still pending because no browser was available in this environment.

## Profile-created account login regression (2026-11-05)

The security state key is the existing `Users.Id`, not a second auto-generated ID. EF previously created `AccountSecurity.UserId` as IDENTITY on a fresh database, causing the first login to fail with SQL error 544. The EF mapping now uses `ValueGeneratedNever`; startup transactionally repairs that legacy table while retaining every security-state column. The fixed singleton retention-policy mapping also allows a fresh database to start.

The expanded staff-access smoke suite verifies that custom role creation is blocked and uses SaveRolePermissions on Parts Clerk in a disposable test database. It creates an account linked to a staff profile with the mechanic role, signs in by username, changes its role, and signs in by email. It verifies role checkbox persistence, session invalidation after role changes, individual overrides, and forbidden direct access. A role deliberately configured without any View grants still has no application access; its login result explains that permissions must be assigned.

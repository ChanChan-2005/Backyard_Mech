# Standard staff roles

Configured from the requested user-role responsibility table. Permissions belong to roles, not accounts. Existing role keys and employee assignments are preserved; unrelated custom roles are retained.

| Role | Responsibilities | Dashboard sections |
| --- | --- | --- |
| Root Administrator (`admin123`) | System-protected full access; separate from operational roles | All |
| Garage Owner | Full access through editable explicit permissions, including employee and role administration | All initially |
| Administrator | Employee profiles, accounts, schedules, staff roles and general settings | None; opens staff modules instead |
| Service Advisor | Customers, vehicles, check-in, appointments, estimates, customer approval communication and work-order creation | Active work orders, vehicles serviced, pending approvals, vehicles in garage, service queue |
| Mechanic / Technician | Repair status, initial inspections, labour timers/entries, installed and returned parts against a work order | Active work orders, vehicles in garage, queue, labour, inspections |
| Parts Clerk | Parts/product records, suppliers, receipts, reservations, installation/issue, returns and stock corrections | Inventory alerts |
| Supervisor / QC Inspector | Inspections, QC approval and release of completed work | Active work orders, vehicles serviced/in garage, queue, labour, inspections |
| Accounting / Cashier | Invoices, payments, adjustments and financial reporting | Revenue, vehicles serviced, unpaid invoices, recent invoices |

Administrators receive the explicit **Manage Staff Role Permissions** permission so they can configure and assign operational staff roles without needing those roles' business-data access themselves. Protected Root Administrator access cannot be assigned or edited through staff administration. This is a powerful permission intended for trusted account administrators.

Mechanics can record only installed/returned parts linked to a work order. Other stock movement types require **Manage Stock Transactions**. QC approval is restricted to the QC role and Garage Owner. Invoice/payment voiding and permanent deletion are not granted by these staff profiles.

These profiles control modules, actions and dashboard sections; they do not add employee-specific row filtering. Recent Shop Activity remains Garage Owner and Root Administrator only because it mixes information across departments.

The explicit profile definitions are in `Data/StandardRoleProfiles.cs`. Initial configuration replaced grants only for the six named staff roles and was audited with before/after permission IDs. Startup does not reapply these profiles, so later edits through Roles & Permissions are preserved.

The legacy protected Garage Owner was separated from Root Administrator by a one-time audited migration. `admin123` now uses `RootAdmin`; Garage Owner (`Owner`) has explicit editable grants. Later startup does not restore removed Garage Owner grants.

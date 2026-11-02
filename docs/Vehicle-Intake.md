# Vehicle records and intake

Implements the vehicle/appointment/check-in portion of the master plan (pages 2, 4, 5 and 11). Initial inspection and estimate approval remain separate workflows.

## Screens and permissions

- Vehicle Records (`/Owner/VehicleHistory`): searchable registry, ownership/odometer history, Add Vehicle, Transfer Ownership and Record Odometer modals. Requires Vehicles.View; writes require Vehicles.Create or Vehicles.Edit. Existing role defaults are not overwritten. Grant new supported vehicle permissions in Roles & Permissions or individual access overrides.
- Appointments (`/Owner/Appointments`): real day/week/month calendar, list, search/status filters, new/edit/cancel/check-in modals. Appointments.Create/Edit/Delete are checked server-side and controls hidden without access.
- Customer vehicle counts link to that customer's ownership records, including previous ownership. Queue details display actual check-in time separately from scheduled start.

## Rules

- Registration checks VIN and plate across active, archived and deleted vehicles. It creates the owner snapshot and initial odometer history atomically.
- Transfer requires verified authority and a reason. It closes prior ownership effective dates and adds a new snapshot. Open visits or work orders block transfer. Old appointment customer snapshots and ownership records are never rewritten.
- Odometer entries are append-only, nonnegative and cannot decrease below the vehicle's current reading or historical maximum. Values use the existing system's odometer unit; no unit conversion is performed.
- New appointment status is always Scheduled; entry type is Scheduled or Walk-in. Requested services have separate AppointmentService rows with catalog IDs and name snapshots. Preferred mechanic is separate from assigned mechanic, including staff without accounts.
- Editing/cancelling is restricted to scheduled, not-yet-checked-in visits without inspections, estimates or work orders. Cancellation retains history and requires a reason. Changes after repair intake belong to their workflow rather than generic appointment editing.
- Appointment times are garage-local (GarageTimeZone); history timestamps are UTC. Schedules are revalidated under the shared serializable workflow transaction and application lock. Conflicts include vehicle, bay, assigned mechanic, weekly availability, exceptions, work orders and maintenance.
- Check-in requires today's scheduled visit, current ownership, identity/vehicle verification and an odometer reading. It revalidates assigned resources, stamps CheckedInUtc and actor snapshots, creates the mileage entry and QueueStatusHistory, and transitions to Arrived. Repeated/stale check-in is rejected.
- Walk-ins create their appointment/services and check-in together, using the garage's current time and a one-hour intake booking. Failed validation rolls everything back.
- Updates include optimistic stamps; mutations require anti-forgery tokens and write audit records. No database schema changes are needed: existing VehicleOwnership, VehicleOdometerHistory, AppointmentService and QueueStatusHistory tables are used.

## Verification

Start a local development instance, then run `powershell -File tests/VehicleIntake.Smoke.ps1`. It uses temporary, clearly prefixed fixtures, checks persistence and rejected requests, and removes those fixtures in finally. Audit entries remain. Use only a development database. Connection/login parameters can be overridden.

Also run StaffAccess.Smoke.ps1, MasterData.Smoke.ps1 and RepairWorkflow.Smoke.ps1 for surrounding workflow regressions.

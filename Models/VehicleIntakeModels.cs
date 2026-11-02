using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public class VehicleHistoryForm
{
    [Range(1, int.MaxValue)] public int VehicleId { get; set; }
    [Required] public string Expected { get; set; } = "";
}
public class TransferVehicleForm : VehicleHistoryForm
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
    public bool AuthorityVerified { get; set; }
}
public class RecordOdometerForm : VehicleHistoryForm
{
    [Required, Range(0, int.MaxValue)] public int? Odometer { get; set; }
}
public class CheckInAppointmentForm
{
    [Range(1, int.MaxValue)] public int AppointmentId { get; set; }
    [Required] public string Expected { get; set; } = "";
    [Required, Range(0, int.MaxValue)] public int? Odometer { get; set; }
    public bool IdentityVerified { get; set; }
}
public class VehicleHistoryPage
{
    public List<Vehicle> Vehicles { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public Vehicle? Selected { get; set; }
    public List<VehicleOwnership> Owners { get; set; } = new();
    public List<VehicleOdometerHistory> Readings { get; set; } = new();
    public string Expected { get; set; } = "";
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
}
public record IntakeAppointment(Appointment Row, AppointmentFormModel Form, string Vehicle, string Services, bool CanChange)
{
    public List<Inspection> Inspections { get; init; } = new();
    public List<InspectionMedia> Media { get; init; } = new();
}
public class IntakeAppointmentsPage
{
    public List<IntakeAppointment> Rows { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public List<AppointmentVehicleOption> Vehicles { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public List<Bay> Bays { get; set; } = new();
    public List<EmployeeProfile> Employees { get; set; } = new();
    public DateTime Today { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanInspect { get; set; }
    public string View { get; set; } = "calendar";
}
public record IntakeEditor(IntakeAppointmentsPage Page, AppointmentFormModel Form, string DialogId, string Title);

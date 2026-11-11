using Backyard_Mech.Models;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
        public DbSet<AccountSecurity> AccountSecurity => Set<AccountSecurity>();
        public DbSet<AppRole> AppRoles => Set<AppRole>();
        public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
        public DbSet<EmployeeRateHistory> EmployeeRateHistories => Set<EmployeeRateHistory>();
        public DbSet<EmployeeAvailability> EmployeeAvailabilities => Set<EmployeeAvailability>();
        public DbSet<EmployeeAvailabilityException> EmployeeAvailabilityExceptions => Set<EmployeeAvailabilityException>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
        public DbSet<RecordsDispositionPolicy> RecordsDispositionPolicies => Set<RecordsDispositionPolicy>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<VehicleOwnership> VehicleOwnerships => Set<VehicleOwnership>();
        public DbSet<VehicleOdometerHistory> VehicleOdometerHistories => Set<VehicleOdometerHistory>();
        public DbSet<Bay> Bays => Set<Bay>();
        public DbSet<BayMaintenanceSchedule> BayMaintenanceSchedules => Set<BayMaintenanceSchedule>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
        public DbSet<QueueStatusHistory> QueueStatusHistories => Set<QueueStatusHistory>();
        public DbSet<Inspection> Inspections => Set<Inspection>();
        public DbSet<InspectionMedia> InspectionMedia => Set<InspectionMedia>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<PartCategory> PartCategories => Set<PartCategory>();
        public DbSet<Part> Parts => Set<Part>();
        public DbSet<PartTransaction> PartTransactions => Set<PartTransaction>();
        public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<Estimate> Estimates => Set<Estimate>();
        public DbSet<EstimateRevision> EstimateRevisions => Set<EstimateRevision>();
        public DbSet<EstimateRevisionLineItem> EstimateRevisionLineItems => Set<EstimateRevisionLineItem>();
        public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
        public DbSet<WorkOrderAssignment> WorkOrderAssignments => Set<WorkOrderAssignment>();
        public DbSet<ServiceEntry> ServiceEntries => Set<ServiceEntry>();
        public DbSet<ServiceTimeSegment> ServiceTimeSegments => Set<ServiceTimeSegment>();
        public DbSet<ToolCategory> ToolCategories => Set<ToolCategory>();
        public DbSet<Tool> Tools => Set<Tool>();
        public DbSet<ToolCheckout> ToolCheckouts => Set<ToolCheckout>();
        public DbSet<ToolMaintenance> ToolMaintenances => Set<ToolMaintenance>();
        public DbSet<QualityCheck> QualityChecks => Set<QualityCheck>();
        public DbSet<TaxRate> TaxRates => Set<TaxRate>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
        public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<InvoiceNumberCounter> InvoiceNumberCounters => Set<InvoiceNumberCounter>();
        public DbSet<ProductListing> ProductListings => Set<ProductListing>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(user => user.Id);
                entity.HasIndex(user => user.Email).IsUnique();

                entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
                entity.Property(user => user.UserName).HasMaxLength(120).IsRequired();
                entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
                entity.Property(user => user.Role).HasMaxLength(50).IsRequired();
                entity.Property(user => user.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<AppRole>(entity =>
            {
                entity.ToTable("AppRole");
                entity.HasKey(role => role.AppRoleId);
                entity.HasIndex(role => role.RoleKey).IsUnique();
                entity.Property(role => role.RoleKey).HasMaxLength(80).IsRequired();
                entity.Property(role => role.RoleName).HasMaxLength(120).IsRequired();
                entity.Property(role => role.Description).HasMaxLength(500);
                entity.Property(role => role.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<EmployeeProfile>(entity =>
            {
                entity.ToTable("EmployeeProfile");
                entity.Property(profile => profile.EmploymentType).HasMaxLength(30);
                entity.Property(profile => profile.SkillsAndQualifications).HasMaxLength(2000);
            });
            modelBuilder.Entity<EmployeeRateHistory>().ToTable("EmployeeRateHistory");
            modelBuilder.Entity<EmployeeAvailability>().ToTable("EmployeeAvailability");
            modelBuilder.Entity<EmployeeAvailabilityException>().ToTable("EmployeeAvailabilityException");
            modelBuilder.Entity<Permission>().ToTable("Permission");
            modelBuilder.Entity<UserPermissionOverride>().ToTable("UserPermissionOverride")
                .HasIndex(x=>new {x.UserId,x.PermissionId}).IsUnique();
            modelBuilder.Entity<UserPermissionOverride>().HasOne<AppUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserPermissionOverride>().HasOne<Permission>().WithMany().HasForeignKey(x=>x.PermissionId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RolePermission>().ToTable("RolePermission");
            modelBuilder.Entity<AuditLogEntry>().ToTable("AuditLogEntry");
            modelBuilder.Entity<RecordsDispositionPolicy>(entity =>
            {
                entity.ToTable("RecordsDispositionPolicy");
                entity.Property(policy => policy.RetentionUnit).HasMaxLength(20).IsRequired();
            });
            modelBuilder.Entity<Customer>().ToTable("Customer");
            modelBuilder.Entity<Vehicle>().ToTable("Vehicle");
            modelBuilder.Entity<VehicleOwnership>().ToTable("VehicleOwnership");
            modelBuilder.Entity<VehicleOdometerHistory>().ToTable("VehicleOdometerHistory");
            modelBuilder.Entity<Bay>().ToTable("Bay");
            modelBuilder.Entity<BayMaintenanceSchedule>().ToTable("BayMaintenanceSchedule");
            modelBuilder.Entity<Appointment>().ToTable("Appointment");
            modelBuilder.Entity<AppointmentService>().ToTable("AppointmentService");
            modelBuilder.Entity<QueueStatusHistory>().ToTable("QueueStatusHistory");
            modelBuilder.Entity<Inspection>().ToTable("Inspection");
            modelBuilder.Entity<InspectionMedia>().ToTable("InspectionMedia");
            modelBuilder.Entity<Supplier>().ToTable("Supplier");
            modelBuilder.Entity<PartCategory>().ToTable("PartCategory");
            modelBuilder.Entity<Part>().ToTable("Part");
            modelBuilder.Entity<PartTransaction>().ToTable("PartTransaction");
            modelBuilder.Entity<ServiceCategory>().ToTable("ServiceCategory");
            modelBuilder.Entity<Service>(entity =>
            {
                entity.ToTable("Service");
                entity.Property(service => service.DefaultEstimatedHours).HasPrecision(8, 2);
            });
            modelBuilder.Entity<Estimate>().ToTable("Estimate");
            modelBuilder.Entity<EstimateRevision>().ToTable("EstimateRevision");
            modelBuilder.Entity<EstimateRevisionLineItem>().ToTable("EstimateRevisionLineItem");
            modelBuilder.Entity<WorkOrder>().ToTable("WorkOrder");
            modelBuilder.Entity<WorkOrderAssignment>().ToTable("WorkOrderAssignment");
            modelBuilder.Entity<ServiceEntry>(entity =>
            {
                entity.ToTable("ServiceEntry");
                entity.Property(entry => entry.EstimatedHours).HasPrecision(8, 2);
            });
            modelBuilder.Entity<ServiceTimeSegment>().ToTable("ServiceTimeSegment");
            modelBuilder.Entity<ToolCategory>().ToTable("ToolCategory");
            modelBuilder.Entity<Tool>().ToTable("Tool");
            modelBuilder.Entity<ToolCheckout>().ToTable("ToolCheckout");
            modelBuilder.Entity<ToolMaintenance>().ToTable("ToolMaintenance");
            modelBuilder.Entity<QualityCheck>().ToTable("QualityCheck");
            modelBuilder.Entity<TaxRate>(entity =>
            {
                entity.ToTable("TaxRate");
                entity.Property(taxRate => taxRate.Rate).HasPrecision(6, 4);
            });
            modelBuilder.Entity<Invoice>().ToTable("Invoice");
            modelBuilder.Entity<InvoiceLineItem>().ToTable("InvoiceLineItem");
            modelBuilder.Entity<InvoiceAdjustment>().ToTable("InvoiceAdjustment");
            modelBuilder.Entity<Payment>().ToTable("Payment");
            modelBuilder.Entity<InvoiceNumberCounter>().ToTable("InvoiceNumberCounter");
            modelBuilder.Entity<ProductListing>().ToTable("ProductListing");
        }
    }
}

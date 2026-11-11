using System.ComponentModel.DataAnnotations;

namespace Backyard_Mech.Models;

public sealed class MasterDataForm
{
    public int Id { get; set; }
    [StringLength(100)] public string? Expected { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    [RegularExpression("services|parts|tools")] public string? Kind { get; set; }
    public int? ParentId { get; set; }
    public int? CategoryId { get; set; }
    [Range(typeof(decimal), "0", "99999999.99")] public decimal Price { get; set; }
    [Range(typeof(decimal), "0", "9999.99")] public decimal Hours { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal Percent { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    [StringLength(120)] public string? ContactFirstName { get; set; }
    [StringLength(120)] public string? ContactMiddleName { get; set; }
    [StringLength(120)] public string? ContactLastName { get; set; }
    [Phone, StringLength(40)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(250)] public string? StreetAddress { get; set; }
    [StringLength(120)] public string? City { get; set; }
    [StringLength(120)] public string? Province { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    [StringLength(80)] public string? AccountNumber { get; set; }
}

public sealed class MasterStateForm
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required] public string Expected { get; set; } = "";
    [RegularExpression("services|parts|tools")] public string? Kind { get; set; }
    public bool Active { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}

public sealed record MasterOption(int Id, string Name);
public sealed record MasterRow(MasterDataForm Form, bool Active, string Status, string[] Cells);
public sealed class MasterDataPage
{
    public string Mode { get; set; } = "";
    public string? Kind { get; set; }
    public string Title { get; set; } = "";
    public string Module { get; set; } = "Settings";
    public string CreateAction { get; set; } = "";
    public string EditAction { get; set; } = "";
    public string StateAction { get; set; } = "";
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<MasterRow> Rows { get; set; } = new();
    public List<MasterOption> Options { get; set; } = new();
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
}
public sealed record MasterEditor(MasterDataPage Page, MasterDataForm Form, bool ReadOnly = false);

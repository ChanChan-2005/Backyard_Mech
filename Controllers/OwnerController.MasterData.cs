using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backyard_Mech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backyard_Mech.Controllers;

public partial class OwnerController
{
    private async Task ValidatePartReferences(PartFormModel form, Part? existing)
    {
        Require(form.SupplierId.HasValue, "Select a supplier for this part.");
        Require(await _dbContext.Suppliers.AnyAsync(x=>x.SupplierId==form.SupplierId && x.IsActive),"Choose an active supplier.");
        if(form.PartCategoryId.HasValue && form.PartCategoryId != existing?.PartCategoryId)
            Require(await _dbContext.PartCategories.AnyAsync(x=>x.PartCategoryId==form.PartCategoryId && x.IsActive),"Choose an active part category.");
    }
    private async Task ValidateToolCategory(int? categoryId, int? existing)
    {
        if(categoryId.HasValue && categoryId!=existing)
            Require(await _dbContext.ToolCategories.AnyAsync(x=>x.ToolCategoryId==categoryId && x.IsActive),"Choose an active tool category.");
    }
    private static string MasterStamp(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void CheckMaster(object row, string? expected) => Require(MasterStamp(row) == expected, "This record changed. Refresh before saving.");
    private async Task<IActionResult> MasterPage(MasterDataPage page)
    {
        var role = HttpContext.Session.GetString("UserRole");
        page.CanCreate = await _permissionService.HasPermissionAsync(role, page.Module, "Create");
        page.CanEdit = await _permissionService.HasPermissionAsync(role, page.Module, "Edit");
        page.CanDelete = !string.IsNullOrEmpty(page.DeleteAction) &&
            await _permissionService.HasPermissionAsync(role, page.Module, "Delete");
        return View("~/Views/Owner/Settings/MasterData.cshtml", page);
    }

    public async Task<IActionResult> Suppliers()
    {
        var rows = await _dbContext.Suppliers.AsNoTracking().OrderBy(x => x.SupplierName).ToListAsync();
        return await MasterPage(new MasterDataPage { Mode = "suppliers", Title = "Suppliers", Module = "Suppliers",
            CreateAction = nameof(CreateSupplier), EditAction = nameof(UpdateSupplier), StateAction = nameof(SetSupplierState),
            Headers = new[] { "Supplier", "Contact", "Phone", "Email", "Location" }, Rows = rows.Select(x => new MasterRow(
                new MasterDataForm { Id=x.SupplierId, Expected=MasterStamp(x), Name=x.SupplierName ?? "", ContactFirstName=x.ContactFirstName,
                    ContactMiddleName=x.ContactMiddleName, ContactLastName=x.ContactLastName, Phone=x.Phone, Email=x.Email,
                    UnitNumber=x.UnitNumber, StreetNumber=x.StreetNumber, StreetName=x.StreetName, StreetType=x.StreetType,
                    StreetAddress=x.StreetAddress, City=x.City, Province=x.Province, PostalCode=x.PostalCode, AccountNumber=x.AccountNumber },
                x.IsActive, x.IsActive ? "Active" : "Inactive", new[] { x.SupplierName ?? "", $"{x.ContactFirstName} {x.ContactLastName}".Trim(), x.Phone ?? "", x.Email ?? "", x.City ?? "" })).ToList() });
    }

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CreateSupplier(MasterDataForm form) { form.Id=0; return SaveSupplier(form); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> UpdateSupplier(MasterDataForm form) { if(form.Id<=0) ModelState.AddModelError("Id","Select a supplier."); return SaveSupplier(form); }
    private Task<IActionResult> SaveSupplier(MasterDataForm f) => SaveWorkflow(async () => {
        var name=f.Name.Trim();
        Require(!await _dbContext.Suppliers.AnyAsync(x => x.SupplierId != f.Id && x.SupplierName == name), "A supplier with this name already exists, including inactive records.");
        var row=f.Id==0 ? new Supplier() : await _dbContext.Suppliers.FindAsync(f.Id);
        Require(row is not null, "Supplier not found.");
        if(f.Id!=0) { CheckMaster(row!,f.Expected); WorkflowAudit("Supplier",f.Id,"Before edit",row); }
        await ValidateSupplierAddressAsync(f, row!);
        var hasStreetParts = new[] { f.UnitNumber, f.StreetNumber, f.StreetName, f.StreetType }.Any(value => !string.IsNullOrWhiteSpace(value));
        var hadStreetParts = !string.IsNullOrWhiteSpace(row!.StreetName);
        row.UnitNumber = Clean(f.UnitNumber); row.StreetNumber = Clean(f.StreetNumber);
        row.StreetName = Clean(f.StreetName); row.StreetType = Clean(f.StreetType);
        if (hasStreetParts) {
            var street = string.Join(" ", new[] { Clean(f.StreetNumber), Clean(f.StreetName), Clean(f.StreetType) }.Where(value => value is not null));
            row.StreetAddress = string.IsNullOrWhiteSpace(f.UnitNumber) ? street : $"Unit {f.UnitNumber.Trim()}, {street}";
        }
        else if (f.Id == 0 || hadStreetParts || !string.IsNullOrWhiteSpace(f.StreetAddress)) row.StreetAddress = Clean(f.StreetAddress);
        row!.SupplierName=name; row.ContactFirstName=Clean(f.ContactFirstName); row.ContactMiddleName=Clean(f.ContactMiddleName); row.ContactLastName=Clean(f.ContactLastName);
        row.Phone=Clean(f.Phone); row.Email=Clean(f.Email); row.City=Clean(f.City); row.Province=Clean(f.Province); row.PostalCode=Clean(f.PostalCode); row.AccountNumber=Clean(f.AccountNumber);
        if(f.Id==0) _dbContext.Suppliers.Add(row);
        await _dbContext.SaveChangesAsync(); WorkflowAudit("Supplier",row.SupplierId,f.Id==0 ? "Create" : "Edit",row);
        return RedirectToAction(nameof(Suppliers));
    },nameof(Suppliers));

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SetSupplierState(MasterStateForm f) => SaveWorkflow(async () => {
        var row=await _dbContext.Suppliers.FindAsync(f.Id); Require(row is not null,"Supplier not found."); CheckMaster(row!,f.Expected);
        row!.IsActive=f.Active; WorkflowAudit("Supplier",f.Id,f.Active ? "Reactivate" : "Deactivate",f.Reason);
        return RedirectToAction(nameof(Suppliers));
    },nameof(Suppliers));

    private sealed record CategoryRow(int Id, int? Parent, string Name, string? Description, bool Active, object Entity);
    private async Task<List<CategoryRow>> CategoryRows(string kind) => kind switch {
        "services" => (await _dbContext.ServiceCategories.ToListAsync()).Select(x=>new CategoryRow(x.ServiceCategoryId,x.ParentServiceCategoryId,x.CategoryName??"",x.Description,x.IsActive,x)).ToList(),
        "parts" => (await _dbContext.PartCategories.ToListAsync()).Select(x=>new CategoryRow(x.PartCategoryId,x.ParentPartCategoryId,x.CategoryName??"",x.Description,x.IsActive,x)).ToList(),
        "tools" => (await _dbContext.ToolCategories.ToListAsync()).Select(x=>new CategoryRow(x.ToolCategoryId,x.ParentToolCategoryId,x.CategoryName??"",x.Description,x.IsActive,x)).ToList(),
        _ => throw new WorkflowValidationException("Choose service, part or tool categories.")
    };
    private static bool ValidKind(string? kind) => kind is "services" or "parts" or "tools";

    public async Task<IActionResult> SettingsCategories(string kind = "services")
    {
        if(!ValidKind(kind)) return BadRequest();
        var rows=await CategoryRows(kind);
        return await MasterPage(new MasterDataPage { Mode="categories",Kind=kind,Title=$"{(kind=="services"?"Service":kind=="parts"?"Part":"Tool")} Categories",
            CreateAction=nameof(CreateCategory),EditAction=nameof(UpdateCategory),StateAction=nameof(SetCategoryState),
            Headers=new[]{"Category","Parent Category","Description"}, Options=rows.Where(x=>x.Active).OrderBy(x=>x.Name).Select(x=>new MasterOption(x.Id,x.Name)).ToList(),
            Rows=rows.OrderBy(x=>x.Name).Select(x=>new MasterRow(new MasterDataForm { Id=x.Id,Expected=MasterStamp(x.Entity),Name=x.Name,ParentId=x.Parent,Description=x.Description,Kind=kind },x.Active,x.Active?"Active":"Inactive",
                new[]{x.Name,rows.FirstOrDefault(p=>p.Id==x.Parent)?.Name??"Parent category",x.Description??""})).ToList() });
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CreateCategory(MasterDataForm f) { f.Id=0; return SaveCategory(f); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> UpdateCategory(MasterDataForm f) { if(f.Id<=0) ModelState.AddModelError("Id","Select a category."); return SaveCategory(f); }
    private Task<IActionResult> SaveCategory(MasterDataForm f) => SaveWorkflow(async () => {
        Require(ValidKind(f.Kind),"Select a category type.");
        var codes = await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
        Require(Backyard_Mech.Services.SubTabPermissionCatalog.CanAccess(codes, "SettingsCategories", f.Kind!), "You cannot edit this category tab.");
        Require(f.Name.Trim().Length<=120,"Category name must be at most 120 characters.");
        Require((f.Description?.Length ?? 0)<=500,"Category description must be at most 500 characters.");
        var rows=await CategoryRows(f.Kind!); var original=rows.FirstOrDefault(x=>x.Id==f.Id);
        if(f.Id!=0) { Require(original is not null,"Category not found."); CheckMaster(original!.Entity,f.Expected); }
        Require(!rows.Any(x=>x.Id!=f.Id && x.Parent==f.ParentId && x.Name.Equals(f.Name.Trim(),StringComparison.OrdinalIgnoreCase)),"This category already exists under the selected parent.");
        var visited=new HashSet<int>(); if(f.Id>0) visited.Add(f.Id);
        for(var parent=f.ParentId;parent.HasValue;)
        {
            Require(visited.Add(parent.Value),"A category cannot be its own parent or a descendant of itself.");
            var ancestor=rows.SingleOrDefault(x=>x.Id==parent); Require(ancestor is not null && ancestor.Active,"Select an active parent category."); parent=ancestor!.Parent;
        }
        if(original is not null) WorkflowAudit("Category",f.Id,"Before edit",new {f.Kind,Original=original.Entity});
        object entity;
        if(f.Kind=="services") { var x=(ServiceCategory?)original?.Entity??new(); x.CategoryName=f.Name.Trim(); x.Description=Clean(f.Description); x.ParentServiceCategoryId=f.ParentId; if(f.Id==0)_dbContext.ServiceCategories.Add(x); entity=x; }
        else if(f.Kind=="parts") { var x=(PartCategory?)original?.Entity??new(); x.CategoryName=f.Name.Trim(); x.Description=Clean(f.Description); x.ParentPartCategoryId=f.ParentId; if(f.Id==0)_dbContext.PartCategories.Add(x); entity=x; }
        else { var x=(ToolCategory?)original?.Entity??new(); x.CategoryName=f.Name.Trim(); x.Description=Clean(f.Description); x.ParentToolCategoryId=f.ParentId; if(f.Id==0)_dbContext.ToolCategories.Add(x); entity=x; }
        await _dbContext.SaveChangesAsync();
        var id=entity switch {ServiceCategory x=>x.ServiceCategoryId,PartCategory x=>x.PartCategoryId,ToolCategory x=>x.ToolCategoryId,_=>0};
        WorkflowAudit("Category",id,f.Id==0?"Create":"Edit",new{f.Kind,Record=entity});
        return RedirectToAction(nameof(SettingsCategories),new{kind=f.Kind});
    },nameof(SettingsCategories),new{kind=ValidKind(f.Kind)?f.Kind:"services"});

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SetCategoryState(MasterStateForm f) => SaveWorkflow(async () => {
        Require(ValidKind(f.Kind),"Select a category type.");
        var codes = await _permissionService.GetPermissionCodesAsync(HttpContext.Session.GetString("UserRole"));
        Require(Backyard_Mech.Services.SubTabPermissionCatalog.CanAccess(codes, "SettingsCategories", f.Kind!), "You cannot edit this category tab.");
        var rows=await CategoryRows(f.Kind!); var row=rows.SingleOrDefault(x=>x.Id==f.Id);
        Require(row is not null,"Category not found."); CheckMaster(row!.Entity,f.Expected);
        if(!f.Active) Require(!rows.Any(x=>x.Parent==f.Id && x.Active),"Deactivate or move the active subcategories first.");
        if(f.Active && row.Parent.HasValue) Require(rows.Any(x=>x.Id==row.Parent && x.Active),"Reactivate the parent category first.");
        switch(row.Entity) { case ServiceCategory x:x.IsActive=f.Active;break; case PartCategory x:x.IsActive=f.Active;break; case ToolCategory x:x.IsActive=f.Active;break; }
        WorkflowAudit("Category",f.Id,f.Active?"Reactivate":"Deactivate",new{f.Kind,f.Reason});
        return RedirectToAction(nameof(SettingsCategories),new{kind=f.Kind});
    },nameof(SettingsCategories),new{kind=ValidKind(f.Kind)?f.Kind:"services"});

    public async Task<IActionResult> ServiceCatalog()
    {
        var categories=await _dbContext.ServiceCategories.AsNoTracking().ToListAsync();
        var rows=await _dbContext.Services.AsNoTracking().Where(x=>!x.IsDeleted).OrderBy(x=>x.ServiceName).ToListAsync();
        return await MasterPage(new MasterDataPage {Mode="services",Title="Service Catalog",Module="Services",CreateAction=nameof(CreateService),EditAction=nameof(UpdateService),StateAction=nameof(SetServiceState),DeleteAction=nameof(DeleteService),
            Headers=new[]{"Service","Category","Default Price","Estimated Hours"},Options=categories.Where(x=>x.IsActive).Select(x=>new MasterOption(x.ServiceCategoryId,x.CategoryName??"")).ToList(),
            Rows=rows.Select(x=>new MasterRow(new MasterDataForm {Id=x.ServiceId,Expected=MasterStamp(x),Name=x.ServiceName??"",Description=x.Description,CategoryId=x.ServiceCategoryId,Price=x.DefaultPrice??0,Hours=x.DefaultEstimatedHours??0},x.IsActive,x.IsActive?"Active":"Inactive",
                new[]{x.ServiceName??"",categories.FirstOrDefault(c=>c.ServiceCategoryId==x.ServiceCategoryId)?.CategoryName??"Uncategorized",CurrencyDisplay.Cad(x.DefaultPrice),(x.DefaultEstimatedHours??0).ToString("0.##")})).ToList()});
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CreateService(MasterDataForm f) { f.Id=0;return SaveService(f); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> UpdateService(MasterDataForm f) { if(f.Id<=0)ModelState.AddModelError("Id","Select a service.");return SaveService(f); }
    private Task<IActionResult> SaveService(MasterDataForm f) => SaveWorkflow(async () => {
        Require(f.Price==Math.Round(f.Price,2) && f.Hours==Math.Round(f.Hours,2),"Price and hours must have at most two decimal places.");
        Require(!await _dbContext.Services.AnyAsync(x=>x.ServiceId!=f.Id && !x.IsDeleted && x.ServiceName==f.Name.Trim()),"A service with this name already exists.");
        var row=f.Id==0?new Service():await _dbContext.Services.FindAsync(f.Id);Require(row is not null,"Service not found.");
        Require(!row!.IsDeleted,"This service was deleted. Refresh the catalog.");
        if(f.Id!=0) {CheckMaster(row!,f.Expected);WorkflowAudit("Service",f.Id,"Before edit",row);}
        if(f.CategoryId.HasValue && (f.Id==0 || row!.ServiceCategoryId!=f.CategoryId))
            Require(await _dbContext.ServiceCategories.AnyAsync(x=>x.ServiceCategoryId==f.CategoryId && x.IsActive),"Choose an active service category.");
        row!.ServiceName=f.Name.Trim();row.ServiceCategoryId=f.CategoryId;row.Description=Clean(f.Description);row.DefaultPrice=f.Price;row.DefaultEstimatedHours=f.Hours;
        if(f.Id==0)_dbContext.Services.Add(row); await _dbContext.SaveChangesAsync();WorkflowAudit("Service",row.ServiceId,f.Id==0?"Create":"Edit",row);
        return RedirectToAction(nameof(ServiceCatalog));
    },nameof(ServiceCatalog));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SetServiceState(MasterStateForm f) => SaveWorkflow(async () => {
        var row=await _dbContext.Services.FindAsync(f.Id);Require(row is not null && !row.IsDeleted,"Service not found.");CheckMaster(row!,f.Expected);
        row!.IsActive=f.Active;WorkflowAudit("Service",f.Id,f.Active?"Reactivate":"Deactivate",f.Reason);return RedirectToAction(nameof(ServiceCatalog));
    },nameof(ServiceCatalog));

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> DeleteService(MasterStateForm f) => SaveWorkflow(async () => {
        var row=await _dbContext.Services.FindAsync(f.Id);
        Require(row is not null && !row.IsDeleted,"Service not found.");
        CheckMaster(row!,f.Expected);
        row!.IsActive=false;
        row.IsDeleted=true;
        row.DeletedUtc=DateTime.UtcNow;
        var listings=await _dbContext.ProductListings
            .Where(x=>x.ServiceId==f.Id && x.IsPublished && !x.IsDeleted).ToListAsync();
        foreach(var listing in listings) listing.IsPublished=false;
        WorkflowAudit("Service",f.Id,"Delete",new {row.ServiceName,f.Reason,UnpublishedListings=listings.Count});
        return RedirectToAction(nameof(ServiceCatalog));
    },nameof(ServiceCatalog));

    private async Task<IActionResult> TaxMasterPage()
    {
        var now=DateTime.UtcNow;var rows=await _dbContext.TaxRates.AsNoTracking().OrderByDescending(x=>x.EffectiveFrom).ToListAsync();
        return await MasterPage(new MasterDataPage {Mode="tax",Title="Tax Rates",EditAction=nameof(UpdateTax),
            Headers=new[]{"Tax Name","Rate","Effectivity Date"},Rows=rows.Select(x=>new MasterRow(new MasterDataForm {Id=x.TaxRateId,Expected=MasterStamp(x),Name=x.Description??"",Percent=(x.Rate??0)*100,Start=x.EffectiveFrom,End=x.EffectiveTo},
                !x.EffectiveTo.HasValue||x.EffectiveTo>now,x.EffectiveTo<=now?"Inactive":x.EffectiveFrom>now?"Scheduled":"Active",
                new[]{x.Description??"",((x.Rate??0)*100).ToString("0.##")+"%",x.EffectiveFrom?.ToString("MMM dd, yyyy")??"Not set"})).ToList()});
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> UpdateTax(MasterDataForm f) {if(f.Id<=0)ModelState.AddModelError("Id","Select a tax rate.");return SaveTax(f);}
    private Task<IActionResult> SaveTax(MasterDataForm f) => SaveWorkflow(async () => {
        Require(f.Id>0,"The tax rate must be edited from the existing HST record.");
        Require(f.Start.HasValue,"Enter the effectivity date."); Require(f.Percent==Math.Round(f.Percent,2),"Use at most two decimal places for the percentage.");
        var row=await _dbContext.TaxRates.FindAsync(f.Id);Require(row is not null,"Tax rate not found.");CheckMaster(row!,f.Expected);
        var before=new {row!.Description,row.Rate,row.EffectiveFrom,row.EffectiveTo};
        row.Description="Harmonized Sales Tax";row.Rate=f.Percent/100m;row.EffectiveFrom=f.Start!.Value.Date;
        await _dbContext.SaveChangesAsync();WorkflowAudit("TaxRate",row.TaxRateId,"Edit",new {Before=before,After=row});return RedirectToAction(nameof(SettingsTax));
    },nameof(SettingsTax));
}

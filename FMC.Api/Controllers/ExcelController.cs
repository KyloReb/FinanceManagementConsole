using FMC.Application.Interfaces;
using FMC.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FMC.Api.Controllers;

[ApiController]
[Route("api/excel")]
[Authorize(Roles = "Maker,SuperAdmin")]
public class ExcelController : ControllerBase
{
    private readonly IExcelParserService _parser;
    private readonly IApplicationDbContext _context;

    public ExcelController(IExcelParserService parser, IApplicationDbContext context)
    {
        _parser = parser;
        _context = context;
    }

    [HttpPost("parse-cardholders/{organizationId:guid}")]
    public async Task<IActionResult> ParseCardholders(Guid organizationId, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        using var stream = file.OpenReadStream();
        var rows = await _parser.ParseCardholdersAsync(stream, organizationId, ct);
        
        return Ok(rows);
    }

    [HttpPost("validate-rows/{organizationId:guid}")]
    public async Task<IActionResult> ValidateRows(Guid organizationId, [FromBody] List<BulkTransactionRowDto> rows, CancellationToken ct)
    {
        if (rows == null) return BadRequest("No rows provided.");
        var updatedRows = await _parser.ValidateRowsAsync(rows, organizationId, ct);
        return Ok(updatedRows);
    }

    [HttpGet("download-template")]
    [AllowAnonymous] // Allow downloading template without full auth for convenience
    public IActionResult DownloadTemplate()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Template");
        
        // Headers
        worksheet.Cell(1, 1).Value = "Subscriber";
        worksheet.Cell(1, 2).Value = "CardNumber";
        worksheet.Cell(1, 3).Value = "Amount";
        
        // Style Headers
        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#4F46E5");
        headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

        // Force CardNumber column to be Text format
        worksheet.Column(2).Style.NumberFormat.Format = "@";

        // Sample Row
        worksheet.Cell(2, 1).Value = "John Doe";
        worksheet.Cell(2, 2).Value = "6364100000000001";
        worksheet.Cell(2, 3).Value = 1500.00;

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FMC_Bulk_Upload_Template.xlsx");
    }

    [HttpPost("fix-ids")]
    public async Task<IActionResult> FixIds(CancellationToken ct)
    {
        var cardholders = await _context.Cardholders.IgnoreQueryFilters().ToListAsync(ct);
        int updated = 0;
        
        foreach (var c in cardholders)
        {
            if (string.IsNullOrEmpty(c.IdentityUserId)) continue;

            // Find account that is still linked to the old IdentityUserId
            var legacyAccount = await _context.Accounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == c.IdentityUserId, ct);

            if (legacyAccount != null)
            {
                // Update Account to use the new Cardholder ID
                legacyAccount.TenantId = c.Id.ToString();
                
                // Update all associated transactions too
                var transactions = await _context.Transactions.IgnoreQueryFilters()
                    .Where(t => t.TenantId == c.IdentityUserId)
                    .ToListAsync(ct);
                
                foreach(var t in transactions)
                {
                    t.TenantId = c.Id.ToString();
                }
                
                updated++;
            }
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { Message = $"Aligned {updated} legacy accounts with new Cardholder IDs.", TotalCardholders = cardholders.Count });
    }
}

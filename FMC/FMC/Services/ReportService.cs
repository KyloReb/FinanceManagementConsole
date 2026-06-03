using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FMC.Shared.DTOs;
using FMC.Shared.Utils;

namespace FMC.Services;

/// <summary>
/// Defines the interface for generating financial reports in multiple formats.
/// </summary>
public interface IReportService
{
    byte[] GenerateExcel(string title, IEnumerable<TransactionDto> transactions, string? filterApplied = null);
    byte[] GeneratePdf(string title, IEnumerable<TransactionDto> transactions, string? filterApplied = null);
    byte[] GenerateAuditExcel(string title, IEnumerable<FMC.Shared.DTOs.Admin.AuditLogDto> logs, string? filterApplied = null);
    byte[] GenerateAuditPdf(string title, IEnumerable<FMC.Shared.DTOs.Admin.AuditLogDto> logs, string? filterApplied = null);
    byte[] GenerateCardholderExcel(string orgName, IEnumerable<FMC.Shared.DTOs.User.UserDto> cardholders, string? filterApplied = null);
    byte[] GenerateCardholderPdf(string orgName, IEnumerable<FMC.Shared.DTOs.User.UserDto> cardholders, string? filterApplied = null);
    byte[] GenerateOrgHealthExcel(string title, IEnumerable<FMC.Shared.DTOs.Organization.OrganizationDto> orgs, string? filterApplied = null);
    byte[] GenerateOrgHealthPdf(string title, IEnumerable<FMC.Shared.DTOs.Organization.OrganizationDto> orgs, string? filterApplied = null);
    byte[] GenerateCompensationRegisterExcel(string title, IEnumerable<CompensationRegisterRow> rows, string? filterApplied = null);
    byte[] GenerateCompensationRegisterPdf(string title, IEnumerable<CompensationRegisterRow> rows, string? filterApplied = null);
}

public class ReportService : IReportService
{
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateExcel(string title, IEnumerable<TransactionDto> transactions, string? filterApplied = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");
        
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Subscriber";
        worksheet.Cell(1, 3).Value = "Card Number";
        worksheet.Cell(1, 4).Value = "Category";
        worksheet.Cell(1, 5).Value = "Amount";
        worksheet.Cell(1, 6).Value = "Status";
        worksheet.Cell(1, 7).Value = "Label";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");

        int row = 2;
        foreach (var t in transactions)
        {
            worksheet.Cell(row, 1).Value = t.Date;
            worksheet.Cell(row, 2).Value = t.Subscriber;
            worksheet.Cell(row, 3).Value = FinanceUtils.MaskCard(t.AccountNumber ?? "");
            worksheet.Cell(row, 4).Value = t.Category;
            worksheet.Cell(row, 5).Value = t.Amount;
            worksheet.Cell(row, 6).Value = t.Status;
            worksheet.Cell(row, 7).Value = t.Label;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GeneratePdf(string title, IEnumerable<TransactionDto> transactions, string? filterApplied = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Column(innerCol =>
                    {
                        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "nlkLogo.png");
                        if (!System.IO.File.Exists(logoPath))
                            logoPath = @"c:\Users\Administrator\source\repos\FMC\FMC.Api\wwwroot\nlkLogo.png";

                        if (System.IO.File.Exists(logoPath))
                        {
                            innerCol.Item().AlignCenter().Width(180).Image(logoPath);
                        }

                        innerCol.Item().PaddingTop(5).AlignCenter().Text("U/GF Vernida I Condominium, 120 Amorsolo St., Legaspi Village, Makati City").FontSize(8).FontColor(Colors.Grey.Darken1);
                        innerCol.Item().AlignCenter().Text("Tel No. (02) 8713 6279 | Email: nl.admin@nationlink.ph").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(Colors.Blue.Darken4);
                });

                page.Content().PaddingBottom(1, Unit.Centimetre).PaddingTop(0.5f, Unit.Centimetre).Column(contentCol =>
                {
                    contentCol.Item().PaddingBottom(15).AlignCenter().Column(innerCol =>
                    {
                        innerCol.Item().AlignCenter().Text(title).FontSize(22).SemiBold().FontColor(Colors.Blue.Darken3);
                        if (!string.IsNullOrEmpty(filterApplied))
                        {
                            innerCol.Item().PaddingVertical(2).AlignCenter().Text($"Active Filters: {filterApplied}").FontSize(9).FontColor(Colors.Grey.Darken2).SemiBold();
                        }
                        innerCol.Item().AlignCenter().Text($"Generated: {DateTime.Now:MMMM dd, yyyy 'at' hh:mm:ss tt}").FontSize(10).FontColor(Colors.Grey.Medium);
                        innerCol.Item().AlignCenter().Text("Finance Management Console - Official Settlement Report").FontSize(9).FontColor(Colors.Grey.Medium).Italic();
                    });

                    contentCol.Item().Table(table =>
                    {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
                        header.Cell().Element(HeaderStyle).Text("Date / Time");
                        header.Cell().Element(HeaderStyle).Text("Cardholder");
                        header.Cell().Element(HeaderStyle).Text("Card Number");
                        header.Cell().Element(HeaderStyle).Text("Amount");
                        header.Cell().Element(HeaderStyle).Text("Status");

                        static IContainer HeaderStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White)).PaddingVertical(6).PaddingHorizontal(4).Background(Colors.Blue.Darken3);
                        }
                    });

                    bool isEven = false;
                    int index = 1;
                    foreach (var tx in transactions)
                    {
                        var bgColor = isEven ? Colors.Grey.Lighten4 : Colors.White;
                        
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(index.ToString("D2")).FontColor(Colors.Grey.Medium);
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Date.ToString("MM/dd/yyyy HH:mm:ss"));
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Subscriber);
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(FinanceUtils.MaskCard(tx.AccountNumber ?? ""));
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Amount.ToString("C2"));
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Status.ToUpper());

                        index++;
                        isEven = !isEven;

                        static IContainer CellStyle(IContainer container, string bg)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Background(bg).PaddingVertical(5).PaddingHorizontal(4);
                        }
                    }
                });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateAuditExcel(string title, IEnumerable<FMC.Shared.DTOs.Admin.AuditLogDto> logs, string? filterApplied = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Audit Logs");
        
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Action";
        worksheet.Cell(1, 3).Value = "Organization/Context";
        worksheet.Cell(1, 4).Value = "Performed By";
        worksheet.Cell(1, 5).Value = "Amount";
        worksheet.Cell(1, 6).Value = "Label";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");

        int row = 2;
        foreach (var l in logs)
        {
            worksheet.Cell(row, 1).Value = l.CreatedAt.ToLocalTime().ToString("g");
            worksheet.Cell(row, 2).Value = l.Action;
            worksheet.Cell(row, 3).Value = l.EntityName ?? l.Organization ?? "System";
            worksheet.Cell(row, 4).Value = l.PerformedBy ?? "System";
            worksheet.Cell(row, 5).Value = l.Amount;
            worksheet.Cell(row, 6).Value = l.Label ?? l.Details;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerateAuditPdf(string title, IEnumerable<FMC.Shared.DTOs.Admin.AuditLogDto> logs, string? filterApplied = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Column(innerCol =>
                    {
                        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "nlkLogo.png");
                        if (!System.IO.File.Exists(logoPath))
                            logoPath = @"c:\Users\Administrator\source\repos\FMC\FMC.Api\wwwroot\nlkLogo.png";

                        if (System.IO.File.Exists(logoPath))
                        {
                            innerCol.Item().AlignCenter().Width(180).Image(logoPath);
                        }

                        innerCol.Item().PaddingTop(5).AlignCenter().Text("U/GF Vernida I Condominium, 120 Amorsolo St., Legaspi Village, Makati City").FontSize(8).FontColor(Colors.Grey.Darken1);
                        innerCol.Item().AlignCenter().Text("Tel No. (02) 8713 6279 | Email: nl.admin@nationlink.ph").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(Colors.Blue.Darken4);
                });

                page.Content().PaddingBottom(1, Unit.Centimetre).PaddingTop(0.5f, Unit.Centimetre).Column(contentCol =>
                {
                    contentCol.Item().PaddingBottom(15).AlignCenter().Column(innerCol =>
                    {
                        innerCol.Item().AlignCenter().Text(title).FontSize(22).SemiBold().FontColor(Colors.Blue.Darken3);
                        if (!string.IsNullOrEmpty(filterApplied))
                        {
                            innerCol.Item().PaddingVertical(2).AlignCenter().Text($"Active Filters: {filterApplied}").FontSize(9).FontColor(Colors.Grey.Darken2).SemiBold();
                        }
                        innerCol.Item().AlignCenter().Text($"Generated: {DateTime.Now:MMMM dd, yyyy 'at' hh:mm:ss tt}").FontSize(10).FontColor(Colors.Grey.Medium);
                        innerCol.Item().AlignCenter().Text("Finance Management Console - Official Audit Report").FontSize(9).FontColor(Colors.Grey.Medium).Italic();
                    });

                    contentCol.Item().Table(table =>
                    {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
                        header.Cell().Element(HeaderStyle).Text("Date / Time");
                        header.Cell().Element(HeaderStyle).Text("Action");
                        header.Cell().Element(HeaderStyle).Text("Context");
                        header.Cell().Element(HeaderStyle).Text("Amount");
                        header.Cell().Element(HeaderStyle).Text("Label");

                        static IContainer HeaderStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White)).PaddingVertical(6).PaddingHorizontal(4).Background(Colors.Blue.Darken3);
                        }
                    });

                    bool isEven = false;
                    int index = 1;
                    foreach (var tx in logs)
                    {
                        var bgColor = isEven ? Colors.Grey.Lighten4 : Colors.White;
                        
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(index.ToString("D2")).FontColor(Colors.Grey.Medium);
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.CreatedAt.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss"));
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Action);
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.EntityName ?? tx.Organization ?? "System");
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Amount?.ToString("C2") ?? "-");
                        table.Cell().Element(c => CellStyle(c, bgColor)).Text(tx.Label ?? tx.Details);

                        index++;
                        isEven = !isEven;

                        static IContainer CellStyle(IContainer container, string bg)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Background(bg).PaddingVertical(5).PaddingHorizontal(4);
                        }
                    }
                });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateCardholderExcel(string orgName, IEnumerable<FMC.Shared.DTOs.User.UserDto> cardholders, string? filterApplied = null)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Cardholders");

        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Email";
        ws.Cell(1, 3).Value = "Account Number";
        ws.Cell(1, 4).Value = "Balance";
        ws.Cell(1, 5).Value = "Status";
        ws.Cell(1, 6).Value = "Registered";

        var hRow = ws.Row(1);
        hRow.Style.Font.Bold = true;
        hRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");

        int row = 2;
        foreach (var u in cardholders)
        {
            ws.Cell(row, 1).Value = u.DisplayName;
            ws.Cell(row, 2).Value = u.Email;
            ws.Cell(row, 3).Value = u.AccountNumber;
            ws.Cell(row, 4).Value = (double)u.Balance;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = u.IsActive ? "Active" : "Suspended";
            ws.Cell(row, 6).Value = u.CreatedAt.ToLocalTime().ToString("MM/dd/yyyy");
            row++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerateCardholderPdf(string orgName, IEnumerable<FMC.Shared.DTOs.User.UserDto> cardholders, string? filterApplied = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Column(innerCol =>
                    {
                        var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "nlkLogo.png");
                        if (!System.IO.File.Exists(logoPath))
                            logoPath = @"c:\Users\Administrator\source\repos\FMC\FMC.Api\wwwroot\nlkLogo.png";
                        if (System.IO.File.Exists(logoPath))
                            innerCol.Item().AlignCenter().Width(180).Image(logoPath);
                        innerCol.Item().PaddingTop(5).AlignCenter().Text("U/GF Vernida I Condominium, 120 Amorsolo St., Legaspi Village, Makati City").FontSize(8).FontColor(Colors.Grey.Darken1);
                        innerCol.Item().AlignCenter().Text("Tel No. (02) 8713 6279 | Email: nl.admin@nationlink.ph").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(10).BorderBottom(1.5f).BorderColor(Colors.Blue.Darken4);
                });

                page.Content().PaddingBottom(1, Unit.Centimetre).PaddingTop(0.5f, Unit.Centimetre).Column(contentCol =>
                {
                    contentCol.Item().PaddingBottom(15).AlignCenter().Column(innerCol =>
                    {
                        innerCol.Item().AlignCenter().Text($"Cardholder Ledger — {orgName}").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken3);
                        if (!string.IsNullOrEmpty(filterApplied))
                            innerCol.Item().PaddingVertical(2).AlignCenter().Text($"Filter: {filterApplied}").FontSize(9).FontColor(Colors.Grey.Darken2).SemiBold();
                        innerCol.Item().AlignCenter().Text($"Generated: {DateTime.Now:MMMM dd, yyyy 'at' hh:mm:ss tt}").FontSize(10).FontColor(Colors.Grey.Medium);
                        innerCol.Item().AlignCenter().Text("Finance Management Console — Official Subscriber Ledger Report").FontSize(9).FontColor(Colors.Grey.Medium).Italic();
                    });

                    contentCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(H).Text("#");
                            header.Cell().Element(H).Text("Name");
                            header.Cell().Element(H).Text("Email");
                            header.Cell().Element(H).Text("Account No.");
                            header.Cell().Element(H).Text("Balance");
                            header.Cell().Element(H).Text("Status");
                            header.Cell().Element(H).Text("Registered");
                            static IContainer H(IContainer c) => c.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White)).PaddingVertical(6).PaddingHorizontal(4).Background(Colors.Blue.Darken3);
                        });

                        bool isEven = false;
                        int idx = 1;
                        foreach (var u in cardholders)
                        {
                            var bg = isEven ? Colors.Grey.Lighten4 : Colors.White;
                            table.Cell().Element(c => C(c, bg)).Text(idx.ToString("D2")).FontColor(Colors.Grey.Medium);
                            table.Cell().Element(c => C(c, bg)).Text(u.DisplayName);
                            table.Cell().Element(c => C(c, bg)).Text(u.Email ?? "-");
                            table.Cell().Element(c => C(c, bg)).Text(u.AccountNumber);
                            table.Cell().Element(c => C(c, bg)).Text(u.Balance.ToString("C2"));
                            table.Cell().Element(c => C(c, bg)).Text(u.IsActive ? "Active" : "Suspended");
                            table.Cell().Element(c => C(c, bg)).Text(u.CreatedAt.ToLocalTime().ToString("MM/dd/yyyy"));
                            idx++;
                            isEven = !isEven;
                            static IContainer C(IContainer c, string bg) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Background(bg).PaddingVertical(5).PaddingHorizontal(4);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateOrgHealthExcel(string title, IEnumerable<FMC.Shared.DTOs.Organization.OrganizationDto> orgs, string? filterApplied = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Org Health");

        worksheet.Cell(1, 1).Value = "Organization";
        worksheet.Cell(1, 2).Value = "Wallet Limit";
        worksheet.Cell(1, 3).Value = "Balance";
        worksheet.Cell(1, 4).Value = "Usage";
        worksheet.Cell(1, 5).Value = "Remaining";
        worksheet.Cell(1, 6).Value = "Usage %";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");

        int row = 2;
        foreach (var o in orgs)
        {
            var usagePct = o.WalletLimit > 0 ? (o.Usage / o.WalletLimit) * 100m : 0;
            worksheet.Cell(row, 1).Value = o.Name;
            worksheet.Cell(row, 2).Value = o.WalletLimit;
            worksheet.Cell(row, 3).Value = o.TotalBalance;
            worksheet.Cell(row, 4).Value = o.Usage;
            worksheet.Cell(row, 5).Value = o.RemainingBalance;
            worksheet.Cell(row, 6).Value = $"{usagePct:N1}%";
            row++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerateOrgHealthPdf(string title, IEnumerable<FMC.Shared.DTOs.Organization.OrganizationDto> orgs, string? filterApplied = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(title).FontSize(18).SemiBold().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrEmpty(filterApplied))
                        col.Item().AlignCenter().Text($"Filters: {filterApplied}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(5).BorderBottom(1.5f).BorderColor(Colors.Blue.Darken4);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2);
                        c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        static IContainer H(IContainer c) => c.DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.White)).Background(Colors.Blue.Darken4).PaddingVertical(6).PaddingHorizontal(4);
                        h.Cell().Element(H).Text("Organization");
                        h.Cell().Element(H).Text("Wallet Limit");
                        h.Cell().Element(H).Text("Balance");
                        h.Cell().Element(H).Text("Usage");
                        h.Cell().Element(H).Text("Remaining");
                        h.Cell().Element(H).Text("Usage %");
                    });

                    int idx = 0;
                    foreach (var o in orgs)
                    {
                        var usagePct = o.WalletLimit > 0 ? (o.Usage / o.WalletLimit) * 100m : 0;
                        var bg = idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                        table.Cell().Element(c => C(c, bg)).Text(o.Name);
                        table.Cell().Element(c => C(c, bg)).Text(o.WalletLimit.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(o.TotalBalance.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(o.Usage.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(o.RemainingBalance.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text($"{usagePct:N1}%");
                        idx++;
                        static IContainer C(IContainer c, string bg) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Background(bg).PaddingVertical(5).PaddingHorizontal(4);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateCompensationRegisterExcel(string title, IEnumerable<CompensationRegisterRow> rows, string? filterApplied = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Compensation Register");

        worksheet.Cell(1, 1).Value = "Subscriber";
        worksheet.Cell(1, 2).Value = "Account Number";
        worksheet.Cell(1, 3).Value = "Total Credits";
        worksheet.Cell(1, 4).Value = "Total Debits";
        worksheet.Cell(1, 5).Value = "Net Amount";
        worksheet.Cell(1, 6).Value = "TX Count";
        worksheet.Cell(1, 7).Value = "Organization";

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8f9fa");

        int row = 2;
        foreach (var r in rows)
        {
            worksheet.Cell(row, 1).Value = r.Subscriber;
            worksheet.Cell(row, 2).Value = r.AccountNumber;
            worksheet.Cell(row, 3).Value = r.TotalCredits;
            worksheet.Cell(row, 4).Value = r.TotalDebits;
            worksheet.Cell(row, 5).Value = r.NetAmount;
            worksheet.Cell(row, 6).Value = r.TransactionCount;
            worksheet.Cell(row, 7).Value = r.OrganizationName;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GenerateCompensationRegisterPdf(string title, IEnumerable<CompensationRegisterRow> rows, string? filterApplied = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(title).FontSize(18).SemiBold().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrEmpty(filterApplied))
                        col.Item().AlignCenter().Text($"Filters: {filterApplied}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(5).BorderBottom(1.5f).BorderColor(Colors.Blue.Darken4);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2);
                        c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        static IContainer H(IContainer c) => c.DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.White)).Background(Colors.Blue.Darken4).PaddingVertical(6).PaddingHorizontal(4);
                        h.Cell().Element(H).Text("Subscriber");
                        h.Cell().Element(H).Text("Account #");
                        h.Cell().Element(H).Text("Credits");
                        h.Cell().Element(H).Text("Debits");
                        h.Cell().Element(H).Text("Net");
                        h.Cell().Element(H).Text("TX");
                        h.Cell().Element(H).Text("Organization");
                    });

                    int idx = 0;
                    foreach (var r in rows)
                    {
                        var bg = idx % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                        table.Cell().Element(c => C(c, bg)).Text(r.Subscriber);
                        table.Cell().Element(c => C(c, bg)).Text(r.AccountNumber);
                        table.Cell().Element(c => C(c, bg)).Text(r.TotalCredits.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(r.TotalDebits.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(r.NetAmount.ToString("N2"));
                        table.Cell().Element(c => C(c, bg)).Text(r.TransactionCount.ToString());
                        table.Cell().Element(c => C(c, bg)).Text(r.OrganizationName);
                        idx++;
                        static IContainer C(IContainer c, string bg) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Background(bg).PaddingVertical(5).PaddingHorizontal(4);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}

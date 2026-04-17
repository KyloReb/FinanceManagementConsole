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
    byte[] GenerateExcel(string title, IEnumerable<TransactionDto> transactions);
    byte[] GeneratePdf(string title, IEnumerable<TransactionDto> transactions);
}

public class ReportService : IReportService
{
    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateExcel(string title, IEnumerable<TransactionDto> transactions)
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

    public byte[] GeneratePdf(string title, IEnumerable<TransactionDto> transactions)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(title).FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                        col.Item().Text($"{DateTime.Now:MMMM dd, yyyy}").FontSize(10);
                    });
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Date");
                        header.Cell().Element(HeaderStyle).Text("Cardholder");
                        header.Cell().Element(HeaderStyle).Text("Card Number");
                        header.Cell().Element(HeaderStyle).Text("Amount");
                        header.Cell().Element(HeaderStyle).Text("Status");

                        static IContainer HeaderStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5);
                        }
                    });

                    foreach (var tx in transactions)
                    {
                        table.Cell().Element(CellStyle).Text(tx.Date.ToString("MM/dd HH:mm"));
                        table.Cell().Element(CellStyle).Text(tx.Subscriber);
                        table.Cell().Element(CellStyle).Text(FinanceUtils.MaskCard(tx.AccountNumber ?? ""));
                        table.Cell().Element(CellStyle).Text(tx.Amount.ToString("C"));
                        table.Cell().Element(CellStyle).Text(tx.Status);

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(5);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }
}

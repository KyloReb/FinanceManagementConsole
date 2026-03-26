using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FMC.Shared.DTOs;

namespace FMC.Services;

/// <summary>
/// Defines the interface for generating financial reports in multiple formats.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates an Excel workbook containing transaction details.
    /// </summary>
    /// <param name="transactions">The list of transactions to include in the report.</param>
    /// <returns>A byte array representing the generated .xlsx file.</returns>
    byte[] GenerateExcel(List<TransactionDto> transactions);

    /// <summary>
    /// Generates a PDF document containing a formatted transactions report.
    /// </summary>
    /// <param name="transactions">The list of transactions to include in the report.</param>
    /// <returns>A byte array representing the generated .pdf file.</returns>
    byte[] GeneratePdf(List<TransactionDto> transactions);
}

/// <summary>
/// Implementation of <see cref="IReportService"/> using ClosedXML for Excel and QuestPDF for PDF generation.
/// </summary>
public class ReportService : IReportService
{
    static ReportService()
    {
        // Set the community license for QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Creates a Microsoft Excel file with a worksheet containing all provided transactions.
    /// </summary>
    /// <param name="transactions">The transactions to export.</param>
    /// <returns>Excel file content as a binary array.</returns>
    public byte[] GenerateExcel(List<TransactionDto> transactions)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");
        
        // Add Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Label";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Amount";

        // Add Data Rows
        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            worksheet.Cell(i + 2, 1).Value = t.Date;
            worksheet.Cell(i + 2, 2).Value = t.Label;
            worksheet.Cell(i + 2, 3).Value = t.Category;
            worksheet.Cell(i + 2, 4).Value = t.Amount;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Creates a professionally styled PDF report with a table of transactions.
    /// </summary>
    /// <param name="transactions">The transactions to export.</param>
    /// <returns>PDF file content as a binary array.</returns>
    public byte[] GeneratePdf(List<TransactionDto> transactions)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Report Header
                page.Header().Text("Financial Transactions Report").SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                // Report Content Table
                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCellStyle).Text("Date");
                        header.Cell().Element(HeaderCellStyle).Text("Label");
                        header.Cell().Element(HeaderCellStyle).Text("Category");
                        header.Cell().Element(HeaderCellStyle).Text("Amount");

                        static IContainer HeaderCellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                        }
                    });

                    foreach (var t in transactions)
                    {
                        table.Cell().Element(DataCellStyle).Text(t.Date.ToShortDateString());
                        table.Cell().Element(DataCellStyle).Text(t.Label);
                        table.Cell().Element(DataCellStyle).Text(t.Category);
                        table.Cell().Element(DataCellStyle).Text(t.Amount.ToString("C"));

                        static IContainer DataCellStyle(IContainer container)
                        {
                            return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    }
                });

                // Report Footer with Page Numbers
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }
}

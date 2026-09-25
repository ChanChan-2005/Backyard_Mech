using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Backyard_Mech.Services
{
    public sealed class ReportExportService
    {
        public ReportFile CreateFile(ReportExportData report, string format)
        {
            return format.Trim().ToLowerInvariant() switch
            {
                "csv" => new ReportFile(CreateCsv(report), "text/csv; charset=utf-8", "csv"),
                "excel" or "xlsx" => new ReportFile(CreateExcel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                "pdf" => new ReportFile(CreatePdf(report), "application/pdf", "pdf"),
                _ => throw new ArgumentException("Unsupported report export format.", nameof(format))
            };
        }

        private static byte[] CreateCsv(ReportExportData report)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', report.Headers.Select(EscapeCsv)));
            foreach (var row in report.Rows)
            {
                csv.AppendLine(string.Join(',', row.Select(EscapeCsv)));
            }

            var content = Encoding.UTF8.GetBytes(csv.ToString());
            var preamble = Encoding.UTF8.GetPreamble();
            var output = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, output, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, output, preamble.Length, content.Length);
            return output;
        }

        private static string EscapeCsv(string? value)
        {
            var safeValue = value ?? string.Empty;
            if (safeValue.Length > 0 && "=+-@".Contains(safeValue[0]))
            {
                safeValue = "'" + safeValue;
            }

            return $"\"{safeValue.Replace("\"", "\"\"")}\"";
        }

        private static byte[] CreateExcel(ReportExportData report)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Report");

            sheet.Cell(1, 1).Value = report.Title;
            sheet.Range(1, 1, 1, Math.Max(1, report.Headers.Count)).Merge();
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;
            sheet.Cell(2, 1).Value = $"Period: {report.PeriodLabel}";
            sheet.Range(2, 1, 2, Math.Max(1, report.Headers.Count)).Merge();

            var headerRow = 4;
            for (var column = 0; column < report.Headers.Count; column++)
            {
                var cell = sheet.Cell(headerRow, column + 1);
                cell.Value = report.Headers[column];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
            }

            for (var rowIndex = 0; rowIndex < report.Rows.Count; rowIndex++)
            {
                var row = report.Rows[rowIndex];
                for (var column = 0; column < report.Headers.Count; column++)
                {
                    sheet.Cell(headerRow + rowIndex + 1, column + 1).Value = column < row.Count ? row[column] : string.Empty;
                }
            }

            var usedRange = sheet.RangeUsed();
            if (usedRange is not null)
            {
                usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                usedRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                usedRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#DDE3EA");
            }
            sheet.SheetView.FreezeRows(headerRow);
            sheet.Columns().AdjustToContents(8, 40);
            sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            sheet.PageSetup.FitToPages(1, 0);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static byte[] CreatePdf(ReportExportData report)
        {
            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(report.Title).FontSize(18).Bold().FontColor(Colors.Grey.Darken4);
                        column.Item().PaddingTop(3).Text($"Period: {report.PeriodLabel}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingVertical(14).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in report.Headers)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var heading in report.Headers)
                            {
                                header.Cell().Background(Colors.Grey.Darken3).Padding(5).Text(heading).Bold().FontColor(Colors.White);
                            }
                        });

                        if (report.Rows.Count == 0)
                        {
                            table.Cell().ColumnSpan((uint)Math.Max(1, report.Headers.Count)).Padding(8).Text("No records found for this report period.");
                        }
                        else
                        {
                            foreach (var row in report.Rows)
                            {
                                for (var column = 0; column < report.Headers.Count; column++)
                                {
                                    var value = column < row.Count ? row[column] : string.Empty;
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value);
                                }
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Backyard Mech | ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();
        }
    }

    public sealed record ReportExportData(
        string Title,
        string PeriodLabel,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string>> Rows);

    public sealed record ReportFile(byte[] Content, string ContentType, string Extension);
}

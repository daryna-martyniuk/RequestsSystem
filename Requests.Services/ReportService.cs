using System;
using System.Collections.Generic;
using System.Linq;
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// Вирішення конфлікту імен Document через псевдоніми
using PdfDocument = QuestPDF.Fluent.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace Requests.Services
{
    public class ReportService
    {
        private readonly RequestRepository _requestRepository;
        private readonly DepartmentTaskRepository _taskRepository;
        private readonly IRepository<AuditLog> _logRepository;
        private readonly UserRepository _userRepository;

        public ReportService(
            RequestRepository requestRepository,
            DepartmentTaskRepository taskRepository,
            IRepository<AuditLog> logRepository,
            UserRepository userRepository)
        {
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
            _logRepository = logRepository;
            _userRepository = userRepository;

            // Налаштування ліцензії QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // === ОТРИМАННЯ ДАНИХ ===

        public ManagerReportData GetManagerReportData(int departmentId, DateTime start, DateTime end)
        {
            var tasks = _taskRepository.Find(t =>
                t.DepartmentId == departmentId &&
                t.AssignedAt >= start &&
                t.AssignedAt <= end).ToList();

            return new ManagerReportData
            {
                DepartmentId = departmentId,
                StartDate = start,
                EndDate = end,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone),
                InProgressTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusInProgress),
                Tasks = tasks
            };
        }

        public DirectorReportData GetDirectorReportData(DateTime start, DateTime end)
        {
            var allRequests = _requestRepository.Find(r => r.CreatedAt >= start && r.CreatedAt <= end).ToList();

            var conferences = allRequests
                .Where(r => r.Category.Name.Contains("Конференція", StringComparison.OrdinalIgnoreCase) ||
                            r.Title.Contains("Конференція", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new DirectorReportData
            {
                StartDate = start,
                EndDate = end,
                TotalRequests = allRequests.Count,
                CompletedRequests = allRequests.Count(r => r.GlobalStatus.Name == ServiceConstants.StatusCompleted),
                ConferenceRequests = conferences,
                AllRequests = allRequests
            };
        }

        public List<AuditLog> GetAdminLogs(DateTime start, DateTime end)
        {
            return _logRepository.Find(l => l.Timestamp >= start && l.Timestamp <= end)
                                 .OrderByDescending(l => l.Timestamp)
                                 .ToList();
        }

        // === ГЕНЕРАЦІЯ PDF ===

        public void ExportToPdf(string filePath, string title, List<string[]> headersAndData)
        {
            // Використовуємо псевдонім PdfDocument
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text(title)
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            if (headersAndData.Any())
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    foreach (var _ in headersAndData.First())
                                        columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    foreach (var cell in headersAndData[0])
                                    {
                                        // Використовуємо Colors.Grey.Lighten2 замість Light
                                        header.Cell().Element(CellStyle).Background(Colors.Grey.Lighten2).Text(cell).SemiBold();
                                    }
                                });

                                foreach (var row in headersAndData.Skip(1))
                                {
                                    foreach (var cell in row)
                                    {
                                        table.Cell().Element(CellStyle).Text(cell);
                                    }
                                }
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Сторінка ");
                            x.CurrentPageNumber();
                        });
                });
            })
            .GeneratePdf(filePath);
        }

        static IContainer CellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
        }

        // === ГЕНЕРАЦІЯ WORD ===

        public void ExportToWord(string filePath, string title, List<string[]> dataRows)
        {
            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new WordDocument(); // Використовуємо псевдонім WordDocument
                var body = mainPart.Document.AppendChild(new Body());

                var para = body.AppendChild(new Paragraph());
                var run = para.AppendChild(new Run());
                run.AppendChild(new Text(title));
                run.RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "32" });

                var table = new Table();

                var tblPr = new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                    )
                );
                table.AppendChild(tblPr);

                foreach (var rowData in dataRows)
                {
                    var tr = new TableRow();
                    foreach (var cellData in rowData)
                    {
                        var tc = new TableCell();
                        tc.Append(new Paragraph(new Run(new Text(cellData ?? ""))));
                        tc.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }));
                        tr.Append(tc);
                    }
                    table.Append(tr);
                }

                body.Append(table);
                mainPart.Document.Save();
            }
        }
    }

    public class ManagerReportData
    {
        public int DepartmentId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public List<DepartmentTask> Tasks { get; set; } = new();
    }

    public class DirectorReportData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public List<Request> ConferenceRequests { get; set; } = new();
        public List<Request> AllRequests { get; set; } = new();
    }
}
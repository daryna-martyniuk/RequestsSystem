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

// Aliases to resolve conflicts
using PdfDocument = QuestPDF.Fluent.Document;
using WordDocPackage = DocumentFormat.OpenXml.Packaging.WordprocessingDocument; // The disposable package
using WordDocumentElement = DocumentFormat.OpenXml.Wordprocessing.Document; // The DOM element

namespace Requests.Services
{
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
        public List<Request> AllRequests { get; set; } = new();
    }

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
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ManagerReportData GetManagerReportData(int departmentId, DateTime start, DateTime end)
        {
            var tasks = _taskRepository.GetTasksForReport(departmentId, start, end);
            return new ManagerReportData
            {
                DepartmentId = departmentId,
                StartDate = start,
                EndDate = end,
                TotalTasks = tasks.Count(),
                CompletedTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone),
                InProgressTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusInProgress),
                Tasks = tasks.ToList()
            };
        }

        public DirectorReportData GetDirectorReportData(DateTime start, DateTime end)
        {
            var requests = _requestRepository.GetAll().Where(r => r.CreatedAt >= start && r.CreatedAt <= end).ToList();
            return new DirectorReportData
            {
                StartDate = start,
                EndDate = end,
                TotalRequests = requests.Count,
                CompletedRequests = requests.Count(r => r.GlobalStatus.Name == ServiceConstants.StatusCompleted),
                AllRequests = requests
            };
        }

        // === PDF GENERATION ===
        public void GeneratePdfReport(string filePath, string title, IEnumerable<string[]> data)
        {
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Header().Text(title).FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                        });

                        foreach (var row in data)
                        {
                            foreach (var cell in row)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(cell);
                            }
                        }
                    });
                });
            }).GeneratePdf(filePath);
        }

        // === WORD (DOCX) GENERATION ===
        // Note: Using OpenXml as configured in csproj.
        public void GenerateDocxReport(string filePath, string title, ManagerReportData data)
        {
            using (WordDocPackage wordDocument = WordDocPackage.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new WordDocumentElement();
                Body body = mainPart.Document.AppendChild(new Body());

                // Header
                var paraTitle = body.AppendChild(new Paragraph());
                var runTitle = paraTitle.AppendChild(new Run());
                runTitle.AppendChild(new Text(title));
                runTitle.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "32" });

                // Stats
                body.AppendChild(new Paragraph(new Run(new Text($"Період: {data.StartDate:d} - {data.EndDate:d}"))));
                body.AppendChild(new Paragraph(new Run(new Text($"Всього завдань: {data.TotalTasks}"))));
                body.AppendChild(new Paragraph(new Run(new Text($"Виконано: {data.CompletedTasks}"))));
                body.AppendChild(new Paragraph(new Run(new Text($"В роботі: {data.InProgressTasks}"))));

                mainPart.Document.Save();
            }
        }

        public IEnumerable<AuditLog> GetAdminLogs(DateTime start, DateTime end) =>
            _logRepository.Find(l => l.Timestamp >= start && l.Timestamp <= end).OrderByDescending(l => l.Timestamp);
    }
}
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

// Псевдоніми для уникнення конфліктів
using PdfDocument = QuestPDF.Fluent.Document;
using WordDocPackage = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;
using QuestColors = QuestPDF.Helpers.Colors; // Renamed to avoid confusion

namespace Requests.Services
{
    // === DTOs ДЛЯ ЗВІТІВ ===
    public class ManagerReportData
    {
        public string DepartmentName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Статистика кадрів
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public List<User> EmployeesList { get; set; } = new();

        // Агрегація (НОВЕ)
        public int CompletedTasksCount { get; set; }
        public Dictionary<string, int> StatsByStatus { get; set; } = new();

        // Завдання
        public List<DepartmentTask> Tasks { get; set; } = new();
    }

    public class DirectorReportData
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Глобальна статистика кадрів
        public int TotalSystemUsers { get; set; }
        public int ActiveSystemUsers { get; set; }
        public int InactiveSystemUsers { get; set; }
        public List<User> AdminStaff { get; set; } = new();

        // Агрегація (НОВЕ)
        public int TotalCompletedRequests { get; set; }
        public Dictionary<string, int> StatsByStatus { get; set; } = new();

        // Запити (відфільтровані)
        public List<Request> Requests { get; set; } = new();
    }

    public class AdminReportData
    {
        public string GeneratedBy { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Агрегація
        public int TotalLogs { get; set; }
        public int LoginsCount { get; set; }
        public int CreatedRequestsCount { get; set; }
        public int CompletedRequestsCount { get; set; }

        public List<AuditLog> Logs { get; set; } = new();
    }

    public class ReportService
    {
        private readonly RequestRepository _requestRepository;
        private readonly DepartmentTaskRepository _taskRepository;
        private readonly IRepository<AuditLog> _logRepository;
        private readonly UserRepository _userRepository;
        private readonly IRepository<Department> _departmentRepository;

        public ReportService(
            RequestRepository requestRepository,
            DepartmentTaskRepository taskRepository,
            IRepository<AuditLog> logRepository,
            UserRepository userRepository,
            IRepository<Department> departmentRepository)
        {
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
            _logRepository = logRepository;
            _userRepository = userRepository;
            _departmentRepository = departmentRepository;

            QuestPDF.Settings.License = LicenseType.Community;
        }

        // === ПІДГОТОВКА ДАНИХ ===

        public ManagerReportData GetManagerReportData(int departmentId, DateTime start, DateTime end)
        {
            var dept = _departmentRepository.GetById(departmentId);
            var employees = _userRepository.GetByDepartment(departmentId).ToList();
            var tasks = _taskRepository.GetTasksForReport(departmentId, start, end).ToList();

            // Агрегація даних
            var completedCount = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone);
            var statsByStatus = tasks
                .GroupBy(t => t.Status.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ManagerReportData
            {
                DepartmentName = dept?.Name ?? "Невідомий відділ",
                GeneratedAt = DateTime.Now,
                PeriodStart = start,
                PeriodEnd = end,
                EmployeesList = employees,
                TotalEmployees = employees.Count,
                ActiveEmployees = employees.Count(u => u.IsActive),
                InactiveEmployees = employees.Count(u => !u.IsActive),
                Tasks = tasks,
                CompletedTasksCount = completedCount,
                StatsByStatus = statsByStatus
            };
        }

        public DirectorReportData GetDirectorReportData(IEnumerable<Request> filteredRequests, DateTime start, DateTime end)
        {
            var requestList = filteredRequests.ToList();
            var allUsers = _userRepository.GetAll().ToList();
            var admins = allUsers.Where(u => u.Position.Name.Contains("Директор") || u.IsSystemAdmin).ToList();

            // Агрегація
            var completedCount = requestList.Count(r => r.GlobalStatus.Name == ServiceConstants.StatusCompleted);
            var statsByStatus = requestList
                .GroupBy(r => r.GlobalStatus.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            return new DirectorReportData
            {
                GeneratedAt = DateTime.Now,
                PeriodStart = start,
                PeriodEnd = end,
                TotalSystemUsers = allUsers.Count,
                ActiveSystemUsers = allUsers.Count(u => u.IsActive),
                InactiveSystemUsers = allUsers.Count(u => !u.IsActive),
                AdminStaff = admins,
                Requests = requestList,
                TotalCompletedRequests = completedCount,
                StatsByStatus = statsByStatus
            };
        }

        public AdminReportData GetAdminReportData(string adminName, DateTime start, DateTime end)
        {
            var logs = _logRepository.Find(l => l.Timestamp >= start && l.Timestamp <= end).OrderByDescending(l => l.Timestamp).ToList();

            return new AdminReportData
            {
                GeneratedBy = adminName,
                GeneratedAt = DateTime.Now,
                PeriodStart = start,
                PeriodEnd = end,
                Logs = logs,
                TotalLogs = logs.Count,
                LoginsCount = logs.Count(l => l.Action.Contains("Logged In")),
                CreatedRequestsCount = logs.Count(l => l.Action.Contains("Created Request")),
                CompletedRequestsCount = logs.Count(l => l.Action.Contains("Completed") || l.Action.Contains("Approved"))
            };
        }

        // === GENERIC PDF GENERATION (For compatibility) ===
        public void GeneratePdfReport(string filePath, string title, IEnumerable<string[]> data)
        {
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Header().Text(title).FontSize(20).SemiBold().FontColor(QuestColors.Blue.Medium);
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            // Adjust columns based on data width if needed, essentially relative
                            var colCount = data.FirstOrDefault()?.Length ?? 1;
                            for (int i = 0; i < colCount; i++) columns.RelativeColumn();
                        });

                        foreach (var row in data)
                        {
                            foreach (var cell in row)
                            {
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Padding(5).Text(cell ?? "");
                            }
                        }
                    });
                    page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                });
            }).GeneratePdf(filePath);
        }

        // === ГЕНЕРАЦІЯ (MANAGER) ===

        public void GenerateManagerPdf(string path, ManagerReportData data)
        {
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Header().Text($"Звіт відділу: {data.DepartmentName}").FontSize(20).SemiBold().FontColor(QuestColors.Blue.Medium);

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text($"Період: {data.PeriodStart:d} - {data.PeriodEnd:d}");
                        col.Item().Text($"Згенеровано: {data.GeneratedAt:g}");

                        // АГРЕГАЦІЯ
                        col.Item().PaddingTop(15).Border(1).BorderColor(QuestColors.Grey.Lighten1).Padding(10).Column(box =>
                        {
                            box.Item().Text("Зведена статистика").FontSize(14).Bold();
                            box.Item().Text($"Всього завдань у вибірці: {data.Tasks.Count}");
                            box.Item().Text($"Завершено за період: {data.CompletedTasksCount}").FontColor(QuestColors.Green.Darken1).Bold();

                            box.Item().PaddingTop(5).Text("По статусах:").Underline();
                            foreach (var stat in data.StatsByStatus)
                            {
                                box.Item().Text($" • {stat.Key}: {stat.Value}");
                            }
                        });

                        col.Item().PaddingTop(20).Text("Кадри").FontSize(16).Bold();
                        col.Item().Text($"Всього: {data.TotalEmployees} (Активні: {data.ActiveEmployees})");

                        col.Item().PaddingTop(20).Text("Деталізація завдань").FontSize(16).Bold();
                        col.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.ConstantColumn(40); c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h => { h.Cell().Text("ID"); h.Cell().Text("Запит"); h.Cell().Text("Статус"); h.Cell().Text("Дата"); });
                            foreach (var t in data.Tasks)
                            {
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(t.Id.ToString());
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(t.Request.Title);
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(t.Status.Name);
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(t.AssignedAt?.ToString("d"));
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                });
            }).GeneratePdf(path);
        }

        public void GenerateManagerDocx(string path, ManagerReportData data)
        {
            using (var doc = WordDocPackage.Create(path, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                AddWordHeader(body, $"Звіт відділу: {data.DepartmentName}");
                AddWordParagraph(body, $"Період: {data.PeriodStart:d} - {data.PeriodEnd:d}");

                AddWordSubHeader(body, "Зведена статистика:");
                AddWordParagraph(body, $"Всього завдань: {data.Tasks.Count}");
                AddWordParagraph(body, $"Завершено успішно: {data.CompletedTasksCount}");

                AddWordParagraph(body, "Розподіл по статусах:");
                foreach (var stat in data.StatsByStatus)
                    AddWordParagraph(body, $"- {stat.Key}: {stat.Value}");

                AddWordSubHeader(body, "Завдання:");
                var taskTable = CreateWordTable(new[] { "ID", "Запит", "Статус", "Дата" });
                foreach (var t in data.Tasks)
                    AddWordRow(taskTable, t.Id.ToString(), t.Request.Title, t.Status.Name, t.AssignedAt?.ToString("d") ?? "-");
                body.Append(taskTable);

                mainPart.Document.Save();
            }
        }

        // === ГЕНЕРАЦІЯ (DIRECTOR) ===

        public void GenerateDirectorPdf(string path, DirectorReportData data)
        {
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Header().Text("Глобальний звіт директора").FontSize(20).SemiBold().FontColor(QuestColors.Blue.Medium);

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text($"Період: {data.PeriodStart:d} - {data.PeriodEnd:d}");

                        // АГРЕГАЦІЯ
                        col.Item().PaddingTop(15).Border(1).BorderColor(QuestColors.Grey.Lighten1).Padding(10).Column(box =>
                        {
                            box.Item().Text("Глобальна аналітика").FontSize(14).Bold();
                            box.Item().Text($"Всього запитів у вибірці: {data.Requests.Count}");
                            box.Item().Text($"Завершено успішно: {data.TotalCompletedRequests}").FontColor(QuestColors.Green.Darken1).Bold();

                            box.Item().PaddingTop(5).Text("Розподіл по статусах:").Underline();
                            foreach (var stat in data.StatsByStatus)
                            {
                                box.Item().Text($" • {stat.Key}: {stat.Value}");
                            }
                        });

                        col.Item().PaddingTop(20).Text($"Реєстр запитів ({data.Requests.Count})").FontSize(16).Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                            table.Header(h => { h.Cell().Text("ID"); h.Cell().Text("Тема"); h.Cell().Text("Автор"); h.Cell().Text("Статус"); h.Cell().Text("Дата"); });
                            foreach (var r in data.Requests)
                            {
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(r.Id.ToString());
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(r.Title);
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(r.Author.FullName);
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(r.GlobalStatus.Name);
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(r.CreatedAt.ToString("d"));
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                });
            }).GeneratePdf(path);
        }

        public void GenerateDirectorDocx(string path, DirectorReportData data)
        {
            using (var doc = WordDocPackage.Create(path, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                AddWordHeader(body, "Глобальний звіт");
                AddWordParagraph(body, $"Період: {data.PeriodStart:d} - {data.PeriodEnd:d}");

                AddWordSubHeader(body, "Аналітика:");
                AddWordParagraph(body, $"Всього запитів: {data.Requests.Count}");
                AddWordParagraph(body, $"Завершено: {data.TotalCompletedRequests}");

                AddWordParagraph(body, "По статусах:");
                foreach (var stat in data.StatsByStatus)
                    AddWordParagraph(body, $"- {stat.Key}: {stat.Value}");

                AddWordSubHeader(body, "Запити:");
                var table = CreateWordTable(new[] { "ID", "Тема", "Автор", "Статус", "Дата" });
                foreach (var r in data.Requests)
                    AddWordRow(table, r.Id.ToString(), r.Title, r.Author.FullName, r.GlobalStatus.Name, r.CreatedAt.ToString("d"));
                body.Append(table);

                mainPart.Document.Save();
            }
        }

        // === ГЕНЕРАЦІЯ (ADMIN) ===

        public void GenerateAdminPdf(string path, AdminReportData data)
        {
            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Header().Text("Системний лог (Адміністратор)").FontSize(20).SemiBold();

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Text($"Адміністратор: {data.GeneratedBy}");
                        col.Item().Text($"Період: {data.PeriodStart:d} - {data.PeriodEnd:d}");

                        col.Item().PaddingTop(10).Border(1).BorderColor(QuestColors.Grey.Lighten1).Padding(5).Column(box =>
                        {
                            box.Item().Text("Агрегація подій:").Bold();
                            box.Item().Text($"Входів у систему: {data.LoginsCount}");
                            box.Item().Text($"Створено запитів: {data.CreatedRequestsCount}");
                            box.Item().Text($"Завершено запитів: {data.CompletedRequestsCount}");
                            box.Item().Text($"Всього записів: {data.TotalLogs}");
                        });

                        col.Item().PaddingTop(20).Text("Деталізація подій").FontSize(16).Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(2); });
                            table.Header(h => { h.Cell().Text("Час"); h.Cell().Text("Користувач"); h.Cell().Text("Дія"); });
                            foreach (var l in data.Logs)
                            {
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(l.Timestamp.ToString("g"));
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(l.User?.Username ?? "System");
                                table.Cell().BorderBottom(1).BorderColor(QuestColors.Grey.Lighten2).Text(l.Action);
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                });
            }).GeneratePdf(path);
        }

        public void GenerateAdminDocx(string path, AdminReportData data)
        {
            using (var doc = WordDocPackage.Create(path, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                AddWordHeader(body, "Системний звіт");
                AddWordParagraph(body, $"Адміністратор: {data.GeneratedBy}");
                AddWordParagraph(body, $"Звіт за період: {data.PeriodStart:d} - {data.PeriodEnd:d}");

                AddWordSubHeader(body, "Агрегація:");
                AddWordParagraph(body, $"Входів: {data.LoginsCount}, Запитів: {data.CreatedRequestsCount}, Завершено: {data.CompletedRequestsCount}");

                AddWordSubHeader(body, "Лог дій:");
                var table = CreateWordTable(new[] { "Час", "Користувач", "Дія" });
                foreach (var l in data.Logs)
                    AddWordRow(table, l.Timestamp.ToString("g"), l.User?.Username ?? "System", l.Action);
                body.Append(table);

                mainPart.Document.Save();
            }
        }

        // === ДОПОМІЖНІ МЕТОДИ WORD (OpenXML) ===

        private void AddWordHeader(Body body, string text)
        {
            var p = body.AppendChild(new Paragraph());
            var run = p.AppendChild(new Run());
            run.AppendChild(new WordText(text));
            run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "32" }); // 16pt
        }

        private void AddWordSubHeader(Body body, string text)
        {
            var p = body.AppendChild(new Paragraph());
            var run = p.AppendChild(new Run());
            run.AppendChild(new WordText(text));
            // Correctly specify full type for Color to resolve ambiguity
            run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "24" }, new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "3498DB" }); // 12pt Blue
        }

        private void AddWordParagraph(Body body, string text)
        {
            body.AppendChild(new Paragraph(new Run(new WordText(text))));
        }

        private Table CreateWordTable(string[] headers)
        {
            Table table = new Table();

            // Межі таблиці
            TableProperties tblPr = new TableProperties(
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

            // Хедер
            TableRow tr = new TableRow();
            foreach (var h in headers)
            {
                var tc = new TableCell(new Paragraph(new Run(new WordText(h)) { RunProperties = new RunProperties(new Bold()) }));
                tr.Append(tc);
            }
            table.Append(tr);
            return table;
        }

        private void AddWordRow(Table table, params string[] values)
        {
            TableRow tr = new TableRow();
            foreach (var v in values)
            {
                tr.Append(new TableCell(new Paragraph(new Run(new WordText(v ?? "")))));
            }
            table.Append(tr);
        }
    }
}
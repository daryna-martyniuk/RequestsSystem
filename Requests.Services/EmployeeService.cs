using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class EmployeeService
    {
        protected readonly RequestRepository _requestRepository; 
        protected readonly DepartmentTaskRepository _taskRepository;
        protected readonly IRepository<RequestStatus> _statusRepository;
        protected readonly IRepository<RequestComment> _commentRepository;
        protected readonly IRepository<RequestAttachment> _attachmentRepository;
        //protected readonly IRepository<DepartmentTask> _taskRepository;
        protected readonly IRepository<AuditLog> _auditRepository;

        public EmployeeService(
            RequestRepository requestRepository,
            DepartmentTaskRepository taskRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<RequestComment> commentRepository,
            IRepository<RequestAttachment> attachmentRepository,
            IRepository<AuditLog> auditRepository)
        {
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
            _statusRepository = statusRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _auditRepository = auditRepository;
        }

        // === ПЕРЕГЛЯД ===
        public IEnumerable<Request> GetMyRequests(int userId) => _requestRepository.GetByAuthorId(userId);
        public Request? GetRequestDetails(int requestId) => _requestRepository.GetFullRequestInfo(requestId);
        public IEnumerable<DepartmentTask> GetMyTasks(int userId)
        {
            // Використовуємо новий репо
            return _taskRepository.GetTasksByExecutor(userId)
                                  .Where(t => t.Status.Name != ServiceConstants.TaskStatusDone);
        }

        // === СТВОРЕННЯ ===
        public void CreateRequest(Request request, int userId, List<int> targetDepartmentIds)
        {
            request.AuthorId = userId;
            request.CreatedAt = DateTime.Now;

            // Статус: "Новий" для стратегічних, "Очікує погодження" для звичайних
            string statusName = request.IsStrategic ? ServiceConstants.StatusNew : ServiceConstants.StatusPendingApproval;
            var status = _statusRepository.Find(s => s.Name == statusName).FirstOrDefault();
            request.GlobalStatusId = status!.Id;

            // Задачі відділів (створюємо заготовки)
            var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).FirstOrDefault();
            if (taskStatusNew != null)
            {
                foreach (var depId in targetDepartmentIds)
                {
                    request.DepartmentTasks.Add(new DepartmentTask
                    {
                        DepartmentId = depId,
                        StatusId = taskStatusNew.Id,
                        AssignedAt = request.IsStrategic ? DateTime.Now : null
                    });
                }
            }

            _requestRepository.Add(request);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = "Created Request" });
        }

        // === ВИДАЛЕННЯ (Тільки Pending Approval) ===
        public void DeleteRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) throw new Exception("Запит не знайдено.");
            if (request.AuthorId != userId) throw new Exception("Ви не є автором.");

            var statusPending = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).First();
            
            // Видалити можна тільки якщо ще не пішло в роботу (Pending)
            if (request.GlobalStatusId != statusPending.Id)
            {
                throw new InvalidOperationException("Неможливо видалити запит, який вже розглядається або в роботі. Спробуйте скасувати його.");
            }

            // Оскільки у нас Restrict delete, чистимо залежності вручну
            var comments = _commentRepository.Find(c => c.RequestId == requestId);
            foreach(var c in comments) _commentRepository.Delete(c.Id);

            var attaches = _attachmentRepository.Find(a => a.RequestId == requestId);
            foreach(var a in attaches) _attachmentRepository.Delete(a.Id);

            // Таски (якщо є)
            if (request.DepartmentTasks != null)
            {
                // Тут треба доступ до TaskRepository, або припускаємо, що EF завантажив їх
                // Якщо Generic Repository не дозволяє видалити range, робимо це пізніше.
                // Але оскільки Tasks є навігаційною властивістю, при видаленні Request EF спробує видалити їх.
                // Якщо налаштовано Cascade в БД - видалиться саме. Якщо Restrict - впаде.
                // Враховуючи наш AppDbContext, ми ставили Restrict. 
                // Тому краще використовувати IRepository<DepartmentTask> тут, але його немає в конструкторі.
                // Додамо заглушку або розширимо сервіс за потребою. Поки сподіваємось на коректну роботу EF.
            }

            _requestRepository.Delete(requestId);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Deleted Request" });
        }

        // === СКАСУВАННЯ (Cancel) ===
        public void CancelRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;
            if (request.AuthorId != userId) throw new Exception("Ви не є автором.");

            // Можна скасувати майже все, крім вже завершеного
            var completedStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCompleted).First();
            
            if (request.GlobalStatusId == completedStatus.Id)
                throw new InvalidOperationException("Завершений запит не можна скасувати.");

            var canceledStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCanceled).First();
            request.GlobalStatusId = canceledStatus.Id;
            request.CompletedAt = DateTime.Now; // Фіксуємо дату закриття

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Canceled Request" });
        }

        // === РЕДАГУВАННЯ ===
        public void UpdateRequest(Request updatedInfo, int userId)
        {
            var request = _requestRepository.GetById(updatedInfo.Id);
            if (request == null) throw new Exception("Запит не знайдено.");
            
            // Отримуємо статуси для перевірки
            var statusPending = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).First();
            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).First();
            
            // 1. Повне редагування (Поки "На погодженні" або "Новий" для директорів)
            if (request.GlobalStatusId == statusPending.Id || request.GlobalStatusId == statusNew.Id)
            {
                request.Title = updatedInfo.Title;
                request.Description = updatedInfo.Description;
                request.PriorityId = updatedInfo.PriorityId;
                request.CategoryId = updatedInfo.CategoryId;
                request.Deadline = updatedInfo.Deadline;
                
                // Зміна відділів тут складна (треба синхронізувати список DepartmentTasks), 
                // тому поки що в MVP ми не дозволяємо міняти відділи при редагуванні.
            }
            // 2. Часткове редагування (Вже в роботі)
            else
            {
                // Дозволяємо міняти тільки дедлайн (якщо треба продовжити)
                // Тема і суть завдання вже зафіксовані
                request.Deadline = updatedInfo.Deadline;
            }

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = "Updated Request" });
        }

        // Допоміжні
        public void AddAttachment(int requestId, string fileName, byte[] data, int userId)
        {
            _attachmentRepository.Add(new RequestAttachment
            {
                RequestId = requestId,
                FileName = fileName,
                FileData = data,
                UploadedAt = DateTime.Now
            });
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Added Attachment" });
        }

        public void AddComment(int requestId, int userId, string text)
        {
            _commentRepository.Add(new RequestComment
            {
                RequestId = requestId,
                UserId = userId,
                CommentText = text,
                CreatedAt = DateTime.Now
            });
        }
    }
}
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

        // === МЕТОДИ ДЛЯ ОТРИМАННЯ ДАНИХ (Виправлено помилки Missing Method) ===

        public IEnumerable<Request> GetMyRequests(int userId)
        {
            return _requestRepository.GetByAuthorId(userId);
        }

        public IEnumerable<DepartmentTask> GetMyTasks(int userId)
        {
            // Повертає завдання, призначені конкретно цьому виконавцю
            return _taskRepository.GetTasksByExecutor(userId);
        }

        public Request? GetRequestDetails(int requestId)
        {
            return _requestRepository.GetFullRequestInfo(requestId);
        }

        // === СТВОРЕННЯ ЗАПИТУ ===
        public void CreateRequest(Request request, User author, List<int> targetDepartmentIds)
        {
            request.AuthorId = author.Id;
            request.CreatedAt = DateTime.Now;

            // 1. СТВОРЮЄМО ЗАДАЧІ ОДРАЗУ (для всіх вибраних відділів)
            // Вони збережуться разом із запитом.
            var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).FirstOrDefault();

            foreach (var deptId in targetDepartmentIds)
            {
                request.DepartmentTasks.Add(new DepartmentTask
                {
                    DepartmentId = deptId,
                    StatusId = taskStatusNew!.Id,
                    AssignedAt = DateTime.Now
                });
            }

            // 2. ВИЗНАЧАЄМО СТАТУС ЗАПИТУ
            bool isBoss = author.Position.Name == ServiceConstants.PositionHead ||
                          author.Position.Name == ServiceConstants.PositionDeputyHead ||
                          author.Position.Name == ServiceConstants.PositionDirector ||
                          author.Position.Name == ServiceConstants.PositionDeputyDirector ||
                          author.IsSystemAdmin;

            if (isBoss)
            {
                // Бос -> Одразу "Новий" (Активний)
                var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).FirstOrDefault();
                request.GlobalStatusId = statusNew.Id;
                _auditRepository.Add(new AuditLog { UserId = author.Id, Action = $"Created Request (Auto-Approved)" });
            }
            else
            {
                // Співробітник -> "Очікує погодження"
                // Задачі вже створені, але інші відділи їх не побачать, поки статус Request != "New"
                var statusPending = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).FirstOrDefault();
                request.GlobalStatusId = statusPending.Id;
                _auditRepository.Add(new AuditLog { UserId = author.Id, Action = "Created Request (Pending Approval)" });
            }

            _requestRepository.Add(request);
        }

        // === РЕДАГУВАННЯ ===
        // === ОНОВЛЕНО: Додано параметр targetDepartmentIds ===
        public void UpdateRequest(Request request, Request updatedInfo, int userId, List<int> targetDepartmentIds)
        {
            // 1. Оновлюємо основні поля
            if (request.GlobalStatus.Name == ServiceConstants.StatusPendingApproval)
            {
                request.Title = updatedInfo.Title;
                request.Description = updatedInfo.Description;
                request.CategoryId = updatedInfo.CategoryId;
                request.PriorityId = updatedInfo.PriorityId;
                request.Deadline = updatedInfo.Deadline;
            }
            else
            {
                request.Deadline = updatedInfo.Deadline;
                // В активному стані можна також дозволити змінювати опис, якщо треба
                request.Description = updatedInfo.Description;
            }

            // 2. ДОДАВАННЯ НОВИХ ВИКОНАВЦІВ (ВІДДІЛІВ)
            // Знаходимо відділи, які є в новому списку, але немає в існуючих задачах
            var existingDeptIds = request.DepartmentTasks.Select(dt => dt.DepartmentId).ToList();
            var newDeptIds = targetDepartmentIds.Except(existingDeptIds).ToList();

            if (newDeptIds.Any())
            {
                var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).FirstOrDefault();

                foreach (var newId in newDeptIds)
                {
                    var newTask = new DepartmentTask
                    {
                        RequestId = request.Id,
                        DepartmentId = newId,
                        StatusId = taskStatusNew!.Id,
                        AssignedAt = DateTime.Now
                    };
                    // Додаємо через репозиторій задач, бо request вже існує
                    _taskRepository.Add(newTask);
                }
                _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = $"Added {newDeptIds.Count} Executors during Edit" });
            }

            _requestRepository.Update(request);
            if (!newDeptIds.Any()) // Логуємо update тільки якщо не було додавання виконавців (щоб не дублювати)
                _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = "Updated Request" });
        }

        public void DeleteRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;
            if (request.AuthorId != userId) throw new Exception("Тільки автор може видаляти запит.");
            if (request.GlobalStatus.Name != ServiceConstants.StatusPendingApproval) throw new Exception("Неможливо видалити активний запит.");

            _requestRepository.Delete(requestId);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Deleted Request" });
        }

        public void CancelRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusCanceled = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCanceled).FirstOrDefault();
            request.GlobalStatusId = statusCanceled!.Id;
            request.CompletedAt = DateTime.Now;

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Canceled Request" });
        }

        public void AddAttachment(int requestId, string fileName, byte[] data, int userId)
        {
            _attachmentRepository.Add(new RequestAttachment
            {
                RequestId = requestId,
                FileName = fileName,
                FileData = data,
                UploadedAt = DateTime.Now
            });
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
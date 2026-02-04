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

        public IEnumerable<Request> GetMyRequests(int userId) => _requestRepository.GetByAuthorId(userId);

        // ОНОВЛЕНО: Передаємо статуси у репозиторій
        public IEnumerable<DepartmentTask> GetMyTasks(int userId)
        {
            return _taskRepository.GetTasksByExecutor(
                userId
            );
        }

        public Request? GetRequestDetails(int requestId) => _requestRepository.GetFullRequestInfo(requestId);

        public void UpdateTaskStatus(int taskId, int userId, string newStatusName)
        {
            var task = _taskRepository.GetById(taskId);
            if (task == null) return;

            var status = _statusRepository.Find(s => s.Name == newStatusName).FirstOrDefault()
                         ?? _statusRepository.Find(s => s.Name.ToLower() == newStatusName.ToLower()).FirstOrDefault();

            if (status == null) throw new Exception($"Статус '{newStatusName}' не знайдено.");

            task.StatusId = status.Id;

            if (newStatusName == ServiceConstants.TaskStatusDone)
            {
                task.CompletedAt = DateTime.Now;
            }

            _taskRepository.Update(task);
            _auditRepository.Add(new AuditLog { UserId = userId, Action = $"Task {taskId} status -> {newStatusName}" });

            if (newStatusName == ServiceConstants.TaskStatusDone)
            {
                CheckAndCompleteRequest(task.RequestId);
            }
        }

        private void CheckAndCompleteRequest(int requestId)
        {
            var allTasks = _taskRepository.Find(t => t.RequestId == requestId).ToList();
            var doneStatus = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusDone).FirstOrDefault();

            if (doneStatus == null) return;

            bool allDone = allTasks.All(t => t.StatusId == doneStatus.Id);

            if (allDone)
            {
                var request = _requestRepository.GetById(requestId);
                var reqCompletedStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCompleted).FirstOrDefault();

                if (request != null && reqCompletedStatus != null)
                {
                    request.GlobalStatusId = reqCompletedStatus.Id;
                    request.CompletedAt = DateTime.Now;
                    _requestRepository.Update(request);
                    _auditRepository.Add(new AuditLog { RequestId = requestId, Action = "Request Auto-Completed (All Depts Finished)" });
                }
            }
        }

        public void PauseTask(int taskId, int userId)
        {
            UpdateTaskStatus(taskId, userId, ServiceConstants.TaskStatusPaused);
        }

        public void ResumeTask(int taskId, int userId)
        {
            UpdateTaskStatus(taskId, userId, ServiceConstants.TaskStatusInProgress);
        }
        public void CreateRequest(Request request, User author, List<int> targetDepartmentIds)
        {
            request.AuthorId = author.Id;
            request.CreatedAt = DateTime.Now;

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

            bool isBoss = author.Position.Name == ServiceConstants.PositionHead ||
                          author.Position.Name == ServiceConstants.PositionDeputyHead ||
                          author.Position.Name == ServiceConstants.PositionDirector ||
                          author.Position.Name == ServiceConstants.PositionDeputyDirector ||
                          author.IsSystemAdmin;

            if (isBoss)
            {
                var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).FirstOrDefault();
                request.GlobalStatusId = statusNew.Id;
                _auditRepository.Add(new AuditLog { UserId = author.Id, Action = "Created Request (Auto-Approved)" });
            }
            else
            {
                var statusPending = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).FirstOrDefault();
                request.GlobalStatusId = statusPending.Id;
                _auditRepository.Add(new AuditLog { UserId = author.Id, Action = "Created Request (Pending Approval)" });
            }

            _requestRepository.Add(request);
        }

        public void UpdateRequest(Request request, Request updatedInfo, int userId, List<int> targetDepartmentIds)
        {
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
                request.Description = updatedInfo.Description;
            }

            var existingDeptIds = request.DepartmentTasks.Select(dt => dt.DepartmentId).ToList();
            var newDeptIds = targetDepartmentIds.Except(existingDeptIds).ToList();

            if (newDeptIds.Any())
            {
                var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).FirstOrDefault();
                foreach (var newId in newDeptIds)
                {
                    var newTask = new DepartmentTask { RequestId = request.Id, DepartmentId = newId, StatusId = taskStatusNew!.Id, AssignedAt = DateTime.Now };
                    _taskRepository.Add(newTask);
                }
                _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = $"Added {newDeptIds.Count} Executors" });
            }

            _requestRepository.Update(request);
            if (!newDeptIds.Any()) _auditRepository.Add(new AuditLog { UserId = userId, RequestId = request.Id, Action = "Updated Request" });
        }

        public void DeleteRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;
            if (request.AuthorId != userId) throw new Exception("Тільки автор може видаляти.");
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
            _attachmentRepository.Add(new RequestAttachment { RequestId = requestId, FileName = fileName, FileData = data, UploadedAt = DateTime.Now });
        }

        public void AddComment(int requestId, int userId, string text)
        {
            _commentRepository.Add(new RequestComment { RequestId = requestId, UserId = userId, CommentText = text, CreatedAt = DateTime.Now });
        }

        // МЕТОД ДЛЯ ПОВТОРНОЇ ВІДПРАВКИ (після редагування)
        public void ResubmitRequest(int requestId, Request updatedData, int userId)
        {
            var req = _requestRepository.GetById(requestId);
            if (req == null) return;

            // Оновлюємо поля
            req.Title = updatedData.Title;
            req.Description = updatedData.Description;
            req.CategoryId = updatedData.CategoryId;
            req.PriorityId = updatedData.PriorityId;
            req.Deadline = updatedData.Deadline;

            // Змінюємо статус на "Очікує погодження", щоб керівник знову побачив його
            var statusPending = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).First();
            req.GlobalStatusId = statusPending.Id;

            _requestRepository.Update(req);
            _auditRepository.Add(new AuditLog { UserId = userId, RequestId = requestId, Action = "Resubmitted Request" });
        }
    }
}
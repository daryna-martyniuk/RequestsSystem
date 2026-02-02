using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class ManagerService
    {
        private readonly RequestRepository _requestRepository;
        private readonly DepartmentTaskRepository _taskRepository;
        private readonly IRepository<TaskExecutor> _executorRepository;
        private readonly IRepository<RequestStatus> _statusRepository;
        private readonly IRepository<AuditLog> _auditRepository;
        private readonly UserRepository _userRepository;
        private readonly IRepository<RequestComment> _commentRepository;

        public ManagerService(
            RequestRepository requestRepository,
            DepartmentTaskRepository taskRepository,
            IRepository<TaskExecutor> executorRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<AuditLog> auditRepository,
            UserRepository userRepository,
            IRepository<RequestComment> commentRepository)
        {
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
            _executorRepository = executorRepository;
            _statusRepository = statusRepository;
            _auditRepository = auditRepository;
            _userRepository = userRepository;
            _commentRepository = commentRepository;
        }

        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId)
        {
            return _requestRepository.GetPendingApprovals(managerDepartmentId, ServiceConstants.StatusPendingApproval);
        }

        // Отримання запитів "На уточненні"
        public IEnumerable<Request> GetRequestsInDiscussion()
        {
            return _requestRepository.GetByGlobalStatus(ServiceConstants.StatusClarification);
        }

        // === ЗАВЕРШЕННЯ ОБГОВОРЕННЯ / ПОГОДЖЕННЯ ===
        public void ApproveRequest(int requestId, int managerId, Request editedValues, string? newStatusName = null, string? conclusion = null)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) throw new Exception("Запит не знайдено");

            // 1. Зберігаємо зміни в самому запиті (назва, опис, пріоритет тощо)
            request.Title = editedValues.Title;
            request.Description = editedValues.Description;
            request.PriorityId = editedValues.PriorityId;
            request.CategoryId = editedValues.CategoryId;
            request.Deadline = editedValues.Deadline;

            // 2. Змінюємо статус (виводимо з обговорення в "Новий" або інший)
            string targetStatus = newStatusName ?? ServiceConstants.StatusNew;
            var statusObj = _statusRepository.Find(s => s.Name == targetStatus).FirstOrDefault();

            if (statusObj == null) throw new Exception($"Статус '{targetStatus}' не знайдено.");

            request.GlobalStatusId = statusObj.Id;
            _requestRepository.Update(request);

            // 3. Додаємо підсумок обговорення в коментарі
            if (!string.IsNullOrWhiteSpace(conclusion))
            {
                _commentRepository.Add(new RequestComment
                {
                    RequestId = requestId,
                    UserId = managerId,
                    CommentText = $"[ПІДСУМОК ОБГОВОРЕННЯ]: {conclusion}",
                    CreatedAt = DateTime.Now
                });
            }

            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = $"Discussion Finished -> {targetStatus}" });
        }

        public void RejectRequest(int requestId, int managerId, string reason)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusRejected = _statusRepository.Find(s => s.Name == ServiceConstants.StatusRejected).First();
            request.GlobalStatusId = statusRejected.Id;
            request.CompletedAt = DateTime.Now;

            _requestRepository.Update(request);

            _commentRepository.Add(new RequestComment { RequestId = requestId, UserId = managerId, CommentText = $"[ВІДХИЛЕНО]: {reason}", CreatedAt = DateTime.Now });
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = $"Rejected: {reason}" });
        }

        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId)
        {
            var tasks = _taskRepository.GetIncomingTasks(
                departmentId,
                ServiceConstants.TaskStatusDone,
                ServiceConstants.StatusPendingApproval,
                ServiceConstants.StatusCanceled,
                ServiceConstants.StatusRejected);

            return tasks.Where(t =>
                t.Status?.Name == ServiceConstants.TaskStatusNew &&
                t.Request?.GlobalStatus?.Name != ServiceConstants.StatusClarification
            );
        }

        public void AssignExecutor(int departmentTaskId, int employeeId, int managerId)
        {
            _executorRepository.Add(new TaskExecutor { DepartmentTaskId = departmentTaskId, UserId = employeeId, AssignedAt = DateTime.Now, IsLead = true });
            var task = _taskRepository.GetById(departmentTaskId);
            var inProgress = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusInProgress).FirstOrDefault();
            task!.StatusId = inProgress!.Id;
            _taskRepository.Update(task);
            if (task.Request != null && task.Request.GlobalStatus.Name == ServiceConstants.StatusNew)
            {
                var globalProgress = _statusRepository.Find(s => s.Name == ServiceConstants.StatusInProgress).FirstOrDefault();
                task.Request.GlobalStatusId = globalProgress!.Id;
                _requestRepository.Update(task.Request);
            }
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Assigned User {employeeId} to Task {departmentTaskId}" });
        }

        public void ForwardTask(int departmentTaskId, int newDepartmentId, int managerId)
        {
            var task = _taskRepository.GetById(departmentTaskId);
            if (task == null) throw new Exception("Task not found");
            task.DepartmentId = newDepartmentId;
            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).First();
            task.StatusId = statusNew.Id;
            _taskRepository.Update(task);
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Forwarded Task {departmentTaskId} to Dept {newDepartmentId}" });
        }

        public void SetRequestToDiscussion(int requestId, int managerId, string reason)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusClarification = _statusRepository.Find(s => s.Name == ServiceConstants.StatusClarification).First();
            request.GlobalStatusId = statusClarification.Id;

            _requestRepository.Update(request);

            _commentRepository.Add(new RequestComment
            {
                RequestId = requestId,
                UserId = managerId,
                CommentText = $"[ВИНЕСЕНО НА ОБГОВОРЕННЯ]: {reason}",
                CreatedAt = DateTime.Now
            });

            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Discussion started" });
        }

        public IEnumerable<User> GetMyEmployees(int departmentId) => _userRepository.GetByDepartment(departmentId);
    }
}
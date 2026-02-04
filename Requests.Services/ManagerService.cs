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

        public IEnumerable<Request> GetPendingApprovals(int managerDeptId)
            => _requestRepository.GetPendingApprovals(managerDeptId, ServiceConstants.StatusPendingApproval);

        public IEnumerable<Request> GetRequestsInDiscussion(int managerDeptId)
        {
            return _requestRepository.GetByGlobalStatus(ServiceConstants.StatusClarification);
        }

        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId)
        {
            var allTasks = _taskRepository.GetTasksForReport(departmentId, DateTime.MinValue, DateTime.MaxValue);
            return allTasks.Where(t => t.Status.Name == ServiceConstants.TaskStatusNew);
        }

        // Погодження (переводить у "В роботі")
        public void ApproveRequest(int requestId, int managerId, Request requestToUpdate)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            request.PriorityId = requestToUpdate.PriorityId;
            request.Deadline = requestToUpdate.Deadline;

            var statusInProgress = _statusRepository.Find(s => s.Name == ServiceConstants.StatusInProgress).First();
            request.GlobalStatusId = statusInProgress.Id;

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Approved Request" });
        }

        // Відхилення
        public void RejectRequest(int requestId, int managerId, string reason)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;
            var statusRejected = _statusRepository.Find(s => s.Name == ServiceConstants.StatusRejected).First();
            request.GlobalStatusId = statusRejected.Id;
            request.CompletedAt = DateTime.Now;
            _requestRepository.Update(request);

            AddSystemComment(requestId, managerId, $"ЗАПИТ ВІДХИЛЕНО: {reason}");
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Rejected Request" });
        }

        // Відправка на обговорення (уточнення)
        public void SetRequestToDiscussion(int requestId, int managerId, string reason)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusClarification = _statusRepository.Find(s => s.Name == ServiceConstants.StatusClarification).First();
            request.GlobalStatusId = statusClarification.Id;

            _requestRepository.Update(request);

            AddSystemComment(requestId, managerId, $"[НА УТОЧНЕННІ]: {reason}");
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Sent to Discussion" });
        }

        // НОВИЙ МЕТОД: Повернення з обговорення (Завершити обговорення)
        public void ReturnFromDiscussion(int requestId, int managerId, Request updatedRequest, string targetStatusName, string comment)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            // 1. Оновлюємо дані з переданого об'єкта (щоб не перезаписати старими даними з кешу)
            request.Title = updatedRequest.Title;
            request.Description = updatedRequest.Description;
            request.PriorityId = updatedRequest.PriorityId;
            request.CategoryId = updatedRequest.CategoryId;
            request.Deadline = updatedRequest.Deadline;

            // 2. Змінюємо статус
            var targetStatus = _statusRepository.Find(s => s.Name == targetStatusName).FirstOrDefault();
            if (targetStatus != null)
            {
                request.GlobalStatusId = targetStatus.Id;
            }

            _requestRepository.Update(request);

            if (!string.IsNullOrWhiteSpace(comment))
            {
                AddSystemComment(requestId, managerId, $"[ОБГОВОРЕННЯ ЗАВЕРШЕНО]: {comment}");
            }

            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Discussion Finished" });
        }

        // --- Tasks ---
        public void AssignExecutor(int departmentTaskId, int executorId, int managerId)
        {
            var task = _taskRepository.GetById(departmentTaskId);
            if (task == null) return;

            var statusInProgress = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusInProgress).First();
            task.StatusId = statusInProgress.Id;
            task.AssignedAt = DateTime.Now;
            _taskRepository.Update(task);

            _executorRepository.Add(new TaskExecutor
            {
                DepartmentTaskId = departmentTaskId,
                UserId = executorId,
                IsLead = true,
                AssignedAt = DateTime.Now
            });

            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Assigned Task {departmentTaskId} to User {executorId}" });
        }

        public void ForwardTask(int departmentTaskId, int newDepartmentId, int managerId)
        {
            var task = _taskRepository.GetById(departmentTaskId);
            if (task == null) return;

            task.DepartmentId = newDepartmentId;
            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).First();
            task.StatusId = statusNew.Id;
            task.Executors.Clear();

            _taskRepository.Update(task);
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Forwarded Task {departmentTaskId} to Dept {newDepartmentId}" });
        }

        public void PauseTask(int departmentTaskId, int managerId)
        {
            var task = _taskRepository.GetById(departmentTaskId);
            if (task == null) return;

            var statusPaused = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusPaused).FirstOrDefault();
            if (statusPaused != null)
            {
                task.StatusId = statusPaused.Id;
                _taskRepository.Update(task);
                _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Paused Task {departmentTaskId}" });
            }
        }

        private void AddSystemComment(int requestId, int userId, string text)
        {
            _commentRepository.Add(new RequestComment
            {
                RequestId = requestId,
                UserId = userId,
                CommentText = text,
                CreatedAt = DateTime.Now
            });
        }

        public IEnumerable<User> GetMyEmployees(int departmentId) => _userRepository.GetByDepartment(departmentId);
        public IEnumerable<DepartmentTask> GetAllTasksForDepartment(int departmentId) => _taskRepository.GetAllTasksByDepartment(departmentId);
        public IEnumerable<DepartmentTask> GetAllTasksForEmployee(int employeeId) => _taskRepository.GetAllTasksByExecutor(employeeId);
    }
}
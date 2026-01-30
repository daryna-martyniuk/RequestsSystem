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
        private readonly IRepository<DepartmentTask> _taskRepository;
        private readonly IRepository<TaskExecutor> _executorRepository;
        private readonly IRepository<RequestStatus> _statusRepository;
        private readonly IRepository<AuditLog> _auditRepository;
        private readonly UserRepository _userRepository;

        public ManagerService(
            RequestRepository requestRepository,
            IRepository<DepartmentTask> taskRepository,
            IRepository<TaskExecutor> executorRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<AuditLog> auditRepository,
            UserRepository userRepository)
        {
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
            _executorRepository = executorRepository;
            _statusRepository = statusRepository;
            _auditRepository = auditRepository;
            _userRepository = userRepository;
        }

        // === 1. РОБОТА З ВХІДНИМИ ЗАПИТАМИ (Від підлеглих) ===

        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId)
        {
            var pendingStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).First();
            return _requestRepository.Find(r => r.Author.DepartmentId == managerDepartmentId && r.GlobalStatusId == pendingStatus.Id);
        }

        public void ApproveRequest(int requestId, int managerId)
        {
            var request = _requestRepository.GetFullRequestInfo(requestId);
            if (request == null) return;

            var newStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).First();
            request.GlobalStatusId = newStatus.Id;

            // Активація задач
            foreach (var task in request.DepartmentTasks)
            {
                if (task.AssignedAt == null) task.AssignedAt = DateTime.Now;
            }

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Approved Request" });
        }

        public void RejectRequest(int requestId, int managerId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var rejected = _statusRepository.Find(s => s.Name == ServiceConstants.StatusRejected).First();
            request.GlobalStatusId = rejected.Id;
            _requestRepository.Update(request);

            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Rejected Request" });
        }

        public void SendForClarification(int requestId, int managerId, string comment)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var clarification = _statusRepository.Find(s => s.Name == ServiceConstants.StatusClarification).FirstOrDefault();
            if (clarification != null)
            {
                request.GlobalStatusId = clarification.Id;
                _requestRepository.Update(request);
                _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = $"Sent for Clarification: {comment}" });
            }
        }

        // Редагування запиту підлеглого перед погодженням
        public void EditRequestBeforeApproval(Request updatedRequest, int managerId)
        {
            var request = _requestRepository.GetById(updatedRequest.Id);
            if (request == null) return;

            // Керівник може змінити пріоритет, категорію або дедлайн
            request.PriorityId = updatedRequest.PriorityId;
            request.CategoryId = updatedRequest.CategoryId;
            request.Deadline = updatedRequest.Deadline;
            request.Description = updatedRequest.Description; // Може підправити опис

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = request.Id, Action = "Edited Request Before Approval" });
        }

        // === 2. РОБОТА З ВЛАСНИМИ ЗАПИТАМИ ===

        public void CancelMyRequest(int requestId, int managerId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null || request.AuthorId != managerId) return;

            var statusCompleted = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCompleted).First();

            if (request.GlobalStatusId != statusCompleted.Id)
            {
                var canceled = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCanceled).First();
                request.GlobalStatusId = canceled.Id;
                request.CompletedAt = DateTime.Now;
                _requestRepository.Update(request);
                _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Manager Canceled Own Request" });
            }
        }

        // === 3. РОЗПОДІЛ ЗАДАЧ ===
        // ... (код AssignExecutor без змін) ...
        public IEnumerable<Request> GetIncomingRequests(int departmentId) => _requestRepository.GetByExecutorDepartment(departmentId);

        public void AssignExecutor(int departmentTaskId, int employeeId, int managerId)
        {
            _executorRepository.Add(new TaskExecutor { DepartmentTaskId = departmentTaskId, UserId = employeeId, AssignedAt = DateTime.Now });
            var task = _taskRepository.GetById(departmentTaskId);
            var inProgress = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusInProgress).First();
            task!.StatusId = inProgress.Id;
            _taskRepository.Update(task);
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Assigned User {employeeId} to Task {departmentTaskId}" });
        }

        public IEnumerable<User> GetMyEmployees(int departmentId) => _userRepository.GetByDepartment(departmentId);
    }
}
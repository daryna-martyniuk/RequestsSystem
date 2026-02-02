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

        public ManagerService(
            RequestRepository requestRepository,
            DepartmentTaskRepository taskRepository,
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

        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId)
        {
            return _requestRepository.GetPendingApprovals(managerDepartmentId, ServiceConstants.StatusPendingApproval);
        }

        public void ApproveRequest(int requestId, int managerId, Request editedValues)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) throw new Exception("Запит не знайдено");

            request.PriorityId = editedValues.PriorityId;
            request.CategoryId = editedValues.CategoryId;
            request.Deadline = editedValues.Deadline;

            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).First();
            request.GlobalStatusId = statusNew.Id;

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = "Approved Request" });
        }

        public void RejectRequest(int requestId, int managerId, string reason)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusRejected = _statusRepository.Find(s => s.Name == ServiceConstants.StatusRejected).First();
            request.GlobalStatusId = statusRejected.Id;
            request.CompletedAt = DateTime.Now;

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = $"Rejected: {reason}" });
        }

        // === ВХІДНІ ЗАДАЧІ НА ВІДДІЛ ===
        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId)
        {
            // Використовуємо метод репозиторію, передаючи константи з сервісу
            return _taskRepository.GetIncomingTasks(
                departmentId,
                ServiceConstants.TaskStatusDone,
                ServiceConstants.StatusPendingApproval
            );
        }

        // Призначити виконавця (існуючий метод)
        public void AssignExecutor(int departmentTaskId, int employeeId, int managerId)
        {
            _executorRepository.Add(new TaskExecutor
            {
                DepartmentTaskId = departmentTaskId,
                UserId = employeeId,
                AssignedAt = DateTime.Now,
                IsLead = true
            });

            var task = _taskRepository.GetById(departmentTaskId);
            if (task != null)
            {
                var inProgress = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusInProgress).FirstOrDefault();
                task.StatusId = inProgress!.Id;
                _taskRepository.Update(task);

                if (task.Request != null && task.Request.GlobalStatus.Name == ServiceConstants.StatusNew)
                {
                    var globalProgress = _statusRepository.Find(s => s.Name == ServiceConstants.StatusInProgress).FirstOrDefault();
                    task.Request.GlobalStatusId = globalProgress!.Id;
                    _requestRepository.Update(task.Request);
                }
            }
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Assigned User {employeeId} to Task {departmentTaskId}" });
        }

        // === ПЕРЕСЛАТИ ІНШОМУ ВІДДІЛУ (НОВЕ) ===
        public void ForwardTask(int departmentTaskId, int newDepartmentId, int managerId)
        {
            var task = _taskRepository.GetById(departmentTaskId);
            if (task == null) throw new Exception("Task not found");

            // Змінюємо відділ
            task.DepartmentId = newDepartmentId;

            // Скидаємо статус задачі на "Новий" (щоб у новому відділі її побачили як нову)
            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).First();
            task.StatusId = statusNew.Id;

            // Очищаємо виконавців (бо старі виконавці з нашого відділу вже не актуальні)
            // (Тут треба бути обережним, якщо executors є. В ідеалі - видалити їх, або залишити як історію)
            // Для простоти поки не видаляємо записи TaskExecutor, але вони будуть прив'язані до цієї задачі.

            _taskRepository.Update(task);
            _auditRepository.Add(new AuditLog { UserId = managerId, Action = $"Forwarded Task {departmentTaskId} to Dept {newDepartmentId}" });
        }

        // === ВИСУНУТИ НА ОБГОВОРЕННЯ (НОВЕ) ===
        public void SetRequestToDiscussion(int requestId, int managerId, string comment)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            var statusClarification = _statusRepository.Find(s => s.Name == ServiceConstants.StatusClarification).First();
            request.GlobalStatusId = statusClarification.Id;

            _requestRepository.Update(request);
            _auditRepository.Add(new AuditLog { UserId = managerId, RequestId = requestId, Action = $"Set to Discussion: {comment}" });
        }

        public IEnumerable<User> GetMyEmployees(int departmentId) => _userRepository.GetByDepartment(departmentId);
    }
}
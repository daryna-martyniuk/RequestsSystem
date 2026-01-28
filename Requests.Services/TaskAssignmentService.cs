using Requests.Data.Models;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class TaskAssignmentService
    {
        private readonly IRepository<DepartmentTask> _taskRepository;
        private readonly IRepository<TaskExecutor> _executorRepository;
        private readonly IRepository<RequestStatus> _statusRepository;
        private readonly IRepository<AuditLog> _auditRepository;
        private readonly IRepository<Request> _requestRepository;

        public TaskAssignmentService(
            IRepository<DepartmentTask> taskRepository,
            IRepository<TaskExecutor> executorRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<AuditLog> auditRepository,
            IRepository<Request> requestRepository)
        {
            _taskRepository = taskRepository;
            _executorRepository = executorRepository;
            _statusRepository = statusRepository;
            _auditRepository = auditRepository;
            _requestRepository = requestRepository;
        }

        public void AssignExecutors(int taskId, List<int> userIds, int managerId)
        {
            var old = _executorRepository.Find(e => e.DepartmentTaskId == taskId);
            foreach (var o in old) _executorRepository.Delete(o.Id);

            foreach (var uid in userIds)
            {
                _executorRepository.Add(new TaskExecutor
                {
                    DepartmentTaskId = taskId,
                    UserId = uid,
                    AssignedAt = DateTime.Now
                });
            }

            UpdateTaskStatus(taskId, ServiceConstants.TaskStatusInProgress, managerId);

            LogAction(managerId, null, $"Assigned {userIds.Count} executors to Task #{taskId}");
        }

        public void UpdateTaskStatus(int taskId, string statusName, int userId)
        {
            var task = _taskRepository.GetById(taskId);
            var status = _statusRepository.Find(s => s.Name == statusName).FirstOrDefault();

            if (task != null && status != null)
            {
                task.StatusId = status.Id;
                if (statusName == ServiceConstants.TaskStatusDone || statusName == ServiceConstants.StatusCompleted)
                {
                    task.CompletedAt = DateTime.Now;
                }
                _taskRepository.Update(task);

                CheckAndCloseGlobalRequest(task.RequestId);

                LogAction(userId, task.RequestId, $"Task #{taskId} status changed to {statusName}");
            }
        }

        private void CheckAndCloseGlobalRequest(int requestId)
        {
            var request = _requestRepository.GetById(requestId); 
            var allTasks = _taskRepository.Find(t => t.RequestId == requestId);

            var completedStatus = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCompleted || s.Name == "Виконано").FirstOrDefault();

            if (allTasks.All(t => t.StatusId == completedStatus.Id))
            {
                request.GlobalStatusId = completedStatus.Id;
                request.CompletedAt = DateTime.Now;
                _requestRepository.Update(request);
            }
        }

        private void LogAction(int userId, int? requestId, string action)
        {
            _auditRepository.Add(new AuditLog
            {
                UserId = userId,
                RequestId = requestId,
                Action = action,
                Timestamp = DateTime.Now
            });
        }
    }
}
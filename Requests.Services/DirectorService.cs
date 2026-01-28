using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class DirectorService
    {
        private readonly RequestRepository _requestRepository;
        private readonly IRepository<RequestStatus> _statusRepository;
        private readonly IRepository<RequestPriority> _priorityRepository;
        private readonly IRepository<DepartmentTask> _taskRepository;
        private readonly IRepository<AuditLog> _auditRepository;

        public DirectorService(
            RequestRepository requestRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<RequestPriority> priorityRepository,
            IRepository<DepartmentTask> taskRepository,
            IRepository<AuditLog> auditRepository)
        {
            _requestRepository = requestRepository;
            _statusRepository = statusRepository;
            _priorityRepository = priorityRepository;
            _taskRepository = taskRepository;
            _auditRepository = auditRepository;
        }

        public IEnumerable<Request> GetAllRequestsSystemWide()
        {
            return _requestRepository.GetAll();
        }

        public Dictionary<string, int> GetGlobalStats()
        {
            var all = _requestRepository.GetAll();
            return all.GroupBy(r => r.GlobalStatus.Name)
                      .ToDictionary(g => g.Key, g => g.Count());
        }

        public void CreateStrategicRequest(Request request, int directorId, List<int> targetDepartments)
        {
            request.AuthorId = directorId;
            request.CreatedAt = DateTime.Now;
            request.IsStrategic = true; 

            var statusNew = _statusRepository.Find(s => s.Name == ServiceConstants.StatusNew).First();
            request.GlobalStatusId = statusNew.Id;

            var priorityCrit = _priorityRepository.Find(p => p.Name == ServiceConstants.PriorityCritical).First();
            request.PriorityId = priorityCrit.Id;

            var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).First();
            foreach (var depId in targetDepartments)
            {
                request.DepartmentTasks.Add(new DepartmentTask
                {
                    DepartmentId = depId,
                    StatusId = taskStatusNew.Id,
                    AssignedAt = DateTime.Now
                });
            }

            _requestRepository.Add(request);
            _auditRepository.Add(new AuditLog { UserId = directorId, RequestId = request.Id, Action = "Created STRATEGIC Request" });
        }
    }
}
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class RequestService
    {
        private readonly RequestRepository _requestRepository;
        private readonly IRepository<DepartmentTask> _departmentTaskRepository;
        private readonly IRepository<RequestComment> _commentRepository;
        private readonly IRepository<RequestAttachment> _attachmentRepository;
        private readonly IRepository<RequestStatus> _statusRepository;
        private readonly IRepository<RequestPriority> _priorityRepository;
        private readonly IRepository<AuditLog> _auditRepository;

        public RequestService(
            RequestRepository requestRepository,
            IRepository<DepartmentTask> departmentTaskRepository,
            IRepository<RequestComment> commentRepository,
            IRepository<RequestAttachment> attachmentRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<RequestPriority> priorityRepository,
            IRepository<AuditLog> auditRepository)
        {
            _requestRepository = requestRepository;
            _departmentTaskRepository = departmentTaskRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _statusRepository = statusRepository;
            _priorityRepository = priorityRepository;
            _auditRepository = auditRepository;
        }

        public IEnumerable<Request> GetAllRequests() => _requestRepository.GetAll();
        public IEnumerable<Request> GetMyRequests(int userId) => _requestRepository.GetByAuthorId(userId);
        public IEnumerable<Request> GetRequestsForMyDepartment(int departmentId) => _requestRepository.GetByExecutorDepartment(departmentId);
        public Request? GetRequestDetails(int id) => _requestRepository.GetFullRequestInfo(id);

        public void CreateRequest(Request request, User author, List<int> targetDepartmentIds)
        {
            request.AuthorId = author.Id;
            request.CreatedAt = DateTime.Now;

            string initialStatusName;

            if (author.Position.Name == ServiceConstants.PositionEmployee)
            {
                initialStatusName = ServiceConstants.StatusPendingApproval;
            }
            else
            {
                initialStatusName = ServiceConstants.StatusNew;
            }

            request.GlobalStatusId = GetStatusIdByName(initialStatusName);

            if (author.Position.Name == ServiceConstants.PositionDirector)
            {
                request.IsStrategic = true;
                request.PriorityId = _priorityRepository.Find(p => p.Name == ServiceConstants.PriorityCritical).First().Id;
            }

            if (initialStatusName == ServiceConstants.StatusNew)
            {
                CreateDepartmentTasks(request, targetDepartmentIds);
            }
            else
            {
                CreateDepartmentTasks(request, targetDepartmentIds);
            }

            _requestRepository.Add(request);

            LogAction(author.Id, request.Id, $"Created Request: {request.Title} ({initialStatusName})");
        }

        public void ApproveRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            request.GlobalStatusId = GetStatusIdByName(ServiceConstants.StatusNew);
            _requestRepository.Update(request);

            LogAction(userId, requestId, "Approved Request");
        }

        public void RejectRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request == null) return;

            request.GlobalStatusId = GetStatusIdByName(ServiceConstants.StatusRejected);
            _requestRepository.Update(request);

            LogAction(userId, requestId, "Rejected Request");
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
            LogAction(userId, requestId, "Added Comment");
        }

        public void AddAttachment(int requestId, string name, byte[] data, int userId)
        {
            _attachmentRepository.Add(new RequestAttachment
            {
                RequestId = requestId,
                FileName = name,
                FileData = data,
                UploadedAt = DateTime.Now
            });
            LogAction(userId, requestId, "Added Attachment");
        }

        private int GetStatusIdByName(string name)
        {
            var status = _statusRepository.Find(s => s.Name == name).FirstOrDefault();
            if (status == null) throw new Exception($"Critical Error: Status '{name}' not found in DB Seeder!");
            return status.Id;
        }

        private void CreateDepartmentTasks(Request request, List<int> departmentIds)
        {
            var newTaskStatusId = GetStatusIdByName(ServiceConstants.TaskStatusNew); 

            foreach (var depId in departmentIds)
            {
                request.DepartmentTasks.Add(new DepartmentTask
                {
                    DepartmentId = depId,
                    StatusId = newTaskStatusId,
                    AssignedAt = DateTime.Now
                });
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
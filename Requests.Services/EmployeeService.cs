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
        protected readonly IRepository<RequestStatus> _statusRepository;
        protected readonly IRepository<RequestComment> _commentRepository;
        protected readonly IRepository<RequestAttachment> _attachmentRepository; 
        protected readonly IRepository<AuditLog> _auditRepository;

        public EmployeeService(
            RequestRepository requestRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<RequestComment> commentRepository,
            IRepository<RequestAttachment> attachmentRepository,
            IRepository<AuditLog> auditRepository)
        {
            _requestRepository = requestRepository;
            _statusRepository = statusRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _auditRepository = auditRepository;
        }

        public IEnumerable<Request> GetMyRequests(int userId) => _requestRepository.GetByAuthorId(userId);

        public Request? GetRequestDetails(int requestId) => _requestRepository.GetFullRequestInfo(requestId);

        public void CreateRequest(Request request, int userId, List<int> targetDepartmentIds)
        {
            request.AuthorId = userId;
            request.CreatedAt = DateTime.Now;

            var status = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).FirstOrDefault();
            request.GlobalStatusId = status!.Id;

            var taskStatusNew = _statusRepository.Find(s => s.Name == ServiceConstants.TaskStatusNew).First();

            foreach (var depId in targetDepartmentIds)
            {
                request.DepartmentTasks.Add(new DepartmentTask
                {
                    DepartmentId = depId,
                    StatusId = taskStatusNew.Id,
                    AssignedAt = null // Ще не призначено, бо запит не погоджено
                });
            }

            _requestRepository.Add(request);

            _auditRepository.Add(new AuditLog
            {
                UserId = userId,
                RequestId = request.Id,
                Action = targetDepartmentIds.Count > 1 ? "Created Conference Request" : "Created Standard Request"
            });
        }

        public void AddAttachment(int requestId, string fileName, byte[] data, int userId)
        {
            var attachment = new RequestAttachment
            {
                RequestId = requestId,
                FileName = fileName,
                FileData = data,
                UploadedAt = DateTime.Now
            };
            _attachmentRepository.Add(attachment);

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

        public void CancelRequest(int requestId, int userId)
        {
            var request = _requestRepository.GetById(requestId);
            if (request != null && request.AuthorId == userId)
            {
                var status = _statusRepository.Find(s => s.Name == ServiceConstants.StatusCanceled).FirstOrDefault();
                request.GlobalStatusId = status!.Id;
                _requestRepository.Update(request);
            }
        }
    }
}
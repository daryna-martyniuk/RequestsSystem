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
        protected readonly IRepository<AuditLog> _auditRepository;

        public EmployeeService(
            RequestRepository requestRepository,
            IRepository<RequestStatus> statusRepository,
            IRepository<RequestComment> commentRepository,
            IRepository<AuditLog> auditRepository)
        {
            _requestRepository = requestRepository;
            _statusRepository = statusRepository;
            _commentRepository = commentRepository;
            _auditRepository = auditRepository;
        }

        public IEnumerable<Request> GetMyRequests(int userId)
        {
            return _requestRepository.GetByAuthorId(userId);
        }

        public Request? GetRequestDetails(int requestId)
        {
            return _requestRepository.GetFullRequestInfo(requestId);
        }

        public void CreateRequest(Request request, int userId)
        {
            request.AuthorId = userId;
            request.CreatedAt = DateTime.Now;
            var status = _statusRepository.Find(s => s.Name == ServiceConstants.StatusPendingApproval).FirstOrDefault();
            request.GlobalStatusId = status!.Id;

            _requestRepository.Add(request);

            _auditRepository.Add(new AuditLog
            {
                UserId = userId,
                RequestId = request.Id,
                Action = "Created Request (Pending Approval)"
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
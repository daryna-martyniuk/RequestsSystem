using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Data.Models;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Repositories.Implementations
{
    public class RequestRepository : Repository<Request>
    {
        public RequestRepository(AppDbContext context) : base(context)
        {
        }
        
        public override IEnumerable<Request> GetAll()
        {
            return _dbSet
                .Include(r => r.GlobalStatus)
                .Include(r => r.Priority)
                .Include(r => r.Category)
                .Include(r => r.Author)
                .OrderByDescending(r => r.CreatedAt) 
                .ToList();
        }

        public IEnumerable<Request> GetByAuthorId(int authorId)
        {
            return _dbSet
                .Where(r => r.AuthorId == authorId)
                .Include(r => r.GlobalStatus)
                .Include(r => r.Priority)
                .Include(r => r.Category)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public IEnumerable<Request> GetByExecutorDepartment(int departmentId)
        {
            return _dbSet
                .Where(r => r.DepartmentTasks.Any(dt => dt.DepartmentId == departmentId))
                .Include(r => r.GlobalStatus)
                .Include(r => r.Priority)
                .Include(r => r.Category)
                .Include(r => r.Author)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }

        public Request? GetFullRequestInfo(int id)
        {
            return _dbSet
                .Include(r => r.GlobalStatus)
                .Include(r => r.Priority)
                .Include(r => r.Category)
                .Include(r => r.Author)
                    .ThenInclude(a => a.Department)
                .Include(r => r.DepartmentTasks)
                    .ThenInclude(dt => dt.Department)
                .Include(r => r.DepartmentTasks)
                    .ThenInclude(dt => dt.Status)
                .Include(r => r.DepartmentTasks)
                    .ThenInclude(dt => dt.Executors)
                        .ThenInclude(e => e.User)
                .Include(r => r.Comments)
                    .ThenInclude(c => c.User)
                .Include(r => r.Attachments)
                .FirstOrDefault(r => r.Id == id);
        }
        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId, string pendingStatusName)
        {
            return _dbSet
                .Include(r => r.Author)
                .Include(r => r.GlobalStatus)
                .Include(r => r.Priority)
                .Include(r => r.Category)
                .Where(r => r.Author.DepartmentId == managerDepartmentId &&
                            r.GlobalStatus.Name == pendingStatusName)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }
    }
}
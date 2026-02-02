using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Data.Models;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Repositories.Implementations
{
    public class DepartmentTaskRepository : Repository<DepartmentTask>
    {
        public DepartmentTaskRepository(AppDbContext context) : base(context)
        {
        }

        public IEnumerable<DepartmentTask> GetTasksByExecutor(int userId)
        {
            return _dbSet
                .AsNoTracking()
                .Include(t => t.Request)
                    .ThenInclude(r => r.Priority)
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)
                .Include(t => t.Department)
                .Include(t => t.Status)
                .Include(t => t.Request.GlobalStatus) // Важливо підвантажити статус запиту
                .Where(t => t.Executors.Any(e => e.UserId == userId))
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId, string taskStatusDone, string globalStatusPending, string statusCanceled, string statusRejected)
        {
            return _dbSet
                .AsNoTracking() // <--- ВАЖЛИВО: Ігноруємо кеш, беремо свіже з БД
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)
                .Include(t => t.Request)
                    .ThenInclude(r => r.Priority)
                .Include(t => t.Status)
                .Include(t => t.Request.GlobalStatus) // Важливо!
                .Where(t =>
                    t.DepartmentId == departmentId &&
                    t.Status.Name != taskStatusDone &&
                    t.Request.GlobalStatus.Name != globalStatusPending &&
                    t.Request.GlobalStatus.Name != statusCanceled &&
                    t.Request.GlobalStatus.Name != statusRejected
                )
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId, string statusName)
        {
            return null;
        }
    }
}
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

        // Тепер приймає статуси "Скасовано" і "Відхилено" як параметри
        public IEnumerable<DepartmentTask> GetTasksByExecutor(int userId, string statusCanceled, string statusRejected)
        {
            return _dbSet
                .Include(t => t.Request)
                    .ThenInclude(r => r.Priority)
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)
                .Include(t => t.Department)
                .Include(t => t.Status)
                .Where(t => t.Executors.Any(e => e.UserId == userId))
                // Використовуємо параметри
                .Where(t => t.Request.GlobalStatus.Name != statusCanceled &&
                            t.Request.GlobalStatus.Name != statusRejected)
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        // Тепер приймає всі необхідні статуси для фільтрації
        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId, string taskStatusDone, string globalStatusPending, string statusCanceled, string statusRejected)
        {
            return _dbSet
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)
                .Include(t => t.Request)
                    .ThenInclude(r => r.Priority)
                .Include(t => t.Status)
                .Where(t =>
                    t.DepartmentId == departmentId &&
                    t.Status.Name != taskStatusDone &&
                    t.Request.GlobalStatus.Name != globalStatusPending &&
                    // Використовуємо параметри
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
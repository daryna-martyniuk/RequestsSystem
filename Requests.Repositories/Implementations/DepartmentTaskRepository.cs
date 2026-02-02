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
                .Include(t => t.Request) // Щоб бачити назву запиту
                    .ThenInclude(r => r.Priority)
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)
                .Include(t => t.Department)
                .Include(t => t.Status)
                .Where(t => t.Executors.Any(e => e.UserId == userId))
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public IEnumerable<Request> GetPendingApprovals(int managerDepartmentId, string statusName)
        {
            // Цей метод для ManagerService, щоб отримати запити на погодження
            // Але це стосується Requests, тому логічніше це залишити в RequestRepository
            return null;
        }

        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId, string taskStatusDone, string globalStatusPending)
        {
            return _dbSet
                .Include(t => t.Request)
                    .ThenInclude(r => r.Author)   // Щоб бачити, хто створив
                .Include(t => t.Request)
                    .ThenInclude(r => r.Priority) // Щоб бачити пріоритет
                .Include(t => t.Status)
                .Where(t =>
                    t.DepartmentId == departmentId &&
                    t.Status.Name != taskStatusDone &&
                    t.Request.GlobalStatus.Name != globalStatusPending
                )
                .OrderByDescending(t => t.AssignedAt) // Спочатку найновіші
                .ToList();
        }
    }
}
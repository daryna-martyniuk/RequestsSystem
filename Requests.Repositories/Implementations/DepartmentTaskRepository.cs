using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Data.Models;
using System;
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
                .Include(t => t.Request.GlobalStatus)
                .Where(t => t.Executors.Any(e => e.UserId == userId))
                .Where(t => t.Request.GlobalStatus.Name != "Скасовано" && t.Request.GlobalStatus.Name != "Відхилено") // Старий фільтр
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        // === НОВІ МЕТОДИ ДЛЯ СТАТИСТИКИ (БЕЗ ФІЛЬТРІВ) ===

        public IEnumerable<DepartmentTask> GetAllTasksByExecutor(int userId)
        {
            return _dbSet
                .AsNoTracking()
                .Include(t => t.Request).ThenInclude(r => r.Priority)
                .Include(t => t.Request).ThenInclude(r => r.Author)
                .Include(t => t.Department)
                .Include(t => t.Status)
                .Include(t => t.Request.GlobalStatus)
                .Where(t => t.Executors.Any(e => e.UserId == userId))
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public IEnumerable<DepartmentTask> GetAllTasksByDepartment(int departmentId)
        {
            return _dbSet
                .AsNoTracking()
                .Include(t => t.Request).ThenInclude(r => r.Priority)
                .Include(t => t.Request).ThenInclude(r => r.Author)
                .Include(t => t.Status)
                .Include(t => t.Request.GlobalStatus)
                .Include(t => t.Executors).ThenInclude(e => e.User)
                .Where(t => t.DepartmentId == departmentId)
                .OrderByDescending(t => t.AssignedAt)
                .ToList();
        }

        public IEnumerable<DepartmentTask> GetIncomingTasks(int departmentId, string taskStatusDone, string globalStatusPending, string statusCanceled, string statusRejected)
        {
            return _dbSet
                .AsNoTracking()
                .Include(t => t.Request).ThenInclude(r => r.Author)
                .Include(t => t.Request).ThenInclude(r => r.Priority)
                .Include(t => t.Status)
                .Include(t => t.Request.GlobalStatus)
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

        public IEnumerable<DepartmentTask> GetTasksForReport(int departmentId, DateTime start, DateTime end)
        {
            return _dbSet
                .AsNoTracking()
                .Include(t => t.Status)
                .Include(t => t.Request)
                .Include(t => t.Executors).ThenInclude(e => e.User)
                .Where(t =>
                    t.DepartmentId == departmentId &&
                    t.AssignedAt >= start &&
                    t.AssignedAt <= end)
                .ToList();
        }
    }
}
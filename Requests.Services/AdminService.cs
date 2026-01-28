using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Services
{
    public class AdminService
    {
        private readonly AppDbContext _context;
        private readonly UserRepository _userRepository;
        private readonly IRepository<AuditLog> _auditRepository;

        public AdminService(
            AppDbContext context,
            UserRepository userRepository,
            IRepository<AuditLog> auditRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _auditRepository = auditRepository;
        }

        // 1. Створення користувачів
        public void CreateUser(User user, string rawPassword, int adminId)
        {
            if (_userRepository.GetByUsername(user.Username) != null)
                throw new InvalidOperationException($"Логін '{user.Username}' вже зайнятий.");

            // Хешуємо пароль перед збереженням!
            // Використовуємо той самий метод хешування, що і в AuthService
            user.PasswordHash = AuthService.ComputeHash(rawPassword);

            _userRepository.Add(user);

            _auditRepository.Add(new AuditLog
            {
                UserId = adminId,
                Action = $"Created User: {user.Username}",
                Timestamp = DateTime.Now
            });
        }

        public void BackupDatabase(string backupPath, int adminId)
        {
            var dbName = "RequestsDB";
            var fullPath = System.IO.Path.Combine(backupPath, $"Backup_{DateTime.Now:yyyyMMdd_HHmm}.bak");

            _context.Database.ExecuteSqlRaw($"BACKUP DATABASE [{dbName}] TO DISK = '{fullPath}'");

            _auditRepository.Add(new AuditLog
            {
                UserId = adminId,
                Action = "Database Backup",
                Timestamp = DateTime.Now
            });
        }

        public Dictionary<string, int> GetRequestStats()
        {
            return _context.Requests
                .Include(r => r.GlobalStatus)
                .GroupBy(r => r.GlobalStatus.Name)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Status, x => x.Count);
        }

        public IEnumerable<AuditLog> GetSystemLogs()
        {
            return _auditRepository.GetAll()
                .OrderByDescending(l => l.Timestamp);
        }
    }
}
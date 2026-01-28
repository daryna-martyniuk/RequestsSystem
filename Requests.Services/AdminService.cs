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
        private readonly IRepository<Department> _departmentRepository;
        private readonly IRepository<Position> _positionRepository;

        public AdminService(
            AppDbContext context,
            UserRepository userRepository,
            IRepository<AuditLog> auditRepository,
            IRepository<Department> departmentRepository,
            IRepository<Position> positionRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _auditRepository = auditRepository;
            _departmentRepository = departmentRepository;
            _positionRepository = positionRepository;
        }

        public void CreateUser(User user, string rawPassword, int adminId)
        {
            if (_userRepository.GetByUsername(user.Username) != null)
                throw new InvalidOperationException("Цей логін вже зайнятий.");

            user.PasswordHash = AuthService.ComputeHash(rawPassword);
            _userRepository.Add(user);

            _auditRepository.Add(new AuditLog
            {
                UserId = adminId,
                Action = $"Created User {user.Username} in Dep {user.DepartmentId}"
            });
        }

        public void EditUser(User updatedUser, int adminId)
        {
            var existingUser = _userRepository.GetById(updatedUser.Id);
            if (existingUser == null)
                throw new InvalidOperationException("Користувача не знайдено.");

            existingUser.FullName = updatedUser.FullName;
            existingUser.Email = updatedUser.Email;
            existingUser.DepartmentId = updatedUser.DepartmentId;
            existingUser.PositionId = updatedUser.PositionId;

            _userRepository.Update(existingUser);

            _auditRepository.Add(new AuditLog
            {
                UserId = adminId,
                Action = $"Edited User {existingUser.Username} (ID: {existingUser.Id})",
                Timestamp = DateTime.Now
            });
        }

        public void ToggleUserActivity(int userId, bool isActive, int adminId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return;

            user.IsActive = isActive;
            _userRepository.Update(user);

            string action = isActive ? "Activated User" : "Deactivated User";
            _auditRepository.Add(new AuditLog
            {
                UserId = adminId,
                Action = $"{action}: {user.Username}",
                Timestamp = DateTime.Now
            });
        }

        public IEnumerable<User> GetAllUsers() => _userRepository.GetAll();

        public void CreateDepartment(string name, int adminId)
        {
            _departmentRepository.Add(new Department { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Department: {name}" });
        }

        public void CreatePosition(string name, int adminId)
        {
            _positionRepository.Add(new Position { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Position: {name}" });
        }

        public IEnumerable<Department> GetAllDepartments() => _departmentRepository.GetAll();
        public IEnumerable<Position> GetAllPositions() => _positionRepository.GetAll();


        public void BackupDatabase(string folderPath, int adminId)
        {
            var dbName = "RequestsDB";
            var fileName = $"Backup_{DateTime.Now:yyyyMMdd_HHmm}.bak";
            var fullPath = System.IO.Path.Combine(folderPath, fileName);

            _context.Database.ExecuteSqlRaw($"BACKUP DATABASE [{dbName}] TO DISK = '{fullPath}'");

            _auditRepository.Add(new AuditLog { UserId = adminId, Action = "Database Backup Created" });
        }

        public IEnumerable<AuditLog> GetSystemLogs()
        {
            return _auditRepository.GetAll().OrderByDescending(l => l.Timestamp);
        }
    }
}
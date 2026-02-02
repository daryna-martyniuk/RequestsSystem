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
        private readonly IRepository<RequestCategory> _categoryRepository;

        public AdminService(
            AppDbContext context,
            UserRepository userRepository,
            IRepository<AuditLog> auditRepository,
            IRepository<Department> departmentRepository,
            IRepository<Position> positionRepository,
            IRepository<RequestCategory> categoryRepository)
        {
            _context = context;
            _userRepository = userRepository;
            _auditRepository = auditRepository;
            _departmentRepository = departmentRepository;
            _positionRepository = positionRepository;
            _categoryRepository = categoryRepository;
        }

        public void CreateUser(User user, string rawPassword, int adminId)
        {
            if (_userRepository.GetByUsername(user.Username) != null)
                throw new InvalidOperationException("Цей логін вже зайнятий.");

            user.PasswordHash = AuthService.ComputeHash(rawPassword);
            _userRepository.Add(user);

            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created User {user.Username}" });
        }

        public void EditUser(User updatedUser, int adminId)
        {
            var existingUser = _userRepository.GetById(updatedUser.Id);
            if (existingUser == null) throw new InvalidOperationException("Користувача не знайдено.");

            existingUser.FullName = updatedUser.FullName;
            existingUser.Email = updatedUser.Email;
            existingUser.DepartmentId = updatedUser.DepartmentId;
            existingUser.PositionId = updatedUser.PositionId;

            _userRepository.Update(existingUser);
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Edited User {existingUser.Username}" });
        }

        public void ToggleUserActivity(int userId, bool isActive, int adminId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return;

            user.IsActive = isActive;
            _userRepository.Update(user);
            string action = isActive ? "Activated" : "Deactivated";
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"{action} User {user.Username}" });
        }

        public void ForceChangePassword(int userId, string newPassword, int adminId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return;

            user.PasswordHash = AuthService.ComputeHash(newPassword);
            _userRepository.Update(user);

            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Force reset password for user {user.Username}" });
        }

        public IEnumerable<User> GetAllUsers() => _userRepository.GetAll();

        public void CreateDepartment(string name, int adminId)
        {
            if (_departmentRepository.Find(d => d.Name == name).Any())
                throw new InvalidOperationException("Такий відділ вже існує!");

            _departmentRepository.Add(new Department { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Department: {name}" });
        }

        public void UpdateDepartment(Department department, int adminId)
        {
            _departmentRepository.Update(department);
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated Department: {department.Name}" });
        }

        public void DeleteDepartment(int id, int adminId)
        {
            if (_userRepository.GetByDepartment(id).Any())
                throw new InvalidOperationException("Не можна видалити відділ, у якому працюють люди! Спочатку переведіть їх.");

            var dept = _departmentRepository.GetById(id);
            if (dept != null)
            {
                _departmentRepository.Delete(id);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted Department: {dept.Name}" });
            }
        }

        public void CreatePosition(string name, int adminId)
        {
            if (_positionRepository.Find(p => p.Name == name).Any())
                throw new InvalidOperationException("Така посада вже існує!");

            _positionRepository.Add(new Position { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Position: {name}" });
        }

        public void UpdatePosition(Position position, int adminId)
        {
            _positionRepository.Update(position);
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated Position: {position.Name}" });
        }

        public void DeletePosition(int id, int adminId)
        {
            if (_userRepository.Find(u => u.PositionId == id).Any())
                throw new InvalidOperationException("Не можна видалити посаду, яку займають люди!");

            var pos = _positionRepository.GetById(id);
            if (pos != null)
            {
                _positionRepository.Delete(id);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted Position: {pos.Name}" });
            }
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

        public IEnumerable<AuditLog> GetSystemLogs() => _auditRepository.GetAll().OrderByDescending(l => l.Timestamp);

        // === РОБОТА З КАТЕГОРІЯМИ (НОВЕ) ===
        public IEnumerable<RequestCategory> GetAllCategories() => _categoryRepository.GetAll();

        public void AddCategory(string name, int adminId)
        {
            if (_categoryRepository.Find(c => c.Name == name).Any())
                throw new Exception("Категорія з такою назвою вже існує.");

            _categoryRepository.Add(new RequestCategory { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Category: {name}" });
        }

        public void DeleteCategory(int id, int adminId)
        {
            // Перевірка, чи використовується категорія в запитах
            // Тут потрібен доступ до requests, або перевірка через БД (якщо foreign key restrict - то вилетить помилка)
            try
            {
                _categoryRepository.Delete(id);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted Category ID: {id}" });
            }
            catch (Exception)
            {
                throw new Exception("Не можна видалити категорію, яка використовується в активних запитах.");
            }
        }
    }
}
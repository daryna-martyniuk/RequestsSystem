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

        // === ВАЛІДАЦІЯ БЕЗПЕКИ СТРУКТУРИ ===
        private void ValidateHierarchyForDeactivation(User user)
        {
            // Якщо користувач не є керівником/заступником/директором - пропускаємо
            if (user.Position.Name != ServiceConstants.PositionHead &&
                user.Position.Name != ServiceConstants.PositionDeputyHead &&
                user.Position.Name != ServiceConstants.PositionDirector &&
                user.Position.Name != ServiceConstants.PositionDeputyDirector)
            {
                return;
            }

            // 1. Логіка для Директора
            if (user.Position.Name == ServiceConstants.PositionDirector)
            {
                bool hasBackup = _userRepository.Find(u =>
                    u.Id != user.Id &&
                    u.IsActive &&
                    (u.Position.Name == ServiceConstants.PositionDirector || u.Position.Name == ServiceConstants.PositionDeputyDirector)
                ).Any();

                if (!hasBackup)
                    throw new InvalidOperationException("Неможливо деактивувати єдиного активного Директора (немає заступників).");
            }

            // 2. Логіка для Керівника відділу або Заступника
            if (user.Position.Name == ServiceConstants.PositionHead || user.Position.Name == ServiceConstants.PositionDeputyHead)
            {
                // Шукаємо, чи залишиться у цьому відділі хоча б один активний бос (Head або Deputy)
                bool hasBackup = _userRepository.Find(u =>
                    u.DepartmentId == user.DepartmentId &&
                    u.Id != user.Id &&
                    u.IsActive &&
                    (u.Position.Name == ServiceConstants.PositionHead || u.Position.Name == ServiceConstants.PositionDeputyHead)
                ).Any();

                if (!hasBackup)
                {
                    throw new InvalidOperationException(
                        $"Неможливо деактивувати користувача '{user.FullName}'.\n" +
                        $"Він є останнім активним керівником у відділі '{user.Department.Name}'.\n" +
                        "Спочатку призначте або активуйте іншого керівника/заступника.");
                }
            }
        }

        // === КОРИСТУВАЧІ ===
        public IEnumerable<User> GetAllUsers() => _userRepository.GetAll();

        public void CreateUser(User user, string rawPassword, int adminId)
        {
            if (_userRepository.GetByUsername(user.Username) != null)
                throw new InvalidOperationException($"Логін '{user.Username}' вже зайнятий.");

            if (!string.IsNullOrWhiteSpace(user.Email) && _userRepository.Find(u => u.Email == user.Email).Any())
                throw new InvalidOperationException($"Email '{user.Email}' вже використовується.");

            // При створенні перевіряємо, чи є куди додавати (якщо це співробітник)
            var position = _positionRepository.GetById(user.PositionId);
            if (position != null && position.Name == ServiceConstants.PositionEmployee)
            {
                var bosses = _userRepository.Find(u =>
                    u.DepartmentId == user.DepartmentId &&
                    u.IsActive &&
                    (u.Position.Name == ServiceConstants.PositionHead || u.Position.Name == ServiceConstants.PositionDeputyHead));

                if (!bosses.Any())
                    throw new InvalidOperationException("У відділі повинен бути активний Керівник або Заступник.");
            }

            user.PasswordHash = AuthService.ComputeHash(rawPassword);
            user.IsActive = true;

            _userRepository.Add(user);
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created User: {user.Username}" });
        }

        public void UpdateUser(User userToUpdate, int adminId)
        {
            var existingUser = _userRepository.GetById(userToUpdate.Id);
            if (existingUser == null) throw new Exception("Користувача не знайдено");

            // Валідація Email
            if (!string.IsNullOrWhiteSpace(userToUpdate.Email) && userToUpdate.Email != existingUser.Email)
            {
                if (_userRepository.Find(u => u.Email == userToUpdate.Email && u.Id != userToUpdate.Id).Any())
                    throw new InvalidOperationException($"Email '{userToUpdate.Email}' вже зайнятий.");
            }

            // Якщо ми змінюємо відділ або посаду керівнику - це те саме, що деактивація його на старій посаді
            // Тому треба перевірити, чи не "осиротіє" старий відділ.
            // (Для спрощення тут поки не додаємо глибоку перевірку при Update, але варто врахувати в майбутньому)

            existingUser.FullName = userToUpdate.FullName;
            existingUser.Email = userToUpdate.Email;
            existingUser.DepartmentId = userToUpdate.DepartmentId;
            existingUser.PositionId = userToUpdate.PositionId;
            existingUser.IsSystemAdmin = userToUpdate.IsSystemAdmin;

            // Якщо змінюється активність через редагування - перевіряємо
            if (existingUser.IsActive && !userToUpdate.IsActive)
            {
                ValidateHierarchyForDeactivation(existingUser);
            }
            existingUser.IsActive = userToUpdate.IsActive;

            _userRepository.Update(existingUser);
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated User: {existingUser.Username}" });
        }

        // === ЗМІНА АКТИВНОСТІ (ВІДПУСТКА) ===
        public void ToggleUserActivity(int userId, bool isActive, int adminId)
        {
            var user = _userRepository.GetById(userId);
            if (user != null)
            {
                // Якщо намагаємося вимкнути (isActive = false) - запускаємо перевірку
                if (!isActive)
                {
                    ValidateHierarchyForDeactivation(user);
                }

                user.IsActive = isActive;
                _userRepository.Update(user);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"{(isActive ? "Activated" : "Deactivated")} User: {user.Username}" });
            }
        }

        public void DeleteUser(int userId, int adminId)
        {
            var user = _userRepository.GetById(userId);
            if (user != null)
            {
                // Видалення рівноцінне деактивації
                ValidateHierarchyForDeactivation(user);

                _userRepository.Delete(userId);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted User: {user.Username}" });
            }
        }

        public void ForceChangePassword(int userId, string newPassword, int adminId) { var u = _userRepository.GetById(userId); if (u != null) { u.PasswordHash = AuthService.ComputeHash(newPassword); _userRepository.Update(u); } }


        // === ВІДДІЛИ (DEPARTMENTS) ===
        public IEnumerable<Department> GetAllDepartments() => _departmentRepository.GetAll();

        public void AddDepartment(string name, int adminId)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Назва обов'язкова");
            _departmentRepository.Add(new Department { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Dept: {name}" });
        }

        public void EditDepartment(int id, string name, int adminId)
        {
            var dept = _departmentRepository.GetById(id);
            if (dept != null)
            {
                dept.Name = name;
                _departmentRepository.Update(dept);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated Dept: {name}" });
            }
        }

        public void DeleteDepartment(int id, int adminId)
        {
            if (_userRepository.Find(u => u.DepartmentId == id).Any())
                throw new InvalidOperationException("Не можна видалити відділ, у якому працюють люди!");

            var dept = _departmentRepository.GetById(id);
            if (dept != null)
            {
                _departmentRepository.Delete(id);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted Dept: {dept.Name}" });
            }
        }

        // === ПОСАДИ (POSITIONS) ===
        public IEnumerable<Position> GetAllPositions() => _positionRepository.GetAll();

        public void AddPosition(string name, int adminId)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Назва обов'язкова");
            _positionRepository.Add(new Position { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Position: {name}" });
        }

        public void EditPosition(int id, string name, int adminId)
        {
            var pos = _positionRepository.GetById(id);
            if (pos != null)
            {
                pos.Name = name;
                _positionRepository.Update(pos);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated Position: {name}" });
            }
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

        // === КАТЕГОРІЇ (CATEGORIES) ===
        public IEnumerable<RequestCategory> GetAllCategories() => _categoryRepository.GetAll();

        public void AddCategory(string name, int adminId)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Назва обов'язкова");
            if (_categoryRepository.Find(c => c.Name.ToLower() == name.ToLower()).Any())
                throw new InvalidOperationException("Така категорія вже існує");

            _categoryRepository.Add(new RequestCategory { Name = name });
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Created Category: {name}" });
        }

        public void EditCategory(int id, string name, int adminId)
        {
            var cat = _categoryRepository.GetById(id);
            if (cat != null)
            {
                cat.Name = name;
                _categoryRepository.Update(cat);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Updated Category: {name}" });
            }
        }

        public void DeleteCategory(int id, int adminId)
        {
            // Увага: БД викине помилку, якщо категорія використовується (Restrict)
            // Обробка цього має бути у ViewModel або через try-catch тут
            var cat = _categoryRepository.GetById(id);
            if (cat != null)
            {
                _categoryRepository.Delete(id);
                _auditRepository.Add(new AuditLog { UserId = adminId, Action = $"Deleted Category: {cat.Name}" });
            }
        }

        // === ІНШЕ ===
        public void BackupDatabase(string folderPath, int adminId)
        {
            var dbName = "RequestsSystemDb";
            var fileName = $"Backup_{DateTime.Now:yyyyMMdd_HHmm}.bak";
            var fullPath = System.IO.Path.Combine(folderPath, fileName);
            _context.Database.ExecuteSqlRaw($"BACKUP DATABASE [{dbName}] TO DISK = '{fullPath}'");
            _auditRepository.Add(new AuditLog { UserId = adminId, Action = "Database Backup Created" });
        }

        public IEnumerable<AuditLog> GetSystemLogs() => _auditRepository.GetAll().OrderByDescending(l => l.Timestamp);
    }
}
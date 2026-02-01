using Requests.Data;
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using Requests.Services;
using Requests.UI.Views;
using System.Windows;

namespace Requests.UI
{
    public partial class App : Application
    {
        private static AppDbContext CreateContext() => new AppDbContext();
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            using (var context = new AppDbContext())
            {
                DbInitializer.Seed(context);
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        // === ФАБРИКИ СЕРВІСІВ (Centralized Service Creation) ===

        // Цей метод створює готовий AdminService з усіма залежностями
        public static AdminService CreateAdminService()
        {
            var context = new AppDbContext();
            var userRepo = new UserRepository(context);
            var auditRepo = new Repository<AuditLog>(context);
            var deptRepo = new Repository<Department>(context);
            var posRepo = new Repository<Position>(context);
            // ADDED: Category Repository needed for the updated AdminService constructor
            var catRepo = new Repository<RequestCategory>(context);

            return new AdminService(context, userRepo, auditRepo, deptRepo, posRepo);
        }

        public static AuthService CreateAuthService()
        {
            var context = new AppDbContext();
            var userRepo = new UserRepository(context);
            var auditRepo = new Repository<AuditLog>(context);

            return new AuthService(userRepo, auditRepo);
        }

        public static EmployeeService CreateEmployeeService()
        {
            var context = CreateContext();
            return new EmployeeService(
                new RequestRepository(context),
                new DepartmentTaskRepository(context),
                new Repository<RequestStatus>(context),
                new Repository<RequestComment>(context),
                new Repository<RequestAttachment>(context),
                new Repository<AuditLog>(context)
            );
        }

        public static ManagerService CreateManagerService()
        {
            var context = CreateContext();

            // Ініціалізуємо всі необхідні залежності для ManagerService
            var requestRepo = new RequestRepository(context);
            var taskRepo = new DepartmentTaskRepository(context);
            var executorRepo = new Repository<TaskExecutor>(context); // Використовуємо базовий репозиторій для виконавців
            var statusRepo = new Repository<RequestStatus>(context);
            var auditRepo = new Repository<AuditLog>(context);
            var userRepo = new UserRepository(context);

            return new ManagerService(
                requestRepo,
                taskRepo,
                executorRepo,
                statusRepo,
                auditRepo,
                userRepo
            );
        }
    }
}
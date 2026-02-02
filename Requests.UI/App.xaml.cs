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

        // === ФАБРИКИ СЕРВІСІВ ===

        // Виправлено: передаємо всі необхідні репозиторії
        public static AdminService CreateAdminService()
        {
            var context = new AppDbContext();

            return new AdminService(
                context,
                new UserRepository(context),
                new Repository<AuditLog>(context),
                new Repository<Department>(context),
                new Repository<Position>(context),
                new Repository<RequestCategory>(context) // Додано аргумент
            );
        }

        public static AuthService CreateAuthService()
        {
            var context = CreateContext();
            return new AuthService(
                new UserRepository(context),
                new Repository<AuditLog>(context)
            );
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
            return new ManagerService(
                new RequestRepository(context),
                new DepartmentTaskRepository(context),
                new Repository<TaskExecutor>(context),
                new Repository<RequestStatus>(context),
                new Repository<AuditLog>(context),
                new UserRepository(context)
            );
        }

        public static DirectorService CreateDirectorService()
        {
            var context = CreateContext();
            return new DirectorService(
                new RequestRepository(context),
                new Repository<RequestStatus>(context),
                new Repository<RequestPriority>(context),
                new DepartmentTaskRepository(context),
                new Repository<AuditLog>(context)
            );
        }

        // Виправлено: додано UserRepository у конструктор
        public static ReportService CreateReportService()
        {
            var context = CreateContext();
            return new ReportService(
                new RequestRepository(context),
                new DepartmentTaskRepository(context),
                new Repository<AuditLog>(context),
                new UserRepository(context) // Додано аргумент
            );
        }
    }
}
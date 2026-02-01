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
            var context = new AppDbContext();
            var reqRepo = new RequestRepository(context);
            var statusRepo = new Repository<RequestStatus>(context);
            var commentRepo = new Repository<RequestComment>(context);
            var attachRepo = new Repository<RequestAttachment>(context);
            // Assuming DepartmentTask repository is needed here or handled internally by context in service
            // but based on previous service definition, let's check what EmployeeService needs.
            // It needs: RequestRepo, StatusRepo, CommentRepo, AttachmentRepo, AuditRepo.
            // Wait, looking at previous EmployeeService code, it accepted AuditRepo as the last arg.
            var auditRepo = new Repository<AuditLog>(context);

            return new EmployeeService(reqRepo, statusRepo, commentRepo, attachRepo, auditRepo);
        }
    }
}
using Requests.Data.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Requests.Data
{
    public static class DbInitializer
    {
        public static void Seed(AppDbContext context)
        {
            context.Database.EnsureCreated();

            if (!context.Departments.Any())
            {
                context.Departments.AddRange(
                    new Department { Name = "Адміністрація" },
                    new Department { Name = "IT Відділ" },
                    new Department { Name = "Бухгалтерія" },
                    new Department { Name = "HR Відділ" },
                    new Department { Name = "Юридичний відділ" },
                    new Department { Name = "АГВ" } 
                );
                context.SaveChanges();
            }

            if (!context.Positions.Any())
            {
                context.Positions.AddRange(
                    new Position { Name = "Директор" },
                    new Position { Name = "Заступник директора" },
                    new Position { Name = "Керівник відділу" },
                    new Position { Name = "Заступник керівника" },
                    new Position { Name = "Співробітник" },
                    new Position { Name = "Адміністратор" }
                );
                context.SaveChanges();
            }

            if (!context.RequestStatuses.Any())
            {
                context.RequestStatuses.AddRange(
                    new RequestStatus { Name = "Новий" },
                    new RequestStatus { Name = "Очікує погодження" },
                    new RequestStatus { Name = "На уточненні" },
                    new RequestStatus { Name = "В роботі" },
                    new RequestStatus { Name = "Відхилено" },
                    new RequestStatus { Name = "Скасовано" },
                    new RequestStatus { Name = "Завершено" }
                );
                context.SaveChanges();
            }

            if (!context.RequestPriorities.Any())
            {
                context.RequestPriorities.AddRange(
                    new RequestPriority { Name = "Низький" },
                    new RequestPriority { Name = "Середній" },
                    new RequestPriority { Name = "Високий" },
                    new RequestPriority { Name = "Критичний" }
                );
                context.SaveChanges();
            }

            if (!context.RequestCategories.Any())
            {
                context.RequestCategories.AddRange(
                    new RequestCategory { Name = "Технічна підтримка" },
                    new RequestCategory { Name = "Закупівля обладнання" },
                    new RequestCategory { Name = "Доступ до ресурсів" },
                    new RequestCategory { Name = "Ремонт" },
                    new RequestCategory { Name = "Інше" }
                );
                context.SaveChanges();
            }

            if (!context.Users.Any())
            {
                var adminDep = context.Departments.First(d => d.Name == "IT Відділ");
                var adminPos = context.Positions.First(p => p.Name == "Адміністратор");

                context.Users.Add(new User
                {
                    FullName = "System Administrator",
                    Username = "admin",
                    PasswordHash = ComputeHash("admin"),
                    Email = "admin@company.com",
                    IsSystemAdmin = true,
                    IsActive = true,
                    Department = adminDep,
                    Position = adminPos
                });
                context.SaveChanges();
            }
        }

        private static string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
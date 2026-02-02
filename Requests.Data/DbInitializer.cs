using Requests.Data.Models;
using System.Collections.Generic;
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

            // 1. Відділи
            var departments = new[] { "Адміністрація", "IT Відділ", "Бухгалтерія", "HR Відділ", "Юридичний відділ"};
            foreach (var depName in departments)
            {
                if (!context.Departments.Any(d => d.Name == depName))
                    context.Departments.Add(new Department { Name = depName });
            }
            context.SaveChanges();

            // 2. Посади
            var positions = new[] { "Директор", "Заступник директора", "Керівник відділу", "Заступник керівника", "Співробітник", "Стажер" };
            foreach (var posName in positions)
            {
                if (!context.Positions.Any(p => p.Name == posName))
                    context.Positions.Add(new Position { Name = posName });
            }
            context.SaveChanges();

            // 3. Статуси (ДОДАЄМО ТІЛЬКИ ВІДСУТНІ)
            var statuses = new[]
            { 
                // Глобальні статуси
                "Новий", "Очікує погодження", "На уточненні", "В роботі",
                "Відхилено", "Скасовано", "Завершено", 
                
                // Локальні статуси задач (яких не вистачало)
                "На паузі", "Виконано"
            };

            foreach (var statusName in statuses)
            {
                // Якщо такого статусу ще немає в базі - додаємо
                if (!context.RequestStatuses.Any(s => s.Name == statusName))
                {
                    context.RequestStatuses.Add(new RequestStatus { Name = statusName });
                }
            }
            context.SaveChanges();

            // 4. Пріоритети
            var priorities = new[] { "Низький", "Середній", "Високий", "Критичний" };
            foreach (var pName in priorities)
            {
                if (!context.RequestPriorities.Any(p => p.Name == pName))
                    context.RequestPriorities.Add(new RequestPriority { Name = pName });
            }
            context.SaveChanges();

            // 5. Категорії
            var categories = new[] { "Технічна підтримка", "Закупівля обладнання", "Доступ до ресурсів", "Ремонт", "Інше" };
            foreach (var cName in categories)
            {
                if (!context.RequestCategories.Any(c => c.Name == cName))
                    context.RequestCategories.Add(new RequestCategory { Name = cName });
            }
            context.SaveChanges();

            // 6. Адмін
            if (!context.Users.Any(u => u.Username == "admin"))
            {
                var adminDep = context.Departments.FirstOrDefault(d => d.Name == "IT Відділ") ?? context.Departments.First();
                var adminPos = context.Positions.FirstOrDefault(p => p.Name == "Адміністратор") ?? context.Positions.First();

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
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
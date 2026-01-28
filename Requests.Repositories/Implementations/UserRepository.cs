using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Data.Models;
using System.Collections.Generic;
using System.Linq;

namespace Requests.Repositories.Implementations
{
    public class UserRepository : Repository<User>
    {
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public User? GetByUsername(string username)
        {
            return _dbSet
                .Include(u => u.Department)
                .Include(u => u.Position)
                .FirstOrDefault(u => u.Username == username);
        }

        public override IEnumerable<User> GetAll()
        {
            return _dbSet
                .Include(u => u.Department)
                .Include(u => u.Position)
                .ToList();
        }

        public IEnumerable<User> GetByDepartment(int departmentId)
        {
            return _dbSet
                .Where(u => u.DepartmentId == departmentId)
                .Include(u => u.Department)
                .Include(u => u.Position)
                .ToList();
        }

        public IEnumerable<User> GetByDepartmentName(string departmentName)
        {
            return _dbSet
                .Include(u => u.Department)
                .Include(u => u.Position)
                .Where(u => u.Department.Name == departmentName)
                .ToList();
        }
    }
}
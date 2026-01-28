using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Requests.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;
        private readonly IRepository<AuditLog> _auditRepository;

        public AuthService(UserRepository userRepository, IRepository<AuditLog> auditRepository)
        {
            _userRepository = userRepository;
            _auditRepository = auditRepository;
        }

        public User? Login(string username, string password)
        {
            var user = _userRepository.GetByUsername(username);

            if (user != null && user.PasswordHash == ComputeHash(password) && user.IsActive)
            {
                _auditRepository.Add(new AuditLog
                {
                    UserId = user.Id,
                    Action = "User Logged In",
                    Timestamp = DateTime.Now
                });
                return user;
            }
            return null;
        }

        public void ChangePassword(int userId, string oldPassword, string newPassword)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return;

            if (user.PasswordHash != ComputeHash(oldPassword))
                throw new Exception("Старий пароль невірний");

            user.PasswordHash = ComputeHash(newPassword);
            _userRepository.Update(user);

            _auditRepository.Add(new AuditLog { UserId = user.Id, Action = "Password Changed" });
        }

        public static string ComputeHash(string input)
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
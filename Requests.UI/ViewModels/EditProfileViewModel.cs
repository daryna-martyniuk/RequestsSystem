using Requests.Data.Models;
using Requests.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class EditProfileViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly AuthService _authService;
        private readonly AdminService _adminService; // Для збереження загальних даних (email, etc.)
        private readonly Action<bool> _closeAction;

        private string _email;
        private string _fullName;

        // Поля для зміни пароля
        private string _oldPassword;
        // NewPassword беремо з PasswordBox

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string OldPassword
        {
            get => _oldPassword;
            set { _oldPassword = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditProfileViewModel(User user, AuthService authService, AdminService adminService, Action<bool> closeAction)
        {
            _user = user;
            _authService = authService;
            _adminService = adminService; // Можна використати й інший сервіс для update, але AdminService вже має метод EditUser
            _closeAction = closeAction;

            FullName = user.FullName;
            Email = user.Email;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save(object parameter)
        {
            try
            {
                // 1. Оновлення основних даних
                // Створюємо копію юзера з оновленими даними, щоб не зламати поточну сесію якщо збереження впаде
                var updatedUser = new User
                {
                    Id = _user.Id,
                    FullName = FullName,
                    Email = Email,
                    Username = _user.Username,
                    DepartmentId = _user.DepartmentId,
                    PositionId = _user.PositionId,
                    IsActive = _user.IsActive,
                    IsSystemAdmin = _user.IsSystemAdmin
                };

                // Використовуємо AdminService.EditUser (або створити окремий метод UpdateProfile в EmployeeService)
                // Оскільки AdminService.EditUser приймає adminId для логів, передаємо ID самого юзера
                _adminService.EditUser(updatedUser, _user.Id);

                // Оновлюємо локальний об'єкт _user (щоб у UI відобразились зміни одразу)
                _user.FullName = FullName;
                _user.Email = Email;

                // 2. Зміна пароля (якщо введено новий)
                var passwordBox = parameter as PasswordBox;
                string newPassword = passwordBox?.Password;

                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    if (string.IsNullOrWhiteSpace(OldPassword))
                    {
                        MessageBox.Show("Для зміни пароля введіть старий пароль!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        _authService.ChangePassword(_user.Id, OldPassword, newPassword);
                        MessageBox.Show("Профіль та пароль оновлено!", "Успіх");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка зміни пароля: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; // Не закриваємо вікно, щоб користувач міг виправити пароль
                    }
                }
                else
                {
                    MessageBox.Show("Профіль оновлено!", "Успіх");
                }

                _closeAction(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel(object obj)
        {
            _closeAction(false);
        }
    }
}
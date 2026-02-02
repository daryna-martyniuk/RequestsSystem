using Requests.Data.Models;
using Requests.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class EditUserViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly AdminService _adminService;
        private readonly int _adminId;
        private readonly Action<bool> _closeWindowAction;

        // Поля
        private string _fullName;
        private string _username;
        private string _email;
        private bool _isActive;
        private bool _isSystemAdmin;
        private Department _selectedDepartment;
        private Position _selectedPosition;

        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }
        public bool IsSystemAdmin { get => _isSystemAdmin; set { _isSystemAdmin = value; OnPropertyChanged(); } }

        public Department SelectedDepartment { get => _selectedDepartment; set { _selectedDepartment = value; OnPropertyChanged(); } }
        public Position SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        public ObservableCollection<Department> Departments { get; }
        public ObservableCollection<Position> Positions { get; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public bool IsEditMode => _user.Id != 0;
        public string WindowTitle => IsEditMode ? $"Редагування: {_user.Username}" : "Новий користувач";

        public EditUserViewModel(User user, AdminService adminService, int adminId, Action<bool> closeWindowAction)
        {
            _user = user;
            _adminService = adminService;
            _adminId = adminId;
            _closeWindowAction = closeWindowAction;

            Departments = new ObservableCollection<Department>(_adminService.GetAllDepartments());
            Positions = new ObservableCollection<Position>(_adminService.GetAllPositions());

            FullName = user.FullName;
            Username = user.Username;
            Email = user.Email;
            IsActive = user.IsActive;
            IsSystemAdmin = user.IsSystemAdmin;

            SelectedDepartment = Departments.FirstOrDefault(d => d.Id == user.DepartmentId) ?? Departments.FirstOrDefault();
            SelectedPosition = Positions.FirstOrDefault(p => p.Id == user.PositionId) ?? Positions.FirstOrDefault();

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save(object parameter)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("ПІБ та Логін обов'язкові!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password;

            // Заповнюємо об'єкт (але не зберігаємо в базу напряму)
            // Ми передаємо цей об'єкт у сервіс як DTO
            var userDto = new User
            {
                Id = _user.Id, // Важливо для Update
                Username = Username,
                FullName = FullName,
                Email = Email,
                IsActive = IsActive,
                IsSystemAdmin = IsSystemAdmin,
                DepartmentId = SelectedDepartment.Id,
                PositionId = SelectedPosition.Id
            };

            try
            {
                if (IsEditMode)
                {
                    // === ОНОВЛЕННЯ ===
                    _adminService.UpdateUser(userDto, _adminId);

                    // Зміна пароля (опціонально)
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        _adminService.ForceChangePassword(_user.Id, password, _adminId);
                        MessageBox.Show("Дані користувача та пароль оновлено!", "Успіх");
                    }
                    else
                    {
                        MessageBox.Show("Дані користувача оновлено!", "Успіх");
                    }
                }
                else
                {
                    // === СТВОРЕННЯ ===
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        MessageBox.Show("Для нового користувача пароль обов'язковий!", "Помилка");
                        return;
                    }
                    _adminService.CreateUser(userDto, password, _adminId);
                    MessageBox.Show("Користувача створено!", "Успіх");
                }

                _closeWindowAction(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка валидації", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel(object parameter) => _closeWindowAction(false);
    }
}
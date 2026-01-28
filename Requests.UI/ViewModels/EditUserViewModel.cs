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

        private string _fullName;
        private string _username;
        private string _email;
        private bool _isActive;

        private Department _selectedDepartment;
        private Position _selectedPosition;

        public ObservableCollection<Department> Departments { get; }
        public ObservableCollection<Position> Positions { get; }

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

            SelectedDepartment = Departments.FirstOrDefault(d => d.Id == user.DepartmentId);
            SelectedPosition = Positions.FirstOrDefault(p => p.Id == user.PositionId);

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
        }

        public bool IsEditMode => _user.Id != 0;
        public string WindowTitle => IsEditMode ? $"Редагування: {_user.Username}" : "Новий співробітник";
        public Visibility PasswordVisibility => IsEditMode ? Visibility.Collapsed : Visibility.Visible;

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public Department SelectedDepartment
        {
            get => _selectedDepartment;
            set { _selectedDepartment = value; OnPropertyChanged(); }
        }

        public Position SelectedPosition
        {
            get => _selectedPosition;
            set { _selectedPosition = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private void Save(object parameter)
        {
            try
            {
                if (SelectedDepartment == null || SelectedPosition == null)
                {
                    MessageBox.Show("Оберіть відділ та посаду!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("Заповніть ПІБ та Логін!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _user.FullName = FullName;
                _user.Username = Username;
                _user.Email = Email;
                _user.DepartmentId = SelectedDepartment.Id;
                _user.PositionId = SelectedPosition.Id;
                _user.IsActive = IsActive;

                if (IsEditMode)
                {
                    _adminService.EditUser(_user, _adminId);

                    if (_user.IsActive != IsActive)
                        _adminService.ToggleUserActivity(_user.Id, IsActive, _adminId);
                }
                else
                {
                    var passwordBox = parameter as PasswordBox;
                    string password = passwordBox?.Password;

                    if (string.IsNullOrWhiteSpace(password))
                    {
                        MessageBox.Show("Для нового користувача потрібен пароль!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    _adminService.CreateUser(_user, password, _adminId);
                }

                _closeWindowAction(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel(object parameter)
        {
            _closeWindowAction(false);
        }
    }
}
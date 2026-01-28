using Microsoft.Win32;
using Requests.Data;
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private readonly AdminService _adminService;
        private readonly User _currentUser;
        public ObservableCollection<User> Users { get; set; }
        public ObservableCollection<AuditLog> SystemLogs { get; set; }
        public ObservableCollection<Department> Departments { get; set; }
        public ObservableCollection<Position> Positions { get; set; }

        public ICollectionView UsersView { get; private set; }
        private string _searchText;
        private string _currentView = "Users"; // "Users", "Logs", "Structure"
        private string _newDepartmentName;
        private string _newPositionName;

        // Команди
        public ICommand CreateUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand SwitchViewCommand { get; }
        public ICommand CreateDepartmentCommand { get; }
        public ICommand CreatePositionCommand { get; }

        public AdminViewModel(User currentUser)
        {
            _currentUser = currentUser;

            var context = new AppDbContext();
            var userRepo = new UserRepository(context);
            var auditRepo = new Repository<AuditLog>(context);
            var deptRepo = new Repository<Department>(context);
            var posRepo = new Repository<Position>(context);

            _adminService = new AdminService(context, userRepo, auditRepo, deptRepo, posRepo);

            LoadData();

            UsersView = CollectionViewSource.GetDefaultView(Users);
            UsersView.Filter = FilterUsers;

            CreateUserCommand = new RelayCommand(OpenCreateUserWindow);
            EditUserCommand = new RelayCommand(OpenEditUserWindow);
            BackupCommand = new RelayCommand(BackupDatabase);
            LogoutCommand = new RelayCommand(Logout);
            SwitchViewCommand = new RelayCommand(SwitchView);
            CreateDepartmentCommand = new RelayCommand(CreateDepartment, _ => !string.IsNullOrWhiteSpace(NewDepartmentName));
            CreatePositionCommand = new RelayCommand(CreatePosition, _ => !string.IsNullOrWhiteSpace(NewPositionName));
        }

        private void LoadData()
        {
            Users = new ObservableCollection<User>(_adminService.GetAllUsers());
            SystemLogs = new ObservableCollection<AuditLog>(_adminService.GetSystemLogs());
            Departments = new ObservableCollection<Department>(_adminService.GetAllDepartments());
            Positions = new ObservableCollection<Position>(_adminService.GetAllPositions());
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                UsersView.Refresh();
            }
        }

        public string NewDepartmentName
        {
            get => _newDepartmentName;
            set { _newDepartmentName = value; OnPropertyChanged(); }
        }

        public string NewPositionName
        {
            get => _newPositionName;
            set { _newPositionName = value; OnPropertyChanged(); }
        }

        // Властивості видимості
        public Visibility IsUsersVisible => _currentView == "Users" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsLogsVisible => _currentView == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsStructureVisible => _currentView == "Structure" ? Visibility.Visible : Visibility.Collapsed;

        private bool FilterUsers(object obj)
        {
            if (obj is User user)
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return true;
                string search = SearchText.ToLower();
                return user.FullName.ToLower().Contains(search) ||
                       user.Username.ToLower().Contains(search) ||
                       user.Department.Name.ToLower().Contains(search);
            }
            return false;
        }

        private void SwitchView(object viewName)
        {
            _currentView = viewName.ToString();
            OnPropertyChanged(nameof(IsUsersVisible));
            OnPropertyChanged(nameof(IsLogsVisible));
            OnPropertyChanged(nameof(IsStructureVisible));

            if (_currentView == "Logs")
            {
                var logs = _adminService.GetSystemLogs();
                SystemLogs.Clear();
                foreach (var log in logs) SystemLogs.Add(log);
            }
            else if (_currentView == "Structure")
            {
                Departments.Clear();
                foreach (var d in _adminService.GetAllDepartments()) Departments.Add(d);

                Positions.Clear();
                foreach (var p in _adminService.GetAllPositions()) Positions.Add(p);
            }
        }

        private void CreateDepartment(object obj)
        {
            try
            {
                _adminService.CreateDepartment(NewDepartmentName, _currentUser.Id);
                NewDepartmentName = string.Empty; 
                SwitchView("Structure"); 
                MessageBox.Show("Відділ створено!", "Успіх");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}");
            }
        }

        private void CreatePosition(object obj)
        {
            try
            {
                _adminService.CreatePosition(NewPositionName, _currentUser.Id);
                NewPositionName = string.Empty;
                SwitchView("Structure");
                MessageBox.Show("Посаду створено!", "Успіх");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}");
            }
        }

        private void OpenCreateUserWindow(object obj)
        {
            var newUser = new User { IsActive = true };
            var createWindow = new EditUserWindow(newUser, _adminService, _currentUser.Id);
            if (createWindow.ShowDialog() == true)
            {
                var allUsers = _adminService.GetAllUsers();
                Users.Clear();
                foreach (var u in allUsers) Users.Add(u);
                MessageBox.Show("Співробітника успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenEditUserWindow(object obj)
        {
            if (obj is User userToEdit)
            {
                var editWindow = new EditUserWindow(userToEdit, _adminService, _currentUser.Id);
                if (editWindow.ShowDialog() == true)
                {
                    UsersView.Refresh();
                    MessageBox.Show("Дані оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BackupDatabase(object obj)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Збереження резервної копії БД",
                Filter = "SQL Backup Files (*.bak)|*.bak",
                DefaultExt = ".bak",
                FileName = $"Backup_{DateTime.Now:yyyyMMdd_HHmm}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string selectedFolder = Path.GetDirectoryName(saveDialog.FileName);
                try
                {
                    _adminService.BackupDatabase(selectedFolder, _currentUser.Id);
                    MessageBox.Show($"Бекап успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Logout(object obj)
        {
            new LoginWindow().Show();
            Application.Current.Windows[0].Close();
        }
    }
}
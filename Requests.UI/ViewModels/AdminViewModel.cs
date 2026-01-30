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

        // Колекції
        public ObservableCollection<User> Users { get; set; }
        public ObservableCollection<AuditLog> SystemLogs { get; set; }
        public ObservableCollection<Department> Departments { get; set; }
        public ObservableCollection<Position> Positions { get; set; }

        public ICollectionView UsersView { get; private set; }
        private string _searchText;
        private string _currentView = "Users";

        private string _newDepartmentName;
        private string _newPositionName;

        // Команди
        public ICommand CreateUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand SwitchViewCommand { get; }

        // Структура - Створення
        public ICommand CreateDepartmentCommand { get; }
        public ICommand CreatePositionCommand { get; }

        // Структура - Редагування/Видалення (НОВЕ)
        public ICommand EditDepartmentCommand { get; }
        public ICommand DeleteDepartmentCommand { get; }
        public ICommand EditPositionCommand { get; }
        public ICommand DeletePositionCommand { get; }

        public AdminViewModel(User currentUser)
        {
            _currentUser = currentUser;

            try
            {
                var context = new AppDbContext();
                var userRepo = new UserRepository(context);
                var auditRepo = new Repository<AuditLog>(context);
                var deptRepo = new Repository<Department>(context);
                var posRepo = new Repository<Position>(context);

                _adminService = new AdminService(context, userRepo, auditRepo, deptRepo, posRepo);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка ініціалізації: {ex.Message}");
                // Init empty collections to avoid crash
                Users = new ObservableCollection<User>();
                SystemLogs = new ObservableCollection<AuditLog>();
                Departments = new ObservableCollection<Department>();
                Positions = new ObservableCollection<Position>();
            }

            if (Users != null)
            {
                UsersView = CollectionViewSource.GetDefaultView(Users);
                UsersView.Filter = FilterUsers;
            }

            // Ініціалізація команд
            CreateUserCommand = new RelayCommand(OpenCreateUserWindow);
            EditUserCommand = new RelayCommand(OpenEditUserWindow);
            BackupCommand = new RelayCommand(BackupDatabase);
            SwitchViewCommand = new RelayCommand(SwitchView);

            CreateDepartmentCommand = new RelayCommand(CreateDepartment, _ => !string.IsNullOrWhiteSpace(NewDepartmentName));
            CreatePositionCommand = new RelayCommand(CreatePosition, _ => !string.IsNullOrWhiteSpace(NewPositionName));

            // НОВІ КОМАНДИ
            EditDepartmentCommand = new RelayCommand(EditDepartment);
            DeleteDepartmentCommand = new RelayCommand(DeleteDepartment);
            EditPositionCommand = new RelayCommand(EditPosition);
            DeletePositionCommand = new RelayCommand(DeletePosition);
        }

        private void LoadData()
        {
            Users = new ObservableCollection<User>(_adminService.GetAllUsers());
            SystemLogs = new ObservableCollection<AuditLog>(_adminService.GetSystemLogs());
            Departments = new ObservableCollection<Department>(_adminService.GetAllDepartments());
            Positions = new ObservableCollection<Position>(_adminService.GetAllPositions());
        }

        // ... SearchText, NewDepartmentName properties ... 
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); UsersView?.Refresh(); }
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

        // Visibility props
        public Visibility IsUsersVisible => _currentView == "Users" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsLogsVisible => _currentView == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsStructureVisible => _currentView == "Structure" ? Visibility.Visible : Visibility.Collapsed;

        // ... FilterUsers, SwitchView ...
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

        // === CRUD FOR STRUCTURE ===

        private void EditDepartment(object obj)
        {
            if (obj is Department dept)
            {
                var dialog = new EditNameWindow(dept.Name);
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        dept.Name = dialog.ResultName;
                        _adminService.UpdateDepartment(dept, _currentUser.Id);
                        SwitchView("Structure"); // Оновити UI
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
                }
            }
        }

        private void DeleteDepartment(object obj)
        {
            if (obj is Department dept)
            {
                if (MessageBox.Show($"Видалити відділ '{dept.Name}'?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _adminService.DeleteDepartment(dept.Id, _currentUser.Id);
                        Departments.Remove(dept);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Неможливо видалити"); }
                }
            }
        }

        private void EditPosition(object obj)
        {
            if (obj is Position pos)
            {
                var dialog = new EditNameWindow(pos.Name);
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        pos.Name = dialog.ResultName;
                        _adminService.UpdatePosition(pos, _currentUser.Id);
                        SwitchView("Structure");
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
                }
            }
        }

        private void DeletePosition(object obj)
        {
            if (obj is Position pos)
            {
                if (MessageBox.Show($"Видалити посаду '{pos.Name}'?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _adminService.DeletePosition(pos.Id, _currentUser.Id);
                        Positions.Remove(pos);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Неможливо видалити"); }
                }
            }
        }

        // ... Existing methods: CreateDepartment, CreatePosition, OpenCreateUserWindow, OpenEditUserWindow, BackupDatabase ...

        private void CreateDepartment(object obj)
        {
            try { _adminService.CreateDepartment(NewDepartmentName, _currentUser.Id); NewDepartmentName = ""; SwitchView("Structure"); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        private void CreatePosition(object obj)
        {
            try { _adminService.CreatePosition(NewPositionName, _currentUser.Id); NewPositionName = ""; SwitchView("Structure"); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
            }
        }

        private void OpenEditUserWindow(object obj)
        {
            if (obj is User userToEdit)
            {
                var editWindow = new EditUserWindow(userToEdit, _adminService, _currentUser.Id);
                if (editWindow.ShowDialog() == true) UsersView.Refresh();
            }
        }

        private void BackupDatabase(object obj)
        {
            var saveDialog = new SaveFileDialog { Filter = "Backup|*.bak", FileName = $"Backup_{DateTime.Now:yyyyMMdd}" };
            if (saveDialog.ShowDialog() == true)
            {
                try { _adminService.BackupDatabase(Path.GetDirectoryName(saveDialog.FileName), _currentUser.Id); MessageBox.Show("Успіх!"); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
    }
}
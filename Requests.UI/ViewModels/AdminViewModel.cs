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
using System.Linq;
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

        public ObservableCollection<User> DepartmentEmployees { get; set; }

        public ICollectionView UsersView { get; private set; }
        public ICollectionView LogsView { get; private set; }

        // === СТАТИСТИКА ===
        private int _totalUsersCount;
        private int _activeUsersCount;
        private int _departmentsCount;
        private int _positionsCount;

        public int TotalUsersCount
        {
            get => _totalUsersCount;
            set { _totalUsersCount = value; OnPropertyChanged(); }
        }
        public int ActiveUsersCount
        {
            get => _activeUsersCount;
            set { _activeUsersCount = value; OnPropertyChanged(); }
        }
        public int DepartmentsCount
        {
            get => _departmentsCount;
            set { _departmentsCount = value; OnPropertyChanged(); }
        }
        public int PositionsCount
        {
            get => _positionsCount;
            set { _positionsCount = value; OnPropertyChanged(); }
        }

        private string _searchText;
        private string _logsSearchText;
        private bool _showOnlyActiveUsers;

        private Department _selectedDepartment;

        private string _currentView = "Users";

        private string _newDepartmentName;
        // _newPositionName видалено, бо ми прибрали додавання посад

        // Команди
        public ICommand CreateUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand SwitchViewCommand { get; }

        public ICommand CreateDepartmentCommand { get; }
        // CreatePositionCommand видалено або можна залишити пустим

        // Структура - Редагування/Видалення
        public ICommand EditDepartmentCommand { get; }
        public ICommand DeleteDepartmentCommand { get; }

        public AdminViewModel(User currentUser)
        {
            _currentUser = currentUser;
            DepartmentEmployees = new ObservableCollection<User>();

            try
            {
                var context = new AppDbContext();
                var userRepo = new UserRepository(context);
                var auditRepo = new Repository<AuditLog>(context);
                var deptRepo = new Repository<Department>(context);
                var posRepo = new Repository<Position>(context);
                var catRepo = new Repository<RequestCategory>(context);

                _adminService = new AdminService(context, userRepo, auditRepo, deptRepo, posRepo, catRepo);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка ініціалізації: {ex.Message}");
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

            if (SystemLogs != null)
            {
                LogsView = CollectionViewSource.GetDefaultView(SystemLogs);
                LogsView.Filter = FilterLogs;
            }

            CreateUserCommand = new RelayCommand(OpenCreateUserWindow);
            EditUserCommand = new RelayCommand(OpenEditUserWindow);
            BackupCommand = new RelayCommand(BackupDatabase);
            SwitchViewCommand = new RelayCommand(SwitchView);

            CreateDepartmentCommand = new RelayCommand(CreateDepartment, _ => !string.IsNullOrWhiteSpace(NewDepartmentName));

            EditDepartmentCommand = new RelayCommand(EditDepartment);
            DeleteDepartmentCommand = new RelayCommand(DeleteDepartment);
        }

        private void LoadData()
        {
            Users = new ObservableCollection<User>(_adminService.GetAllUsers());
            SystemLogs = new ObservableCollection<AuditLog>(_adminService.GetSystemLogs());
            Departments = new ObservableCollection<Department>(_adminService.GetAllDepartments());
            Positions = new ObservableCollection<Position>(_adminService.GetAllPositions());

            UpdateStats();
        }

        private void UpdateStats()
        {
            if (Users != null)
            {
                TotalUsersCount = Users.Count;
                ActiveUsersCount = Users.Count(u => u.IsActive);
            }
            if (Departments != null) DepartmentsCount = Departments.Count;
            if (Positions != null) PositionsCount = Positions.Count;
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); UsersView?.Refresh(); }
        }

        public string LogsSearchText
        {
            get => _logsSearchText;
            set { _logsSearchText = value; OnPropertyChanged(); LogsView?.Refresh(); }
        }

        public bool ShowOnlyActiveUsers
        {
            get => _showOnlyActiveUsers;
            set { _showOnlyActiveUsers = value; OnPropertyChanged(); UsersView?.Refresh(); }
        }

        public Department SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                _selectedDepartment = value;
                OnPropertyChanged();
                UpdateDepartmentEmployees();
            }
        }

        private void UpdateDepartmentEmployees()
        {
            DepartmentEmployees.Clear();
            if (SelectedDepartment != null)
            {
                var employees = Users.Where(u => u.DepartmentId == SelectedDepartment.Id).ToList();
                foreach (var emp in employees) DepartmentEmployees.Add(emp);
            }
        }

        public string NewDepartmentName
        {
            get => _newDepartmentName;
            set { _newDepartmentName = value; OnPropertyChanged(); }
        }

        public Visibility IsUsersVisible => _currentView == "Users" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsLogsVisible => _currentView == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsStructureVisible => _currentView == "Structure" ? Visibility.Visible : Visibility.Collapsed;

        // === ОНОВЛЕНА ЛОГІКА ФІЛЬТРАЦІЇ ===

        private bool FilterUsers(object obj)
        {
            if (obj is User user)
            {
                if (ShowOnlyActiveUsers && !user.IsActive) return false;
                if (string.IsNullOrWhiteSpace(SearchText)) return true;

                string search = SearchText.ToLower();
                return user.FullName.ToLower().Contains(search) ||
                       user.Username.ToLower().Contains(search) ||
                       user.Department.Name.ToLower().Contains(search) ||
                       // Додано пошук за посадою:
                       user.Position.Name.ToLower().Contains(search);
            }
            return false;
        }

        private bool FilterLogs(object obj)
        {
            if (obj is AuditLog log)
            {
                if (string.IsNullOrWhiteSpace(LogsSearchText)) return true;
                string search = LogsSearchText.ToLower();

                // Пошук по дії, ID та даті
                bool matchAction = log.Action != null && log.Action.ToLower().Contains(search);
                bool matchUser = log.UserId.ToString().Contains(search);
                // Додано пошук по даті (текстове представлення)
                bool matchDate = log.Timestamp.ToString("dd.MM.yyyy HH:mm").Contains(search);

                return matchAction || matchUser || matchDate;
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
                UpdateStats();
            }
        }

        // === CRUD ВІДДІЛИ ===

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
                        SwitchView("Structure");
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

        private void CreateDepartment(object obj)
        {
            try { _adminService.CreateDepartment(NewDepartmentName, _currentUser.Id); NewDepartmentName = ""; SwitchView("Structure"); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // === КОРИСТУВАЧІ ===
        private void OpenCreateUserWindow(object obj)
        {
            var newUser = new User { IsActive = true };
            var createWindow = new EditUserWindow(newUser, _adminService, _currentUser.Id);
            if (createWindow.ShowDialog() == true)
            {
                var allUsers = _adminService.GetAllUsers();
                Users.Clear();
                foreach (var u in allUsers) Users.Add(u);
                UpdateStats();
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
                    UpdateStats();
                }
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
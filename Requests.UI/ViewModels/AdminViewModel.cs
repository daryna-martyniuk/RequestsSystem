using Microsoft.Win32;
using Requests.Data;
using Requests.Data.Models;
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
        private readonly ReportService _reportService;
        private readonly User _currentUser;

        // Колекції
        public ObservableCollection<User> Users { get; set; }
        public ObservableCollection<AuditLog> SystemLogs { get; set; }
        public ObservableCollection<Department> Departments { get; set; }
        public ObservableCollection<Position> Positions { get; set; }
        public ObservableCollection<RequestCategory> Categories { get; set; }

        public ICollectionView UsersView { get; private set; }
        public ICollectionView LogsView { get; private set; }

        // === СТАТИСТИКА ===
        private int _totalUsersCount;
        private int _activeUsersCount;
        private int _departmentsCount;
        private int _categoriesCount;

        public int TotalUsersCount { get => _totalUsersCount; set { _totalUsersCount = value; OnPropertyChanged(); } }
        public int ActiveUsersCount { get => _activeUsersCount; set { _activeUsersCount = value; OnPropertyChanged(); } }
        public int DepartmentsCount { get => _departmentsCount; set { _departmentsCount = value; OnPropertyChanged(); } }
        public int CategoriesCount { get => _categoriesCount; set { _categoriesCount = value; OnPropertyChanged(); } }

        // === ФІЛЬТРИ КОРИСТУВАЧІВ ===
        private string _userSearchText;
        public string UserSearchText
        {
            get => _userSearchText;
            set { _userSearchText = value; OnPropertyChanged(); UsersView.Refresh(); }
        }

        private Department _selectedDepartmentFilter;
        public Department SelectedDepartmentFilter
        {
            get => _selectedDepartmentFilter;
            set { _selectedDepartmentFilter = value; OnPropertyChanged(); UsersView.Refresh(); }
        }

        // Фільтр статусу (Всі, Активні, Неактивні)
        public ObservableCollection<string> StatusOptions { get; } = new ObservableCollection<string> { "Всі", "Активні", "Неактивні" };
        private string _selectedStatusFilter = "Всі";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set { _selectedStatusFilter = value; OnPropertyChanged(); UsersView.Refresh(); }
        }

        // === ПОШУК В ЛОГАХ ===
        private string _logsSearchText;
        public string LogsSearchText
        {
            get => _logsSearchText;
            set { _logsSearchText = value; LogsView.Refresh(); OnPropertyChanged(); }
        }

        // Поля для нових сутностей
        private string _newCategoryName;
        public string NewCategoryName { get => _newCategoryName; set { _newCategoryName = value; OnPropertyChanged(); } }

        private string _newDepartmentName;
        public string NewDepartmentName { get => _newDepartmentName; set { _newDepartmentName = value; OnPropertyChanged(); } }

        // Вкладки
        private string _currentAdminView = "Users";
        public Visibility IsUsersVisible => _currentAdminView == "Users" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsLogsVisible => _currentAdminView == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsStructureVisible => _currentAdminView == "Structure" ? Visibility.Visible : Visibility.Collapsed;

        // КОМАНДИ
        public ICommand SwitchViewCommand { get; }

        // Users
        public ICommand CreateUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ToggleUserActiveCommand { get; } // НОВА
        public ICommand ClearUserFiltersCommand { get; } // НОВА

        // Structure
        public ICommand AddDepartmentCommand { get; }
        public ICommand EditDepartmentCommand { get; }
        public ICommand DeleteDepartmentCommand { get; }
        public ICommand AddCategoryCommand { get; }
        public ICommand EditCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }

        public ICommand GenerateSystemReportCommand { get; }
        public ICommand BackupCommand { get; }

        public AdminViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _adminService = App.CreateAdminService();
            _reportService = App.CreateReportService();

            // Ініціалізація даних
            Users = new ObservableCollection<User>(_adminService.GetAllUsers());
            SystemLogs = new ObservableCollection<AuditLog>(_adminService.GetSystemLogs());
            Departments = new ObservableCollection<Department>(_adminService.GetAllDepartments());
            Positions = new ObservableCollection<Position>(_adminService.GetAllPositions());
            Categories = new ObservableCollection<RequestCategory>(_adminService.GetAllCategories());

            // Налаштування фільтрів
            UsersView = CollectionViewSource.GetDefaultView(Users);
            UsersView.Filter = FilterUsers;

            LogsView = CollectionViewSource.GetDefaultView(SystemLogs);
            LogsView.Filter = FilterLogs;

            UpdateCounts();

            // Команди
            SwitchViewCommand = new RelayCommand(SwitchView);

            CreateUserCommand = new RelayCommand(OpenCreateUserWindow);
            EditUserCommand = new RelayCommand(OpenEditUserWindow);
            DeleteUserCommand = new RelayCommand(DeleteUser);
            ToggleUserActiveCommand = new RelayCommand(ToggleUserActive);
            ClearUserFiltersCommand = new RelayCommand(o => { UserSearchText = ""; SelectedDepartmentFilter = null; SelectedStatusFilter = "Всі"; });

            AddDepartmentCommand = new RelayCommand(AddDepartment);
            EditDepartmentCommand = new RelayCommand(EditDepartment);
            DeleteDepartmentCommand = new RelayCommand(DeleteDepartment);

            AddCategoryCommand = new RelayCommand(AddCategory);
            EditCategoryCommand = new RelayCommand(EditCategory);
            DeleteCategoryCommand = new RelayCommand(DeleteCategory);

            GenerateSystemReportCommand = new RelayCommand(GenerateReport);
            BackupCommand = new RelayCommand(BackupDb);
        }

        private void UpdateCounts()
        {
            TotalUsersCount = Users.Count;
            ActiveUsersCount = Users.Count(u => u.IsActive);
            DepartmentsCount = Departments.Count;
            CategoriesCount = Categories.Count;
        }

        private void SwitchView(object parameter)
        {
            if (parameter is string viewName)
            {
                _currentAdminView = viewName;
                OnPropertyChanged(nameof(IsUsersVisible));
                OnPropertyChanged(nameof(IsLogsVisible));
                OnPropertyChanged(nameof(IsStructureVisible));
            }
        }

        // === ФІЛЬТРАЦІЯ ===
        private bool FilterUsers(object obj)
        {
            if (obj is not User user) return false;

            // 1. Пошук (Ім'я, Логін, Посада)
            if (!string.IsNullOrWhiteSpace(UserSearchText))
            {
                var txt = UserSearchText;
                bool match = user.FullName.Contains(txt, StringComparison.OrdinalIgnoreCase) ||
                             user.Username.Contains(txt, StringComparison.OrdinalIgnoreCase) ||
                             user.Position.Name.Contains(txt, StringComparison.OrdinalIgnoreCase);
                if (!match) return false;
            }

            // 2. Відділ
            if (SelectedDepartmentFilter != null && user.DepartmentId != SelectedDepartmentFilter.Id)
                return false;

            // 3. Активність
            if (SelectedStatusFilter == "Активні" && !user.IsActive) return false;
            if (SelectedStatusFilter == "Неактивні" && user.IsActive) return false;

            return true;
        }

        private bool FilterLogs(object obj)
        {
            if (string.IsNullOrWhiteSpace(LogsSearchText)) return true;
            if (obj is AuditLog log)
            {
                return log.Action.Contains(LogsSearchText, StringComparison.OrdinalIgnoreCase) ||
                       (log.User?.Username ?? "").Contains(LogsSearchText, StringComparison.OrdinalIgnoreCase) ||
                       log.Timestamp.ToString().Contains(LogsSearchText);
            }
            return false;
        }

        // === ДІЇ З КОРИСТУВАЧАМИ ===

        // Зміна активності (Чекбокс в таблиці)
        private void ToggleUserActive(object obj)
        {
            if (obj is User u)
            {
                try
                {
                    // Намагаємося змінити. Логіка (true -> false або false -> true)
                    // Тут ми припускаємо, що UI вже змінив властивість IsActive (через Binding TwoWay)
                    // Але краще передати явне значення або перевірити поточне.
                    // У нашому випадку Binding IsActive TwoWay, тому у VM приходить вже змінене значення.

                    // Але є нюанс: якщо сервіс викине помилку, треба повернути значення назад.
                    // Тому краще викликати сервіс, а якщо помилка - відкотити.

                    _adminService.ToggleUserActivity(u.Id, u.IsActive, _currentUser.Id);
                    UpdateCounts();
                }
                catch (Exception ex)
                {
                    // Відкат змін в UI
                    u.IsActive = !u.IsActive;
                    MessageBox.Show(ex.Message, "Заборонено", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UsersView.Refresh(); // Оновити таблицю
                }
            }
        }

        private void OpenCreateUserWindow(object obj)
        {
            var newUser = new User { IsActive = true };
            var win = new EditUserWindow(newUser, _adminService, _currentUser.Id);
            if (win.ShowDialog() == true)
            {
                Users.Clear(); foreach (var u in _adminService.GetAllUsers()) Users.Add(u);
                UpdateCounts();
            }
        }

        private void OpenEditUserWindow(object obj)
        {
            if (obj is User u)
            {
                var win = new EditUserWindow(u, _adminService, _currentUser.Id);
                if (win.ShowDialog() == true) { UsersView.Refresh(); UpdateCounts(); }
            }
        }

        private void DeleteUser(object obj)
        {
            if (obj is User u && MessageBox.Show($"Видалити {u.Username}?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _adminService.DeleteUser(u.Id, _currentUser.Id);
                    Users.Remove(u); UpdateCounts();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // === ВІДДІЛИ ===
        private void AddDepartment(object obj)
        {
            if (!string.IsNullOrWhiteSpace(NewDepartmentName))
            {
                try
                {
                    _adminService.AddDepartment(NewDepartmentName, _currentUser.Id);
                    NewDepartmentName = "";
                    Departments.Clear(); foreach (var d in _adminService.GetAllDepartments()) Departments.Add(d);
                    UpdateCounts();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                var win = new EditNameWindow(""); win.Title = "Новий відділ";
                if (win.ShowDialog() == true)
                {
                    try
                    {
                        _adminService.AddDepartment(win.ResultName, _currentUser.Id);
                        Departments.Clear(); foreach (var d in _adminService.GetAllDepartments()) Departments.Add(d);
                        UpdateCounts();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void EditDepartment(object obj)
        {
            if (obj is Department dep)
            {
                var win = new EditNameWindow(dep.Name);
                if (win.ShowDialog() == true)
                {
                    try
                    {
                        _adminService.EditDepartment(dep.Id, win.ResultName, _currentUser.Id);
                        dep.Name = win.ResultName;
                        Departments.Remove(dep); Departments.Add(dep);
                        Departments.Clear(); foreach (var d in _adminService.GetAllDepartments()) Departments.Add(d);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void DeleteDepartment(object obj)
        {
            if (obj is Department dep && MessageBox.Show($"Видалити відділ '{dep.Name}'?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _adminService.DeleteDepartment(dep.Id, _currentUser.Id);
                    Departments.Remove(dep);
                    UpdateCounts();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // === КАТЕГОРІЇ ===
        private void AddCategory(object obj)
        {
            if (!string.IsNullOrWhiteSpace(NewCategoryName))
            {
                try
                {
                    _adminService.AddCategory(NewCategoryName, _currentUser.Id);
                    NewCategoryName = "";
                    Categories.Clear(); foreach (var c in _adminService.GetAllCategories()) Categories.Add(c);
                    UpdateCounts();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            else
            {
                var win = new EditNameWindow(""); win.Title = "Нова категорія";
                if (win.ShowDialog() == true)
                {
                    try
                    {
                        _adminService.AddCategory(win.ResultName, _currentUser.Id);
                        Categories.Clear(); foreach (var c in _adminService.GetAllCategories()) Categories.Add(c);
                        UpdateCounts();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void EditCategory(object obj)
        {
            if (obj is RequestCategory cat)
            {
                var win = new EditNameWindow(cat.Name);
                if (win.ShowDialog() == true)
                {
                    try
                    {
                        _adminService.EditCategory(cat.Id, win.ResultName, _currentUser.Id);
                        Categories.Clear(); foreach (var c in _adminService.GetAllCategories()) Categories.Add(c);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void DeleteCategory(object obj)
        {
            if (obj is RequestCategory cat && MessageBox.Show($"Видалити категорію '{cat.Name}'?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _adminService.DeleteCategory(cat.Id, _currentUser.Id);
                    Categories.Remove(cat);
                    UpdateCounts();
                }
                catch (Exception ex) { MessageBox.Show("Неможливо видалити категорію, яка використовується."); }
            }
        }

        // === ЗВІТИ ТА БЕКАП ===
        private void GenerateReport(object obj)
        {
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf", FileName = $"System_Log_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var logs = _reportService.GetAdminLogs(DateTime.Now.AddDays(-30), DateTime.Now);
                    var data = new System.Collections.Generic.List<string[]> { new[] { "Time", "User", "Action" } };
                    foreach (var l in logs) data.Add(new[] { l.Timestamp.ToString(), l.User?.Username ?? "-", l.Action });
                    _reportService.ExportToPdf(dialog.FileName, "System Report", data);
                    MessageBox.Show("Звіт створено!");
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void BackupDb(object obj)
        {
            var dialog = new SaveFileDialog { Filter = "Backup|*.bak", FileName = $"Backup_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try { _adminService.BackupDatabase(Path.GetDirectoryName(dialog.FileName), _currentUser.Id); MessageBox.Show("Успіх!"); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }
    }
}
using Microsoft.Win32;
using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class GlobalStatsViewModel : ViewModelBase
    {
        private readonly DirectorService _directorService;
        private readonly ReportService _reportService;
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;

        // Master List: Співробітники
        public ObservableCollection<User> AllEmployees { get; set; }
        private User _selectedEmployee;
        public User SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                _selectedEmployee = value;
                OnPropertyChanged();
                LoadEmployeeDetails();
            }
        }

        // Global Data
        public ObservableCollection<Request> GlobalRequests { get; set; }
        public ICollectionView GlobalRequestsView { get; private set; }

        // Detail Data
        public ObservableCollection<Request> EmployeeRequests { get; set; }
        public ObservableCollection<DepartmentTask> EmployeeTasks { get; set; }

        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }

        public Visibility IsGlobalViewVisible => SelectedEmployee == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsDetailViewVisible => SelectedEmployee != null ? Visibility.Visible : Visibility.Collapsed;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); GlobalRequestsView.Refresh(); }
        }

        public ICommand BackToGlobalCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand GenerateReportCommand { get; }

        public GlobalStatsViewModel(DirectorService directorService, ReportService reportService, EmployeeService employeeService, User currentUser)
        {
            _directorService = directorService;
            _reportService = reportService;
            _employeeService = employeeService;
            _currentUser = currentUser;

            BackToGlobalCommand = new RelayCommand(o => SelectedEmployee = null);
            OpenDetailsCommand = new RelayCommand(OpenDetails);
            RefreshCommand = new RelayCommand(o => LoadGlobalData());
            GenerateReportCommand = new RelayCommand(GenerateReport);

            LoadGlobalData();
        }

        private void LoadGlobalData()
        {
            // Використовуємо AdminService для отримання користувачів, або через репозиторій. 
            // Тут припускаємо, що App.CreateAdminService() доступний, або передаємо сервіс в конструктор.
            // Для спрощення, якщо DirectorService не має методу GetAllUsers, додамо його або використаємо існуючий сервіс.
            var users = App.CreateAdminService().GetAllUsers().Where(u => u.IsActive).ToList();
            AllEmployees = new ObservableCollection<User>(users);
            OnPropertyChanged(nameof(AllEmployees));

            var reqs = _directorService.GetAllRequestsSystemWide().ToList();
            GlobalRequests = new ObservableCollection<Request>(reqs);
            GlobalRequestsView = CollectionViewSource.GetDefaultView(GlobalRequests);
            GlobalRequestsView.Filter = o =>
            {
                if (string.IsNullOrEmpty(SearchText)) return true;
                var r = o as Request;
                return r.Title.ToLower().Contains(SearchText.ToLower()) || r.Author.FullName.ToLower().Contains(SearchText.ToLower());
            };

            TotalCount = reqs.Count;
            CompletedCount = reqs.Count(r => r.GlobalStatus.Name == ServiceConstants.StatusCompleted);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(CompletedCount));
        }

        private void LoadEmployeeDetails()
        {
            if (SelectedEmployee == null) return;

            var reqs = _employeeService.GetMyRequests(SelectedEmployee.Id);
            EmployeeRequests = new ObservableCollection<Request>(reqs);

            // Тут використовуємо ManagerService для отримання тасків.
            var tasks = App.CreateManagerService().GetAllTasksForEmployee(SelectedEmployee.Id);
            EmployeeTasks = new ObservableCollection<DepartmentTask>(tasks);

            OnPropertyChanged(nameof(EmployeeRequests));
            OnPropertyChanged(nameof(EmployeeTasks));
            OnPropertyChanged(nameof(IsGlobalViewVisible));
            OnPropertyChanged(nameof(IsDetailViewVisible));
        }

        private void OpenDetails(object obj)
        {
            Request r = obj as Request ?? (obj as DepartmentTask)?.Request;
            if (r != null)
            {
                var full = _employeeService.GetRequestDetails(r.Id);
                if (full != null) new RequestDetailsWindow(full, _currentUser, _employeeService).ShowDialog();
            }
        }

        private void GenerateReport(object obj)
        {
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf", FileName = $"Director_Report_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Отримуємо дані за останні 30 днів
                    var data = _reportService.GetDirectorReportData(DateTime.Now.AddDays(-30), DateTime.Now);

                    // Формуємо масив рядків для таблиці (3 колонки: ID, Тема, Статус)
                    // Це відповідає структурі таблиці в ReportService.GeneratePdfReport
                    var tableData = data.AllRequests.Select(r => new[]
                    {
                        r.Id.ToString(),
                        r.Title,
                        r.GlobalStatus.Name
                    });

                    _reportService.GeneratePdfReport(dialog.FileName, "Звіт Директора (30 днів)", tableData);
                    MessageBox.Show("Звіт збережено успішно!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка генерації звіту: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
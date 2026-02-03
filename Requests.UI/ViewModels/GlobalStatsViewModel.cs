using Microsoft.Win32;
using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.Generic;
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

        public ObservableCollection<User> AllEmployees { get; set; }
        private User _selectedEmployee;
        public User SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                _selectedEmployee = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGlobalViewVisible));
                OnPropertyChanged(nameof(IsDetailViewVisible));
                LoadEmployeeDetails();
            }
        }

        public ObservableCollection<Request> GlobalRequests { get; set; }
        public ICollectionView GlobalRequestsView { get; private set; }

        public ObservableCollection<Request> EmployeeRequests { get; set; }
        public ObservableCollection<DepartmentTask> EmployeeTasks { get; set; }

        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }

        public Visibility IsGlobalViewVisible => SelectedEmployee == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsDetailViewVisible => SelectedEmployee != null ? Visibility.Visible : Visibility.Collapsed;

        // === ФІЛЬТРИ ===
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); GlobalRequestsView.Refresh(); }
        }

        private DateTime? _filterStartDate;
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set { _filterStartDate = value; OnPropertyChanged(); GlobalRequestsView.Refresh(); }
        }

        private DateTime? _filterEndDate;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set { _filterEndDate = value; OnPropertyChanged(); GlobalRequestsView.Refresh(); }
        }

        public List<string> FilterStatuses { get; } = new List<string>
        {
            "Всі",
            ServiceConstants.StatusNew,
            ServiceConstants.StatusPendingApproval,
            ServiceConstants.StatusClarification,
            ServiceConstants.StatusInProgress,
            ServiceConstants.StatusCompleted,
            ServiceConstants.StatusRejected,
            ServiceConstants.StatusCanceled
        };

        private string _selectedStatusFilter = "Всі";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set { _selectedStatusFilter = value; OnPropertyChanged(); GlobalRequestsView.Refresh(); }
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
            var users = App.CreateAdminService().GetAllUsers().Where(u => u.IsActive).ToList();
            AllEmployees = new ObservableCollection<User>(users);
            OnPropertyChanged(nameof(AllEmployees));

            var reqs = _directorService.GetAllRequestsSystemWide().ToList();
            GlobalRequests = new ObservableCollection<Request>(reqs);

            GlobalRequestsView = CollectionViewSource.GetDefaultView(GlobalRequests);
            GlobalRequestsView.Filter = FilterRequestsLogic;

            TotalCount = reqs.Count;
            CompletedCount = reqs.Count(r => r.GlobalStatus.Name == ServiceConstants.StatusCompleted);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(CompletedCount));
        }

        private bool FilterRequestsLogic(object obj)
        {
            if (obj is not Request r) return false;

            if (!string.IsNullOrEmpty(SearchText))
            {
                var txt = SearchText.ToLower();
                if (!r.Title.ToLower().Contains(txt) && !r.Author.FullName.ToLower().Contains(txt))
                    return false;
            }

            if (FilterStartDate.HasValue && r.CreatedAt.Date < FilterStartDate.Value.Date) return false;
            if (FilterEndDate.HasValue && r.CreatedAt.Date > FilterEndDate.Value.Date) return false;

            if (SelectedStatusFilter != "Всі" && r.GlobalStatus.Name != SelectedStatusFilter) return false;

            return true;
        }

        private void LoadEmployeeDetails()
        {
            if (SelectedEmployee == null) return;

            var reqs = _employeeService.GetMyRequests(SelectedEmployee.Id);
            EmployeeRequests = new ObservableCollection<Request>(reqs);

            var tasks = App.CreateManagerService().GetAllTasksForEmployee(SelectedEmployee.Id);
            EmployeeTasks = new ObservableCollection<DepartmentTask>(tasks);

            OnPropertyChanged(nameof(EmployeeRequests));
            OnPropertyChanged(nameof(EmployeeTasks));
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
            // DOCX/PDF
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf|Word Document|*.docx", FileName = $"Director_Report_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var start = FilterStartDate ?? DateTime.Now.AddDays(-30);
                    var end = FilterEndDate ?? DateTime.Now;

                    // Отримуємо відфільтровані запити з View
                    var filteredRequests = GlobalRequestsView.Cast<Request>();

                    var data = _reportService.GetDirectorReportData(filteredRequests, start, end);

                    if (dialog.FilterIndex == 1)
                        _reportService.GenerateDirectorPdf(dialog.FileName, data);
                    else
                        _reportService.GenerateDirectorDocx(dialog.FileName, data);

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
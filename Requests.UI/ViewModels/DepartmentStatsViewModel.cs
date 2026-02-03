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
    public class DepartmentStatsViewModel : ViewModelBase
    {
        private readonly ManagerService _managerService;
        private readonly ReportService _reportService;
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;

        // Master List
        public ObservableCollection<User> Employees { get; set; }
        private User _selectedEmployee;
        public User SelectedEmployee
        {
            get => _selectedEmployee;
            set { _selectedEmployee = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOverviewVisible)); OnPropertyChanged(nameof(IsDetailVisible)); LoadEmployeeDetails(); }
        }

        // Default Data
        public ObservableCollection<DepartmentTask> AllDepartmentTasks { get; set; }
        public ICollectionView DepartmentTasksView { get; private set; }

        // Details Data
        public ObservableCollection<DepartmentTask> SelectedEmployeeTasks { get; set; }
        public ObservableCollection<Request> SelectedEmployeeRequests { get; set; }

        // Visibility
        public Visibility IsOverviewVisible => SelectedEmployee == null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsDetailVisible => SelectedEmployee != null ? Visibility.Visible : Visibility.Collapsed;

        // Stats
        public int TotalActiveTasks { get; set; }
        public int TotalDoneTasks { get; set; }

        // === ФІЛЬТРИ ===
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); DepartmentTasksView.Refresh(); }
        }

        private DateTime? _filterStartDate;
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set { _filterStartDate = value; OnPropertyChanged(); DepartmentTasksView.Refresh(); }
        }

        private DateTime? _filterEndDate;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set { _filterEndDate = value; OnPropertyChanged(); DepartmentTasksView.Refresh(); }
        }

        public List<string> FilterStatuses { get; } = new List<string>
        {
            "Всі",
            ServiceConstants.TaskStatusNew,
            ServiceConstants.TaskStatusInProgress,
            ServiceConstants.TaskStatusPaused,
            ServiceConstants.TaskStatusDone
        };

        private string _selectedStatusFilter = "Всі";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set { _selectedStatusFilter = value; OnPropertyChanged(); DepartmentTasksView.Refresh(); }
        }

        public ICommand BackCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand GenerateReportCommand { get; }
        public ICommand AssignExecutorCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand ForwardTaskCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        public DepartmentStatsViewModel(ManagerService managerService, ReportService reportService, EmployeeService employeeService, User currentUser)
        {
            _managerService = managerService;
            _reportService = reportService;
            _employeeService = employeeService;
            _currentUser = currentUser;

            // Кнопка назад просто скидає виділеного працівника
            BackCommand = new RelayCommand(o => SelectedEmployee = null);

            OpenDetailsCommand = new RelayCommand(OpenDetails);
            GenerateReportCommand = new RelayCommand(GenerateReport);
            RefreshCommand = new RelayCommand(o => LoadData());

            AssignExecutorCommand = new RelayCommand(AssignExecutor);
            PauseTaskCommand = new RelayCommand(PauseTask);
            ForwardTaskCommand = new RelayCommand(ForwardTask);

            ClearFiltersCommand = new RelayCommand(o => { SearchText = ""; FilterStartDate = null; FilterEndDate = null; SelectedStatusFilter = "Всі"; });


            LoadData();
        }

        private void LoadData()
        {
            var emps = _managerService.GetMyEmployees(_currentUser.DepartmentId).ToList();
            Employees = new ObservableCollection<User>(emps);
            OnPropertyChanged(nameof(Employees));

            var tasks = _managerService.GetAllTasksForDepartment(_currentUser.DepartmentId).ToList();
            AllDepartmentTasks = new ObservableCollection<DepartmentTask>(tasks);

            DepartmentTasksView = CollectionViewSource.GetDefaultView(AllDepartmentTasks);
            DepartmentTasksView.Filter = FilterTasksLogic; // Підключаємо логіку фільтрації

            TotalActiveTasks = tasks.Count(t => t.Status.Name != ServiceConstants.TaskStatusDone);
            TotalDoneTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone);

            OnPropertyChanged(nameof(TotalActiveTasks));
            OnPropertyChanged(nameof(TotalDoneTasks));
        }

        private bool FilterTasksLogic(object obj)
        {
            if (obj is not DepartmentTask task) return false;

            // 1. Пошук (Назва запиту або Виконавець)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var txt = SearchText.ToLower();
                bool matchesTitle = task.Request.Title.ToLower().Contains(txt);
                bool matchesExecutor = task.Executors.Any(e => e.User.FullName.ToLower().Contains(txt));

                if (!matchesTitle && !matchesExecutor) return false;
            }

            // 2. Дата (AssignedAt)
            if (FilterStartDate.HasValue && (!task.AssignedAt.HasValue || task.AssignedAt.Value.Date < FilterStartDate.Value.Date))
                return false;

            if (FilterEndDate.HasValue && (!task.AssignedAt.HasValue || task.AssignedAt.Value.Date > FilterEndDate.Value.Date))
                return false;

            // 3. Статус
            if (SelectedStatusFilter != "Всі" && task.Status.Name != SelectedStatusFilter)
                return false;

            return true;
        }

        private void LoadEmployeeDetails()
        {
            if (SelectedEmployee == null) return;

            var tasks = _managerService.GetAllTasksForEmployee(SelectedEmployee.Id);
            SelectedEmployeeTasks = new ObservableCollection<DepartmentTask>(tasks);

            var reqs = _employeeService.GetMyRequests(SelectedEmployee.Id);
            SelectedEmployeeRequests = new ObservableCollection<Request>(reqs);

            OnPropertyChanged(nameof(SelectedEmployeeTasks));
            OnPropertyChanged(nameof(SelectedEmployeeRequests));
        }

        private void GenerateReport(object obj)
        {
            // ДОДАНО: Формат DOCX у фільтр
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf|Word Document|*.docx", FileName = $"DeptReport_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                var start = FilterStartDate ?? DateTime.Now.AddMonths(-1);
                var end = FilterEndDate ?? DateTime.Now;

                var data = _reportService.GetManagerReportData(_currentUser.DepartmentId, start, end);

                // Вибір методу залежно від розширення
                if (dialog.FilterIndex == 1) // PDF
                {
                    _reportService.GenerateManagerPdf(dialog.FileName, data);
                }
                else // DOCX
                {
                    _reportService.GenerateManagerDocx(dialog.FileName, data);
                }

                MessageBox.Show("Звіт збережено!");
            }
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

        private void PauseTask(object obj)
        {
            if (obj is DepartmentTask t) { _managerService.PauseTask(t.Id, _currentUser.Id); LoadData(); }
        }

        private void ForwardTask(object obj)
        {
            if (obj is DepartmentTask t)
            {
                var win = new SelectDepartmentWindow();
                if (win.ShowDialog() == true) { _managerService.ForwardTask(t.Id, win.SelectedDepartment.Id, _currentUser.Id); LoadData(); }
            }
        }

        private void AssignExecutor(object obj)
        {
            if (obj is DepartmentTask t)
            {
                var win = new SelectUserWindow(Employees);
                if (win.ShowDialog() == true) { _managerService.AssignExecutor(t.Id, win.SelectedUser.Id, _currentUser.Id); LoadData(); }
            }
        }
    }
}
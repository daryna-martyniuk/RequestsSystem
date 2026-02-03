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
            set { _selectedEmployee = value; OnPropertyChanged(); LoadEmployeeDetails(); }
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

        // Filters
        private string _searchText;
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); DepartmentTasksView.Refresh(); } }

        public ICommand BackCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand GenerateReportCommand { get; }
        public ICommand AssignExecutorCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand ForwardTaskCommand { get; }
        public ICommand RefreshCommand { get; }

        // Corrected Constructor (4 arguments)
        public DepartmentStatsViewModel(ManagerService managerService, ReportService reportService, EmployeeService employeeService, User currentUser)
        {
            _managerService = managerService;
            _reportService = reportService;
            _employeeService = employeeService;
            _currentUser = currentUser;

            BackCommand = new RelayCommand(o => SelectedEmployee = null);
            OpenDetailsCommand = new RelayCommand(OpenDetails);
            GenerateReportCommand = new RelayCommand(GenerateReport);
            RefreshCommand = new RelayCommand(o => LoadData());

            AssignExecutorCommand = new RelayCommand(AssignExecutor);
            PauseTaskCommand = new RelayCommand(PauseTask);
            ForwardTaskCommand = new RelayCommand(ForwardTask);

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
            DepartmentTasksView.Filter = o =>
            {
                if (string.IsNullOrEmpty(SearchText)) return true;
                var t = o as DepartmentTask;
                return t.Request.Title.Contains(SearchText);
            };

            TotalActiveTasks = tasks.Count(t => t.Status.Name != ServiceConstants.TaskStatusDone);
            TotalDoneTasks = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone);

            OnPropertyChanged(nameof(TotalActiveTasks));
            OnPropertyChanged(nameof(TotalDoneTasks));
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
            OnPropertyChanged(nameof(IsOverviewVisible));
            OnPropertyChanged(nameof(IsDetailVisible));
        }

        private void GenerateReport(object obj)
        {
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf", FileName = $"DeptReport_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                var data = _reportService.GetManagerReportData(_currentUser.DepartmentId, DateTime.Now.AddMonths(-1), DateTime.Now);
                var pdfData = data.Tasks.Select(t => new[] { t.Request.Title, t.Status.Name, t.AssignedAt?.ToString("d") ?? "-" });
                _reportService.GeneratePdfReport(dialog.FileName, $"Звіт відділу: {_currentUser.Department.Name}", pdfData);
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
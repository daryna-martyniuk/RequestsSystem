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
    public class MyWorkspaceViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly ManagerService _managerService;
        private readonly User _currentUser;
        private readonly ReportService _reportService; // Для звітів

        // === КОЛЕКЦІЇ ===
        public ObservableCollection<Request> MyRequests { get; set; }
        public ObservableCollection<DepartmentTask> MyActiveTasks { get; set; }
        public ObservableCollection<DepartmentTask> MyCompletedTasks { get; set; }

        // Для керівника
        public ObservableCollection<Request> PendingApprovals { get; set; }
        public ObservableCollection<DepartmentTask> IncomingDepartmentTasks { get; set; }
        public ObservableCollection<Request> RequestsInDiscussion { get; set; }
        public ObservableCollection<User> MyEmployees { get; set; } // Підлеглі

        // === VIEWS ДЛЯ ФІЛЬТРАЦІЇ ===
        public ICollectionView MyRequestsView { get; private set; }
        public ICollectionView MyActiveTasksView { get; private set; }
        public ICollectionView IncomingTasksView { get; private set; }

        // === АНАЛІТИКА (Лічильники) ===
        private int _myActiveCount;
        private int _myCompletedCount;
        private int _deptActiveCount;
        private int _deptCriticalCount;

        public int MyActiveCount { get => _myActiveCount; set { _myActiveCount = value; OnPropertyChanged(); } }
        public int MyCompletedCount { get => _myCompletedCount; set { _myCompletedCount = value; OnPropertyChanged(); } }
        public int DeptActiveCount { get => _deptActiveCount; set { _deptActiveCount = value; OnPropertyChanged(); } }
        public int DeptCriticalCount { get => _deptCriticalCount; set { _deptCriticalCount = value; OnPropertyChanged(); } }

        // === ФІЛЬТРИ ===
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public bool IsManager =>
            _currentUser.Position.Name == ServiceConstants.PositionHead ||
            _currentUser.Position.Name == ServiceConstants.PositionDirector ||
            _currentUser.Position.Name == ServiceConstants.PositionDeputyHead;

        public Visibility ManagerVisibility => IsManager ? Visibility.Visible : Visibility.Collapsed;

        // === КОМАНДИ ===
        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand EditRequestCommand { get; }
        public ICommand DeleteRequestCommand { get; }
        public ICommand CancelRequestCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand AssignExecutorCommand { get; }
        public ICommand ForwardTaskCommand { get; }
        public ICommand DiscussTaskCommand { get; }
        public ICommand CompleteTaskCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand ResumeTaskCommand { get; }
        public ICommand DiscussRequestCommand { get; }
        public ICommand FinishDiscussionCommand { get; }

        // Нові команди
        public ICommand GenerateReportCommand { get; }
        public ICommand ViewEmployeeTasksCommand { get; }

        public MyWorkspaceViewModel(User user, EmployeeService service)
        {
            _currentUser = user;
            _employeeService = service;
            _managerService = App.CreateManagerService();
            _reportService = App.CreateReportService();

            // Ініціалізація колекцій
            MyRequests = new ObservableCollection<Request>();
            MyActiveTasks = new ObservableCollection<DepartmentTask>();
            MyCompletedTasks = new ObservableCollection<DepartmentTask>();
            PendingApprovals = new ObservableCollection<Request>();
            IncomingDepartmentTasks = new ObservableCollection<DepartmentTask>();
            RequestsInDiscussion = new ObservableCollection<Request>();
            MyEmployees = new ObservableCollection<User>();

            // Налаштування Views для фільтрації
            MyRequestsView = CollectionViewSource.GetDefaultView(MyRequests);
            MyRequestsView.Filter = FilterRequests;

            MyActiveTasksView = CollectionViewSource.GetDefaultView(MyActiveTasks);
            MyActiveTasksView.Filter = FilterTasks;

            IncomingTasksView = CollectionViewSource.GetDefaultView(IncomingDepartmentTasks);
            IncomingTasksView.Filter = FilterTasks;

            // Ініціалізація команд
            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());
            OpenDetailsCommand = new RelayCommand(OpenDetails);
            EditRequestCommand = new RelayCommand(EditRequest);
            DeleteRequestCommand = new RelayCommand(DeleteRequest);
            CancelRequestCommand = new RelayCommand(CancelRequest);
            ApproveCommand = new RelayCommand(Approve);
            RejectCommand = new RelayCommand(Reject);
            AssignExecutorCommand = new RelayCommand(AssignExecutor);
            ForwardTaskCommand = new RelayCommand(ForwardTask);
            DiscussTaskCommand = new RelayCommand(DiscussTask);
            CompleteTaskCommand = new RelayCommand(CompleteTask);
            PauseTaskCommand = new RelayCommand(PauseTask);
            ResumeTaskCommand = new RelayCommand(ResumeTask);
            DiscussRequestCommand = new RelayCommand(DiscussRequest);
            FinishDiscussionCommand = new RelayCommand(FinishDiscussion);

            GenerateReportCommand = new RelayCommand(GenerateReport);
            ViewEmployeeTasksCommand = new RelayCommand(ViewEmployeeTasks);

            LoadData();
        }

        private void LoadData()
        {
            // 1. Мої запити
            MyRequests.Clear();
            foreach (var r in _employeeService.GetMyRequests(_currentUser.Id)) MyRequests.Add(r);

            // 2. Мої завдання (Розділення + Аналітика)
            MyActiveTasks.Clear();
            MyCompletedTasks.Clear();
            var allTasks = _employeeService.GetMyTasks(_currentUser.Id);

            foreach (var t in allTasks)
            {
                if (t.Status.Name == ServiceConstants.TaskStatusDone) MyCompletedTasks.Add(t);
                else MyActiveTasks.Add(t);
            }

            // Оновлення лічильників (Особисті)
            MyActiveCount = MyActiveTasks.Count;
            MyCompletedCount = MyCompletedTasks.Count;

            // 3. Секція керівника
            if (IsManager)
            {
                PendingApprovals.Clear();
                foreach (var a in _managerService.GetPendingApprovals(_currentUser.DepartmentId)) PendingApprovals.Add(a);

                IncomingDepartmentTasks.Clear();
                var incoming = _managerService.GetIncomingTasks(_currentUser.DepartmentId);
                foreach (var t in incoming) IncomingDepartmentTasks.Add(t);

                RequestsInDiscussion.Clear();
                foreach (var d in _managerService.GetRequestsInDiscussion()) RequestsInDiscussion.Add(d);

                // Завантаження команди
                MyEmployees.Clear();
                foreach (var emp in _managerService.GetMyEmployees(_currentUser.DepartmentId)) MyEmployees.Add(emp);

                // Оновлення лічильників (Відділ)
                DeptActiveCount = IncomingDepartmentTasks.Count; // Це тільки нові, можна додати логіку для "В роботі"
                DeptCriticalCount = incoming.Count(t => t.Request.Priority.Name == ServiceConstants.PriorityCritical);
            }

            // Оновлення фільтрів
            MyRequestsView.Refresh();
            MyActiveTasksView.Refresh();
            IncomingTasksView.Refresh();
        }

        // === ЛОГІКА ФІЛЬТРАЦІЇ ===
        private void ApplyFilters()
        {
            MyRequestsView.Refresh();
            MyActiveTasksView.Refresh();
            IncomingTasksView.Refresh();
        }

        private bool FilterRequests(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is Request req)
            {
                return req.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       req.Category.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       req.GlobalStatus.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private bool FilterTasks(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is DepartmentTask task)
            {
                return task.Request.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       task.Request.Author.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       task.Request.Priority.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        // === ЗВІТИ ТА КОМАНДА ===

        private void GenerateReport(object obj)
        {
            // Простий приклад генерації PDF звіту за останні 30 днів
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf", FileName = $"Report_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = _reportService.GetManagerReportData(_currentUser.DepartmentId, DateTime.Now.AddDays(-30), DateTime.Now);

                    var reportRows = new List<string[]>
                    {
                        new[] { "ID", "Тема", "Виконавець", "Статус", "Дата" }
                    };

                    foreach (var t in data.Tasks)
                    {
                        string executor = t.Executors.FirstOrDefault()?.User.FullName ?? "Не призначено";
                        reportRows.Add(new[] { t.RequestId.ToString(), t.Request.Title, executor, t.Status.Name, t.AssignedAt?.ToString("dd.MM.yyyy") ?? "-" });
                    }

                    _reportService.ExportToPdf(dialog.FileName, $"Звіт відділу: {_currentUser.Department.Name}", reportRows);
                    MessageBox.Show("Звіт успішно збережено!");
                }
                catch (Exception ex) { MessageBox.Show("Помилка генерації звіту: " + ex.Message); }
            }
        }

        private void ViewEmployeeTasks(object obj)
        {
            if (obj is User emp)
            {
                // Тут можна відкрити окреме вікно або відфільтрувати список
                // Для простоти покажемо повідомлення з інформацією
                var tasks = _employeeService.GetMyTasks(emp.Id);
                int active = tasks.Count(t => t.Status.Name != ServiceConstants.TaskStatusDone);
                int done = tasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone);

                MessageBox.Show($"Співробітник: {emp.FullName}\n" +
                                $"Активних завдань: {active}\n" +
                                $"Виконаних завдань: {done}",
                                "Інформація про співробітника");
            }
        }

        // ... [Усі інші методи: FinishDiscussion, DiscussRequest, CompleteTask, PauseTask, ResumeTask, OpenDetails, AssignExecutor, ForwardTask, DiscussTask, Approve, Reject, CreateRequest, EditRequest, DeleteRequest, CancelRequest] залишаються без змін, скопіюйте їх з попереднього файлу ...

        // КОПІЯ МЕТОДІВ З ПОПЕРЕДНЬОЇ ВЕРСІЇ (Щоб файл був повним):
        private void FinishDiscussion(object obj)
        {
            if (obj is Request req)
            {
                var fullReq = _employeeService.GetRequestDetails(req.Id);
                if (fullReq == null) return;
                var editWindow = new CreateRequestWindow(_currentUser, _employeeService, fullReq);
                if (editWindow.ShowDialog() == true)
                {
                    var commentDialog = new EditNameWindow("");
                    commentDialog.Title = "Підсумок обговорення";
                    if (commentDialog.ShowDialog() == true)
                    {
                        try
                        {
                            var updatedReq = _employeeService.GetRequestDetails(req.Id);
                            _managerService.ApproveRequest(req.Id, _currentUser.Id, updatedReq, ServiceConstants.StatusNew, commentDialog.ResultName);
                            MessageBox.Show("Обговорення завершено.");
                            LoadData();
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }
        private void DiscussRequest(object obj) { if (obj is Request r) { var d = new EditNameWindow(""); d.Title = "Причина"; if (d.ShowDialog() == true) { _managerService.SetRequestToDiscussion(r.Id, _currentUser.Id, d.ResultName); MessageBox.Show("На обговоренні"); LoadData(); } } }
        private void CompleteTask(object obj) { if (obj is DepartmentTask t && MessageBox.Show("Виконано?", "Так", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _employeeService.UpdateTaskStatus(t.Id, _currentUser.Id, ServiceConstants.TaskStatusDone); LoadData(); } }
        private void PauseTask(object obj) { if (obj is DepartmentTask t) { _employeeService.PauseTask(t.Id, _currentUser.Id); LoadData(); } }
        private void ResumeTask(object obj) { if (obj is DepartmentTask t) { _employeeService.ResumeTask(t.Id, _currentUser.Id); LoadData(); } }
        private void OpenDetails(object obj) { Request r = obj as Request ?? (obj as DepartmentTask)?.Request; if (r != null) { var f = _employeeService.GetRequestDetails(r.Id); if (f != null) new RequestDetailsWindow(f, _currentUser, _employeeService).ShowDialog(); LoadData(); } }
        private void AssignExecutor(object obj) { if (obj is DepartmentTask t) { var d = new SelectUserWindow(_managerService.GetMyEmployees(_currentUser.DepartmentId)); if (d.ShowDialog() == true) { _managerService.AssignExecutor(t.Id, d.SelectedUser.Id, _currentUser.Id); LoadData(); } } }
        private void ForwardTask(object obj) { if (obj is DepartmentTask t) { var d = new SelectDepartmentWindow(); if (d.ShowDialog() == true) { if (d.SelectedDepartment.Id == _currentUser.DepartmentId) return; _managerService.ForwardTask(t.Id, d.SelectedDepartment.Id, _currentUser.Id); LoadData(); } } }
        private void DiscussTask(object obj) { if (obj is DepartmentTask t) { var d = new EditNameWindow(""); if (d.ShowDialog() == true) { _managerService.SetRequestToDiscussion(t.RequestId, _currentUser.Id, d.ResultName); LoadData(); } } }
        private void Approve(object obj) { if (obj is Request r) { _managerService.ApproveRequest(r.Id, _currentUser.Id, r); LoadData(); } }
        private void Reject(object obj) { if (obj is Request r && MessageBox.Show("Reject?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _managerService.RejectRequest(r.Id, _currentUser.Id, "Rejected"); LoadData(); } }
        private void CreateRequest(object obj) { new CreateRequestWindow(_currentUser, _employeeService).ShowDialog(); LoadData(); }
        private void EditRequest(object obj) { if (obj is Request r) { var f = _employeeService.GetRequestDetails(r.Id); if (f != null) new CreateRequestWindow(_currentUser, _employeeService, f).ShowDialog(); LoadData(); } }
        private void DeleteRequest(object obj) { if (obj is Request r && MessageBox.Show("Delete?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _employeeService.DeleteRequest(r.Id, _currentUser.Id); LoadData(); } }
        private void CancelRequest(object obj) { if (obj is Request r && MessageBox.Show("Cancel?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _employeeService.CancelRequest(r.Id, _currentUser.Id); LoadData(); } }
    }
}
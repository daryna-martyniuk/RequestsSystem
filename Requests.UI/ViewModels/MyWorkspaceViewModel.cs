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
    public class MyWorkspaceViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly ManagerService _managerService;
        private readonly ReportService _reportService;
        private readonly User _currentUser;

        // === КОЛЕКЦІЇ ===
        public ObservableCollection<Request> MyRequests { get; set; }

        // Мої завдання
        public ObservableCollection<DepartmentTask> MyActiveTasks { get; set; }
        public ObservableCollection<DepartmentTask> MyCompletedTasks { get; set; }

        // Керівник: Оперативні списки
        public ObservableCollection<Request> PendingApprovals { get; set; }
        public ObservableCollection<DepartmentTask> IncomingDepartmentTasks { get; set; }
        public ObservableCollection<Request> RequestsInDiscussion { get; set; }

        // Керівник: Аналітика (Всі таски відділу)
        public ObservableCollection<DepartmentTask> AllDepartmentTasks { get; set; }

        // === VIEWS ДЛЯ ФІЛЬТРАЦІЇ ===
        public ICollectionView PendingApprovalsView { get; private set; }
        public ICollectionView IncomingTasksView { get; private set; }
        public ICollectionView AllDepartmentTasksView { get; private set; } // Для аналітики

        // === ФІЛЬТРИ ===
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private DateTime? _filterStartDate;
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set { _filterStartDate = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private DateTime? _filterEndDate;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set { _filterEndDate = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _filterStatus = "Всі";
        public string FilterStatus
        {
            get => _filterStatus;
            set { _filterStatus = value; OnPropertyChanged(); ApplyFilters(); }
        }

        public ObservableCollection<string> StatusOptions { get; } = new ObservableCollection<string>
        {
            "Всі", "Новий", "В роботі", "На паузі", "Виконано", "Очікує погодження", "На уточненні"
        };

        // === СТАТИСТИКА ===
        public int ActiveTasksCount => AllDepartmentTasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusInProgress || t.Status.Name == ServiceConstants.TaskStatusNew);
        public int CompletedTasksCount => AllDepartmentTasks.Count(t => t.Status.Name == ServiceConstants.TaskStatusDone);
        public int CriticalTasksCount => AllDepartmentTasks.Count(t => t.Request.Priority.Name == ServiceConstants.PriorityCritical && t.Status.Name != ServiceConstants.TaskStatusDone);

        // Властивості
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
        public ICommand ClearFiltersCommand { get; }
        public ICommand GenerateReportCommand { get; }

        public MyWorkspaceViewModel(User user, EmployeeService service)
        {
            _currentUser = user;
            _employeeService = service;
            _managerService = App.CreateManagerService();
            _reportService = App.CreateReportService();

            // Ініціалізація
            MyRequests = new ObservableCollection<Request>();
            MyActiveTasks = new ObservableCollection<DepartmentTask>();
            MyCompletedTasks = new ObservableCollection<DepartmentTask>();
            PendingApprovals = new ObservableCollection<Request>();
            IncomingDepartmentTasks = new ObservableCollection<DepartmentTask>();
            RequestsInDiscussion = new ObservableCollection<Request>();
            AllDepartmentTasks = new ObservableCollection<DepartmentTask>();

            // Налаштування Views
            PendingApprovalsView = CollectionViewSource.GetDefaultView(PendingApprovals);
            PendingApprovalsView.Filter = FilterRequestsCommon;

            IncomingTasksView = CollectionViewSource.GetDefaultView(IncomingDepartmentTasks);
            IncomingTasksView.Filter = FilterTasksCommon;

            AllDepartmentTasksView = CollectionViewSource.GetDefaultView(AllDepartmentTasks);
            AllDepartmentTasksView.Filter = FilterTasksCommon; // Використовуємо той самий фільтр

            // Команди
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

            ClearFiltersCommand = new RelayCommand(o => { SearchText = ""; FilterStartDate = null; FilterEndDate = null; FilterStatus = "Всі"; });
            GenerateReportCommand = new RelayCommand(GenerateReport);

            LoadData();
        }

        private void LoadData()
        {
            // Дані співробітника
            MyRequests.Clear();
            foreach (var r in _employeeService.GetMyRequests(_currentUser.Id)) MyRequests.Add(r);

            MyActiveTasks.Clear();
            MyCompletedTasks.Clear();
            var allMyTasks = _employeeService.GetMyTasks(_currentUser.Id);
            foreach (var t in allMyTasks)
            {
                if (t.Status.Name == ServiceConstants.TaskStatusDone) MyCompletedTasks.Add(t);
                else MyActiveTasks.Add(t);
            }

            // Дані керівника
            if (IsManager)
            {
                PendingApprovals.Clear();
                foreach (var a in _managerService.GetPendingApprovals(_currentUser.DepartmentId)) PendingApprovals.Add(a);

                IncomingDepartmentTasks.Clear();
                foreach (var t in _managerService.GetIncomingTasks(_currentUser.DepartmentId)) IncomingDepartmentTasks.Add(t);

                RequestsInDiscussion.Clear();
                foreach (var d in _managerService.GetRequestsInDiscussion(_currentUser.DepartmentId)) RequestsInDiscussion.Add(d);

                // АНАЛІТИКА: Всі таски відділу
                AllDepartmentTasks.Clear();
                var deptTasks = _managerService.GetAllTasksForDepartment(_currentUser.DepartmentId);
                foreach (var t in deptTasks) AllDepartmentTasks.Add(t);

                // Оновлення статистики
                OnPropertyChanged(nameof(ActiveTasksCount));
                OnPropertyChanged(nameof(CompletedTasksCount));
                OnPropertyChanged(nameof(CriticalTasksCount));
            }

            ApplyFilters();
        }

        // === ФІЛЬТРАЦІЯ ===
        private void ApplyFilters()
        {
            PendingApprovalsView.Refresh();
            IncomingTasksView.Refresh();
            AllDepartmentTasksView.Refresh();
        }

        // Фільтр для Запитів (Request)
        private bool FilterRequestsCommon(object obj)
        {
            if (obj is not Request req) return false;

            // 1. Пошук
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var txt = SearchText.ToLower();
                bool match = req.Title.ToLower().Contains(txt) ||
                             req.Author.FullName.ToLower().Contains(txt);
                if (!match) return false;
            }
            // 2. Дати
            if (FilterStartDate.HasValue && req.CreatedAt.Date < FilterStartDate.Value.Date) return false;
            if (FilterEndDate.HasValue && req.CreatedAt.Date > FilterEndDate.Value.Date) return false;

            return true;
        }

        // Фільтр для Завдань (DepartmentTask) - ВИПРАВЛЕНО
        private bool FilterTasksCommon(object obj)
        {
            if (obj is not DepartmentTask task) return false;

            // 1. Пошук (з безпечною перевіркою на null)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var txt = SearchText.ToLower();

                // Використовуємо ?. для перевірки на null
                string title = task.Request?.Title ?? "";
                string author = task.Request?.Author?.FullName ?? "";
                string executor = task.Executors.FirstOrDefault()?.User?.FullName ?? "";

                bool match = title.ToLower().Contains(txt) ||
                             author.ToLower().Contains(txt) ||
                             executor.ToLower().Contains(txt);

                if (!match) return false;
            }

            // 2. Статус (Фільтр по комбобоксу)
            if (FilterStatus != "Всі" && task.Status?.Name != FilterStatus) return false;

            // 3. Дати
            if (FilterStartDate.HasValue && task.AssignedAt?.Date < FilterStartDate.Value.Date) return false;
            if (FilterEndDate.HasValue && task.AssignedAt?.Date > FilterEndDate.Value.Date) return false;

            return true;
        }

        // === ГЕНЕРАЦІЯ ЗВІТУ ===
        private void GenerateReport(object obj)
        {
            var dialog = new SaveFileDialog { Filter = "PDF Report|*.pdf", FileName = $"Report_{_currentUser.Department.Name}_{DateTime.Now:yyyyMMdd}" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Генеруємо звіт на основі поточних відфільтрованих даних у AllDepartmentTasksView
                    var tasks = AllDepartmentTasksView.Cast<DepartmentTask>().ToList();

                    var reportData = new System.Collections.Generic.List<string[]>();
                    reportData.Add(new[] { "ID", "Тема", "Виконавець", "Статус", "Дата", "Дедлайн" });

                    foreach (var t in tasks)
                    {
                        string executor = t.Executors.FirstOrDefault()?.User.FullName ?? "Не призначено";
                        reportData.Add(new[]
                        {
                            t.RequestId.ToString(),
                            t.Request?.Title ?? "Без назви", // Безпечний доступ
                            executor,
                            t.Status?.Name ?? "Невідомо",
                            t.AssignedAt?.ToString("dd.MM.yyyy") ?? "-",
                            t.Request?.Deadline?.ToString("dd.MM.yyyy") ?? "-"
                        });
                    }

                    _reportService.GeneratePdfReport(dialog.FileName, $"Аналітика відділу: {_currentUser.Department.Name}", reportData);
                    MessageBox.Show("Звіт збережено!");
                }
                catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
            }
        }

        // ... [Інші методи без змін] ...
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
                            _managerService.ApproveRequest(req.Id, _currentUser.Id, updatedReq); // Виправлено виклик
                            MessageBox.Show("Обговорення завершено.");
                            LoadData();
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }
        private void DiscussRequest(object obj) { if (obj is Request r) { var d = new EditNameWindow(""); d.Title = "Причина"; if (d.ShowDialog() == true) { _managerService.SetRequestToDiscussion(r.Id, _currentUser.Id, d.ResultName); LoadData(); } } }
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
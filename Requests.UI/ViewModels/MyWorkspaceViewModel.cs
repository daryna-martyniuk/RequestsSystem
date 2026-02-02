using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class MyWorkspaceViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly ManagerService _managerService;
        private readonly User _currentUser;

        // Колекції
        public ObservableCollection<Request> MyRequests { get; set; }
        public ObservableCollection<DepartmentTask> MyActiveTasks { get; set; }
        public ObservableCollection<DepartmentTask> MyCompletedTasks { get; set; }

        public ObservableCollection<Request> PendingApprovals { get; set; }
        public ObservableCollection<DepartmentTask> IncomingDepartmentTasks { get; set; }
        public ObservableCollection<Request> RequestsInDiscussion { get; set; } // Колекція для обговорень

        public bool IsManager =>
            _currentUser.Position.Name == ServiceConstants.PositionHead ||
            _currentUser.Position.Name == ServiceConstants.PositionDirector ||
            _currentUser.Position.Name == ServiceConstants.PositionDeputyHead;

        public Visibility ManagerVisibility => IsManager ? Visibility.Visible : Visibility.Collapsed;

        // Команди
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

        public MyWorkspaceViewModel(User user, EmployeeService service)
        {
            _currentUser = user;
            _employeeService = service;
            _managerService = App.CreateManagerService();

            MyRequests = new ObservableCollection<Request>();
            MyActiveTasks = new ObservableCollection<DepartmentTask>();
            MyCompletedTasks = new ObservableCollection<DepartmentTask>();
            PendingApprovals = new ObservableCollection<Request>();
            IncomingDepartmentTasks = new ObservableCollection<DepartmentTask>();
            RequestsInDiscussion = new ObservableCollection<Request>();

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

            LoadData();
        }

        private void LoadData()
        {
            MyRequests.Clear();
            foreach (var r in _employeeService.GetMyRequests(_currentUser.Id)) MyRequests.Add(r);

            MyActiveTasks.Clear();
            MyCompletedTasks.Clear();
            var allTasks = _employeeService.GetMyTasks(_currentUser.Id);
            foreach (var t in allTasks)
            {
                if (t.Status.Name == ServiceConstants.TaskStatusDone) MyCompletedTasks.Add(t);
                else MyActiveTasks.Add(t);
            }

            if (IsManager)
            {
                PendingApprovals.Clear();
                foreach (var a in _managerService.GetPendingApprovals(_currentUser.DepartmentId)) PendingApprovals.Add(a);

                IncomingDepartmentTasks.Clear();
                foreach (var t in _managerService.GetIncomingTasks(_currentUser.DepartmentId)) IncomingDepartmentTasks.Add(t);

                // Заповнюємо список "На обговоренні"
                RequestsInDiscussion.Clear();
                foreach (var d in _managerService.GetRequestsInDiscussion()) RequestsInDiscussion.Add(d);
            }
        }

        // === ЛОГІКА ЗАВЕРШЕННЯ ОБГОВОРЕННЯ ===
        private void FinishDiscussion(object obj)
        {
            if (obj is Request req)
            {
                // 1. Відкриваємо вікно редагування (з повними даними)
                var fullReq = _employeeService.GetRequestDetails(req.Id);
                if (fullReq == null) return;

                var editWindow = new CreateRequestWindow(_currentUser, _employeeService, fullReq);

                // Якщо користувач натиснув "Зберегти" у вікні редагування
                if (editWindow.ShowDialog() == true)
                {
                    // 2. Просимо написати підсумок
                    var commentDialog = new EditNameWindow("");
                    commentDialog.Title = "Підсумок обговорення (обов'язково)";

                    if (commentDialog.ShowDialog() == true)
                    {
                        try
                        {
                            // Отримуємо оновлений запит (бо CreateRequestWindow вже зберіг зміни в БД)
                            var updatedReq = _employeeService.GetRequestDetails(req.Id);

                            // 3. Завершуємо: статус -> "Новий", додаємо коментар
                            _managerService.ApproveRequest(req.Id, _currentUser.Id, updatedReq, ServiceConstants.StatusNew, commentDialog.ResultName);

                            MessageBox.Show("Обговорення завершено! Запит повернуто в роботу.");
                            LoadData();
                        }
                        catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                    }
                }
            }
        }

        private void DiscussRequest(object obj)
        {
            if (obj is Request req)
            {
                var dialog = new EditNameWindow(""); dialog.Title = "Причина уточнення";
                if (dialog.ShowDialog() == true)
                {
                    _managerService.SetRequestToDiscussion(req.Id, _currentUser.Id, dialog.ResultName);
                    MessageBox.Show("Запит винесено на обговорення.");
                    LoadData();
                }
            }
        }

        // ... Інші методи (CompleteTask, PauseTask і т.д.) ...
        private void CompleteTask(object obj)
        {
            if (obj is DepartmentTask task && MessageBox.Show("Виконано?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _employeeService.UpdateTaskStatus(task.Id, _currentUser.Id, ServiceConstants.TaskStatusDone);
                LoadData();
            }
        }
        private void PauseTask(object obj) { if (obj is DepartmentTask task) { _employeeService.PauseTask(task.Id, _currentUser.Id); LoadData(); } }
        private void ResumeTask(object obj) { if (obj is DepartmentTask task) { _employeeService.ResumeTask(task.Id, _currentUser.Id); LoadData(); } }

        private void OpenDetails(object obj)
        {
            Request r = obj as Request ?? (obj as DepartmentTask)?.Request;
            if (r != null)
            {
                var full = _employeeService.GetRequestDetails(r.Id);
                if (full != null) { new RequestDetailsWindow(full, _currentUser, _employeeService).ShowDialog(); LoadData(); }
            }
        }

        private void AssignExecutor(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var emp = _managerService.GetMyEmployees(_currentUser.DepartmentId);
                var dlg = new SelectUserWindow(emp);
                if (dlg.ShowDialog() == true)
                {
                    _managerService.AssignExecutor(task.Id, dlg.SelectedUser.Id, _currentUser.Id); LoadData();
                }
            }
        }

        private void ForwardTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var dlg = new SelectDepartmentWindow();
                if (dlg.ShowDialog() == true)
                {
                    if (dlg.SelectedDepartment.Id == _currentUser.DepartmentId) { MessageBox.Show("Помилка"); return; }
                    _managerService.ForwardTask(task.Id, dlg.SelectedDepartment.Id, _currentUser.Id); LoadData();
                }
            }
        }

        private void DiscussTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var dlg = new EditNameWindow(""); dlg.Title = "Коментар";
                if (dlg.ShowDialog() == true)
                {
                    _managerService.SetRequestToDiscussion(task.RequestId, _currentUser.Id, dlg.ResultName); LoadData();
                }
            }
        }
        private void Approve(object obj)
        {
            if (obj is Request req)
            {
                try { _managerService.ApproveRequest(req.Id, _currentUser.Id, req); MessageBox.Show("Погоджено!"); LoadData(); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void Reject(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Відхилити?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _managerService.RejectRequest(req.Id, _currentUser.Id, "Відхилено"); LoadData();
                }
            }
        }

        private void CreateRequest(object obj) { new CreateRequestWindow(_currentUser, _employeeService).ShowDialog(); LoadData(); }

        private void EditRequest(object obj)
        {
            if (obj is Request req)
            {
                var full = _employeeService.GetRequestDetails(req.Id);
                if (full != null) { new CreateRequestWindow(_currentUser, _employeeService, full).ShowDialog(); LoadData(); }
            }
        }

        private void DeleteRequest(object obj)
        {
            if (obj is Request req && MessageBox.Show("Видалити?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _employeeService.DeleteRequest(req.Id, _currentUser.Id); LoadData(); }
        }

        private void CancelRequest(object obj)
        {
            if (obj is Request req && MessageBox.Show("Скасувати?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _employeeService.CancelRequest(req.Id, _currentUser.Id); LoadData(); }
        }
    }
}
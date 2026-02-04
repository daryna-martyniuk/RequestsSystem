using Microsoft.Win32;
using Requests.Data;
using Requests.Data.Models;
using Requests.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic; // Added for List<int>

namespace Requests.UI.ViewModels
{
    public class SelectableDepartment : ViewModelBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    public class CreateRequestViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;
        private readonly Action<bool> _closeAction;

        // Для режиму редагування
        private readonly Request _existingRequest;
        public bool IsEditMode => _existingRequest != null;
        public string WindowTitle => IsEditMode ? $"Редагування запиту #{_existingRequest.Id}" : "Новий запит";
        public string SaveButtonText => IsEditMode ? "Зберегти зміни" : "Створити запит";

        public bool CanEditCoreFields { get; private set; } = true;

        // Властивості форми
        private string _title;
        private string _description;
        private RequestPriority _selectedPriority;
        private RequestCategory _selectedCategory;
        private DateTime _deadline = DateTime.Now.AddDays(3);

        // Вкладення
        private string _attachmentName;
        private byte[] _attachmentData;
        public bool HasAttachment => !string.IsNullOrEmpty(_attachmentName);

        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public DateTime Deadline { get => _deadline; set { _deadline = value; OnPropertyChanged(); } }

        public RequestPriority SelectedPriority { get => _selectedPriority; set { _selectedPriority = value; OnPropertyChanged(); } }
        public RequestCategory SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        public string AttachmentName { get => _attachmentName; set { _attachmentName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAttachment)); } }

        // Колекції для вибору
        public ObservableCollection<RequestPriority> Priorities { get; set; }
        public ObservableCollection<RequestCategory> Categories { get; set; }
        public ObservableCollection<SelectableDepartment> Departments { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand RemoveFileCommand { get; }

        public CreateRequestViewModel(User currentUser, EmployeeService service, Action<bool> closeAction, Request requestToEdit = null)
        {
            _currentUser = currentUser;
            _employeeService = service;
            _closeAction = closeAction;
            _existingRequest = requestToEdit;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            AttachFileCommand = new RelayCommand(AttachFile);
            RemoveFileCommand = new RelayCommand(RemoveFile);

            LoadData();

            if (IsEditMode)
            {
                InitializeFromRequest(_existingRequest);
            }
        }

        private void LoadData()
        {
            // Отримуємо довідники через адмін сервіс або напряму з контексту (тут спрощено через new context для прикладу, 
            // але краще передавати сервіси довідників. Припустимо, вони завантажуються тут).
            using (var ctx = new AppDbContext())
            {
                Priorities = new ObservableCollection<RequestPriority>(ctx.RequestPriorities.ToList());
                Categories = new ObservableCollection<RequestCategory>(ctx.RequestCategories.ToList());
                Departments = new ObservableCollection<SelectableDepartment>(
                    ctx.Departments.Select(d => new SelectableDepartment { Id = d.Id, Name = d.Name }).ToList()
                );
            }

            // Дефолтні значення
            if (Priorities.Any()) SelectedPriority = Priorities.First();
            if (Categories.Any()) SelectedCategory = Categories.First();
        }

        private void InitializeFromRequest(Request req)
        {
            Title = req.Title;
            Description = req.Description;
            Deadline = req.Deadline ?? DateTime.Now;

            SelectedPriority = Priorities.FirstOrDefault(p => p.Id == req.PriorityId);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == req.CategoryId);

            // Відмічаємо відділи
            if (req.DepartmentTasks != null)
            {
                var assignedDeptIds = req.DepartmentTasks.Select(t => t.DepartmentId).ToList();
                foreach (var dept in Departments)
                {
                    if (assignedDeptIds.Contains(dept.Id)) dept.IsSelected = true;
                }
            }

            // Блокування редагування для певних статусів (опціонально)
            if (req.GlobalStatus != null &&
                req.GlobalStatus.Name != ServiceConstants.StatusPendingApproval &&
                req.GlobalStatus.Name != ServiceConstants.StatusClarification &&
                req.GlobalStatus.Name != ServiceConstants.StatusNew)
            {
                CanEditCoreFields = false;
                OnPropertyChanged(nameof(CanEditCoreFields));
            }
        }

        public void SetAttachment(string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > 10 * 1024 * 1024) // 10 MB
                {
                    MessageBox.Show("Файл завеликий! Максимум 10 МБ.");
                    return;
                }

                AttachmentName = info.Name;
                _attachmentData = File.ReadAllBytes(filePath);
            }
            catch (Exception ex) { MessageBox.Show("Помилка файлу: " + ex.Message); }
        }

        private void AttachFile(object obj)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                SetAttachment(dialog.FileName);
            }
        }

        private void RemoveFile(object obj)
        {
            AttachmentName = null;
            _attachmentData = null;
        }

        private void Save(object obj)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                MessageBox.Show("Введіть тему запиту!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Збираємо вибрані відділи
                var targetDeptIds = Departments.Where(d => d.IsSelected).Select(d => d.Id).ToList();

                if (IsEditMode)
                {
                    // === РЕДАГУВАННЯ ===
                    // ВАЖЛИВО: Оновлюємо об'єкт _existingRequest даними з форми
                    var updatedInfo = new Request
                    {
                        Title = Title,
                        Description = Description,
                        PriorityId = SelectedPriority.Id,
                        CategoryId = SelectedCategory.Id,
                        Deadline = Deadline
                    };


                    _employeeService.UpdateRequest(_existingRequest, updatedInfo, _currentUser.Id, targetDeptIds);

                    // Якщо є нове вкладення - додаємо
                    if (_attachmentData != null)
                        _employeeService.AddAttachment(_existingRequest.Id, AttachmentName, _attachmentData, _currentUser.Id);

                    MessageBox.Show("Зміни збережено!");
                }
                else
                {
                    // === СТВОРЕННЯ ===
                    var req = new Request
                    {
                        Title = Title,
                        Description = Description,
                        PriorityId = SelectedPriority.Id,
                        CategoryId = SelectedCategory.Id,
                        Deadline = Deadline
                        // AuthorId is set inside CreateRequest
                    };

                    // Call the correct overload: CreateRequest(Request request, User author, List<int> targetDepartmentIds)
                    _employeeService.CreateRequest(req, _currentUser, targetDeptIds);

                    // Handle Attachment separately since the new CreateRequest doesn't take attachment args
                    if (_attachmentData != null)
                    {
                        // req.Id should be populated by EF Core after CreateRequest executes SaveChanges
                        _employeeService.AddAttachment(req.Id, AttachmentName, _attachmentData, _currentUser.Id);
                    }

                    MessageBox.Show("Запит створено!");
                }

                _closeAction(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel(object obj) => _closeAction(false);
    }
}
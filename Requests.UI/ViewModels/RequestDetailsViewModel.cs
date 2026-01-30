using Microsoft.Win32;
using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class RequestDetailsViewModel : ViewModelBase
    {
        private readonly Request _request;
        private readonly User _currentUser;
        private readonly EmployeeService _service;
        private readonly Action _closeAction;
        private string _newCommentText;

        public Request Request => _request;
        public ObservableCollection<RequestComment> Comments { get; set; }
        public ObservableCollection<DepartmentTask> Tasks { get; set; }
        public ObservableCollection<RequestAttachment> Attachments { get; set; }

        public string NewCommentText
        {
            get => _newCommentText;
            set { _newCommentText = value; OnPropertyChanged(); }
        }

        // Властивості для керування видимістю кнопок
        public bool IsAuthor => _request.AuthorId == _currentUser.Id;
        public bool CanDelete => IsAuthor && _request.GlobalStatus.Name == ServiceConstants.StatusPendingApproval;
        public bool CanCancel => IsAuthor && (_request.GlobalStatus.Name == ServiceConstants.StatusInProgress || _request.GlobalStatus.Name == ServiceConstants.StatusClarification || _request.GlobalStatus.Name == ServiceConstants.StatusNew);
        public bool CanEdit => IsAuthor && (_request.GlobalStatus.Name == ServiceConstants.StatusPendingApproval || _request.GlobalStatus.Name == ServiceConstants.StatusNew);

        public ICommand AddCommentCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CloseCommand { get; }

        // Нові команди
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }

        public RequestDetailsViewModel(Request request, User currentUser, EmployeeService service, Action closeAction)
        {
            _request = request;
            _currentUser = currentUser;
            _service = service;
            _closeAction = closeAction;

            Comments = new ObservableCollection<RequestComment>(request.Comments);
            Tasks = new ObservableCollection<DepartmentTask>(request.DepartmentTasks);
            Attachments = new ObservableCollection<RequestAttachment>(request.Attachments);

            AddCommentCommand = new RelayCommand(AddComment, o => !string.IsNullOrWhiteSpace(NewCommentText));
            DownloadFileCommand = new RelayCommand(DownloadFile);
            CloseCommand = new RelayCommand(o => closeAction());

            EditCommand = new RelayCommand(Edit);
            DeleteCommand = new RelayCommand(Delete);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Edit(object obj)
        {
            // Відкриваємо вікно редагування (те саме, що й для створення, але з переданим запитом)
            // Примітка: Нам потрібен доступ до CreateRequestWindow звідси. 
            // У MVVM іноді використовують сервіс діалогів, але ми зробимо прямо.

            var editWindow = new CreateRequestWindow(_currentUser, _service, _request);
            if (editWindow.ShowDialog() == true)
            {
                // Оновлюємо дані у вікні (або закриваємо його, щоб користувач відкрив заново)
                MessageBox.Show("Запит оновлено! Закрийте вікно деталей для оновлення списку.");
                _closeAction();
            }
        }

        private void Delete(object obj)
        {
            if (MessageBox.Show("Видалити цей запит безповоротно?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    _service.DeleteRequest(_request.Id, _currentUser.Id);
                    MessageBox.Show("Запит видалено.");
                    _closeAction();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void Cancel(object obj)
        {
            if (MessageBox.Show("Скасувати виконання запиту?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    _service.CancelRequest(_request.Id, _currentUser.Id);
                    MessageBox.Show("Запит скасовано.");
                    _closeAction();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void AddComment(object obj)
        {
            try
            {
                _service.AddComment(_request.Id, _currentUser.Id, NewCommentText);
                Comments.Add(new RequestComment { UserId = _currentUser.Id, User = _currentUser, CommentText = NewCommentText, CreatedAt = DateTime.Now });
                NewCommentText = string.Empty;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DownloadFile(object obj)
        {
            if (obj is RequestAttachment file)
            {
                var dialog = new SaveFileDialog { FileName = file.FileName };
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(dialog.FileName, file.FileData);
                    MessageBox.Show("Файл збережено!");
                }
            }
        }
    }
}
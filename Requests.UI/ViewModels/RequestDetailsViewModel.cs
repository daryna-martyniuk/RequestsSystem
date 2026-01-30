using Microsoft.Win32;
using Requests.Data.Models;
using Requests.Services;
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

        public ICommand AddCommentCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand CloseCommand { get; }

        public RequestDetailsViewModel(Request request, User currentUser, EmployeeService service, Action closeAction)
        {
            _request = request;
            _currentUser = currentUser;
            _service = service;

            Comments = new ObservableCollection<RequestComment>(request.Comments);
            Tasks = new ObservableCollection<DepartmentTask>(request.DepartmentTasks);
            Attachments = new ObservableCollection<RequestAttachment>(request.Attachments);

            AddCommentCommand = new RelayCommand(AddComment, o => !string.IsNullOrWhiteSpace(NewCommentText));
            DownloadFileCommand = new RelayCommand(DownloadFile);
            CloseCommand = new RelayCommand(o => closeAction());
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
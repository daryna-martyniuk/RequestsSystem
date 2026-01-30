using Requests.Data.Models;
using Requests.Services;
using Requests.UI.ViewModels;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class RequestDetailsWindow : Window
    {
        public RequestDetailsWindow(Request request, User currentUser, EmployeeService service)
        {
            InitializeComponent();
            DataContext = new RequestDetailsViewModel(request, currentUser, service, () => Close());
        }
    }
}
using Requests.Data.Models;
using Requests.UI.ViewModels;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(User currentUser)
        {
            InitializeComponent();
            DataContext = new DashboardViewModel(currentUser);
        }
    }
}
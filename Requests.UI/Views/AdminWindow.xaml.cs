using Requests.Data.Models;
using Requests.UI.ViewModels;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class AdminWindow : Window
    {
        public AdminWindow(User currentUser)
        {
            InitializeComponent();
            DataContext = new AdminViewModel(currentUser);
        }
    }
}
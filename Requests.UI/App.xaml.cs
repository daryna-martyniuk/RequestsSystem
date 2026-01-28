using Requests.Data;
using Requests.UI.Views;
using System.Windows;

namespace Requests.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            using (var context = new AppDbContext())
            {
                DbInitializer.Seed(context);
            }

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
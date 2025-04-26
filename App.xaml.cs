using System.Windows;

namespace RegEnforcer;

public partial class App : System.Windows.Application
{

    private TrayIconManager trayIconManager;
    private void Application_Startup(object sender, StartupEventArgs e)
    {

        //base.OnStartup(e);
        trayIconManager = new TrayIconManager();


    }
}

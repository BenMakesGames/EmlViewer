using System.IO;
using System.Windows;

namespace EmlViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            var win = new MainWindow();
            win.LoadEml(e.Args[0]);
            win.Show();
        }
        else
        {
            var win = new AssociateWindow();
            win.Show();
        }
    }
}

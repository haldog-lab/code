using System.Windows;

namespace Shelf.Windows;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        instanceMutex = new Mutex(true, @"Local\Shelf.Windows", out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show("Shelfはすでに起動しています。タスクバーのShelfを確認してください。", "Shelf");
            Shutdown();
            return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

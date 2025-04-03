using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace csvplot;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWin = new MainWindow();
            desktop.MainWindow = mainWin;

            if ((desktop.Args?.Length ?? 0) > 0)
            {
                var filepath = desktop.Args![0];
                try
                {
                    var source = DataSourceFactory.SourceFromLocalPath(filepath, mainWin.Listener);
                    await mainWin.AddDataSource(source);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Artemis.App.ViewModels;
using Artemis.App.Views;
using System;
using Microsoft.Extensions.DependencyInjection;
using Artemis.Infra;
using System.IO;

namespace Artemis.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        var exeDir = AppContext.BaseDirectory;
        var dbPath = IsWritableDirectory(exeDir) switch
        {
            true => Path.Combine(exeDir, "artemis.db"),
            false => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Artemis", "artemis.db")
        };
        services.AddArtemisInfra($"Data Source=\"{dbPath}\"");
        services.AddSingleton<MainWindowViewModel>();
        _serviceProvider = services.BuildServiceProvider();
        
        var vm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }
        await DbInit.EnsureCreatedAsync(_serviceProvider);
        await vm.InitializeAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static bool IsWritableDirectory(string path)
    {
        try
        {
            var testFile = Path.Combine(path, ".write_test");
            File.WriteAllText(testFile, "x");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Muno.Services;
using Muno.ViewModels;
using Serilog;
using System;

namespace Muno;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Gets the current application instance as <see cref="App"/>.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("Logs/muno-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Build service provider
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(configure =>
        {
            configure.ClearProviders();
            configure.AddSerilog(dispose: true);
        });

        // Services
        services.AddSingleton<IAudioDecodingService, AudioDecodingService>();
        services.AddSingleton<IWaveformGeneratorService, WaveformGeneratorService>();
        services.AddSingleton<IAudioFileLoaderService, AudioFileLoaderService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioExportService, AudioExportService>();
        services.AddSingleton<IPhaseLimiterEngine, PhaseLimiterEngine>();
        services.AddSingleton<IMasteringSessionService, MasteringSessionService>();
        services.AddSingleton<IMelodySimplificationService, MelodySimplificationService>();

        // ViewModels
        services.AddTransient(provider =>
        {
            var viewModel = new MainPageViewModel(
                provider.GetRequiredService<IAudioFileLoaderService>(),
                provider.GetRequiredService<IAudioPlaybackService>(),
                provider.GetRequiredService<IAudioExportService>(),
                provider.GetRequiredService<IMasteringSessionService>(),
                provider.GetRequiredService<ILogger<MainPageViewModel>>());
            viewModel.SetMelodySimplificationService(
                provider.GetRequiredService<IMelodySimplificationService>());
            return viewModel;
        });
    }
}

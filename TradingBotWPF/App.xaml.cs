using System;
using System.IO;
using System.Windows;
using Serilog;

namespace TradingBotWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Erstelle Logs-Verzeichnis falls nicht vorhanden
            Directory.CreateDirectory("logs");

            // Konfiguriere globales Exception Handling
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Initialisiere Logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("logs/tradingbot-app.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("🚀 TradingBot WPF Application gestartet");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("🏁 TradingBot WPF Application beendet");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "💥 Unbehandelte Ausnahme in AppDomain");

            MessageBox.Show(
                $"Ein kritischer Fehler ist aufgetreten:\n\n{exception?.Message}\n\nDie Anwendung wird beendet.",
                "Kritischer Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "💥 Unbehandelte Ausnahme im UI-Thread");

            var result = MessageBox.Show(
                $"Ein Fehler ist aufgetreten:\n\n{e.Exception.Message}\n\nMöchten Sie fortfahren?",
                "Fehler",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                e.Handled = true; // Fehler als behandelt markieren
            }
        }
    }
}
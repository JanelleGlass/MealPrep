namespace MealPrep;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "MealPrep" };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] UnhandledException: {e.ExceptionObject}\n\n");
        };

        return window;
    }
}

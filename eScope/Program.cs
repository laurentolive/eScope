using eScope.License;
using ScottPlot.Colormaps;

namespace eScope
{

    public static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>

        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.

            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            ApplicationConfiguration.Initialize();
            Theme.Initialize(UiSettings.Current.DarkMode);
            // eScope.exe [workspace.escope]
            Application.Run(new eScope(eScope.StartupWorkspace(args)));
        }
    }
}
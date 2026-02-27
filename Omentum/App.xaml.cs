namespace Omentum
{
    public partial class App : Application
    {
        public App()
        {
            bool isNewInstance = false;
            using (Mutex mutex = new Mutex(true, "OmentumMutex", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    return;
                }

                InitializeComponent();
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            window.MinimumHeight = 540;
            window.MinimumWidth = 960;
            return window;
        }
    }
}
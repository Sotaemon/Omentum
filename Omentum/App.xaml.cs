using Microsoft.Extensions.DependencyInjection;

namespace Omentum
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
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
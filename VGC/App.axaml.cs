using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VGC.Composition;
using VGC.Views;

namespace VGC
{
            public partial class App : Application
            {
                        private AppServices? _services;

                        public override void Initialize()
                        {
                                    AvaloniaXamlLoader.Load(this);
                        }

                        public override void OnFrameworkInitializationCompleted()
                        {
                                    _services = ServiceRegistration.Build();

                                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                                    {
                                                desktop.MainWindow = new MainWindow
                                                {
                                                            DataContext = _services.ShellViewModel
                                                };
                                                desktop.ShutdownRequested += OnShutdownRequested;
                                    }
                                    else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
                                    {
                                                singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = _services.ShellViewModel };
                                    }
                                    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
                                    {
                                                singleViewPlatform.MainView = new MainView
                                                {
                                                            DataContext = _services.ShellViewModel
                                                };
                                    }

                                    base.OnFrameworkInitializationCompleted();

                                    _ = _services.ShellViewModel.InitializeAsync();
                        }

                        private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
                        {
                                    if (_services is null)
                                    {
                                                return;
                                    }

                                    var closeCheck = _services.CloseCoordinator.CanCloseAsync().GetAwaiter().GetResult();
                                    if (!closeCheck.CanClose)
                                    {
                                                e.Cancel = true;
                                                return;
                                    }

                                    _services.GcsHeartbeatService.StopAsync().GetAwaiter().GetResult();
                                    _services.LinkManager.DisconnectAllAsync().GetAwaiter().GetResult();
                                    _services.Lifecycle.ShutdownAsync().GetAwaiter().GetResult();
                        }
            }
}

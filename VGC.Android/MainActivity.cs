using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia.Android;
using ReactiveUI.Avalonia;
using VGC.ViewModels;

namespace VGC.Android
{
            [Activity(
                Label = "VGC.Android",
                Theme = "@style/MyTheme.NoActionBar",
                Icon = "@drawable/icon",
                MainLauncher = true,
                ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
            public class MainActivity : AvaloniaMainActivity
            {
                protected override void OnCreate(Bundle? savedInstanceState)
                {
                    var autoConnectTcp = Intent?.GetStringExtra(ShellViewModel.AutoConnectTcpEnvironmentVariable);
                    if (!string.IsNullOrWhiteSpace(autoConnectTcp))
                    {
                        ShellViewModel.StartupAutoConnectTcpEndpoint = autoConnectTcp;
                        global::System.Environment.SetEnvironmentVariable(ShellViewModel.AutoConnectTcpEnvironmentVariable, autoConnectTcp);
                        Log.Info("VGC", $"Configured TCP auto-connect endpoint from intent: {autoConnectTcp}");
                    }

                    base.OnCreate(savedInstanceState);
                }
            }
}

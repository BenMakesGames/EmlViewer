using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace EmlViewer;

public partial class AssociateWindow : Window
{
    private const string ProgId = "EmlViewer.eml.1";
    private const string AppRegName = "EmlViewer";
    private const string FriendlyName = "EmlViewer";
    private const string Ext = ".eml";

    public AssociateWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshStatus();
    }

    private static string ExePath => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;

    private void RefreshStatus()
    {
        var registered = IsRegistered();
        StatusText.Text = registered
            ? "Status: registered. If .eml does not open with EmlViewer, pick it as the default in Settings."
            : "Status: not registered yet.";
        OpenSettingsBtn.IsEnabled = registered;
        RegisterBtn.Content = registered ? "Re-register" : "1. Register app";
    }

    private static bool IsRegistered()
    {
        using var k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}");
        return k != null;
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Register();
            RefreshStatus();
            MessageBox.Show(this,
                "Registered. Click '2. Open Default apps' and set EmlViewer as the default for .eml files.",
                "EmlViewer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Register failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Unregister_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Unregister();
            RefreshStatus();
            MessageBox.Show(this, "Unregistered.", "EmlViewer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unregister failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.DefaultPrograms") { UseShellExecute = true }); } catch { }
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Email message (*.eml)|*.eml|All files (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        var win = new MainWindow();
        win.LoadEml(dlg.FileName);
        win.Show();
        Close();
    }

    private static void Register()
    {
        var exe = ExePath;
        var quotedCmd = $"\"{exe}\" \"%1\"";
        var iconRef = $"\"{exe}\",0";

        using (var progid = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progid.SetValue(null, "Email Message");
            progid.SetValue("FriendlyTypeName", "Email Message");
            using (var icon = progid.CreateSubKey("DefaultIcon")) icon.SetValue(null, iconRef);
            using (var cmd = progid.CreateSubKey(@"shell\open\command")) cmd.SetValue(null, quotedCmd);
        }

        using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Ext}"))
        {
            using var owp = ext.CreateSubKey("OpenWithProgids");
            owp.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        var exeName = Path.GetFileName(exe);
        using (var app = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
        {
            app.SetValue("FriendlyAppName", FriendlyName);
            using (var swe = app.CreateSubKey("SupportedTypes")) swe.SetValue(Ext, "");
            using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue(null, quotedCmd);
            using (var icon = app.CreateSubKey("DefaultIcon")) icon.SetValue(null, iconRef);
        }

        using (var caps = Registry.CurrentUser.CreateSubKey($@"Software\{AppRegName}\Capabilities"))
        {
            caps.SetValue("ApplicationName", FriendlyName);
            caps.SetValue("ApplicationDescription", "Tiny viewer for .eml email files.");
            using var fa = caps.CreateSubKey("FileAssociations");
            fa.SetValue(Ext, ProgId);
        }
        using (var regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
        {
            regApps.SetValue(AppRegName, $@"Software\{AppRegName}\Capabilities");
        }
    }

    private static void Unregister()
    {
        var exe = ExePath;
        var exeName = Path.GetFileName(exe);
        TryDelete($@"Software\Classes\{ProgId}");
        TryDeleteValue($@"Software\Classes\{Ext}\OpenWithProgids", ProgId);
        TryDelete($@"Software\Classes\Applications\{exeName}");
        TryDeleteValue(@"Software\RegisteredApplications", AppRegName);
        TryDelete($@"Software\{AppRegName}");
    }

    private static void TryDelete(string path)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
    }
    private static void TryDeleteValue(string path, string value)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(path, writable: true);
            k?.DeleteValue(value, false);
        }
        catch { }
    }
}

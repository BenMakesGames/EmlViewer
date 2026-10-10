using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using MimeKit;

namespace EmlViewer;

public partial class MainWindow : Window
{
    private string? _tempDir;

    public MainWindow()
    {
        InitializeComponent();
        HtmlView.Navigating += HtmlView_Navigating;
        Closed += (_, _) => Cleanup();
    }

    public void LoadEml(string path)
    {
        try
        {
            var msg = MimeMessage.Load(path);
            Title = (string.IsNullOrWhiteSpace(msg.Subject) ? "(no subject)" : msg.Subject) + " — EmlViewer";
            SubjectText.Text = string.IsNullOrWhiteSpace(msg.Subject) ? "(no subject)" : msg.Subject;
            FromText.Text = msg.From?.ToString() ?? "";
            ToText.Text = msg.To?.ToString() ?? "";
            if (msg.Cc != null && msg.Cc.Count > 0)
            {
                CcLabel.Visibility = Visibility.Visible;
                CcText.Visibility = Visibility.Visible;
                CcText.Text = msg.Cc.ToString();
            }
            DateText.Text = msg.Date.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

            var headers = new StringBuilder();
            foreach (var h in msg.Headers)
                headers.AppendLine($"{h.Field}: {h.Value}");
            HeadersView.Text = headers.ToString();

            var html = msg.HtmlBody;
            var text = msg.TextBody;

            if (!string.IsNullOrEmpty(html))
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "EmlViewer", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_tempDir);
                var htmlPath = Path.Combine(_tempDir, "message.html");
                var (sanitized, blocked) = Sanitize(html);
                File.WriteAllText(htmlPath, sanitized, new UTF8Encoding(false));
                if (blocked) RemoteBlockedBanner.Visibility = Visibility.Visible;
                HtmlView.Navigate(new Uri(htmlPath));
                BodyTabs.SelectedItem = HtmlTab;
            }
            else
            {
                HtmlTab.Visibility = Visibility.Collapsed;
                BodyTabs.SelectedItem = TextTab;
            }

            if (!string.IsNullOrEmpty(text))
                TextView.Text = text;
            else if (string.IsNullOrEmpty(html))
                TextView.Text = "(no body)";
            else
                TextTab.Visibility = Visibility.Collapsed;

            var attachments = msg.Attachments.ToList();
            if (attachments.Count > 0)
            {
                AttachmentsPanel.Visibility = Visibility.Visible;
                foreach (var att in attachments)
                {
                    var name = att.ContentDisposition?.FileName ?? att.ContentType.Name ?? "attachment";
                    var btn = new Button
                    {
                        Content = name,
                        Margin = new Thickness(0, 0, 6, 4),
                        Padding = new Thickness(8, 3, 8, 3),
                        Cursor = Cursors.Hand,
                        Tag = att
                    };
                    btn.Click += Attachment_Click;
                    AttachmentsList.Items.Add(btn);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load:\n{ex.Message}", "EmlViewer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HtmlView_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (_tempDir == null) return;
        if (e.Uri == null) return;
        if (e.Uri.IsFile && e.Uri.LocalPath.StartsWith(_tempDir, StringComparison.OrdinalIgnoreCase))
            return;
        e.Cancel = true;
        if (e.Uri.Scheme is "http" or "https" or "mailto")
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        }
    }

    private void Attachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not MimeEntity entity) return;
        var name = entity.ContentDisposition?.FileName ?? entity.ContentType.Name ?? "attachment.bin";
        name = SafeName(name);
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = name };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var fs = File.Create(dlg.FileName);
            if (entity is MimePart mp && mp.Content != null)
                mp.Content.DecodeTo(fs);
            else if (entity is MessagePart msgp && msgp.Message != null)
                msgp.Message.WriteTo(fs);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SafeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static readonly Regex ScriptTag = new(@"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EventAttr = new(@"\s on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RemoteSrc = new(@"(src|background|href)\s*=\s*(""|')(https?:|//)[^""']*(""|')", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RemoteCssUrl = new(@"url\(\s*['""]?(https?:|//)[^)]*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static (string html, bool blocked) Sanitize(string html)
    {
        bool blocked = false;
        var cleaned = ScriptTag.Replace(html, "");
        cleaned = EventAttr.Replace(cleaned, "");
        cleaned = RemoteSrc.Replace(cleaned, m =>
        {
            blocked = true;
            var attr = m.Groups[1].Value;
            if (attr.Equals("href", StringComparison.OrdinalIgnoreCase)) return m.Value;
            return $"{attr}=\"\"";
        });
        if (RemoteCssUrl.IsMatch(cleaned))
        {
            blocked = true;
            cleaned = RemoteCssUrl.Replace(cleaned, "url()");
        }

        if (cleaned.IndexOf("<meta", StringComparison.OrdinalIgnoreCase) < 0 ||
            cleaned.IndexOf("charset", StringComparison.OrdinalIgnoreCase) < 0)
        {
            cleaned = "<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"></head><body>" + cleaned + "</body></html>";
        }
        return (cleaned, blocked);
    }

    private void Cleanup()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }
}

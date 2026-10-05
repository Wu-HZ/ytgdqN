using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using WindowsFormsApplication2;
using WindowsFormsApplication2.Difficulty;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        var host = RuntimeHelpers.GetUninitializedObject(typeof(Form1));
        typeof(Form1).GetField("DiffDict", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(host, new DifficultyDict());
        using var form = new 新发文((Form1)host) { Opacity = 0, ShowInTaskbar = false };
        var formType = form.GetType();
        var loadMethod = formType.GetMethod("新发文_Load", BindingFlags.Instance | BindingFlags.NonPublic);
        form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, loadMethod);
        SetField(formType, form, "legadoAutoRefreshAttempted", true);
        form.Show();
        Application.DoEvents();

        var contentTabs = (TabControl)Field(formType, form, "tabControl2");
        var sourceTabs = (TabControl)Field(formType, form, "tabControl1");
        var lengthBox = (TextBox)Field(formType, form, "tbxSendCount");
        var styleLabel = (Label)Field(formType, form, "lblStyle");
        var builtInList = (ListBox)Field(formType, form, "lbxTextList");

        // Use clean profile defaults regardless of the developer machine's persisted INI settings.
        SetField(formType, form, "singleSegmentLength", 10);
        SetField(formType, form, "articleSegmentLength", 200);
        SetField(formType, form, "phraseSegmentLength", 25);
        contentTabs.SelectedIndex = (int)NewSendText.ContentTypeValue.Single;
        styleLabel.Text = "";
        styleLabel.Text = "单字";
        Check(lengthBox.Text == "10", $"single default should be 10, got {lengthBox.Text}");

        contentTabs.SelectedIndex = (int)NewSendText.ContentTypeValue.Article;
        Check(styleLabel.Text == "文章" && lengthBox.Text == "200",
            $"article default should be 200, style={styleLabel.Text}, value={lengthBox.Text}");
        lengthBox.Text = "180";
        contentTabs.SelectedIndex = (int)NewSendText.ContentTypeValue.Single;
        Check(styleLabel.Text == "单字" && lengthBox.Text == "10",
            $"article->single should restore 10, style={styleLabel.Text}, value={lengthBox.Text}");
        contentTabs.SelectedIndex = (int)NewSendText.ContentTypeValue.Article;
        Check(lengthBox.Text == "180", $"article custom length should be remembered, got {lengthBox.Text}");

        lengthBox.Text = "200";
        sourceTabs.SelectedIndex = sourceTabs.TabCount - 1;
        Check(lengthBox.Text == "200", $"ebook should use article length 200, got {lengthBox.Text}");
        lengthBox.Text = "180";
        sourceTabs.SelectedIndex = 0;
        builtInList.SelectedIndex = -1;
        builtInList.SelectedIndex = 0;
        Check(styleLabel.Text == "单字" && lengthBox.Text == "10",
            $"selecting a built-in single should switch to 10, style={styleLabel.Text}, value={lengthBox.Text}");
        sourceTabs.SelectedIndex = sourceTabs.TabCount - 1;
        Check(lengthBox.Text == "180", $"ebook should restore its article profile, got {lengthBox.Text}");

        Console.WriteLine("Real NewSend UI segment-length switching passed (single 10, article/ebook 200, independent custom values)");
    }

    private static object Field(Type type, object target, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetField(Type type, object target, string name, object value) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

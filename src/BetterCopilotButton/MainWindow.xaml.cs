using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterCopilotButton.Models;
using BetterCopilotButton.Services;
using Microsoft.Win32;

namespace BetterCopilotButton;

public partial class MainWindow : Window
{
    private bool _updatingUi;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        BuildPresetButtons();
        RefreshFromSettings();
    }

    public void RefreshFromSettings()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        _updatingUi = true;
        try
        {
            var settings = app.Settings;
            RemapToggle.IsChecked = settings.RemapEnabled;
            LoginCheck.IsChecked = settings.StartAtLogin;
            CustomPathBox.Text = settings.CustomPath;
            CustomArgsBox.Text = settings.CustomArguments;

            RemapStatusText.Text = settings.RemapEnabled
                ? "On. The Copilot key opens " + AppLauncher.DescribeTarget(settings) + "."
                : "Off. The Copilot key is unchanged.";

            TargetHint.Text = DescribeSelection(settings);
            HighlightPresets(settings.PresetId);
        }
        finally
        {
            _updatingUi = false;
        }
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void BuildPresetButtons()
    {
        PresetGrid.Children.Clear();
        foreach (var preset in PresetCatalog.All)
        {
            var button = new Button
            {
                Tag = preset.Id,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 10, 10, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderBrush = (Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(1)
            };
            button.Click += OnPresetClick;

            var title = preset.DisplayName;
            if (preset.Id == AppSettings.DefaultPresetId)
            {
                title += "  ·  default";
            }

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = preset.Description,
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            button.Content = stack;
            button.Template = CreatePresetTemplate();
            PresetGrid.Children.Add(button);
        }
    }

    private static ControlTemplate CreatePresetTemplate()
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        borderFactory.AppendChild(presenter);

        return new ControlTemplate(typeof(Button))
        {
            VisualTree = borderFactory
        };
    }

    private void HighlightPresets(string selectedId)
    {
        foreach (var child in PresetGrid.Children)
        {
            if (child is not Button button || button.Tag is not string id)
            {
                continue;
            }

            var selected = id == selectedId;
            button.Background = selected
                ? (Brush)FindResource("AccentSoftBrush")
                : Brushes.White;
            button.BorderBrush = selected
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("LineBrush");
        }
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id || Application.Current is not App app)
        {
            return;
        }

        app.Settings.PresetId = id;
        Persist(app);
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Choose an app",
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        app.Settings.PresetId = "custom";
        app.Settings.CustomPath = dialog.FileName;
        Persist(app);
    }

    private void OnCustomPathLostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || Application.Current is not App app)
        {
            return;
        }

        var path = CustomPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        app.Settings.CustomPath = path;
        app.Settings.PresetId = "custom";
        Persist(app);
    }

    private void OnCustomArgsLostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || Application.Current is not App app)
        {
            return;
        }

        app.Settings.CustomArguments = CustomArgsBox.Text.Trim();
        Persist(app);
    }

    private void OnRemapToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || Application.Current is not App app)
        {
            return;
        }

        app.Settings.RemapEnabled = RemapToggle.IsChecked == true;
        Persist(app);
    }

    private void OnLoginToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingUi || Application.Current is not App app)
        {
            return;
        }

        app.Settings.StartAtLogin = LoginCheck.IsChecked == true;
        Persist(app);
    }

    private void OnTestLaunchClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        try
        {
            AppLauncher.LaunchOrFocus(app.Settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Better Copilot Button",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        try
        {
            InstallService.InstallForCurrentUser();
            MessageBox.Show(
                this,
                "Added to your Start menu under Better Copilot Button.\n\n" +
                "You can also uninstall from this window or from Apps in Windows Settings.",
                "Better Copilot Button",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Could not add the Start menu shortcut.\n\n" + ex.Message,
                "Better Copilot Button",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.RunUninstallInteractive();
        }
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        e.Cancel = true;
        if (Application.Current is App app)
        {
            app.HideSettingsToTray();
        }
    }

    private void Persist(App app)
    {
        app.SaveAndApply(app.Settings, showErrors: true);
    }

    private static string DescribeSelection(AppSettings settings)
    {
        var preset = PresetCatalog.Get(settings.PresetId);
        if (preset.IsCustom)
        {
            return string.IsNullOrWhiteSpace(settings.CustomPath)
                ? "Browse for any .exe."
                : settings.CustomPath;
        }

        if (preset.Id == "chatgpt")
        {
            return PresetCatalog.ResolveChatGptPath()
                   ?? "Not installed. Pick another app or browse.";
        }

        return preset.Target;
    }
}

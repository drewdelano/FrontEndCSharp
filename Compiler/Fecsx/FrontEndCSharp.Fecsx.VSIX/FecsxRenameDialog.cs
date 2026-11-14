using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FrontEndCSharp.Fecsx.VSIX;

internal sealed class FecsxRenameDialog : Window
{
    private readonly TextBox _name;

    public FecsxRenameDialog(string currentName)
    {
        Title = "Rename Symbol";
        Width = 420;
        Height = 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"Rename '{currentName}' to:",
            Margin = new Thickness(0, 0, 0, 8)
        };
        grid.Children.Add(label);

        _name = new TextBox
        {
            Text = currentName,
            Margin = new Thickness(0, 0, 0, 12),
            MinWidth = 360
        };
        Grid.SetRow(_name, 1);
        grid.Children.Add(_name);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var rename = new Button
        {
            Content = "Rename",
            IsDefault = true,
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        rename.Click += (_, __) =>
        {
            if (!string.IsNullOrWhiteSpace(_name.Text))
            {
                DialogResult = true;
            }
        };
        buttons.Children.Add(rename);
        buttons.Children.Add(new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 80
        });
        Grid.SetRow(buttons, 2);
        grid.Children.Add(buttons);
        Content = grid;

        Loaded += (_, __) =>
        {
            _name.Focus();
            _name.SelectAll();
            Keyboard.Focus(_name);
        };
    }

    public string NewName => _name.Text.Trim();
}

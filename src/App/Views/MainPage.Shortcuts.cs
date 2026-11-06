namespace BraviaRemote;

public partial class MainPage
{
    private const int MaxShortcuts = 6;
    private const int MaxSteps = 16;
    private const int HomeKey = 6;
    private static readonly Dictionary<int, string> ShortcutSteps = new()
    {
        [HomeKey] = "Home",
        [8] = "Back",
        [9] = "Up",
        [10] = "Down",
        [11] = "Right",
        [12] = "Left",
        [13] = "OK"
    };
    private static readonly Dictionary<string, int> ShortcutKeys = ShortcutSteps
        .SelectMany(step => new[] { "en", "uk" }.Select(language =>
            (Name: Localization.StepName(step.Value, language), Code: step.Key)))
        .Distinct()
        .ToDictionary(step => step.Name, step => step.Code, StringComparer.OrdinalIgnoreCase);

    private QuickButton[] Shortcuts => CurrentProfile?.Shortcuts ?? [];

    private void RefreshShortcuts()
    {
        QuickButtons.Children.Clear();
        QuickButtons.RowDefinitions.Clear();
        var shortcuts = Shortcuts;
        QuickButtons.IsVisible = shortcuts.Length > 0;
        for (var index = 0; index < shortcuts.Length; index++)
        {
            var shortcut = shortcuts[index];
            if (index % 3 == 0)
                QuickButtons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var button = new Button { Text = shortcut.Name };
            button.Clicked += async (_, _) => await RunAsync(async tv =>
            {
                for (var step = 0; step < shortcut.Keys.Length; step++)
                {
                    await tv.PressAsync(shortcut.Keys[step]);
                    if (step + 1 < shortcut.Keys.Length)
                        await Task.Delay(shortcut.Keys[step] == HomeKey ? 1400 : 350);
                }
                return Localization.Text("ShortcutDone", shortcut.Name);
            });
            QuickButtons.Add(button, index % 3, index / 3);
        }
    }

    private void OnOpenShortcuts(object? sender, EventArgs e)
    {
        if (CurrentProfile is null)
            return;

        RefreshShortcutPicker(-1);
        ShowScreen(Screen.ShortcutEditor);
    }

    private void OnCloseShortcuts(object? sender, EventArgs e) => ShowScreen(Screen.Settings);

    private void OnNewShortcut(object? sender, EventArgs e)
    {
        ShortcutPicker.SelectedIndex = -1;
        LoadShortcutFields();
    }

    private void OnShortcutSelected(object? sender, EventArgs e) => LoadShortcutFields();

    private void RefreshShortcutPicker(int index)
    {
        ShortcutPicker.ItemsSource = Shortcuts.Select(shortcut => shortcut.Name).ToList();
        ShortcutPicker.SelectedIndex = index;
        LoadShortcutFields();
    }

    private void LoadShortcutFields()
    {
        var shortcut = Shortcuts.ElementAtOrDefault(ShortcutPicker.SelectedIndex);
        ShortcutName.Text = shortcut?.Name;
        ShortcutSequence.Text = shortcut is null ? "" : string.Join(", ", shortcut.Keys.Select(key =>
            Localization.Text(ShortcutSteps[key])));
        RemoveShortcutButton.IsEnabled = shortcut is not null;
        ShortcutError.IsVisible = false;
    }

    private void OnSaveShortcut(object? sender, EventArgs e)
    {
        var name = ShortcutName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ShowShortcutError(Localization.Text("MissingShortcutName"));
            return;
        }

        var steps = (ShortcutSequence.Text ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (steps.Length > MaxSteps || steps.Any(step => !ShortcutKeys.ContainsKey(step)))
        {
            ShowShortcutError(Localization.Text("InvalidShortcutSteps", MaxSteps));
            return;
        }

        var shortcuts = Shortcuts.ToList();
        var index = ShortcutPicker.SelectedIndex;
        if (index < 0 && shortcuts.Count >= MaxShortcuts)
        {
            ShowShortcutError(Localization.Text("ShortcutLimit", MaxShortcuts));
            return;
        }

        var shortcut = new QuickButton(name, steps.Select(step => ShortcutKeys[step]).ToArray());
        if (index < 0)
        {
            index = shortcuts.Count;
            shortcuts.Add(shortcut);
        }
        else
        {
            shortcuts[index] = shortcut;
        }
        SaveShortcuts(shortcuts, index);
    }

    private void OnRemoveShortcut(object? sender, EventArgs e)
    {
        var index = ShortcutPicker.SelectedIndex;
        if (index < 0)
            return;

        var shortcuts = Shortcuts.ToList();
        shortcuts.RemoveAt(index);
        SaveShortcuts(shortcuts, -1);
    }

    private void SaveShortcuts(List<QuickButton> shortcuts, int selectedIndex)
    {
        var index = ProfilePicker.SelectedIndex;
        profiles.Items[index] = profiles.Items[index] with { Shortcuts = shortcuts.ToArray() };
        profiles.Save(Preferences.Default);
        RefreshShortcutPicker(selectedIndex);
    }

    private void ShowShortcutError(string message)
    {
        ShortcutError.Text = message;
        ShortcutError.IsVisible = true;
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Settings;

namespace TFM_ACCOUNTING.Desktop.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Func<string, ISettingsService?> serviceProvider;
    private SettingsGroup selectedGroup = SettingsCatalog.Groups[0];
    private SettingItem? selectedItem;
    private string name = string.Empty;
    private string? notes;
    private bool isActive = true;
    private bool isBusy;
    private string? behaviorKey;
    private string message = string.Empty;
    private readonly List<SettingsCommand> commands = [];

    public SettingsViewModel(Func<string, ISettingsService?> serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        ReloadCommand = Command(RefreshAsync);
        NewCommand = Command(() => { SelectedItem = null; ResetEditor(); return Task.CompletedTask; });
        SaveCommand = Command(SaveAsync);
        ToggleActiveCommand = Command(ToggleActiveAsync, () => SelectedItem is not null);
    }

    public IReadOnlyList<SettingsGroup> Groups => SettingsCatalog.Groups;
    public ObservableCollection<SettingItem> Items { get; } = [];
    public IReadOnlyList<BehaviorOption> Behaviors => SelectedGroup.Behaviors;
    public ICommand ReloadCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public SettingsGroup SelectedGroup
    {
        get => selectedGroup;
        set
        {
            if (value is null || value == selectedGroup || IsBusy) return;
            selectedGroup = value;
            Changed(); Changed(nameof(Behaviors)); Changed(nameof(HasBehavior));
            SelectedItem = null;
            ResetEditor();
            Items.Clear();
            _ = RefreshAsync();
        }
    }
    public SettingItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            selectedItem = value;
            if (value is not null)
            {
                Name = value.Name; Notes = value.Notes; IsActive = value.IsActive; BehaviorKey = value.BehaviorKey;
            }
            Changed(); Changed(nameof(EditorTitle)); Changed(nameof(Code)); Changed(nameof(CanChooseBehavior));
            Changed(nameof(ToggleActiveText)); RefreshCommands();
        }
    }
    public string Name { get => name; set { name = value; Changed(); } }
    public string? Notes { get => notes; set { notes = value; Changed(); } }
    public bool IsActive { get => isActive; set { isActive = value; Changed(); } }
    public string? BehaviorKey { get => behaviorKey; set { behaviorKey = value; Changed(); } }
    public string Message { get => message; private set { message = value; Changed(); } }
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            isBusy = value; Changed(); Changed(nameof(IsIdle)); Changed(nameof(CanChooseBehavior)); RefreshCommands();
        }
    }
    public bool IsIdle => !IsBusy;
    public bool HasBehavior => Behaviors.Count > 0;
    public bool CanChooseBehavior => !IsBusy && SelectedItem is null;
    public string EditorTitle => SelectedItem is null ? "Új bejegyzés" : "Bejegyzés szerkesztése";
    public string Code => SelectedItem?.Code ?? "Mentéskor automatikusan készül";
    public string ToggleActiveText => SelectedItem?.IsActive == true ? "Inaktiválás" : "Újraaktiválás";
    public Task InitializeAsync() => RefreshAsync();
    private Task RefreshAsync() => RunAsync(async service =>
    {
        if (await LoadAsync(service, SelectedItem?.Id)) Message = $"{Items.Count} bejegyzés. Az inaktív elemek is láthatók.";
    });
    private Task SaveAsync() => RunAsync(async service =>
    {
        var result = await service.SaveAsync(new(SelectedItem?.Id, Name, Notes, IsActive, BehaviorKey, SelectedItem?.RowVersion));
        if (!result.IsSuccess) { ShowFailure(result); return; }
        if (await LoadAsync(service, result.Data!.Id)) Message = result.Message ?? "Sikeres mentés.";
    });
    private Task ToggleActiveAsync() => RunAsync(async service =>
    {
        if (SelectedItem is not { } item) return;
        var result = await service.SetActiveAsync(item.Id, !item.IsActive, item.RowVersion);
        if (!result.IsSuccess) { ShowFailure(result); return; }
        if (await LoadAsync(service, item.Id)) Message = result.Message ?? "Az állapotot módosítottuk.";
    });
    private async Task<bool> LoadAsync(ISettingsService service, int? id)
    {
        var response = await service.ListAsync();
        if (!response.IsSuccess) { ShowFailure(response); return false; }
        Items.Clear();
        foreach (var item in response.Data!) Items.Add(item);
        SelectedItem = Items.FirstOrDefault(x => x.Id == id);
        if (SelectedItem is null) ResetEditor();
        return true;
    }
    private async Task RunAsync(Func<ISettingsService, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var service = serviceProvider(SelectedGroup.Key);
            if (service is null)
            {
                Message = "Nincs beállított adatbázis-kapcsolat. Állítsd be a TFM_ACCOUNTING_CONNECTION_STRING környezeti változót, majd indítsd újra az alkalmazást.";
                return;
            }
            Message = "Művelet folyamatban…";
            await action(service);
        }
        catch (Exception) { Message = "A művelet nem sikerült. Ellenőrizd a kapcsolatot, majd próbáld újra."; }
        finally { IsBusy = false; }
    }
    private void ShowFailure<T>(Response<T> response) => Message = response.ValidationErrors.Count > 0
        ? string.Join(Environment.NewLine, response.ValidationErrors.Select(x => x.Message))
        : response.Message ?? "A művelet nem sikerült.";
    private void ResetEditor()
    {
        Name = string.Empty; Notes = null; IsActive = true; BehaviorKey = Behaviors.FirstOrDefault()?.Key;
    }
    private SettingsCommand Command(Func<Task> execute, Func<bool>? canExecute = null)
    {
        var command = new SettingsCommand(execute, () => !IsBusy && (canExecute?.Invoke() ?? true));
        commands.Add(command); return command;
    }
    private void RefreshCommands() { foreach (var command in commands) command.Refresh(); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));
}

internal sealed class SettingsCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute();
    public async void Execute(object? parameter) { if (CanExecute(parameter)) await execute(); }
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Bubbles4.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace Bubbles4.Services;

public partial class InputManager
{
    private static Action<KeyCombo>? KeyComboListener;
    private static Action<ButtonName>? GamepadButtonListener;
    public Window? UserSettingsWindow { get; set; }
    public IDialogService? DialogService { get; set; }
    public Border? FocusDump { get; set; } = null;
    
    public partial class ActionBindings:ViewModelBase
    {
        public string ActionName { get; set; } = "";
        [JsonIgnore]public Action Action { get; set; } = DoNothing;
        public List<KeyCombo> KeyCombos { get; set; } = new();
        public List<ButtonName> GamepadButtons { get; set; } = new();
        public ObservableCollection<ActionBindingItem> Inputs
        {
            get
            {
                var inputs = new ObservableCollection<ActionBindingItem>();
                foreach (var kc in KeyCombos)
                    inputs.Add(new ActionBindingItem(new KeyGesture(kc.Key, kc.Modifiers).ToString(), this, kc));
                foreach (var b in GamepadButtons)
                    inputs.Add(new ActionBindingItem(b.ToString(), this, b));
                return inputs;
            }
        } 
        public ActionBindings() { }
        public ActionBindings(string actionName, Action action)
        {
            ActionName = actionName;
            Action = action;
        }

        public bool Add(KeyCombo combo)
        {
            if (UsedKeyCombos.Contains(combo)) return false;
            UsedKeyCombos.Add(combo);
            KeyCombos.Add(combo);
            Instance._keyUpMap[combo] = this;
            OnPropertyChanged(nameof(Inputs));
            return true;
        }

        public bool Add(ButtonName button)
        {
            if (UsedButtons.Contains(button)) return false;
            UsedButtons.Add(button);
            GamepadButtons.Add(button);
            Instance._buttonUpMap[button] = this;
            OnPropertyChanged(nameof(Inputs));
            return true;
        }

        public bool Remove(KeyCombo combo)
        {
            if (!KeyCombos.Remove(combo)) return false;
            UsedKeyCombos.Remove(combo);
            Instance._keyUpMap.Remove(combo);
            OnPropertyChanged(nameof(Inputs));
            return true;
        }

        public bool Remove(ButtonName button)
        {
            if (!GamepadButtons.Remove(button)) return false;
            UsedButtons.Remove(button);
            Instance._buttonUpMap.Remove(button);
            OnPropertyChanged(nameof(Inputs));
            return true;
        }

        public void Clear()
        {
            KeyCombos.Clear();
            GamepadButtons.Clear();
        }
        private static bool _isListening;
        
        [RelayCommand (CanExecute = nameof(CanAddListenerBinding))]
        private void AddListenerBinding()
        {
            if (_isListening) return;
            _isListening = true;
            
            //the button used to call this command will hog keyboard input
            //if we don't rip focus away from it, we don't want that.
            Instance.FocusDump?.Focus();
            
            var okcDlg = new OkCancelViewModel
            {
                Title = "Listening",
                Content = "Waiting for Keyboard or Controller Button Capture",
                ShowOkButton = false,
            };
            Task<bool?>? listeningDlgTask = null;
            Window? listeningDialog = null;
            // Setup listener callbacks
            KeyComboListener = (combo) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    if (UsedKeyCombos.Contains(combo))
                    {
                        if (listeningDlgTask == null || listeningDlgTask.IsCompleted) return;

                        var action = Instance._keyUpMap[combo];
                        string keystring = new KeyGesture(combo.Key, combo.Modifiers).ToString();
                        okcDlg.Content = $"[{keystring}] already assigned to Action: {action.ActionName}! Overwrite?";
                        okcDlg.ShowOkButton = true;
                        okcDlg.Refresh();

                        var overwrite = await listeningDlgTask;
                        if (overwrite == true)
                        {
                            action.Remove(combo);
                            Add(combo);
                        }
                    }
                    else
                    {
                        Add(combo);
                        listeningDialog?.Close();
                    }

                    ClearListeners();
                });
            };

            GamepadButtonListener = (button) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    if (UsedButtons.Contains(button))
                    {
                        if (listeningDlgTask == null || listeningDlgTask.IsCompleted) return;

                        var action = Instance._buttonUpMap[button];
                        okcDlg.Content = $"{button} already assigned to Action: {action.ActionName}! Overwrite?";
                        okcDlg.ShowOkButton = true;
                        okcDlg.Refresh();

                        var overwrite = await listeningDlgTask;
                        if (overwrite == true)
                        {
                            action.Remove(button);
                            Add(button);
                        }
                    }
                    else
                    {
                        Add(button);
                        listeningDialog?.Close();
                    }

                    ClearListeners();
                });
            };

            // Show the dialog
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Instance.UserSettingsWindow != null)
                {
                    listeningDialog = (Instance.DialogService as DialogService)?.CreateWindowForViewModel(okcDlg);
                    if (listeningDialog != null)
                    {
                        listeningDialog.DataContext = okcDlg;
                        listeningDlgTask = listeningDialog.ShowDialog<bool?>(Instance.UserSettingsWindow);

                        // Cleanup on dialog close
                        listeningDlgTask.ContinueWith(_ => Dispatcher.UIThread.Post(ClearListeners));
                    }
                }
            });

            void ClearListeners()
            {
                KeyComboListener = null;
                GamepadButtonListener = null;
                listeningDialog = null;
                listeningDlgTask = null;
                _isListening = false;
                Instance.SaveBindings();
            }
        }
        public bool CanAddListenerBinding() => !_isListening;
    }
    


    public void OnUserSettingsEditorKeyUp(object? sender, KeyEventArgs e)
    {
        var combo = new KeyCombo(e.Key, e.KeyModifiers);
        if (KeyComboListener != null)
        {
            GamepadButtonListener = null;
            KeyComboListener.Invoke(combo);
            KeyComboListener = null;
            e.Handled = true;
        }
    }

    public void OnUserSettingsEditorButtonUp(object? sender, ButtonEventArgs e)
    {
        if (GamepadButtonListener != null)
        {
            KeyComboListener = null;
            GamepadButtonListener.Invoke(e.Button);
            GamepadButtonListener = null;
            e.Handled = true;
        }
    }
    public partial class ActionBindingItem : ViewModelBase
    {
        public string Name { get; init; }
        public KeyCombo? KeyCombo { get; init; } = null;
        public ButtonName? GamepadButton { get; init; } = null;
        public string ActionName { get; init; }
        
        [JsonIgnore]public bool IsKeyCombo => KeyCombo != null;
        [JsonIgnore]public bool IsGamepadButton => GamepadButton != null;
        [JsonIgnore]private ActionBindings Owner { get; init; }

        public ActionBindingItem() { }

        public ActionBindingItem(string name, ActionBindings owner, KeyCombo combo) {
            Name = name;
            Owner = owner;
            KeyCombo = combo;
            ActionName= owner.ActionName;
        }

        public ActionBindingItem(string name, ActionBindings owner, ButtonName button)
        {
            Name = name;
            Owner = owner;
            GamepadButton = button;
            ActionName = owner.ActionName;
        }
        [RelayCommand]
        private void Remove()
        {
            if (KeyCombo != null)
                Owner.Remove(KeyCombo);
            else if (GamepadButton != null)
                Owner.Remove(GamepadButton.Value);
        }
    }

}
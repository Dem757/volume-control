﻿using HotkeyLib;
using System.Collections.Specialized;
using System.Windows.Interop;
using VolumeControl.Hotkeys.Interfaces;
using VolumeControl.Log;
using VolumeControl.WPF;
using VolumeControl.WPF.Collections;

namespace VolumeControl.Hotkeys
{
    /// <summary>This object is responsible for managing hotkeys at runtime.</summary>
    public class HotkeyManager : INotifyCollectionChanged, IDisposable
    {
        #region Initializers
        /// <inheritdoc cref="HotkeyManager"/>
        /// <param name="actionManager">The action manager to use.</param>
        /// <param name="loadNow">When true, the <see cref="LoadHotkeys"/> method is called from the constructor. Set this to false if you want to do it yourself.</param>
        public HotkeyManager(IHotkeyActionManager actionManager, bool loadNow = false)
        {
            _hotkeyActions = actionManager;
            AddHook();
            if (loadNow)
                LoadHotkeys();
        }
        #endregion Initializers

        #region Fields
        public IntPtr OwnerHandle;
        private HwndSource HwndSource = null!;
        private readonly IHotkeyActionManager _hotkeyActions = null!;
        private bool disposedValue;
        #endregion Fields

        #region Events
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => ((INotifyCollectionChanged)Hotkeys).CollectionChanged += value;
            remove => ((INotifyCollectionChanged)Hotkeys).CollectionChanged -= value;
        }
        #endregion Events

        #region Properties
        private static HotkeyManagerSettings Settings => HotkeyManagerSettings.Default;
        private static LogWriter Log => FLog.Log;
        /// <summary>
        /// Action manager object
        /// </summary>
        public IHotkeyActionManager Actions => _hotkeyActions;
        public ObservableList<BindableWindowsHotkey> Hotkeys { get; } = new();
        #endregion Properties

        #region Methods
        #region HotkeysListManipulators
        /// <summary>
        /// Create a new hotkey and add it to <see cref="Hotkeys"/>.
        /// </summary>
        /// <param name="name">The name of the new hotkey.</param>
        /// <param name="keys">The key combination of the new hotkey.</param>
        /// <param name="action">The associated action of the new hotkey.</param>
        /// <param name="registerNow">When true, the new hotkey is registered immediately after construction.</param>
        public void AddHotkey(string name, IKeyCombo keys, string action, bool registerNow = false)
        {
            var hk = new BindableWindowsHotkey(this, name, keys, action, registerNow);
            Hotkeys.Add(hk);
            Log.Info($"Created a new hotkey entry:", hk.GetFullIdentifier());
        }
        /// <summary>
        /// Create a new blank hotkey and add it to <see cref="Hotkeys"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="BindableWindowsHotkey.Name"/> = <see cref="string.Empty"/>
        /// </remarks>
        public void AddHotkey() => AddHotkey(string.Empty, new KeyCombo(), "None", false);
        /// <summary>
        /// Remove the specified hotkey from <see cref="Hotkeys"/>.
        /// </summary>
        /// <param name="hk">The <see cref="BindableWindowsHotkey"/> object to remove.<br/>If this is null, nothing happens.</param>
        public void DelHotkey(BindableWindowsHotkey? hk)
        {
            if (hk == null)
                return;
            Hotkeys.Remove(hk);
            Log.Info($"Deleted hotkey {hk.ID} '{hk.Name}'");
        }
        /// <summary>
        /// Remove the specified hotkey from <see cref="Hotkeys"/>.
        /// </summary>
        /// <param name="id">The ID number of the hotkey to delete.</param>
        public void DelHotkey(int id)
        {
            for (int i = Hotkeys.Count - 1; i >= 0; --i)
            {
                if (Hotkeys[i].ID.Equals(id))
                    Hotkeys.RemoveAt(i);
            }
        }
        /// <summary>
        /// Deletes all hotkeys in the list by first disposing them, then removing them from the list.
        /// </summary>
        public void DelAllHotkeys()
        {
            for (int i = Hotkeys.Count - 1; i >= 0; --i)
            {
                Hotkeys[i].Dispose();
                Hotkeys.RemoveAt(i);
            }
        }
        #endregion HotkeysListManipulators

        #region HotkeysListGetters
        public BindableWindowsHotkey? GetHotkey(int id) => Hotkeys.FirstOrDefault(hk => hk is not null && hk.ID.Equals(id), null);
        #endregion HotkeysListGetters

        #region HotkeysListSaveLoad
        /// <summary>
        /// Loads hotkeys from the settings file and binds them to the associated actions.
        /// </summary>
        /// <remarks><b>Make sure that the <see cref="Actions"/> property is set and initialized before calling this!</b></remarks>
        public void LoadHotkeys()
        {
            // set the settings hotkeys to default if they're null
            StringCollection? list = Settings.Hotkeys ??= Settings.Hotkeys_Default;

            // Load Hotkeys From Settings
            for (int i = 0, end = list.Count; i < end; ++i)
            {
                if (list[i] is not string s || s.Length < 2) //< 2 is the minimum valid length "::" (no name, null keys)
                {
                    Log.Error($"Hotkeys[{i}] wasn't a valid hotkey string!");
                    continue;
                }

                var hk = BindableWindowsHotkey.Parse(s, this);
                Hotkeys.Add(hk);

                Log.Debug($"Hotkeys[{i}] ('{s}') was successfully parsed:", hk.GetFullIdentifier());
            }
        }
        /// <summary>
        /// Saves all hotkeys to the settings file.
        /// </summary>
        public void SaveHotkeys()
        {
            // Save Hotkeys To Settings
            Log.Debug($"Saving {Hotkeys.Count} hotkeys...");
            StringCollection list = new();
            foreach (BindableWindowsHotkey? hk in Hotkeys)
            {
                string serialized = hk.Serialize();
                list.Add(serialized);
                Log.Debug(hk.GetFullIdentifier(), $" => '{serialized}'");
            }
            Settings.Hotkeys = list;
            Log.Debug($"Successfully saved {list.Count} hotkeys.");
            // Save Settings
            Settings.Save();
            Settings.Reload();
        }
        public void ResetHotkeys()
        {
            DelAllHotkeys();
            Settings.Hotkeys = null;
            Settings.Save();
            Settings.Reload();
            LoadHotkeys();
        }
        #endregion HotkeysListSaveLoad

        #region WindowsMessageHook
        /// <summary>
        /// Handles window messages, and triggers <see cref="WindowsHotkey.Pressed"/> events.
        /// </summary>
        protected virtual IntPtr HwndHook(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
            case (int)HotkeyAPI.WM_HOTKEY:
                int pressedID = wParam.ToInt32();
                if (GetHotkey(pressedID) is BindableWindowsHotkey hk)
                {
                    hk.NotifyPressed(); //< trigger the associated hotkey's Pressed event
                    handled = true;
                }
                break;
            }
            return IntPtr.Zero;
        }
        /// <summary>
        /// Adds a window message hook to receive hotkey press messages and route them to the associated event trigger.
        /// </summary>
        protected virtual void AddHook()
        {
            if (HwndSource == null)
                HwndSource = WindowHandleGetter.GetHwndSource(OwnerHandle = WindowHandleGetter.GetWindowHandle());
            HwndSource.AddHook(HwndHook);
            Log.Debug("HotkeyManager HwndHook was added, ready to receive 'WM_HOTKEY' messages.");
        }
        /// <summary>
        /// Removes the message hook from the application's handle.
        /// </summary>
        protected virtual void RemoveHook()
        {
            HwndSource.RemoveHook(HwndHook);
            HwndSource.Dispose();
            Log.Debug("HotkeyManager HwndHook was removed, 'WM_HOTKEY' messages will no longer be received.");
            HwndSource = null!;
        }
        #endregion WindowsMessageHook

        #region IDisposable
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    SaveHotkeys(); //< this saves hotkeys to the settings file
                    DelAllHotkeys(); //< this cleans up Windows API hotkey registrations
                    RemoveHook(); //< this removes the message hook and disposes of HwndSource
                }

                OwnerHandle = IntPtr.Zero;
                disposedValue = true;
            }
        }
        ~HotkeyManager() { Dispose(disposing: false); }
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion IDisposable
        #endregion Methods
    }
}

﻿using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ObservableImmutable;
using System.Collections;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using VolumeControl.Audio.Interfaces;
using VolumeControl.Log;
using VolumeControl.TypeExtensions;

namespace VolumeControl.Audio
{
    /// <summary>Represents an audio device endpoint.<br/>Note that this object cannot be constructed externally.<br/>You can retrieve already-created audio devices through the <see cref="AudioAPI"/> class.</summary>
    /// <remarks>This object uses the MMDevice API from NAudio, and implements the following interfaces:
    /// <list type="bullet">
    /// <item><description><see cref="IDevice"/></description></item>
    /// <item><description><see cref="INotifyPropertyChanged"/></description></item>
    /// <item><description><see cref="IDisposable"/></description></item>
    /// <item><description><see cref="IEquatable{AudioDevice}"/></description></item>
    /// <item><description><see cref="IEquatable{IDevice}"/></description></item>
    /// <item><description><see cref="IList"/></description></item>
    /// <item><description><see cref="ICollection"/></description></item>
    /// <item><description><see cref="IEnumerable"/></description></item>
    /// <item><description><see cref="IList{AudioSession}"/></description></item>
    /// <item><description><see cref="IImmutableList{AudioSession}"/></description></item>
    /// <item><description><see cref="ICollection{AudioSession}"/></description></item>
    /// <item><description><see cref="IEnumerable{AudioSession}"/></description></item>
    /// <item><description><see cref="IReadOnlyList{AudioSession}"/></description></item>
    /// <item><description><see cref="IReadOnlyCollection{AudioSession}"/></description></item>
    /// <item><description><see cref="INotifyCollectionChanged"/></description></item>
    /// <item><description><see cref="INotifyPropertyChanged"/></description></item>
    /// </list>
    /// Note that audio devices implement interfaces so that they may act similarly to lists of audio sessions.<br/>Some properties in this object require the NAudio library.<br/>If you're writing an addon, install the 'NAudio' nuget package if you need to be able to use them.
    /// </remarks>
    public sealed class AudioDevice : IDevice, IDisposable, IEquatable<AudioDevice>, IEquatable<IDevice>, IList, ICollection, IEnumerable, IList<AudioSession>, IImmutableList<AudioSession>, ICollection<AudioSession>, IEnumerable<AudioSession>, IReadOnlyList<AudioSession>, IReadOnlyCollection<AudioSession>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region Constructors
        /// <inheritdoc cref="AudioDevice"/>
        /// <param name="device">An enumerated <see cref="MMDevice"/> instance from NAudio.</param>
        internal AudioDevice(MMDevice device)
        {
            _device = device;
            _deviceID = _device.ID;

            if (SessionManager != null)
            {
                SessionManager.OnSessionCreated += HandleSessionCreated;
                ReloadSessions();
            }
        }
        #endregion Constructors

        #region Fields
        private readonly string _deviceID;
        private readonly MMDevice _device;
        private (ImageSource?, ImageSource?)? _icons = null;
        #endregion Fields

        #region Properties
        private static LogWriter Log => FLog.Log;
        internal MMDevice MMDevice => _device;
        /// <summary>Whether this device is enabled by the user or not.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                NotifyPropertyChanged();
                NotifyEnabledChanged();
            }
        }
        private bool _enabled = false;
        /// <inheritdoc/>
        public string Name => FriendlyName;
        /// <inheritdoc/>
        public string DeviceID => _deviceID;
        /// <summary>The instance ID.</summary>
        public string InstanceID => _device.InstanceId;
        /// <summary>This is the location of this device's icon on the system and may or may not actually be set.</summary>
        /// <remarks>This can point to nothing, one icon in a DLL package, an executable, or all sorts of other formats.<br/>Do not use this for retrieving icon images directly, instead use <see cref="SmallIcon"/>/<see cref="LargeIcon"/>.<br/>You can also use <see cref="Icon"/> directly if you don't care about the icon size.</remarks>
        public string IconPath => _device.IconPath;
        /// <summary>
        /// The small icon located at the <see cref="IconPath"/>, or null if no icon was found.
        /// </summary>
        /// <remarks>Note that the icon properties all use internal caching; you don't need to worry about using these properties repeatedly for fear of performance loss, as the only time the icons are actually retrieved is the first time any of the icon properties are called.</remarks>
        public ImageSource? SmallIcon => (_icons ??= this.GetIcons())?.Item1;
        /// <summary>
        /// The large icon located at the <see cref="IconPath"/>, or null if no icon was found.
        /// </summary>
        /// <remarks>Note that the icon properties all use internal caching; you don't need to worry about using these properties repeatedly for fear of performance loss, as the only time the icons are actually retrieved is the first time any of the icon properties are called.</remarks>
        public ImageSource? LargeIcon => (_icons ??= this.GetIcons())?.Item2;
        /// <summary>
        /// Returns <see cref="SmallIcon"/> or if it is set to null, <see cref="LargeIcon"/> is returned instead.<br/>
        /// If both icons are set to null, this returns null.
        /// </summary>
        /// <remarks>Note that the icon properties all use internal caching; you don't need to worry about using these properties repeatedly for fear of performance loss, as the only time the icons are actually retrieved is the first time any of the icon properties are called.</remarks>
        public ImageSource? Icon => SmallIcon ?? LargeIcon;
        /// <summary>
        /// Gets the friendly name of the endpoint adapter, excluding the name of the device.<br/>Example: "Speakers (XYZ Audio Adapter)"
        /// </summary>
        /// <remarks>For more information on this property, see MSDN: <see href="https://docs.microsoft.com/en-us/windows/win32/coreaudio/pkey-device-friendlyname">PKEY_Device_FriendlyName</see></remarks>
        public string DeviceFriendlyName => _device.DeviceFriendlyName;
        /// <summary>
        /// Gets the friendly name of the endpoint device, including the name of the adapter.<br/>Example: "Speakers (XYZ Audio Adapter)"
        /// </summary>
        /// <remarks>For more information on this property, see MSDN: <see href="https://docs.microsoft.com/en-us/windows/win32/coreaudio/pkey-device-friendlyname">PKEY_Device_FriendlyName</see></remarks>
        public string FriendlyName => _device.FriendlyName;
        /// <summary>
        /// Gets the underlying <see cref="PropertyStore"/> object for this endpoint.
        /// </summary>
        /// <remarks>This object is from NAudio.<br/>If you're writing an addon, install the 'NAudio' nuget package to be able to use this.</remarks>
        public PropertyStore Properties => _device.Properties;
        /// <summary>
        /// Gets the current <see cref="DeviceState"/> of this endpoint.<br/>For more information, see the MSDN page: <see href="https://docs.microsoft.com/en-us/windows/win32/coreaudio/device-state-xxx-constants">DEVICE_STATE_XXX Constants</see>
        /// </summary>
        /// <remarks>This object is from NAudio.<br/>If you're writing an addon, install the 'NAudio' nuget package to be able to use this.</remarks>
        public DeviceState State => _device.State;
        /// <summary>
        /// Gets the <see cref="AudioSessionManager"/> object from the underlying <see cref="MMDevice"/>.
        /// </summary>
        /// <remarks>This object is from NAudio.<br/>If you're writing an addon, install the 'NAudio' nuget package to be able to use this.</remarks>
        public AudioSessionManager? SessionManager => _device.State.Equals(DeviceState.Active) ? _device.AudioSessionManager : null;
        /// <summary>
        /// Gets the <see cref="AudioEndpointVolume"/> object from the underlying <see cref="MMDevice"/>.
        /// </summary>
        /// <remarks>This object is from NAudio.<br/>If you're writing an addon, install the 'NAudio' nuget package to be able to use this.</remarks>
        public AudioEndpointVolume EndpointVolumeObject => _device.AudioEndpointVolume;
        /// <summary>
        /// Gets the minimum and maximum boundary values of this device's endpoint volume.<br/>
        /// This is used in conjunction with <see cref="MathExt.Scale(float,float,float,float,float)"/> by the <see cref="NativeEndpointVolume"/> property, &amp; by extension the <see cref="EndpointVolume"/> property as well.
        /// </summary>
        internal (float, float) VolumeRange
        {
            get
            {
                var range = EndpointVolumeObject.VolumeRange;
                return (range.MinDecibels, range.MaxDecibels);
            }
        }
        /// <inheritdoc/>
        public float NativeEndpointVolume
        {
            get => MathExt.Scale(EndpointVolumeObject.MasterVolumeLevel, VolumeRange, (0f, 1f));
            set
            {
                EndpointVolumeObject.MasterVolumeLevel = MathExt.Scale(MathExt.Clamp(value, 0f, 1f), (0f, 1f), VolumeRange);
                NotifyPropertyChanged();
            }
        }
        /// <inheritdoc/>
        public int EndpointVolume
        {
            get => Convert.ToInt32(NativeEndpointVolume * 100f);
            set
            {
                NativeEndpointVolume = (float)(Convert.ToDouble(MathExt.Clamp(value, 0, 100)) / 100.0);
                NotifyPropertyChanged();
            }
        }
        /// <inheritdoc/>
        public bool EndpointMuted
        {
            get => EndpointVolumeObject.Mute;
            set
            {
                EndpointVolumeObject.Mute = value;
                NotifyPropertyChanged();
            }
        }
        /// <summary>
        /// The sessions that are playing on this device.
        /// </summary>
        public ObservableImmutableList<AudioSession> Sessions { get; } = new();
        #region InterfaceProperties
        /// <inheritdoc/>
        public bool IsFixedSize => ((IList)Sessions).IsFixedSize;
        /// <inheritdoc/>
        public bool IsReadOnly => ((IList)Sessions).IsReadOnly;
        /// <inheritdoc/>
        public int Count => ((ICollection)Sessions).Count;
        /// <inheritdoc/>
        public bool IsSynchronized => ((ICollection)Sessions).IsSynchronized;
        /// <inheritdoc/>
        public object SyncRoot => ((ICollection)Sessions).SyncRoot;
        /// <inheritdoc/>
        AudioSession IReadOnlyList<AudioSession>.this[int index] => ((IReadOnlyList<AudioSession>)Sessions)[index];
        /// <inheritdoc/>
        AudioSession IList<AudioSession>.this[int index] { get => ((IList<AudioSession>)Sessions)[index]; set => ((IList<AudioSession>)Sessions)[index] = value; }
        /// <inheritdoc/>
        public object? this[int index] { get => ((IList)Sessions)[index]; set => ((IList)Sessions)[index] = value; }
        #endregion InterfaceProperties
        #endregion Properties

        #region Events
        /// <summary>Triggered when an audio session is removed from this device.</summary>
        public event EventHandler<AudioSession>? SessionRemoved;
        private void NotifySessionRemoved(AudioSession session) => SessionRemoved?.Invoke(this, session);
        /// <summary>Triggered when an audio session is added to this device.</summary>
        public event EventHandler<AudioSession>? SessionCreated;
        private void NotifySessionCreated(AudioSession session) => SessionCreated?.Invoke(this, session);
        /// <summary>Triggered when the value of the <see cref="Enabled"/> property was changed.</summary>
        public event EventHandler<bool>? EnabledChanged;
        private void NotifyEnabledChanged() => EnabledChanged?.Invoke(this, Enabled);
        /// <summary>Triggered when any property in the <see cref="Properties"/> container was changed.</summary>
        public event EventHandler<PropertyKey>? PropertyStoreChanged;
        internal void ForwardPropertyStoreChanged(object? _, PropertyKey propertyKey) => PropertyStoreChanged?.Invoke(this, propertyKey);

        /// <summary>Audio device was removed.</summary>
        public event EventHandler? Removed;
        internal void ForwardRemoved(object? _, EventArgs e) => Removed?.Invoke(this, e);

        /// <summary>Triggered when the <see cref="State"/> property is changed.</summary>
        /// <remarks>This event is routed from the windows API.</remarks>
        internal event EventHandler<DeviceState>? StateChanged;
        internal void ForwardStateChanged(object? _, DeviceState e) => StateChanged?.Invoke(this, e);

        /// <summary>
        /// Triggered when the endpoint volume changes from any source.
        /// </summary>
        public event EventHandler<(float, bool)>? VolumeChanged;
        internal void ForwardVolumeChanged(AudioVolumeNotificationData data) => VolumeChanged?.Invoke(this, (data.MasterVolume, data.Muted));
        /// <summary>Triggered when the <see cref="Sessions"/> collection is modified.</summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => ((INotifyCollectionChanged)Sessions).CollectionChanged += value;
            remove => ((INotifyCollectionChanged)Sessions).CollectionChanged -= value;
        }
        /// <summary>Triggered when a property is set.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new(propertyName));
        #endregion Events

        #region SessionEventHandlers
        /// <summary>Handles <see cref="AudioSessionManager.OnSessionCreated"/> events, and adds them to the device's session list.</summary>
        private void HandleSessionCreated(object? sender, IAudioSessionControl controller)
        {
            var session = new AudioSession(new AudioSessionControl(controller));
            // Add session to the list
            Sessions.Add(session);
            NotifySessionCreated(session);

            // Bind events to handlers:
            session.StateChanged += HandleSessionStateChanged;

            Log.Debug($"{session.ProcessIdentifier} created an audio session on device '{Name}'");
        }
        /// <summary>Handles <see cref="AudioSession.StateChanged"/> events from sessions within the <see cref="Sessions"/> list.</summary>
        private void HandleSessionStateChanged(object? sender, AudioSessionState state)
        {
            if (sender is AudioSession session)
            {
                switch (state)
                {
                case AudioSessionState.AudioSessionStateExpired: // triggered when a session is closing
                case AudioSessionState.AudioSessionStateInactive: // triggered when a session is deactivated
                    if (!session.IsRunning)
                    {
                        // unbind events because why not
                        session.StateChanged -= HandleSessionStateChanged;
                        session.NotifyExited();
                        // Remove the session from the list & dispose of it
                        Sessions.Remove(session);
                        NotifySessionRemoved(session);
                        Log.Debug($"{session.ProcessIdentifier} exited.");
                        session.Dispose();
                        session = null!;
                        return;
                    }
                    break;
                case AudioSessionState.AudioSessionStateActive: // triggered when a session is activated
                default: break;
                }
                Log.Debug($"{session.ProcessIdentifier} state changed to {state:G}");
            }
            else throw new InvalidOperationException($"{nameof(HandleSessionStateChanged)} was called with illegal type '{sender?.GetType().FullName}' for parameter '{nameof(sender)}'! Expected an object of type {typeof(AudioSession).FullName}");
        }
        #endregion SessionEventHandlers

        #region Methods
        #region Sessions
        /// <summary>Clears the <see cref="Sessions"/> list, disposing of all items, and reloads all sessions from the <see cref="SessionManager"/>.</summary>
        /// <remarks>This should only be used when initializing a new device, or if an error occurs.<br/>If this method is called when the <see cref="State"/> property isn't set to <see cref="DeviceState.Active"/>, the session list is cleared without reloading.<br/>This is because inactive devices do not have a valid <see cref="SessionManager"/> object.</remarks>
        internal void ReloadSessions()
        {
            Sessions.ForEach(s => s.Dispose());
            Sessions.Clear();

            if (SessionManager == null)
                return;

            SessionManager.RefreshSessions();
            SessionCollection? sessions = SessionManager.Sessions;

            for (int i = 0; i < sessions.Count; ++i)
            {
                var s = new AudioSession(sessions[i]);
                s.StateChanged += HandleSessionStateChanged;
                if (s.IsRunning)
                    Sessions.Add(s);
            }
        }
        /// <summary>
        /// Gets the list of audio sessions currently using this device.
        /// </summary>
        /// <returns>A list of new <see cref="AudioSession"/> instances.</returns>
        internal static List<AudioSession> GetAudioSessions(AudioSessionManager manager)
        {
            manager.RefreshSessions();
            SessionCollection? sessions = manager.Sessions;
            List<AudioSession> l = new();
            for (int i = 0; i < sessions.Count; ++i)
            {
                var s = new AudioSession(sessions[i]);
                if (s.IsRunning)
                    l.Add(s);
            }
            return l;
        }
        /// <summary>Finds all sessions with the given <paramref name="predicate"/>.</summary>
        /// <param name="predicate">A predicate function that accepts <see cref="AudioSession"/>.</param>
        /// <returns>An array of matching <see cref="AudioSession"/> instances.</returns>
        public AudioSession[] FindAll(Predicate<AudioSession> predicate)
        {
            List<AudioSession> l = new();
            foreach (var session in Sessions)
                if (predicate(session))
                    l.Add(session);
            return l.ToArray();
        }
        #endregion Sessions
        #region Other
        /// <inheritdoc cref="IconGetter.GetIcons(string)"/>
        public (ImageSource?, ImageSource?)? GetIcons()
        {
            try
            {
                if (IconPath.Length > 0)
                    return IconGetter.GetIcons(IconPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex);
            }
            return null;
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            _device.Dispose();
            _icons = null;
            GC.SuppressFinalize(this);
        }
        /// <inheritdoc/>
        public bool Equals(AudioDevice? other) => other is not null && other.DeviceID.Equals(DeviceID, StringComparison.Ordinal);
        /// <inheritdoc/>
        public bool Equals(IDevice? other) => other is not null && other.DeviceID.Equals(DeviceID, StringComparison.Ordinal);
        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as AudioDevice);
        /// <inheritdoc/>
        public override int GetHashCode() => DeviceID.GetHashCode();
        #endregion Other
        #region InterfaceMethods
        /// <inheritdoc/>
        public int Add(object? value) => ((IList)Sessions).Add(value);
        /// <inheritdoc/>
        public void Clear() => ((IList)Sessions).Clear();
        /// <inheritdoc/>
        public bool Contains(object? value) => ((IList)Sessions).Contains(value);
        /// <inheritdoc/>
        public int IndexOf(object? value) => ((IList)Sessions).IndexOf(value);
        /// <inheritdoc/>
        public void Insert(int index, object? value) => ((IList)Sessions).Insert(index, value);
        /// <inheritdoc/>
        public void Remove(object? value) => ((IList)Sessions).Remove(value);
        /// <inheritdoc/>
        public void RemoveAt(int index) => ((IList)Sessions).RemoveAt(index);
        /// <inheritdoc/>
        public void CopyTo(Array array, int index) => ((ICollection)Sessions).CopyTo(array, index);
        /// <inheritdoc/>
        public IEnumerator GetEnumerator() => ((IEnumerable)Sessions).GetEnumerator();
        /// <inheritdoc/>
        public int IndexOf(AudioSession item) => ((IList<AudioSession>)Sessions).IndexOf(item);
        /// <inheritdoc/>
        public void Insert(int index, AudioSession item) => ((IList<AudioSession>)Sessions).Insert(index, item);
        /// <inheritdoc/>
        public void Add(AudioSession item) => ((ICollection<AudioSession>)Sessions).Add(item);
        /// <inheritdoc/>
        public bool Contains(AudioSession item) => ((ICollection<AudioSession>)Sessions).Contains(item);
        /// <inheritdoc/>
        public void CopyTo(AudioSession[] array, int arrayIndex) => ((ICollection<AudioSession>)Sessions).CopyTo(array, arrayIndex);
        /// <inheritdoc/>
        public bool Remove(AudioSession item) => ((ICollection<AudioSession>)Sessions).Remove(item);
        /// <inheritdoc/>
        IEnumerator<AudioSession> IEnumerable<AudioSession>.GetEnumerator() => ((IEnumerable<AudioSession>)Sessions).GetEnumerator();
        /// <inheritdoc/>
        IImmutableList<AudioSession> IImmutableList<AudioSession>.Add(AudioSession value) => ((IImmutableList<AudioSession>)Sessions).Add(value);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> AddRange(IEnumerable<AudioSession> items) => ((IImmutableList<AudioSession>)Sessions).AddRange(items);
        /// <inheritdoc/>
        IImmutableList<AudioSession> IImmutableList<AudioSession>.Clear() => ((IImmutableList<AudioSession>)Sessions).Clear();
        /// <inheritdoc/>
        public int IndexOf(AudioSession item, int index, int count, IEqualityComparer<AudioSession>? equalityComparer) => ((IImmutableList<AudioSession>)Sessions).IndexOf(item, index, count, equalityComparer);
        /// <inheritdoc/>
        IImmutableList<AudioSession> IImmutableList<AudioSession>.Insert(int index, AudioSession element) => ((IImmutableList<AudioSession>)Sessions).Insert(index, element);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> InsertRange(int index, IEnumerable<AudioSession> items) => ((IImmutableList<AudioSession>)Sessions).InsertRange(index, items);
        /// <inheritdoc/>
        public int LastIndexOf(AudioSession item, int index, int count, IEqualityComparer<AudioSession>? equalityComparer) => ((IImmutableList<AudioSession>)Sessions).LastIndexOf(item, index, count, equalityComparer);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> Remove(AudioSession value, IEqualityComparer<AudioSession>? equalityComparer) => ((IImmutableList<AudioSession>)Sessions).Remove(value, equalityComparer);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> RemoveAll(Predicate<AudioSession> match) => ((IImmutableList<AudioSession>)Sessions).RemoveAll(match);
        /// <inheritdoc/>
        IImmutableList<AudioSession> IImmutableList<AudioSession>.RemoveAt(int index) => ((IImmutableList<AudioSession>)Sessions).RemoveAt(index);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> RemoveRange(IEnumerable<AudioSession> items, IEqualityComparer<AudioSession>? equalityComparer) => ((IImmutableList<AudioSession>)Sessions).RemoveRange(items, equalityComparer);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> RemoveRange(int index, int count) => ((IImmutableList<AudioSession>)Sessions).RemoveRange(index, count);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> Replace(AudioSession oldValue, AudioSession newValue, IEqualityComparer<AudioSession>? equalityComparer) => ((IImmutableList<AudioSession>)Sessions).Replace(oldValue, newValue, equalityComparer);
        /// <inheritdoc/>
        public IImmutableList<AudioSession> SetItem(int index, AudioSession value) => ((IImmutableList<AudioSession>)Sessions).SetItem(index, value);
        #endregion InterfaceMethods
        #endregion Methods
    }
}

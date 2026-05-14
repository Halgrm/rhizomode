#nullable enable

using System;
using System.Collections.Generic;
using R3;
using Rhizomode.OscMidi.Contracts;
using UnityEngine;

#if HAS_MINIS
using UnityEngine.InputSystem;
#endif

namespace Rhizomode.OscMidi.Transport
{
    /// <summary>
    /// MIDI入力サーバー。Minis（Input System互換MIDIパッケージ）を使用。
    /// 未インストール時はスタブとして動作。CC番号+チャンネルごとにObservableを提供。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 12: 旧 static <c>Instance</c> singleton を解消。GameBootstrap が
    /// SerializeField で参照を保持し、<c>OscMidiTransportLifecycleProcessor</c> 経由で
    /// node に <see cref="IMidiSource"/> として注入する。
    /// </remarks>
    public class MidiServer : MonoBehaviour, IMidiSource
    {
        // key: (channel, ccNumber)
        private readonly Dictionary<(int, int), Subject<float>> _ccSubjects = new();
        private readonly Dictionary<(int, int), float> _lastValues = new();

        /// <summary>
        /// 指定MIDI CC番号・チャンネルの値変化Observableを取得する（0-1正規化済み）。
        /// </summary>
        public Observable<float> GetCCObservable(int channel, int ccNumber)
        {
            var key = (channel, ccNumber);
            if (!_ccSubjects.TryGetValue(key, out var subject))
            {
                subject = new Subject<float>();
                _ccSubjects[key] = subject;
                _lastValues[key] = 0f;
            }
            return subject.AsObservable();
        }

        private void Awake()
        {
#if HAS_MINIS
            Debug.Log("[MidiServer] MIDI input active (Minis)");
            SetupMidiCallbacks();
#else
            Debug.LogWarning("[MidiServer] Minis package not installed. MIDI input disabled.");
#endif
        }

#if HAS_MINIS
        private void SetupMidiCallbacks()
        {
            try
            {
                // Minisは Input System経由でMIDIデバイスを自動検出する
                // MidiDevice.current からCC値を取得
                InputSystem.onDeviceChange += OnDeviceChange;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MidiServer] Setup failed: {ex.Message}");
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added)
                Debug.Log($"[MidiServer] MIDI device connected: {device.displayName}");
            else if (change == InputDeviceChange.Removed)
                Debug.Log($"[MidiServer] MIDI device disconnected: {device.displayName}");
        }
#endif

        private void Update()
        {
#if HAS_MINIS
            PollMidiControls();
#endif
        }

#if HAS_MINIS
        private void PollMidiControls()
        {
            try
            {
                // Minis の MidiDevice から CC値を Input System 経由で読む
                foreach (var device in InputSystem.devices)
                {
                    if (device is not Minis.MidiDevice midiDevice) continue;

                    foreach (var ((channel, ccNumber), subject) in _ccSubjects)
                    {
                        // Minis channel is 0-based (0 = "Channel 1")
                        if (midiDevice.channel != channel - 1) continue;

                        var ccControl = midiDevice.GetControl(ccNumber);
                        var value = Mathf.Clamp01(ccControl.ReadValue());
                        var key = (channel, ccNumber);
                        if (!_lastValues.TryGetValue(key, out var last) ||
                            Mathf.Abs(value - last) > 1e-4f)
                        {
                            _lastValues[key] = value;
                            subject.OnNext(value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MidiServer] Poll failed: {ex.Message}");
            }
        }
#endif

        private void OnDestroy()
        {
#if HAS_MINIS
            InputSystem.onDeviceChange -= OnDeviceChange;
#endif

            foreach (var subject in _ccSubjects.Values)
                subject.Dispose();
            _ccSubjects.Clear();
            _lastValues.Clear();
        }
    }
}

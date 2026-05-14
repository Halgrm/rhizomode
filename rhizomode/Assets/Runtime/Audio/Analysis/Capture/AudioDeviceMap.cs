#nullable enable

using System;
using System.Collections.Generic;
using Lasp;
using UnityEngine;

namespace Rhizomode.Audio.Analysis.Capture
{
    /// <summary>
    /// LASP <c>AudioSystem.InputDevices</c> から得たデバイス情報を name → descriptor の
    /// dictionary 形式で保持するヘルパー。
    /// </summary>
    /// <remarks>
    /// Plan v5.3 Phase 10C: AudioAnalyzer 内の <c>_deviceMap</c> + <c>RefreshDeviceMap</c> +
    /// AvailableDevices を独立した class に切り出し、デバイス列挙責務を分離。
    /// </remarks>
    public sealed class AudioDeviceMap
    {
        private readonly Dictionary<string, DeviceDescriptor> _map = new();

        public IReadOnlyDictionary<string, DeviceDescriptor> Devices => _map;

        public bool TryGet(string name, out DeviceDescriptor descriptor)
        {
            return _map.TryGetValue(name, out descriptor);
        }

        /// <summary>
        /// LASP <c>AudioSystem.InputDevices</c> から最新の device 一覧を取得して内部 map を更新する。
        /// </summary>
        public void Refresh()
        {
            _map.Clear();
            try
            {
                foreach (var device in AudioSystem.InputDevices)
                {
                    if (!device.IsValid) continue;
                    var name = device.Name;
                    if (_map.ContainsKey(name))
                        name = $"{name} ({device.ID})";
                    _map[name] = device;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioDeviceMap] Device enumeration failed: {e.Message}");
            }
        }

        public string[] Names()
        {
            var names = new string[_map.Count];
            var i = 0;
            foreach (var name in _map.Keys)
                names[i++] = name;
            return names;
        }
    }
}

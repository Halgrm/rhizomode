#nullable enable

using System;
using R3;
using Rhizomode.Graph.Model;
using Rhizomode.Graph.Serialization;
using Rhizomode.NodeCatalog.Contracts;
using Rhizomode.SharedKernel;
using Rhizomode.UI.Contracts;
using UnityEngine;

namespace Rhizomode.Nodes.Scene
{
    /// <summary>
    /// Requests switching to the selected Cinemachine camera on Active rising edges.
    /// </summary>
    [NodeType("CameraSwitch", "Camera Switch", NodeCategory.Scene)]
    public class CameraSwitchNode : NodeBase, INodeParamAccessor, IInlineButton
    {
        private string _selectedCameraName = "";
        private string[] _cameraNames = Array.Empty<string>();
        private int _cameraIndex = -1;
        private bool _activationRequested;
        private bool _prevActive;

        public string SelectedCameraName => _selectedCameraName;

        string IInlineButton.ButtonLabel =>
            string.IsNullOrEmpty(_selectedCameraName) ? "No Camera" : _selectedCameraName;

        public CameraSwitchNode(string id) : base(id, "CameraSwitch")
        {
            RegisterInput<bool>("Active", ParamType.Bool);
        }

        public override void Setup(GraphState context)
        {
            AddSubscription(
                context.GetInputObservable<bool>(this, "Active")
                    .Subscribe(OnActive));
        }

        void IInlineButton.OnButtonPressed()
        {
            if (_cameraNames.Length == 0) return;

            _cameraIndex = (_cameraIndex + 1) % _cameraNames.Length;
            _selectedCameraName = _cameraNames[_cameraIndex];
        }

        public void SetCameraList(string[] names)
        {
            if (names == null) return;
            _cameraNames = names;
            ResolveCameraIndex();
        }

        public bool ConsumeActivationRequest()
        {
            if (!_activationRequested) return false;
            _activationRequested = false;
            return true;
        }

        bool INodeParamAccessor.TrySetParam(string paramName, ParamValue value) => false;

        bool INodeParamAccessor.TryGetParam(string paramName, out ParamValue value)
        {
            value = default;
            return false;
        }

        public override NodeData ToNodeData()
        {
            var data = base.ToNodeData();
            data.paramsJson = JsonUtility.ToJson(new CameraSwitchParams
            {
                cameraName = _selectedCameraName
            });
            return data;
        }

        public override void RestoreParamsFromJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson)) return;
            try
            {
                var p = JsonUtility.FromJson<CameraSwitchParams>(paramsJson);
                _selectedCameraName = p.cameraName ?? "";
                ResolveCameraIndex();
            }
            catch (Exception)
            {
                // Broken cue JSON is ignored so graph load can continue.
            }
        }

        private void OnActive(bool active)
        {
            if (!_prevActive && active)
                _activationRequested = true;
            _prevActive = active;
        }

        private void ResolveCameraIndex()
        {
            _cameraIndex = -1;
            if (string.IsNullOrEmpty(_selectedCameraName)) return;

            for (var i = 0; i < _cameraNames.Length; i++)
            {
                if (!string.Equals(_cameraNames[i], _selectedCameraName, StringComparison.Ordinal))
                    continue;
                _cameraIndex = i;
                return;
            }
        }

        [Serializable]
        private struct CameraSwitchParams
        {
            public string cameraName;
        }
    }
}

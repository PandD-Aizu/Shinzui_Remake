using UnityEngine;
using Unity.Cinemachine;

namespace Shinzui.View
{
    public partial class PlayerView
    {
        public bool IsHiding { get; private set; }
        private Transform _hidingCamera;
        private Vector3 _savedCameraLocal;
        private Quaternion _savedCameraRotation;
        private CinemachineBrain _hidingBrain;
        private bool _brainEnabled, _controllerEnabled;
        private float _savedNear;
        /// <summary>カメラと移動をロッカーへ固定</summary>
        /// <param name="anchor">スリット内側の視点</param>
        /// <param name="feet">本体の足元位置</param>
        public void BeginHiding(Transform anchor, Vector3 feet)
        {
            _savedCameraLocal = mainCamera.transform.localPosition;
            _savedCameraRotation = mainCamera.transform.localRotation;
            _savedNear = mainCamera.nearClipPlane;
            _hidingBrain = mainCamera.GetComponent<CinemachineBrain>();
            _brainEnabled = _hidingBrain != null && _hidingBrain.enabled;
            if (_hidingBrain != null) _hidingBrain.enabled = false;
            _controllerEnabled = characterController != null && characterController.enabled;
            Warp(feet - transform.TransformVector(Vector3.up * _footLocalY) - transform.position, anchor.forward);
            if (characterController != null) characterController.enabled = false;
            _hidingCamera = anchor; IsHiding = true; mainCamera.nearClipPlane = .02f;
            UpdateHidingCamera(false);
        }
        /// <summary>息止め時はスリットから身を引く</summary>
        /// <param name="holding">息止め中か</param>
        public void UpdateHidingCamera(bool holding)
        {
            if (!IsHiding || _hidingCamera == null) return;
            mainCamera.transform.SetPositionAndRotation(_hidingCamera.position - _hidingCamera.forward * (holding ? .15f : 0f), _hidingCamera.rotation);
        }
        /// <summary>出口へ戻し通常のカメラ制御を復帰</summary>
        /// <param name="exit">出口の足元位置</param>
        /// <param name="forward">出口の向き</param>
        public void EndHiding(Vector3 exit, Vector3 forward)
        {
            if (!IsHiding) return;
            IsHiding = false;
            mainCamera.transform.localPosition = _savedCameraLocal;
            mainCamera.transform.localRotation = _savedCameraRotation;
            mainCamera.nearClipPlane = _savedNear;
            Warp(exit - transform.TransformVector(Vector3.up * _footLocalY) - transform.position, forward);
            if (characterController != null) characterController.enabled = _controllerEnabled;
            if (_hidingBrain != null) _hidingBrain.enabled = _brainEnabled;
            _hidingCamera = null;
        }
    }
}

using Meta.XR.Util;
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Oculus.Interaction.Input.Visuals
{
    [Obsolete("Use " + nameof(ControllerVisual) + " instead.")]
    [SFeature(SFeature.Interaction)]
    public class OVRSControllerVisual : MonoBehaviour
    {
        [SerializeField, Interface(typeof(IController))]
        private UnityEngine.Object _controller;
        public IController Controller;

        [SerializeField]
        private OVRSControllerHelper _ovrControllerHelper;

        public bool ForceOffVisibility { get; set; }

        protected bool _started = false;

        protected virtual void Awake()
        {
            Controller = _controller as IController;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            this.AssertField(Controller, nameof(Controller));
            this.AssertField(_ovrControllerHelper, nameof(_ovrControllerHelper));
            switch (Controller.Handedness)
            {
                case Handedness.Left:
                    _ovrControllerHelper.m_controller = OVRInput.Controller.LTouch;
                    break;
                case Handedness.Right:
                    _ovrControllerHelper.m_controller = OVRInput.Controller.RTouch;
                    break;
            }
            this.EndStart(ref _started);
        }

        protected virtual void OnEnable()
        {
            if (_started)
            {
                Controller.WhenUpdated += HandleUpdated;
            }
        }

        protected virtual void OnDisable()
        {
            if (_started && _controller != null)
            {
                Controller.WhenUpdated -= HandleUpdated;
            }
        }

        private void HandleUpdated()
        {
            if (!Controller.IsConnected ||
                ForceOffVisibility ||
                !Controller.TryGetPose(out Pose rootPose))
            {
                _ovrControllerHelper.gameObject.SetActive(false);
                return;
            }

            _ovrControllerHelper.gameObject.SetActive(true);
            transform.position = rootPose.position;
            transform.rotation = rootPose.rotation;
            float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Controller.Scale / parentScale * Vector3.one;
        }

        #region Inject

        public void InjectAllOVRSControllerVisual(IController controller, OVRSControllerHelper ovrControllerHelper)
        {
            InjectController(controller);
            InjectAllOVRSControllerHelper(ovrControllerHelper);
        }

        public void InjectController(IController controller)
        {
            _controller = controller as UnityEngine.Object;
            Controller = controller;
        }

        public void InjectAllOVRSControllerHelper(OVRSControllerHelper ovrControllerHelper)
        {
            _ovrControllerHelper = ovrControllerHelper;
        }

        #endregion
    }
}

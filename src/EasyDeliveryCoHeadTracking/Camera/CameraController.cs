using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using UnityEngine;
using UnityEngine.Rendering;

namespace EasyDeliveryCoHeadTracking.Camera
{
    /// <summary>
    /// Applies head tracking to the game camera by modifying worldToCameraMatrix.
    /// Hooks both Camera.onPreCull (legacy) and RenderPipelineManager.beginCameraRendering (URP/SRP),
    /// leaving camera.transform untouched so game logic (driving, physics, raycasts) is unaffected.
    /// </summary>
    public class CameraController
    {
        private const float TransitionInDuration = 0.5f;
        private const float TransitionOutDuration = 0.3f;

        private readonly OpenTrackReceiver _receiver;
        private readonly TrackingProcessor _processor;
        private readonly PoseInterpolator _interpolator;
        private readonly PositionProcessor _positionProcessor;
        private readonly PositionInterpolator _positionInterpolator;

        private UnityEngine.Camera _cachedCamera;
        private int _cachedCameraFrame = -1;

        // Current processed values (set in ProcessFrame, applied in OnPreCull).
        private float _currentYaw;
        private float _currentPitch;
        private float _currentRoll;
        private Vec3 _currentPosition;
        private bool _hasPosition;
        private bool _shouldApply;

        // Last applied values, used for the fade-out lerp.
        private float _lastYaw;
        private float _lastPitch;
        private float _lastRoll;
        private Vec3 _lastPosition;

        private bool _wasApplyingTracking;
        private bool _isTransitioningIn;
        private float _transitionInProgress;
        private bool _isTransitioningOut;
        private float _transitionOutProgress;

        // 6DOF stays off until the tracker delivers a non-zero position sample,
        // so 3DOF-only users don't get a position offset before recentering.
        private bool _detected6DOF;

        // worldToCameraMatrix is a sticky override: once set, Unity stops
        // recomputing it from camera.transform each frame. When we stop
        // applying tracking we must call ResetWorldToCameraMatrix() once,
        // otherwise the last head-rotated matrix sticks - producing a
        // permanent residual offset in menus / after toggle-off.
        private bool _needsMatrixReset;

        public bool PositionEnabled { get; set; } = true;
        public bool RotationEnabled { get; set; } = true;
        public bool WorldSpaceYaw { get; set; } = true;
        public bool IsApplyingTracking => _wasApplyingTracking && !_isTransitioningOut;

        public float LastTrackingYaw => _lastYaw;
        public float LastTrackingPitch => _lastPitch;
        public float LastTrackingRoll => _lastRoll;

        public float? GameplayCameraFov
        {
            get
            {
                var cam = GetMainCamera();
                return cam != null ? cam.fieldOfView : (float?)null;
            }
        }

        /// <summary>
        /// Per-frame cached <see cref="UnityEngine.Camera.main"/>. Callers in OnGUI/Update
        /// should prefer this over <c>Camera.main</c> directly to avoid the FindGameObjectWithTag
        /// scan it performs internally.
        /// </summary>
        public UnityEngine.Camera MainCamera => GetMainCamera();

        public CameraController(
            OpenTrackReceiver receiver, TrackingProcessor processor, PoseInterpolator interpolator,
            PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
        {
            _receiver = receiver;
            _processor = processor;
            _interpolator = interpolator;
            _positionProcessor = positionProcessor;
            _positionInterpolator = positionInterpolator;
        }

        public void Enable()
        {
            // Both hooks are needed: onPreCull doesn't fire under SRP/URP, but legacy
            // pipelines don't fire beginCameraRendering. Subscribing to both is safe -
            // a given Unity build only invokes one path per frame.
            UnityEngine.Camera.onPreCull += OnPreCull;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        public void Disable()
        {
            UnityEngine.Camera.onPreCull -= OnPreCull;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            var cam = _cachedCamera;
            if (cam != null)
                cam.ResetWorldToCameraMatrix();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera cam)
        {
            OnPreCull(cam);
        }

        /// <summary>
        /// Process tracking data for this frame. Call from LateUpdate.
        /// </summary>
        public bool ProcessFrame(bool enabled)
        {
            if (enabled && _receiver.IsReceiving)
            {
                _isTransitioningOut = false;

                if (!_wasApplyingTracking)
                    BeginTrackingSession();

                float scale = AdvanceTransitionIn();

                var rawPose = _receiver.GetLatestPose();
                var interpolated = _interpolator.Update(rawPose, Time.deltaTime);
                var processed = _processor.Process(interpolated, Time.deltaTime);

                ApplyRotation(processed, scale);
                ApplyPosition(interpolated, scale);

                _lastYaw = _currentYaw;
                _lastPitch = _currentPitch;
                _lastRoll = _currentRoll;
                _lastPosition = _currentPosition;
                _shouldApply = true;
                _wasApplyingTracking = true;
                return true;
            }

            if (_isTransitioningOut)
            {
                AdvanceTransitionOut();
            }
            else if (_wasApplyingTracking)
            {
                _isTransitioningOut = true;
                _transitionOutProgress = 0f;
                AdvanceTransitionOut();
            }

            return false;
        }

        public void OnTrackingEnabled()
        {
            ResetSmoothingState();
            ResetInterpolators();
            _isTransitioningOut = false;
        }

        /// <summary>
        /// Recenter to the latest received pose. Safe to call regardless of whether
        /// tracking is currently being applied.
        /// </summary>
        public void Recenter()
        {
            RecenterToLatest();
            ResetInterpolators();
        }

        public void OnTrackingDisabled()
        {
            if (_wasApplyingTracking)
            {
                _isTransitioningOut = true;
                _transitionOutProgress = 0f;
            }
        }

        public void ResetState()
        {
            if (_wasApplyingTracking || _isTransitioningOut)
                _needsMatrixReset = true;
            _cachedCamera = null;
            _cachedCameraFrame = -1;
            _isTransitioningOut = false;
            _isTransitioningIn = false;
            _transitionInProgress = 0f;
            _wasApplyingTracking = false;
            _shouldApply = false;
            _lastYaw = 0f;
            _lastPitch = 0f;
            _lastRoll = 0f;
            _lastPosition = Vec3.Zero;
            _currentPosition = Vec3.Zero;
            _hasPosition = false;
            _detected6DOF = false;
            ResetSmoothingState();
            ResetInterpolators();
        }

        private void BeginTrackingSession()
        {
            RecenterToLatest();
            _isTransitioningIn = true;
            _transitionInProgress = 0f;
            _detected6DOF = false;
            ResetInterpolators();
            ResetSmoothingState();
        }

        private void RecenterToLatest()
        {
            _processor.RecenterTo(_receiver.GetLatestPose());
            _positionProcessor.SetCenter(_receiver.GetLatestPosition());
        }

        private void ResetInterpolators()
        {
            _interpolator.Reset();
            _positionInterpolator.Reset();
        }

        private void ResetSmoothingState()
        {
            _processor.ResetSmoothing();
            _positionProcessor.ResetSmoothing();
        }

        private float AdvanceTransitionIn()
        {
            if (!_isTransitioningIn)
                return 1f;

            _transitionInProgress += Time.deltaTime / TransitionInDuration;
            if (_transitionInProgress >= 1f)
            {
                _transitionInProgress = 1f;
                _isTransitioningIn = false;

                // Re-recenter after stabilization so the rest of the session uses
                // a clean reference pose rather than whatever was first received.
                if (_receiver.IsReceiving)
                    RecenterToLatest();
            }
            return _transitionInProgress * _transitionInProgress;
        }

        private void ApplyRotation(TrackingPose processed, float scale)
        {
            if (RotationEnabled)
            {
                _currentYaw = processed.Yaw * scale;
                _currentPitch = processed.Pitch * scale;
                _currentRoll = processed.Roll * scale;
            }
            else
            {
                _currentYaw = 0f;
                _currentPitch = 0f;
                _currentRoll = 0f;
            }
        }

        private void ApplyPosition(TrackingPose interpolated, float scale)
        {
            if (!PositionEnabled)
            {
                _currentPosition = Vec3.Zero;
                _hasPosition = false;
                return;
            }

            var rawPos = _receiver.GetLatestPosition();
            if (!_detected6DOF && (rawPos.X != 0f || rawPos.Y != 0f || rawPos.Z != 0f))
                _detected6DOF = true;

            if (!_detected6DOF)
            {
                _currentPosition = Vec3.Zero;
                _hasPosition = false;
                return;
            }

            var interpolatedPos = _positionInterpolator.Update(rawPos, Time.deltaTime);
            var physicalRotQ = QuaternionUtils.FromYawPitchRoll(
                interpolated.Yaw, -interpolated.Pitch, interpolated.Roll);
            var finalPos = _positionProcessor.Process(interpolatedPos, physicalRotQ, Time.deltaTime);
            _currentPosition = finalPos * scale;
            _hasPosition = true;
        }

        private UnityEngine.Camera GetMainCamera()
        {
            int frame = Time.frameCount;
            if (frame != _cachedCameraFrame)
            {
                _cachedCamera = UnityEngine.Camera.main;
                _cachedCameraFrame = frame;
            }
            return _cachedCamera;
        }

        private void OnPreCull(UnityEngine.Camera cam)
        {
            var mainCam = GetMainCamera();
            if (cam != mainCam || mainCam == null)
                return;

            if (_needsMatrixReset && !_shouldApply && !_isTransitioningOut)
            {
                cam.ResetWorldToCameraMatrix();
                _needsMatrixReset = false;
                return;
            }

            if (_shouldApply)
            {
                ApplyViewMatrix(cam, _currentYaw, _currentPitch, _currentRoll,
                    _hasPosition ? _currentPosition : Vec3.Zero);
                _shouldApply = false;
                return;
            }

            if (_isTransitioningOut)
            {
                float t = _transitionOutProgress;
                float fadedYaw = Mathf.Lerp(_lastYaw, 0f, t);
                float fadedPitch = Mathf.Lerp(_lastPitch, 0f, t);
                float fadedRoll = Mathf.Lerp(_lastRoll, 0f, t);
                var fadedPos = Vec3.Lerp(_lastPosition, Vec3.Zero, t);

                if (fadedYaw != 0f || fadedPitch != 0f || fadedRoll != 0f || IsNonZero(fadedPos))
                    ApplyViewMatrix(cam, fadedYaw, fadedPitch, fadedRoll, fadedPos);
            }
        }

        private void ApplyViewMatrix(UnityEngine.Camera cam, float yaw, float pitch, float roll, Vec3 position)
        {
            if (WorldSpaceYaw)
                ApplyViewMatrixWorldYaw(cam, yaw, pitch, roll, position);
            else
                ApplyViewMatrixLocal(cam, yaw, pitch, roll, position);
        }

        private static void ApplyViewMatrixLocal(UnityEngine.Camera cam, float yaw, float pitch, float roll, Vec3 position)
        {
            cam.ResetWorldToCameraMatrix();
            Matrix4x4 gameMatrix = cam.worldToCameraMatrix;

            var headRot = Quaternion.Euler(pitch, yaw, -roll);
            Matrix4x4 headRotMatrix = Matrix4x4.Rotate(headRot);

            if (IsNonZero(position))
            {
                var offset = new Vector3(position.X, position.Y, position.Z);
                cam.worldToCameraMatrix = headRotMatrix * Matrix4x4.Translate(-offset) * gameMatrix;
            }
            else
            {
                cam.worldToCameraMatrix = headRotMatrix * gameMatrix;
            }
        }

        private static void ApplyViewMatrixWorldYaw(UnityEngine.Camera cam, float yaw, float pitch, float roll, Vec3 position)
        {
            // Cache transform - each property access on Camera goes through a native call.
            var camTransform = cam.transform;
            Quaternion worldYaw = Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion localPitchRoll = Quaternion.Euler(pitch, 0f, roll);
            Quaternion baseRotation = camTransform.rotation;
            Quaternion finalRotation = worldYaw * baseRotation * localPitchRoll;

            Vector3 cameraPos = camTransform.position;
            if (IsNonZero(position))
            {
                Vector3 offsetBody = new Vector3(position.X, position.Y, position.Z);
                cameraPos += baseRotation * offsetBody;
            }

            // R * T(-p) folds into a single matrix: rotation in the upper-left, with the
            // translation column equal to -(R * cameraPos). Building it directly skips
            // the Matrix4x4 * Matrix4x4 product the previous form did every frame.
            // finalRotation is a product of unit quaternions, so its inverse is just its
            // conjugate - skip the Quaternion.Inverse native call and the magnitude divide.
            Quaternion invRot = new Quaternion(-finalRotation.x, -finalRotation.y, -finalRotation.z, finalRotation.w);
            Matrix4x4 viewMatrix = Matrix4x4.Rotate(invRot);
            Vector3 rotatedPos = invRot * cameraPos;
            viewMatrix.m03 = -rotatedPos.x;
            viewMatrix.m13 = -rotatedPos.y;
            viewMatrix.m23 = -rotatedPos.z;

            // worldToCameraMatrix expects negative-Z-forward; flip the third row to match.
            viewMatrix.m20 = -viewMatrix.m20;
            viewMatrix.m21 = -viewMatrix.m21;
            viewMatrix.m22 = -viewMatrix.m22;
            viewMatrix.m23 = -viewMatrix.m23;

            cam.worldToCameraMatrix = viewMatrix;
        }

        private void AdvanceTransitionOut()
        {
            _transitionOutProgress += Time.deltaTime / TransitionOutDuration;
            if (_transitionOutProgress >= 1f)
            {
                _isTransitioningOut = false;
                _wasApplyingTracking = false;
                _shouldApply = false;
                _needsMatrixReset = true;
            }
        }

        private static bool IsNonZero(Vec3 v) => v.X != 0f || v.Y != 0f || v.Z != 0f;
    }
}

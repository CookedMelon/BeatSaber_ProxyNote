using System;
using System.Collections.Generic;
using System.Reflection;
using CameraUtils.Core;
using HarmonyLib;
using UnityEngine;

namespace ProxyNote
{
    internal sealed class ProxyNoteVisualController : MonoBehaviour,
        INoteControllerNoteWasCutEvent,
        INoteControllerNoteWasMissedEvent,
        INoteControllerNoteDidStartDissolvingEvent
    {
        private struct RendererState
        {
            internal Renderer Renderer;
            internal bool Enabled;
            internal MaterialPropertyBlock PropertyBlock;
        }

        private struct LayerState
        {
            internal GameObject GameObject;
            internal int Layer;
        }

        private static readonly HashSet<ProxyNoteVisualController> ActiveControllers =
            new HashSet<ProxyNoteVisualController>();

        private static readonly int[] ColorPropertyIds =
        {
            Shader.PropertyToID("_Color"),
            Shader.PropertyToID("_BaseColor"),
            Shader.PropertyToID("_TintColor")
        };

        private static readonly FieldInfo NoteMovementJumpField =
            AccessTools.Field(typeof(NoteMovement), "_jump");

        private static readonly FieldInfo NoteControllerMovementField =
            AccessTools.Field(typeof(NoteController), "_noteMovement");

        private static readonly FieldInfo NoteMovementZOffsetField =
            AccessTools.Field(typeof(NoteMovement), "_zOffset");

        private static readonly FieldInfo NoteJumpMovementProviderField =
            AccessTools.Field(typeof(NoteJump), "_variableMovementDataProvider");

        private static readonly FieldInfo NoteJumpPlayerTransformsField =
            AccessTools.Field(typeof(NoteJump), "_playerTransforms");

        private static readonly FieldInfo NoteJumpAudioTimeSourceField =
            AccessTools.Field(typeof(NoteJump), "_audioTimeSyncController");

        private static readonly FieldInfo NoteJumpStartRotationField =
            AccessTools.Field(typeof(NoteJump), "_startRotation");

        private static readonly FieldInfo NoteJumpMiddleRotationField =
            AccessTools.Field(typeof(NoteJump), "_middleRotation");

        private static readonly FieldInfo NoteJumpEndRotationField =
            AccessTools.Field(typeof(NoteJump), "_endRotation");

        private static readonly FieldInfo NoteJumpYAvoidanceField =
            AccessTools.Field(typeof(NoteJump), "_yAvoidance");

        private static readonly FieldInfo NoteJumpRotateTowardsPlayerField =
            AccessTools.Field(typeof(NoteJump), "_rotateTowardsPlayer");

        private static readonly FieldInfo NoteJumpPlayerSpaceConvertorField =
            AccessTools.Field(typeof(NoteJump), "_playerSpaceConvertor");

        private readonly List<RendererState> _originalRenderers = new List<RendererState>();
        private readonly List<RendererState> _proxyRenderers = new List<RendererState>();
        private readonly List<RendererState> _debugOriginalRenderers = new List<RendererState>();
        private readonly List<LayerState> _originalLayers = new List<LayerState>();

        private NoteController _noteController;
        private NoteMovement _noteMovement;
        private IVariableMovementDataProvider _movementData;
        private Transform _originalVisualRoot;
        private GameObject _proxyRoot;
        private GameObject _guideRoot;
        private GameObject _debugOriginalRoot;
        private PlayerTransforms _playerTransforms;
        private PlayerSpaceConvertor _playerSpaceConvertor;
        private IAudioTimeSource _audioTimeSource;
        private NoteSpawnData _spawnData;
        private Quaternion _startRotation;
        private Quaternion _middleRotation;
        private Quaternion _endRotation;
        private Quaternion _lastVanillaRotation = Quaternion.identity;
        private float _stableTargetX;
        private float _jumpLeadDistance;
        private float _rotationCoefficient;
        private float _yAvoidance;
        private bool _isBomb;
        private bool _preservePositionSwaps;
        private bool _rotateTowardsPlayer;
        private bool _hasDirectionalCut;
        private bool _guideRetired;
        private bool _initialized;
        private bool _sampleLogged;
        private int _legacyGuideCleanupFrames;
        private bool _debugModeApplied;
        private bool _desktopModeApplied;
        private bool _guideEnabledApplied;
        private float _guideLengthApplied = -1f;
        private float _guideThicknessApplied = -1f;
        private float _guideOffsetApplied = float.MinValue;
        private float _guideHideDistanceApplied = -1f;
        private float _originalOpacityApplied = -1f;
        private float _proxyOpacityApplied = -1f;

        internal static void RestoreAll()
        {
            ProxyNoteVisualController[] snapshot =
                new ProxyNoteVisualController[ActiveControllers.Count];
            ActiveControllers.CopyTo(snapshot);

            foreach (ProxyNoteVisualController controller in snapshot)
            {
                if (controller != null)
                {
                    controller.Shutdown(restoreOriginal: true);
                }
            }
        }

        internal static bool IsReplacing(NoteController noteController)
        {
            ProxyNoteVisualController controller =
                noteController == null
                    ? null
                    : noteController.GetComponent<ProxyNoteVisualController>();
            return controller != null && controller._initialized;
        }

        internal void Initialize(
            GameNoteController noteController,
            NoteData noteData,
            in NoteSpawnData spawnData)
        {
            InitializeCore(
                noteController,
                noteController.noteMovement,
                noteData,
                in spawnData,
                isBomb: false);
        }

        internal void Initialize(
            BurstSliderGameNoteController noteController,
            NoteData noteData,
            in NoteSpawnData spawnData)
        {
            InitializeCore(
                noteController,
                noteController.noteMovement,
                noteData,
                in spawnData,
                isBomb: false);
        }

        internal void Initialize(
            BombNoteController noteController,
            NoteData noteData,
            in NoteSpawnData spawnData)
        {
            NoteMovement movement =
                NoteControllerMovementField?.GetValue(noteController) as NoteMovement;
            InitializeCore(
                noteController,
                movement,
                noteData,
                in spawnData,
                isBomb: true);
        }

        private void InitializeCore(
            NoteController noteController,
            NoteMovement movement,
            NoteData noteData,
            in NoteSpawnData spawnData,
            bool isBomb)
        {
            Shutdown(restoreOriginal: true);

            _noteController = noteController;
            _noteMovement = movement;
            _spawnData = spawnData;
            _isBomb = isBomb;
            _jumpLeadDistance = Mathf.Clamp(
                PluginConfig.Instance.JumpLeadDistance,
                0f,
                5f);
            _rotationCoefficient = isBomb
                ? 1f
                : Mathf.Clamp01(
                    PluginConfig.Instance.NoteRotationCoefficient);
            _preservePositionSwaps =
                isBomb ||
                PluginConfig.Instance.EnableNotePositionSwaps;
            _lastVanillaRotation = Quaternion.identity;
            _hasDirectionalCut =
                !isBomb && noteData.cutDirection != NoteCutDirection.Any;
            _endRotation = isBomb
                ? Quaternion.identity
                : Quaternion.Euler(
                    0f,
                    0f,
                    noteData.cutDirection.RotationAngle() +
                    noteData.cutDirectionAngleOffset);

            NoteJump jump = movement == null
                ? null
                : NoteMovementJumpField.GetValue(movement) as NoteJump;
            _movementData = jump == null
                ? null
                : NoteJumpMovementProviderField.GetValue(jump) as IVariableMovementDataProvider;
            _playerTransforms = jump == null
                ? null
                : NoteJumpPlayerTransformsField.GetValue(jump) as PlayerTransforms;
            _audioTimeSource = jump == null
                ? null
                : NoteJumpAudioTimeSourceField.GetValue(jump) as IAudioTimeSource;
            _playerSpaceConvertor = jump == null
                ? null
                : NoteJumpPlayerSpaceConvertorField.GetValue(jump) as PlayerSpaceConvertor;
            _yAvoidance = ReadField(NoteJumpYAvoidanceField, jump, 0f);
            _rotateTowardsPlayer =
                ReadField(NoteJumpRotateTowardsPlayerField, jump, false);
            if (jump != null)
            {
                _startRotation = ReadField(
                    NoteJumpStartRotationField,
                    jump,
                    Quaternion.identity);
                _middleRotation = ReadField(
                    NoteJumpMiddleRotationField,
                    jump,
                    _endRotation);
                _endRotation = ReadField(
                    NoteJumpEndRotationField,
                    jump,
                    _endRotation);
            }

            if (_movementData == null)
            {
                Plugin.Log.Error(
                    "Could not resolve Beat Saber movement services; leaving visual unchanged.");
                Shutdown(restoreOriginal: true);
                return;
            }

            _stableTargetX =
                _movementData.jumpEndPosition.x +
                _spawnData.jumpEndOffset.x;
            _originalVisualRoot = noteController.noteTransform;
            if (!isBomb)
            {
                RemoveLegacyGuides();
            }

            _proxyRoot = VisualMeshCloner.CloneRenderHierarchy(
                _originalVisualRoot,
                isBomb ? "ProxyBombVisual" : "ProxyNoteVisual");
            if (_proxyRoot == null)
            {
                Plugin.Log.Warn("A note had no supported renderers; leaving it unchanged.");
                Shutdown(restoreOriginal: true);
                return;
            }

            _proxyRoot.transform.SetParent(noteController.transform.parent, false);
            _proxyRoot.transform.localScale = _originalVisualRoot.lossyScale;
            if (_hasDirectionalCut)
            {
                _guideRoot = CutGuideVisualFactory.Create(_proxyRoot.transform);
                if (_guideRoot == null)
                {
                    Plugin.Log.Warn("Could not create a cut guide for a note.");
                }
            }

            CaptureOriginalState();
            CaptureRendererStates(_proxyRoot.transform, _proxyRenderers);
            noteController.noteWasCutEvent.Add(this);
            noteController.noteWasMissedEvent.Add(this);
            noteController.noteDidStartDissolvingEvent.Add(this);

            _sampleLogged = false;
            _guideRetired = false;
            _legacyGuideCleanupFrames = isBomb ? 0 : 4;
            _initialized = true;
            ActiveControllers.Add(this);
            ApplyVisualMode(force: true);
            UpdateVisualPoses();
        }

        private void LateUpdate()
        {
            if (!_initialized || _proxyRoot == null)
            {
                return;
            }

            if (!PluginConfig.Instance.Enabled)
            {
                Shutdown(restoreOriginal: true);
                return;
            }

            if (_legacyGuideCleanupFrames > 0)
            {
                RemoveLegacyGuides();
                _legacyGuideCleanupFrames--;
            }

            ApplyVisualMode(force: false);
            UpdateVisualPoses();
        }

        private void UpdateVisualPoses()
        {
            Vector3 moveStart =
                _movementData.moveStartPosition + _spawnData.moveStartOffset;
            Vector3 start = _movementData.moveEndPosition + _spawnData.moveEndOffset;
            Vector3 end = _movementData.jumpEndPosition + _spawnData.jumpEndOffset;
            float zOffset = ReadNoteZOffset();
            moveStart.z += zOffset;
            start.z += zOffset;
            end.z += zOffset;

            Vector3 originalLocalPosition = _noteMovement.localPosition;
            float currentZ = originalLocalPosition.z;
            float hitZ = (start.z + end.z) * 0.5f;

            Quaternion worldRotation = _noteController.worldRotation;
            Transform proxyTransform = _proxyRoot.transform;
            float halfJumpDuration = _movementData.halfJumpDuration;
            float jumpDuration = _movementData.jumpDuration;
            float songTime = _audioTimeSource == null
                ? float.NaN
                : _audioTimeSource.songTime;
            bool shouldWait =
                _audioTimeSource != null &&
                TrajectoryTiming.ShouldWaitForFloorMovement(
                    songTime,
                    _noteController.noteTime,
                    _movementData.spawnAheadTime,
                    _movementData.waitingDuration);
            SetWaitingVisualsHidden(shouldWait);
            if (shouldWait)
            {
                return;
            }

            float effectiveNjs = TrajectoryTiming.CalculateEffectiveNjs(
                start.z,
                end.z,
                jumpDuration);
            float leadTime = TrajectoryTiming.CalculateLeadTime(
                _jumpLeadDistance,
                effectiveNjs);
            if (_audioTimeSource == null)
            {
                leadTime = 0f;
            }
            float originalJumpStartTime =
                _noteController.noteTime - halfJumpDuration;
            float floorMovementStartTime =
                TrajectoryTiming.CalculateFloorMovementStartTime(
                    _noteController.noteTime,
                    halfJumpDuration,
                    _movementData.moveDuration);
            float visualStartTime =
                originalJumpStartTime - leadTime;
            float jumpVelocityZ = jumpDuration <= 0.001f
                ? 0f
                : (end.z - start.z) / jumpDuration;
            float advancedStartZ =
                start.z - jumpVelocityZ * leadTime;
            Vector3 visualJumpStart = EvaluateFloorPositionAtSongTime(
                visualStartTime,
                moveStart,
                start);
            float originalJumpProgress = CalculateOriginalJumpProgress(
                currentZ,
                start.z,
                end.z);
            float jumpProgress = -1f;
            float visualElapsedTime = 0f;
            float proxyZ = currentZ;
            if (_audioTimeSource != null)
            {
                originalJumpProgress = jumpDuration <= 0.001f
                    ? 0.5f
                    : (songTime - (_noteController.noteTime - halfJumpDuration)) /
                      jumpDuration;
                jumpProgress =
                    TrajectoryTiming.CalculateTimeWarpedJumpProgress(
                    songTime,
                    _noteController.noteTime,
                    halfJumpDuration,
                    jumpDuration,
                    leadTime);
                visualElapsedTime = Mathf.Clamp(
                    songTime - visualStartTime,
                    0f,
                    halfJumpDuration + leadTime);
                proxyZ = TrajectoryTiming.EvaluateAdvancedDepth(
                    songTime,
                    floorMovementStartTime,
                    visualStartTime,
                    originalJumpStartTime,
                    _noteController.noteTime,
                    moveStart.z,
                    advancedStartZ,
                    currentZ,
                    jumpVelocityZ);
            }
            else if (originalJumpProgress >= 0f)
            {
                jumpProgress = originalJumpProgress;
                visualElapsedTime = Mathf.Max(
                    0f,
                    jumpProgress * jumpDuration);
            }

            Vector3 visualPosition = EvaluateVisualPosition(
                proxyZ,
                moveStart,
                visualJumpStart,
                start,
                end,
                jumpProgress,
                visualElapsedTime,
                halfJumpDuration,
                leadTime);

            proxyTransform.localPosition = worldRotation * visualPosition;
            ApplyNoteRotation(
                proxyTransform,
                visualPosition,
                jumpProgress,
                worldRotation,
                _rotationCoefficient);

            FinalizeVisualPose(currentZ, hitZ, proxyTransform);
        }

        private void SetWaitingVisualsHidden(bool hidden)
        {
            if (_proxyRoot != null && _proxyRoot.activeSelf == hidden)
            {
                _proxyRoot.SetActive(!hidden);
            }

            if (_debugOriginalRoot != null)
            {
                bool showDebugOriginal =
                    !hidden && PluginConfig.Instance.DebugMode;
                if (_debugOriginalRoot.activeSelf != showDebugOriginal)
                {
                    _debugOriginalRoot.SetActive(showDebugOriginal);
                }
            }
        }

        private void FinalizeVisualPose(
            float currentZ,
            float hitZ,
            Transform proxyTransform)
        {
            UpdateGuideVisibility(currentZ, hitZ);
            UpdateDebugOriginalPose();
            if (PluginConfig.Instance.LogCalibrationSamples &&
                !_sampleLogged &&
                Mathf.Abs(currentZ - hitZ) <= 0.05f)
            {
                float separation = Vector3.Distance(
                    proxyTransform.position,
                    _originalVisualRoot.position);
                Plugin.Log.Info(
                    $"Calibration note: proxy/original separation={separation:F4}m.");
                _sampleLogged = true;
            }
        }

        private Quaternion EvaluateNoteRotation(float jumpProgress)
        {
            if (jumpProgress < 0.125f)
            {
                return Quaternion.Slerp(
                    _startRotation,
                    _middleRotation,
                    TrajectoryTiming.CalculateVanillaStartToMiddleRotationProgress(
                        jumpProgress));
            }

            if (jumpProgress < 0.5f)
            {
                return Quaternion.Slerp(
                    _middleRotation,
                    _endRotation,
                    TrajectoryTiming.CalculateVanillaMiddleToEndRotationProgress(
                        jumpProgress));
            }

            return _endRotation;
        }

        private void UpdateDebugOriginalPose()
        {
            if (_debugOriginalRoot == null || !_debugOriginalRoot.activeSelf)
            {
                return;
            }

            Transform debugTransform = _debugOriginalRoot.transform;
            debugTransform.position = _originalVisualRoot.position;
            debugTransform.rotation = _originalVisualRoot.rotation;
        }

        private void UpdateGuideVisibility(float currentZ, float hitZ)
        {
            if (_guideRoot == null)
            {
                return;
            }

            if (!PluginConfig.Instance.GuideEnabled)
            {
                _guideRoot.SetActive(false);
                return;
            }

            if (!_guideRetired)
            {
                float hideDistance =
                    Mathf.Clamp(PluginConfig.Instance.GuideHideDistance, 0.5f, 6f);
                bool reachedHideDistance = _playerTransforms != null
                    ? _proxyRoot.transform.position.z <=
                      _playerTransforms.headPseudoLocalPos.z + hideDistance
                    : Mathf.Abs(currentZ - hitZ) <= hideDistance;
                if (reachedHideDistance)
                {
                    _guideRetired = true;
                }
            }

            _guideRoot.SetActive(!_guideRetired);
        }

        private void RemoveLegacyGuides()
        {
            if (_originalVisualRoot == null)
            {
                return;
            }

            Transform[] transforms =
                _originalVisualRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform child in transforms)
            {
                if (child == null || child.name != "NoteCutGuide")
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private Vector3 EvaluateVisualPosition(
            float proxyZ,
            Vector3 moveStart,
            Vector3 visualJumpStart,
            Vector3 vanillaStart,
            Vector3 vanillaEnd,
            float jumpProgress,
            float visualElapsedTime,
            float halfJumpDuration,
            float leadTime)
        {
            if (jumpProgress >= 0f)
            {
                return EvaluateJumpPosition(
                    proxyZ,
                    visualJumpStart,
                    vanillaStart,
                    vanillaEnd,
                    jumpProgress,
                    visualElapsedTime,
                    halfJumpDuration,
                    leadTime);
            }

            Vector3 floorPosition = EvaluateVanillaFloorPosition(
                proxyZ,
                moveStart,
                vanillaStart);
            floorPosition.x = TrajectoryTiming.EvaluatePositionSwap(
                _preservePositionSwaps,
                floorPosition.x,
                _stableTargetX,
                jumpProgress: 0f);
            return floorPosition;
        }

        private Vector3 EvaluateJumpPosition(
            float proxyZ,
            Vector3 visualStart,
            Vector3 vanillaStart,
            Vector3 vanillaEnd,
            float jumpProgress,
            float visualElapsedTime,
            float halfJumpDuration,
            float leadTime)
        {
            float x = TrajectoryTiming.EvaluatePositionSwap(
                _preservePositionSwaps,
                visualStart.x,
                vanillaEnd.x,
                jumpProgress);

            float y;
            if (jumpProgress <= 0.5f)
            {
                float hitY = vanillaStart.y + _spawnData.gravityBase;
                y = TrajectoryTiming.EvaluateTimeWarpedHeight(
                    visualElapsedTime,
                    halfJumpDuration,
                    leadTime,
                    visualStart.y,
                    hitY);
            }
            else
            {
                float arcProgress =
                    4f * jumpProgress * (1f - jumpProgress);
                y = vanillaStart.y + _spawnData.gravityBase * arcProgress;
            }

            y += TrajectoryTiming.EvaluateSwapAvoidance(
                _preservePositionSwaps,
                _yAvoidance,
                jumpProgress);

            return new Vector3(x, y, proxyZ);
        }

        private void ApplyNoteRotation(
            Transform proxyTransform,
            Vector3 visualPosition,
            float jumpProgress,
            Quaternion worldRotation,
            float rotationCoefficient)
        {
            if (jumpProgress < 0f)
            {
                _lastVanillaRotation = _startRotation;
            }
            else if (jumpProgress < 0.5f)
            {
                Quaternion visualRotation = EvaluateNoteRotation(jumpProgress);
                if (_rotateTowardsPlayer &&
                    _playerTransforms != null &&
                    _playerSpaceConvertor != null)
                {
                    Vector3 headPosition = _playerTransforms.headPseudoLocalPos;
                    headPosition.y = Mathf.Lerp(
                        headPosition.y,
                        visualPosition.y,
                        0.8f);
                    Quaternion inverseWorldRotation =
                        _noteController.inverseWorldRotation;
                    headPosition = inverseWorldRotation * headPosition;
                    Vector3 lookDirection =
                        (visualPosition - headPosition).normalized;
                    Quaternion playerFacingRotation = default(Quaternion);
                    Quaternion previousVanillaRootRotation =
                        worldRotation * _lastVanillaRotation;
                    Vector3 vanillaWorldUp =
                        previousVanillaRootRotation * Vector3.up;
                    if (proxyTransform.parent != null)
                    {
                        vanillaWorldUp =
                            proxyTransform.parent.rotation *
                            vanillaWorldUp;
                    }
                    Vector3 playerSpaceUp =
                        _playerSpaceConvertor.worldToPlayerSpaceRotation *
                        vanillaWorldUp;
                    playerFacingRotation.SetLookRotation(
                        lookDirection,
                        inverseWorldRotation * playerSpaceUp);
                    visualRotation = Quaternion.Lerp(
                        visualRotation,
                        playerFacingRotation,
                        jumpProgress * 2f);
                }

                _lastVanillaRotation = visualRotation;
            }

            float coefficient = Mathf.Clamp01(rotationCoefficient);
            Quaternion weightedRotation = Quaternion.Slerp(
                _endRotation,
                _lastVanillaRotation,
                coefficient);
            proxyTransform.localRotation = worldRotation * weightedRotation;
        }

        private static float CalculateOriginalJumpProgress(
            float currentZ,
            float jumpStartZ,
            float jumpEndZ)
        {
            float jumpDistance = jumpEndZ - jumpStartZ;
            return Mathf.Abs(jumpDistance) <= 0.001f
                ? 0.5f
                : (currentZ - jumpStartZ) / jumpDistance;
        }

        private static Vector3 EvaluateFloorPosition(
            float currentZ,
            Vector3 moveStart,
            Vector3 moveEnd)
        {
            float distance = moveEnd.z - moveStart.z;
            if (Mathf.Abs(distance) <= 0.001f)
            {
                return moveEnd;
            }

            float progress = Mathf.Clamp01((currentZ - moveStart.z) / distance);
            Vector3 position =
                Vector3.LerpUnclamped(moveStart, moveEnd, progress);
            position.z = currentZ;
            return position;
        }

        private Vector3 EvaluateFloorPositionAtSongTime(
            float songTime,
            Vector3 moveStart,
            Vector3 moveEnd)
        {
            float moveDuration = _movementData.moveDuration;
            float progress = TrajectoryTiming.CalculateFloorProgressAtSongTime(
                songTime,
                _noteController.noteTime,
                _movementData.halfJumpDuration,
                moveDuration);
            return Vector3.LerpUnclamped(moveStart, moveEnd, progress);
        }

        private Vector3 EvaluateVanillaFloorPosition(
            float currentZ,
            Vector3 moveStart,
            Vector3 moveEnd)
        {
            if (_audioTimeSource == null)
            {
                return EvaluateFloorPosition(currentZ, moveStart, moveEnd);
            }

            float moveDuration = _movementData.moveDuration;
            if (moveDuration <= 0.001f)
            {
                Vector3 endPosition = moveEnd;
                endPosition.z = currentZ;
                return endPosition;
            }

            float elapsed =
                _audioTimeSource.songTime -
                (_noteController.noteTime -
                 moveDuration -
                 _movementData.halfJumpDuration);
            float progress = Mathf.Clamp01(elapsed / moveDuration);
            Vector3 position =
                Vector3.LerpUnclamped(moveStart, moveEnd, progress);
            position.z = currentZ;
            return position;
        }

        private static T ReadField<T>(
            FieldInfo field,
            object instance,
            T fallback)
        {
            if (field == null || instance == null)
            {
                return fallback;
            }

            object value = field.GetValue(instance);
            return value is T typedValue ? typedValue : fallback;
        }

        private float ReadNoteZOffset()
        {
            if (NoteMovementZOffsetField == null)
            {
                return 0f;
            }

            object value = NoteMovementZOffsetField.GetValue(_noteMovement);
            return value is float zOffset ? zOffset : 0f;
        }

        private void CaptureOriginalState()
        {
            CaptureRendererStates(_originalVisualRoot, _originalRenderers);
            Transform[] transforms =
                _originalVisualRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (Transform child in transforms)
            {
                _originalLayers.Add(new LayerState
                {
                    GameObject = child.gameObject,
                    Layer = child.gameObject.layer
                });
            }
        }

        private static void CaptureRendererStates(
            Transform visualRoot,
            List<RendererState> destination)
        {
            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (Renderer renderer in renderers)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                destination.Add(new RendererState
                {
                    Renderer = renderer,
                    Enabled = renderer.enabled,
                    PropertyBlock = propertyBlock
                });
            }
        }

        private void ApplyVisualMode(bool force)
        {
            bool debugMode = PluginConfig.Instance.DebugMode;
            bool showProxyOnDesktop = PluginConfig.Instance.ShowProxyOnDesktop;
            bool guideEnabled = PluginConfig.Instance.GuideEnabled;
            float guideLength = Mathf.Clamp(PluginConfig.Instance.GuideLength, 0.1f, 1.5f);
            float guideThickness =
                Mathf.Clamp(PluginConfig.Instance.GuideThickness, 0.02f, 0.3f);
            float guideOffset = Mathf.Clamp(PluginConfig.Instance.GuideOffset, -0.5f, 1f);
            float guideHideDistance =
                Mathf.Clamp(PluginConfig.Instance.GuideHideDistance, 0.5f, 6f);
            float originalOpacity = Mathf.Clamp01(PluginConfig.Instance.OriginalNoteOpacity);
            float proxyOpacity = debugMode
                ? Mathf.Clamp01(PluginConfig.Instance.ProxyNoteOpacity)
                : 1f;

            if (!force &&
                debugMode == _debugModeApplied &&
                showProxyOnDesktop == _desktopModeApplied &&
                guideEnabled == _guideEnabledApplied &&
                Mathf.Approximately(guideLength, _guideLengthApplied) &&
                Mathf.Approximately(guideThickness, _guideThicknessApplied) &&
                Mathf.Approximately(guideOffset, _guideOffsetApplied) &&
                Mathf.Approximately(guideHideDistance, _guideHideDistanceApplied) &&
                Mathf.Approximately(originalOpacity, _originalOpacityApplied) &&
                Mathf.Approximately(proxyOpacity, _proxyOpacityApplied))
            {
                return;
            }

            if (_guideRoot != null)
            {
                Transform guideTransform = _guideRoot.transform;
                guideTransform.localPosition = new Vector3(0f, guideOffset, 0f);
                guideTransform.localRotation = Quaternion.identity;
                guideTransform.localScale =
                    new Vector3(guideThickness, guideLength, guideThickness);
                if (!guideEnabled)
                {
                    _guideRoot.SetActive(false);
                }
            }

            VisibilityUtils.SetLayerRecursively(
                _proxyRoot,
                showProxyOnDesktop ? VisibilityLayer.Default : VisibilityLayer.HmdOnly);
            ApplyRendererOpacity(_proxyRenderers, proxyOpacity);

            if (debugMode)
            {
                EnsureDebugOriginal();
            }

            foreach (RendererState state in _originalRenderers)
            {
                if (state.Renderer != null)
                {
                    VisibilityUtils.SetLayer(
                        state.Renderer.gameObject,
                        VisibilityLayer.DesktopOnly);
                }
            }
            RestoreRendererProperties(
                _originalRenderers,
                enableRenderers: !showProxyOnDesktop);

            if (debugMode)
            {
                if (_debugOriginalRoot != null)
                {
                    VisibilityUtils.SetLayerRecursively(
                        _debugOriginalRoot,
                        showProxyOnDesktop ? VisibilityLayer.Default : VisibilityLayer.HmdOnly);
                    _debugOriginalRoot.SetActive(true);
                    ApplyRendererOpacity(_debugOriginalRenderers, originalOpacity);
                }
            }
            else if (_debugOriginalRoot != null)
            {
                _debugOriginalRoot.SetActive(false);
            }

            _debugModeApplied = debugMode;
            _desktopModeApplied = showProxyOnDesktop;
            _guideEnabledApplied = guideEnabled;
            _guideLengthApplied = guideLength;
            _guideThicknessApplied = guideThickness;
            _guideOffsetApplied = guideOffset;
            _guideHideDistanceApplied = guideHideDistance;
            _originalOpacityApplied = originalOpacity;
            _proxyOpacityApplied = proxyOpacity;
        }

        private void EnsureDebugOriginal()
        {
            if (_debugOriginalRoot != null)
            {
                return;
            }

            // The desktop mode may have disabled the source renderers before
            // debug mode was toggled. Clone their captured enabled state.
            RestoreRendererProperties(_originalRenderers, enableRenderers: true);
            _debugOriginalRoot = VisualMeshCloner.CloneRenderHierarchy(
                _originalVisualRoot,
                _isBomb ? "ProxyBombDebugOriginal" : "ProxyNoteDebugOriginal");
            if (_debugOriginalRoot == null)
            {
                Plugin.Log.Warn("Could not create the debug copy for a note.");
                return;
            }

            _debugOriginalRoot.transform.SetParent(_noteController.transform.parent, false);
            _debugOriginalRoot.transform.localScale = _originalVisualRoot.lossyScale;
            CaptureRendererStates(_debugOriginalRoot.transform, _debugOriginalRenderers);
        }

        private static void RestoreRendererProperties(
            List<RendererState> renderers,
            bool enableRenderers)
        {
            foreach (RendererState state in renderers)
            {
                if (state.Renderer == null)
                {
                    continue;
                }

                state.Renderer.SetPropertyBlock(state.PropertyBlock);
                state.Renderer.enabled = enableRenderers && state.Enabled;
            }
        }

        private static void ApplyRendererOpacity(
            List<RendererState> renderers,
            float opacity)
        {
            foreach (RendererState state in renderers)
            {
                if (state.Renderer == null)
                {
                    continue;
                }

                state.Renderer.SetPropertyBlock(state.PropertyBlock);
                state.Renderer.enabled = state.Enabled;
                if (!state.Enabled || Mathf.Approximately(opacity, 1f))
                {
                    continue;
                }

                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                state.Renderer.GetPropertyBlock(properties);
                foreach (int propertyId in ColorPropertyIds)
                {
                    Color color = properties.GetColor(propertyId);
                    bool hasPropertyBlockColor = color != default(Color);
                    bool hasMaterialColor =
                        state.Renderer.sharedMaterial != null &&
                        state.Renderer.sharedMaterial.HasProperty(propertyId);
                    if (!hasPropertyBlockColor && !hasMaterialColor)
                    {
                        continue;
                    }

                    if (!hasPropertyBlockColor)
                    {
                        color = state.Renderer.sharedMaterial.GetColor(propertyId);
                    }

                    color.a *= opacity;
                    properties.SetColor(propertyId, color);
                }

                state.Renderer.SetPropertyBlock(properties);
            }
        }

        public void HandleNoteControllerNoteWasCut(
            NoteController noteController,
            in NoteCutInfo noteCutInfo)
        {
            HideVisualsImmediately();
        }

        public void HandleNoteControllerNoteWasMissed(NoteController noteController)
        {
            HideVisualsImmediately();
        }

        public void HandleNoteControllerNoteDidStartDissolving(
            NoteControllerBase noteController,
            float duration)
        {
            HideVisualsImmediately();
        }

        private void HideVisualsImmediately()
        {
            if (_proxyRoot != null)
            {
                _proxyRoot.SetActive(false);
            }

            if (_debugOriginalRoot != null)
            {
                _debugOriginalRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Shutdown(restoreOriginal: true);
        }

        private void OnDestroy()
        {
            Shutdown(restoreOriginal: true);
        }

        private void Shutdown(bool restoreOriginal)
        {
            if (_noteController != null)
            {
                _noteController.noteWasCutEvent.Remove(this);
                _noteController.noteWasMissedEvent.Remove(this);
                _noteController.noteDidStartDissolvingEvent.Remove(this);
            }

            if (restoreOriginal)
            {
                foreach (LayerState state in _originalLayers)
                {
                    if (state.GameObject != null)
                    {
                        state.GameObject.layer = state.Layer;
                    }
                }

                RestoreRendererProperties(_originalRenderers, enableRenderers: true);
            }

            _originalRenderers.Clear();
            _proxyRenderers.Clear();
            _debugOriginalRenderers.Clear();
            _originalLayers.Clear();

            if (_proxyRoot != null)
            {
                Destroy(_proxyRoot);
                _proxyRoot = null;
            }

            if (_debugOriginalRoot != null)
            {
                Destroy(_debugOriginalRoot);
                _debugOriginalRoot = null;
            }

            _noteController = null;
            _noteMovement = null;
            _movementData = null;
            _originalVisualRoot = null;
            _guideRoot = null;
            _playerTransforms = null;
            _playerSpaceConvertor = null;
            _audioTimeSource = null;
            _startRotation = Quaternion.identity;
            _middleRotation = Quaternion.identity;
            _endRotation = Quaternion.identity;
            _lastVanillaRotation = Quaternion.identity;
            _stableTargetX = 0f;
            _jumpLeadDistance = 0f;
            _rotationCoefficient = 0f;
            _yAvoidance = 0f;
            _isBomb = false;
            _preservePositionSwaps = false;
            _rotateTowardsPlayer = false;
            _hasDirectionalCut = false;
            _guideRetired = false;
            _legacyGuideCleanupFrames = 0;
            _initialized = false;
            _debugModeApplied = false;
            _desktopModeApplied = false;
            _guideEnabledApplied = false;
            _guideLengthApplied = -1f;
            _guideThicknessApplied = -1f;
            _guideOffsetApplied = float.MinValue;
            _guideHideDistanceApplied = -1f;
            _originalOpacityApplied = -1f;
            _proxyOpacityApplied = -1f;
            ActiveControllers.Remove(this);
        }
    }
}

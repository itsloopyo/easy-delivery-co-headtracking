using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyDeliveryCoHeadTracking.Camera
{
    /// <summary>
    /// Detects gameplay vs menus/loading/paused using heuristics.
    /// Uses Time.timeScale, Cursor.lockState, and scene names.
    /// </summary>
    public class GameStateDetector
    {
        private const float CheckIntervalSeconds = 0.1f;

        private GameState _currentState = GameState.Unknown;
        private float _lastCheckTime;

        // Memoizes the scene-name classification keyed by Scene.buildIndex rather than
        // Scene.name (which allocates a fresh managed string every getter call). The
        // classification (menu / loading / dynamic-check) is a pure function of the
        // name, so we only fetch the name and re-classify when the buildIndex changes.
        // _staticSceneState == null means "scene is gameplay-like; check timeScale + cursor".
        // Sentinel int.MinValue forces the first call to compute.
        private int _classifiedSceneBuildIndex = int.MinValue;
        private GameState? _staticSceneState;

        public event Action<GameState> StateChanged;
        public GameState CurrentState => _currentState;

        /// <summary>
        /// True when the player is in active gameplay (not menus/loading/paused).
        /// </summary>
        public bool IsGameplayActive => _currentState == GameState.Gameplay;

        public void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateState();
        }

        public void Shutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void Update()
        {
            if (Time.time - _lastCheckTime < CheckIntervalSeconds)
                return;

            _lastCheckTime = Time.time;
            UpdateState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastCheckTime = 0f;
            UpdateState();
        }

        private void UpdateState()
        {
            var newState = DetectState();
            if (newState != _currentState)
            {
                _currentState = newState;
                StateChanged?.Invoke(newState);
            }
        }

        private GameState DetectState()
        {
            var scene = SceneManager.GetActiveScene();
            int buildIndex = scene.buildIndex;
            if (buildIndex != _classifiedSceneBuildIndex)
            {
                _classifiedSceneBuildIndex = buildIndex;
                _staticSceneState = ClassifyScene(scene.name);
            }

            if (_staticSceneState.HasValue)
                return _staticSceneState.Value;

            if (Time.timeScale < 0.01f)
                return GameState.Paused;

            if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
                return GameState.Paused;

            return GameState.Gameplay;
        }

        private static GameState? ClassifyScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return GameState.Unknown;

            var lower = sceneName.ToLowerInvariant();
            if (lower.Contains("menu") || lower.Contains("title") || lower.Contains("main"))
                return GameState.MainMenu;

            if (lower.Contains("load") || lower.Contains("boot") || lower.Contains("intro"))
                return GameState.Loading;

            if (lower.Contains("credit"))
                return GameState.MainMenu;

            // null = scene is gameplay-like; caller checks timeScale + cursor.
            return null;
        }
    }
}

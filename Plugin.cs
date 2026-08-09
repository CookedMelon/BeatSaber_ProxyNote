using System;
using System.Reflection;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Util;
using HarmonyLib;
using IPA;
using IPA.Config.Stores;
using IPALogger = IPA.Logging.Logger;

namespace ProxyNote
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public sealed class Plugin
    {
        private const string MenuName = "ProxyNote";
        private const string SettingsResource = "ProxyNote.Views.settings.bsml";

        private readonly Harmony _harmony;
        private bool _enabled;
        private GameplaySetup _registeredGameplaySetup;

        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, IPA.Config.Config config)
        {
            Log = logger;
            PluginConfig.Instance = config.Generated<PluginConfig>();
            PluginConfig.Instance.ApplyMigrations();
            _harmony = new Harmony("dev.learning.ProxyNote");
        }

        [OnEnable]
        public void OnEnable()
        {
            _enabled = true;
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            MainMenuAwaiter.MainMenuInitializing += HandleMainMenuInitializing;
            RegisterMenusWhenReady();
            Log.Info("ProxyNote enabled.");
        }

        [OnDisable]
        public void OnDisable()
        {
            _enabled = false;
            MainMenuAwaiter.MainMenuInitializing -= HandleMainMenuInitializing;
            _harmony.UnpatchSelf();
            ProxyNoteVisualController.RestoreAll();
            RemoveMenus();
            Log.Info("ProxyNote disabled.");
        }

        private async void RegisterMenusWhenReady()
        {
            try
            {
                await MainMenuAwaiter.WaitForMainMenuAsync();
                RegisterMenus();
            }
            catch (Exception exception)
            {
                Log.Error($"Could not wait for the main menu: {exception}");
            }
        }

        private void HandleMainMenuInitializing()
        {
            RegisterMenusWhenReady();
        }

        private void RegisterMenus()
        {
            if (!_enabled)
            {
                return;
            }

            try
            {
                GameplaySetup gameplaySetup = GameplaySetup.Instance;
                if (gameplaySetup == null ||
                    ReferenceEquals(gameplaySetup, _registeredGameplaySetup))
                {
                    return;
                }

                gameplaySetup.AddTab(
                    MenuName,
                    SettingsResource,
                    SettingsViewModel.Instance,
                    MenuType.All);
                _registeredGameplaySetup = gameplaySetup;
                Log.Info("Registered gameplay setup menu.");
            }
            catch (Exception exception)
            {
                Log.Error($"Could not register menus: {exception}");
            }
        }

        private void RemoveMenus()
        {
            try
            {
                (_registeredGameplaySetup ?? GameplaySetup.Instance)?.RemoveTab(MenuName);
                _registeredGameplaySetup = null;
            }
            catch (Exception exception)
            {
                Log.Warn($"Could not remove menus cleanly: {exception}");
            }
        }
    }
}

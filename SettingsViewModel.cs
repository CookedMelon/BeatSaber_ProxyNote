using System.ComponentModel;
using BeatSaberMarkupLanguage.Attributes;

namespace ProxyNote
{
    internal sealed class SettingsViewModel : INotifyPropertyChanged
    {
        internal static SettingsViewModel Instance { get; } = new SettingsViewModel();

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Chinese => PluginConfig.Instance.UseChinese;

        [UIValue("enabled")]
        private bool Enabled
        {
            get { return PluginConfig.Instance.Enabled; }
            set { PluginConfig.Instance.Enabled = value; }
        }

        [UIAction("set-enabled")]
        private void SetEnabled(bool value)
        {
            Enabled = value;
            Save();
        }

        [UIValue("jump-lead-distance")]
        private float JumpLeadDistance
        {
            get { return PluginConfig.Instance.JumpLeadDistance; }
            set
            {
                PluginConfig.Instance.JumpLeadDistance =
                    value < 0f ? 0f : value > 5f ? 5f : value;
            }
        }

        [UIAction("set-jump-lead-distance")]
        private void SetJumpLeadDistance(float value)
        {
            JumpLeadDistance = value;
            Save();
        }

        [UIValue("note-rotation-coefficient")]
        private float NoteRotationCoefficient
        {
            get { return PluginConfig.Instance.NoteRotationCoefficient; }
            set
            {
                PluginConfig.Instance.NoteRotationCoefficient =
                    value < 0f ? 0f : value > 1f ? 1f : value;
            }
        }

        [UIAction("set-note-rotation-coefficient")]
        private void SetNoteRotationCoefficient(float value)
        {
            NoteRotationCoefficient = value;
            Save();
        }

        [UIValue("enable-note-position-swaps")]
        private bool EnableNotePositionSwaps
        {
            get { return PluginConfig.Instance.EnableNotePositionSwaps; }
            set { PluginConfig.Instance.EnableNotePositionSwaps = value; }
        }

        [UIAction("set-enable-note-position-swaps")]
        private void SetEnableNotePositionSwaps(bool value)
        {
            EnableNotePositionSwaps = value;
            Save();
        }

        [UIValue("guide-enabled")]
        private bool GuideEnabled
        {
            get { return PluginConfig.Instance.GuideEnabled; }
            set { PluginConfig.Instance.GuideEnabled = value; }
        }

        [UIAction("set-guide-enabled")]
        private void SetGuideEnabled(bool value)
        {
            GuideEnabled = value;
            Save();
        }

        [UIValue("show-proxy-on-desktop")]
        private bool ShowProxyOnDesktop
        {
            get { return PluginConfig.Instance.ShowProxyOnDesktop; }
            set { PluginConfig.Instance.ShowProxyOnDesktop = value; }
        }

        [UIAction("set-show-proxy-on-desktop")]
        private void SetShowProxyOnDesktop(bool value)
        {
            ShowProxyOnDesktop = value;
            Save();
        }

        [UIValue("debug-mode")]
        private bool DebugMode
        {
            get { return PluginConfig.Instance.DebugMode; }
            set { PluginConfig.Instance.DebugMode = value; }
        }

        [UIAction("set-debug-mode")]
        private void SetDebugMode(bool value)
        {
            DebugMode = value;
            Save();
            Notify(nameof(DebugMode));
        }

        [UIValue("original-opacity")]
        private float OriginalOpacity
        {
            get { return PluginConfig.Instance.OriginalNoteOpacity; }
            set
            {
                PluginConfig.Instance.OriginalNoteOpacity =
                    value < 0f ? 0f : value > 1f ? 1f : value;
            }
        }

        [UIAction("set-original-opacity")]
        private void SetOriginalOpacity(float value)
        {
            OriginalOpacity = value;
            Save();
        }

        [UIValue("proxy-opacity")]
        private float ProxyOpacity
        {
            get { return PluginConfig.Instance.ProxyNoteOpacity; }
            set
            {
                PluginConfig.Instance.ProxyNoteOpacity =
                    value < 0f ? 0f : value > 1f ? 1f : value;
            }
        }

        [UIAction("set-proxy-opacity")]
        private void SetProxyOpacity(float value)
        {
            ProxyOpacity = value;
            Save();
        }

        [UIValue("suppress-debris")]
        private bool SuppressDebris
        {
            get { return PluginConfig.Instance.SuppressVanillaDebris; }
            set { PluginConfig.Instance.SuppressVanillaDebris = value; }
        }

        [UIAction("set-suppress-debris")]
        private void SetSuppressDebris(bool value)
        {
            SuppressDebris = value;
            Save();
        }

        [UIValue("language-button-label")]
        private string LanguageButtonLabel => Chinese ? "English" : "中文";

        [UIAction("toggle-language")]
        private void ToggleLanguage()
        {
            PluginConfig.Instance.UseChinese = !PluginConfig.Instance.UseChinese;
            Save();
            NotifyLocalizedText();
        }

        [UIValue("reset-label")]
        private string ResetLabel => Chinese ? "重置" : "Reset";

        [UIAction("reset-settings")]
        private void ResetSettings()
        {
            PluginConfig.Instance.ResetToDefaults();
            Save();
            NotifyAll();
        }

        [UIValue("description")]
        private string Description => Chinese
            ? "本插件可对方块交换、变向、跳跃过程进行编辑。\n使用该插件对方块碰撞箱不会造成影响。"
            : "Removes note swaps and direction changes. Original hitbox checks are not affected.";

        [UIValue("enabled-label")]
        private string EnabledLabel => Chinese ? "启用插件" : "Enabled";

        [UIValue("jump-lead-label")]
        private string JumpLeadLabel => Chinese ? "方块跳跃提前（米）" : "Note jump lead distance (m)";

        [UIValue("note-rotation-label")]
        private string NoteRotationLabel =>
            Chinese ? "方块变向系数" : "Note rotation coefficient";

        [UIValue("note-position-swaps-label")]
        private string NotePositionSwapsLabel =>
            Chinese ? "保留方块交换" : "Preserve note swaps";

        [UIValue("guide-enabled-label")]
        private string GuideEnabledLabel => Chinese ? "显示切割引导" : "Show cut guide";

        [UIValue("desktop-label")]
        private string DesktopLabel => Chinese ? "PC视角显示插件效果" : "Show plugin effects on PC";

        [UIValue("debug-label")]
        private string DebugLabel => Chinese ? "调试模式" : "Debug mode";

        [UIValue("original-opacity-label")]
        private string OriginalOpacityLabel => Chinese ? "原始方块不透明度" : "Original note opacity";

        [UIValue("proxy-opacity-label")]
        private string ProxyOpacityLabel => Chinese ? "新方块不透明度" : "New note opacity";

        [UIValue("debris-label")]
        private string DebrisLabel => Chinese ? "隐藏原版碎块" : "Hide vanilla note debris";

        private static void Save()
        {
            PluginConfig.Instance.Changed();
        }

        private void NotifyLocalizedText()
        {
            Notify(nameof(LanguageButtonLabel));
            Notify(nameof(ResetLabel));
            Notify(nameof(Description));
            Notify(nameof(EnabledLabel));
            Notify(nameof(JumpLeadLabel));
            Notify(nameof(NoteRotationLabel));
            Notify(nameof(NotePositionSwapsLabel));
            Notify(nameof(GuideEnabledLabel));
            Notify(nameof(DesktopLabel));
            Notify(nameof(DebugLabel));
            Notify(nameof(OriginalOpacityLabel));
            Notify(nameof(ProxyOpacityLabel));
            Notify(nameof(DebrisLabel));
        }

        private void NotifyAll()
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(null));
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

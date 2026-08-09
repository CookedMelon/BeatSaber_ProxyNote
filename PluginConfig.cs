namespace ProxyNote
{
    public class PluginConfig
    {
        internal static PluginConfig Instance { get; set; }

        public virtual int ConfigVersion { get; set; } = 0;

        public virtual bool Enabled { get; set; } = true;

        // Positive values start the visual jump farther away than the vanilla jump.
        public virtual float JumpLeadDistance { get; set; } = 0f;

        // Retained only so older configuration files remain readable.
        public virtual float BombJumpLeadDistance { get; set; } = 0f;

        // Retained only to migrate the pre-0.7 binary setting.
        public virtual bool EnableNoteRotation { get; set; } = true;

        public virtual float NoteRotationCoefficient { get; set; } = 0.2f;

        public virtual bool EnableNotePositionSwaps { get; set; } = false;

        public virtual bool GuideEnabled { get; set; } = true;

        public virtual float GuideLength { get; set; } = 0.5f;

        public virtual float GuideThickness { get; set; } = 0.05f;

        public virtual float GuideOffset { get; set; } = 0.3f;

        public virtual float GuideHideDistance { get; set; } = 2.5f;

        public virtual bool ShowProxyOnDesktop { get; set; } = false;

        public virtual bool DebugMode { get; set; } = false;

        public virtual float OriginalNoteOpacity { get; set; } = 0.1f;

        public virtual float ProxyNoteOpacity { get; set; } = 1.0f;

        // Keep hit particles/haptics, but do not spawn the two vanilla note halves.
        public virtual bool SuppressVanillaDebris { get; set; } = true;

        public virtual bool UseChinese { get; set; } = false;

        // Emit one overlap sample per note around the hit time. This is intentionally off by default.
        public virtual bool LogCalibrationSamples { get; set; } = false;

        public virtual void OnReload()
        {
        }

        public virtual void Changed()
        {
        }

        internal void ApplyMigrations()
        {
            if (ConfigVersion < 1)
            {
                JumpLeadDistance = 0f;
                BombJumpLeadDistance = 0f;
                ConfigVersion = 1;
            }

            if (ConfigVersion < 2)
            {
                EnableNoteRotation = true;
                EnableNotePositionSwaps = true;
                OriginalNoteOpacity = 0.1f;
                ProxyNoteOpacity = 1f;
                ConfigVersion = 2;
            }

            if (ConfigVersion < 3)
            {
                NoteRotationCoefficient = EnableNoteRotation ? 1f : 0f;
                ConfigVersion = 3;
            }

            if (ConfigVersion < 4)
            {
                BombJumpLeadDistance = JumpLeadDistance;
                ConfigVersion = 4;
            }

            if (ConfigVersion < 5)
            {
                JumpLeadDistance = JumpLeadDistance < 0f
                    ? 0f
                    : JumpLeadDistance > 5f
                        ? 5f
                        : JumpLeadDistance;
                BombJumpLeadDistance = JumpLeadDistance;
                NoteRotationCoefficient = 0.2f;
                EnableNotePositionSwaps = false;
                ConfigVersion = 5;
            }

            Changed();
        }

        internal void ResetToDefaults()
        {
            ConfigVersion = 5;
            Enabled = true;
            JumpLeadDistance = 0f;
            BombJumpLeadDistance = 0f;
            EnableNoteRotation = true;
            NoteRotationCoefficient = 0.2f;
            EnableNotePositionSwaps = false;
            GuideEnabled = true;
            GuideLength = 0.5f;
            GuideThickness = 0.05f;
            GuideOffset = 0.3f;
            GuideHideDistance = 2.5f;
            ShowProxyOnDesktop = false;
            DebugMode = false;
            OriginalNoteOpacity = 0.1f;
            ProxyNoteOpacity = 1f;
            SuppressVanillaDebris = true;
            UseChinese = false;
            LogCalibrationSamples = false;
        }

        public virtual void CopyFrom(PluginConfig other)
        {
            ConfigVersion = other.ConfigVersion;
            Enabled = other.Enabled;
            JumpLeadDistance = other.JumpLeadDistance;
            BombJumpLeadDistance = other.BombJumpLeadDistance;
            EnableNoteRotation = other.EnableNoteRotation;
            NoteRotationCoefficient = other.NoteRotationCoefficient;
            EnableNotePositionSwaps = other.EnableNotePositionSwaps;
            GuideEnabled = other.GuideEnabled;
            GuideLength = other.GuideLength;
            GuideThickness = other.GuideThickness;
            GuideOffset = other.GuideOffset;
            GuideHideDistance = other.GuideHideDistance;
            ShowProxyOnDesktop = other.ShowProxyOnDesktop;
            DebugMode = other.DebugMode;
            OriginalNoteOpacity = other.OriginalNoteOpacity;
            ProxyNoteOpacity = other.ProxyNoteOpacity;
            SuppressVanillaDebris = other.SuppressVanillaDebris;
            UseChinese = other.UseChinese;
            LogCalibrationSamples = other.LogCalibrationSamples;
        }
    }
}

namespace OpenUtau.Core.Ustx {
    /// <summary>
    /// Per-track post-processing effects state.  Persisted in ustx alongside
    /// the rest of the track.  When a UTrack's MixFx is null or has Enabled =
    /// false, the render path bypasses the entire FX chain (zero overhead).
    /// </summary>
    public class UMixFx {
        public bool Enabled { get; set; } = false;

        // Preset name keys (kept for UI display only).  Slider values below
        // are the source of truth for the actual DSP.
        public string EqPreset { get; set; } = "vocal_air";
        public string CompPreset { get; set; } = "gentle";
        public string ReverbPreset { get; set; } = "small_room";
        public string DeEsserPreset { get; set; } = "standard";
        public string DeThumperPreset { get; set; } = "standard";
        public string SaturationPreset { get; set; } = "standard";

        // EQ
        public double EqLowDb { get; set; } = 0.0;
        public double EqMidFreq { get; set; } = 3000.0;
        public double EqMidDb { get; set; } = 1.5;
        public double EqHighDb { get; set; } = 3.0;

        // Compressor
        public double CompThresholdDb { get; set; } = -18.0;
        public double CompRatio { get; set; } = 2.0;
        public double CompMakeupDb { get; set; } = 2.5;

        // Reverb
        public double ReverbSize { get; set; } = 0.30;
        public double ReverbDamp { get; set; } = 0.7;
        public double ReverbWet { get; set; } = 1.0;
        public double ReverbPreDelayMs { get; set; } = 0.0;
        
        // De-esser
        public double DeEsserFreq { get; set; } = 6000.0;
        public double DeEsserThresholdDb { get; set; } = -20.0;
        
        // De-thumper
        public double DeThumperFreq { get; set; } = 80.0;
        public double DeThumperReductionDb { get; set; } = -6.0;
        
        // Saturation
        public double SaturationDrive { get; set; } = 0.0;
        public double SaturationMix { get; set; } = 0.0;

        public UMixFx Clone() {
            return new UMixFx {
                Enabled = Enabled,
                EqPreset = EqPreset,
                CompPreset = CompPreset,
                ReverbPreset = ReverbPreset,
                EqLowDb = EqLowDb,
                EqMidFreq = EqMidFreq,
                EqMidDb = EqMidDb,
                EqHighDb = EqHighDb,
                CompThresholdDb = CompThresholdDb,
                CompRatio = CompRatio,
                CompMakeupDb = CompMakeupDb,
                ReverbSize = ReverbSize,
                ReverbDamp = ReverbDamp,
                ReverbWet = ReverbWet,
                ReverbPreDelayMs = ReverbPreDelayMs,
                DeEsserFreq = DeEsserFreq,
                DeEsserThresholdDb = DeEsserThresholdDb,
                DeThumperFreq = DeThumperFreq,
                DeThumperReductionDb = DeThumperReductionDb,
                SaturationDrive = SaturationDrive,
                SaturationMix = SaturationMix,
                DeEsserPreset = DeEsserPreset,
                DeThumperPreset = DeThumperPreset,
                SaturationPreset = SaturationPreset,
            };
        }
    }
}
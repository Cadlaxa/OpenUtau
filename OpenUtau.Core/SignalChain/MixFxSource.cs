using System;
using OpenUtau.Core.SignalChain.Effects;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.SignalChain {
    public class MixFxSource : ISignalSource {
        public const int SampleRate = 44100;
        public const int Channels = 2;

        private readonly ISignalSource source;
        private readonly DeThumper deThumper;
        private readonly BiquadEQ eq;
        private readonly DeEsser deEsser;
        private readonly SimpleCompressor comp;
        private readonly Saturation saturation;
        private readonly Freeverb reverb;

        private float[] scratch;

        public MixFxSource(ISignalSource source,
                           DeThumper deThumper, BiquadEQ eq, DeEsser deEsser, 
                           SimpleCompressor comp, Saturation saturation, Freeverb reverb) {
            this.source = source;
            this.deThumper = deThumper;
            this.eq = eq;
            this.deEsser = deEsser;
            this.comp = comp;
            this.saturation = saturation;
            this.reverb = reverb;
        }

        public bool IsReady(int position, int count) => source.IsReady(position, count);

        public int Mix(int position, float[] buffer, int index, int count) {
            if (scratch == null || scratch.Length < count) {
                scratch = new float[count];
            }
            Array.Clear(scratch, 0, count);
            int ret = source.Mix(position, scratch, 0, count);

            // Apply effects in series.
            // Typical Chain: Thump -> EQ -> Esser -> Comp -> Saturation -> Reverb
            deThumper.Process(scratch, 0, count);
            eq.Process(scratch, 0, count);
            deEsser.Process(scratch, 0, count);
            comp.Process(scratch, 0, count);
            saturation.Process(scratch, 0, count);
            reverb.Process(scratch, 0, count);

            for (int i = 0; i < count; i++) {
                buffer[index + i] += scratch[i];
            }
            return ret;
        }

        public bool IsAnythingEnabled => 
            !deThumper.IsBypassed || !eq.IsBypassed || !deEsser.IsBypassed || 
            !comp.IsBypassed || !saturation.IsBypassed || !reverb.IsBypassed;

        public static ISignalSource WrapWith(ISignalSource inner, UMixFx fx) {
            if (fx == null || !fx.Enabled) return inner;

            // 1. De-Thumper
            var deThumper = new DeThumper(SampleRate, Channels);
            deThumper.Configure(fx.DeThumperFreq, fx.DeThumperReductionDb, SampleRate);

            // 2. EQ
            var eq = new BiquadEQ(SampleRate, Channels);
            eq.Configure(fx.EqLowDb, fx.EqMidFreq, 0.707, fx.EqMidDb, fx.EqHighDb);

            // 3. De-Esser
            var deEsser = new DeEsser(SampleRate, Channels);
            deEsser.Configure(fx.DeEsserFreq, fx.DeEsserThresholdDb, SampleRate);

            // 4. Compressor
            var comp = new SimpleCompressor(SampleRate, Channels);
            FxPresets.CompParams cParams = FxPresets.Comp.TryGetValue(fx.CompPreset ?? FxPresets.Off, out var cp)
                ? cp : FxPresets.Comp[FxPresets.Off];
            comp.Configure(fx.CompThresholdDb, fx.CompRatio, cParams.AttackMs, cParams.ReleaseMs, fx.CompMakeupDb);

            // 5. Saturation
            var saturation = new Saturation();
            saturation.Configure(fx.SaturationDrive, fx.SaturationMix);

            // 6. Reverb
            var reverb = new Freeverb(SampleRate, Channels);
            FxPresets.ReverbParams rParams = FxPresets.Reverb.TryGetValue(fx.ReverbPreset ?? FxPresets.Off, out var rp)
                ? rp : FxPresets.Reverb[FxPresets.Off];
            double userWet = Math.Clamp(fx.ReverbWet, 0.0, 2.0);
            reverb.Configure(fx.ReverbSize, fx.ReverbDamp, rParams.Width, rParams.Wet * userWet, rParams.Dry, fx.ReverbPreDelayMs);

            var wrapper = new MixFxSource(inner, deThumper, eq, deEsser, comp, saturation, reverb);
            return wrapper.IsAnythingEnabled ? wrapper : inner;
        }
    }
}
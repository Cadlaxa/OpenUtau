using System;

namespace OpenUtau.Core.SignalChain.Effects {
    /// <summary>
    /// Isolates high-frequency sibilance using a sidechain high-pass filter, 
    /// dynamically ducking those frequencies when they exceed a threshold.
    /// </summary>
    public class DeEsser {
        private int channels;
        private float thresholdLinear;
        private float[] envelopes;
        
        // High-pass filter variables for the sidechain
        private float hp_b0, hp_b1, hp_b2, hp_a1, hp_a2;
        private float[] hp_x1, hp_x2, hp_y1, hp_y2;

        public bool IsBypassed => thresholdLinear >= 1.0f;

        public DeEsser(int sampleRate, int channels) {
            this.channels = channels;
            envelopes = new float[channels];
            hp_x1 = new float[channels]; hp_x2 = new float[channels];
            hp_y1 = new float[channels]; hp_y2 = new float[channels];
        }

        public void Configure(double freq, double thresholdDb, int sampleRate) {
            this.thresholdLinear = (float)Math.Pow(10, thresholdDb / 20.0);
            if (IsBypassed) return;

            // High-pass filter coefficients
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / 2 * 0.707;
            double a0_c = 1 + alpha;
            
            hp_b0 = (float)((1 + Math.Cos(w0)) / 2 / a0_c);
            hp_b1 = (float)(-(1 + Math.Cos(w0)) / a0_c);
            hp_b2 = (float)((1 + Math.Cos(w0)) / 2 / a0_c);
            hp_a1 = (float)(-2 * Math.Cos(w0) / a0_c);
            hp_a2 = (float)((1 - alpha) / a0_c);
        }

        public void Process(float[] buffer, int offset, int count) {
            if (IsBypassed) return;
            
            float attack = 0.05f; 
            float release = 0.005f; 

            for (int i = offset; i < offset + count; i++) {
                int ch = i % channels;
                float x = buffer[i];

                // Isolate the "Ess" frequencies
                float det = hp_b0 * x + hp_b1 * hp_x1[ch] + hp_b2 * hp_x2[ch] - hp_a1 * hp_y1[ch] - hp_a2 * hp_y2[ch];
                hp_x2[ch] = hp_x1[ch]; hp_x1[ch] = x;
                hp_y2[ch] = hp_y1[ch]; hp_y1[ch] = det;

                // Read the volume of the "Ess"
                float detAbs = Math.Abs(det);
                if (detAbs > envelopes[ch]) envelopes[ch] += attack * (detAbs - envelopes[ch]);
                else envelopes[ch] += release * (detAbs - envelopes[ch]);

                // Calculate gain reduction if Sibilance is too loud
                float gain = 1.0f;
                if (envelopes[ch] > thresholdLinear) {
                    gain = thresholdLinear / envelopes[ch];
                    gain = Math.Max(gain, 0.1f);
                }

                // Subtract the over-loud sibilance from the main signal
                float duckingAmount = 1.0f - gain;
                buffer[i] = x - (det * duckingAmount); 
            }
        }
    }
}
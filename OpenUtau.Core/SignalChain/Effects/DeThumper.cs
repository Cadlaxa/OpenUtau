using System;

namespace OpenUtau.Core.SignalChain.Effects {
    /// <summary>
    /// A low-shelf biquad filter designed to reduce low-frequency rumble, plosives, and mud.
    /// </summary>
    public class DeThumper {
        private int channels;
        private float reduction;
        private float b0, b1, b2, a1, a2;
        private float[] x1, x2, y1, y2;

        public bool IsBypassed => reduction >= -0.01f;

        public DeThumper(int sampleRate, int channels) {
            this.channels = channels;
            x1 = new float[channels]; x2 = new float[channels];
            y1 = new float[channels]; y2 = new float[channels];
        }

        public void Configure(double freq, double reductionDb, int sampleRate) {
            this.reduction = (float)reductionDb;
            if (IsBypassed) return;

            // Low-shelf Biquad Filter Coefficients
            double A = Math.Pow(10, reductionDb / 40.0);
            double w0 = 2 * Math.PI * freq / sampleRate;
            double alpha = Math.Sin(w0) / 2.0 * Math.Sqrt(2) / 2.0;

            double b0_c = A * ((A + 1) - (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha);
            double b1_c = 2 * A * ((A - 1) - (A + 1) * Math.Cos(w0));
            double b2_c = A * ((A + 1) - (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha);
            double a0_c = (A + 1) + (A - 1) * Math.Cos(w0) + 2 * Math.Sqrt(A) * alpha;
            double a1_c = -2 * ((A - 1) + (A + 1) * Math.Cos(w0));
            double a2_c = (A + 1) + (A - 1) * Math.Cos(w0) - 2 * Math.Sqrt(A) * alpha;

            b0 = (float)(b0_c / a0_c);
            b1 = (float)(b1_c / a0_c);
            b2 = (float)(b2_c / a0_c);
            a1 = (float)(a1_c / a0_c);
            a2 = (float)(a2_c / a0_c);
        }

        public void Process(float[] buffer, int offset, int count) {
            if (IsBypassed) return;
            for (int i = offset; i < offset + count; i++) {
                int ch = i % channels;
                float x = buffer[i];
                float y = b0 * x + b1 * x1[ch] + b2 * x2[ch] - a1 * y1[ch] - a2 * y2[ch];
                
                x2[ch] = x1[ch]; x1[ch] = x;
                y2[ch] = y1[ch]; y1[ch] = y;
                
                buffer[i] = y;
            }
        }
    }
}
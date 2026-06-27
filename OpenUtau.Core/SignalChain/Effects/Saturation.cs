using System;

namespace OpenUtau.Core.SignalChain.Effects {
    /// <summary>
    /// Applies soft clipping via a hyperbolic tangent (Tanh) function to introduce 
    /// harmonic distortion, thickening the vocal and simulating analog warmth.
    /// </summary>
    public class Saturation {
        private float drive;
        private float mix;

        public bool IsBypassed => mix <= 0.001f || drive <= 0.001f;

        public void Configure(double driveDb, double mixFactor) {
            this.drive = (float)driveDb;
            this.mix = (float)mixFactor;
        }

        public void Process(float[] buffer, int offset, int count) {
            if (IsBypassed) return;
            
            // Map 0-10 Drive to a gain multiplier
            float gain = 1f + (drive * 0.4f);
            float invGain = 1f / (float)Math.Tanh(gain); // Gain compensation

            for (int i = offset; i < offset + count; i++) {
                float dry = buffer[i];
                float wet = (float)Math.Tanh(dry * gain) * invGain;
                buffer[i] = dry + (wet - dry) * mix;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Render {
    public static class SpectralMorpher {
        private const int N_FFT = 2048;
        private const int HOP_LENGTH = 256;

        public static float[] MorphN(float[] baseAudio, List<float[]> colorAudios, List<float[]> colorCurves, int sampleRate = 44100) {
            double[] window = new double[N_FFT];
            for (int i = 0; i < N_FFT; i++) window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (N_FFT - 1)));

            Complex[][] stftBase = ComputeSTFT(baseAudio, window);
            List<Complex[][]> stftColors = new List<Complex[][]>();
            foreach (var audio in colorAudios) stftColors.Add(ComputeSTFT(audio, window));

            int numFrames = stftBase.Length; 
            Complex[][] stftMorphed = new Complex[numFrames][];
            int numColors = colorAudios.Count;

            double[] targetMags = new double[N_FFT];
            Complex[] targetComplexes = new Complex[N_FFT];
            double[] rawCoherence = new double[N_FFT];
            double[] smoothCoherence = new double[N_FFT];

            for (int i = 0; i < numFrames; i++) {
                stftMorphed[i] = new Complex[N_FFT];
                double timeMs = (i * HOP_LENGTH / (double)sampleRate) * 1000.0;
                int curveIndex = (int)(timeMs / 5.0); 

                double[] weights = new double[numColors];
                double sumColorWeights = 0;
                for (int c = 0; c < numColors; c++) {
                    int idx = Math.Min(curveIndex, colorCurves[c].Length - 1);
                    weights[c] = Math.Max(0, colorCurves[c][idx]) / 100.0; 
                    sumColorWeights += weights[c];
                }
                if (sumColorWeights > 1.0) {
                    for (int c = 0; c < numColors; c++) weights[c] /= sumColorWeights;
                    sumColorWeights = 1.0;
                }
                double baseWeight = 1.0 - sumColorWeights;

                bool usePhaseLocked = Preferences.Default.PhaseLocked; 

                if (usePhaseLocked) {
                    // AUTO MODE: Dynamic Per-Bin Adaptive Blend (Optimized for Organic Transitions)
                    // raw processing vectors and basic vector coherence metrics
                    for (int bin = 0; bin < N_FFT; bin++) {
                        double targetMag = baseWeight * stftBase[i][bin].Magnitude;
                        for (int c = 0; c < numColors; c++) {
                            var frameColor = (i < stftColors[c].Length) ? stftColors[c][i] : stftBase[i];
                            targetMag += weights[c] * frameColor[bin].Magnitude;
                        }
                        targetMags[bin] = targetMag;

                        Complex targetComplex = stftBase[i][bin] * baseWeight;
                        for (int c = 0; c < numColors; c++) {
                            var frameColor = (i < stftColors[c].Length) ? stftColors[c][i] : stftBase[i];
                            targetComplex += frameColor[bin] * weights[c];
                        }
                        targetComplexes[bin] = targetComplex;

                        double complexMag = targetComplex.Magnitude;
                        rawCoherence[bin] = (targetMag > 1e-8) ? Math.Clamp(complexMag / targetMag, 0.0, 1.0) : 1.0;
                    }

                    // Smooth coherence across the frequency axis via a localized 5-tap filter.
                    for (int bin = 0; bin < N_FFT; bin++) {
                        double sum = 0;
                        double weightSum = 0;

                        for (int k = -2; k <= 2; k++) {
                            int neighbor = bin + k;
                            if (neighbor >= 0 && neighbor < N_FFT) {
                                // Smooth bell curve distribution weights
                                double w = (k == 0) ? 0.40 : (Math.Abs(k) == 1 ? 0.25 : 0.05);
                                sum += rawCoherence[neighbor] * w;
                                weightSum += w;
                            }
                        }
                        smoothCoherence[bin] = sum / weightSum;
                    }

                    for (int bin = 0; bin < N_FFT; bin++) {
                        Complex purePhaseLocked = Complex.FromPolarCoordinates(targetMags[bin], stftBase[i][bin].Phase);
                        double c = smoothCoherence[bin];
                        double blendFactor = c * c * (3.0 - 2.0 * c);

                        stftMorphed[i][bin] = (purePhaseLocked * (1.0 - blendFactor)) + (targetComplexes[bin] * blendFactor);
                    }
                } else {
                    // Force Pure Phase-Locked (Stable volume, better for smooth vowels)
                    for (int bin = 0; bin < N_FFT; bin++) {
                        double targetMag = baseWeight * stftBase[i][bin].Magnitude;

                        for (int c = 0; c < numColors; c++) {
                            var frameColor = (i < stftColors[c].Length) ? stftColors[c][i] : stftBase[i];
                            targetMag += weights[c] * frameColor[bin].Magnitude;
                        }

                        stftMorphed[i][bin] = Complex.FromPolarCoordinates(targetMag, stftBase[i][bin].Phase);
                    }
                }
            }
            return ComputeISTFT(stftMorphed, window, baseAudio.Length);
        }

        private static Complex[][] ComputeSTFT(float[] audio, double[] window) {
            int numFrames = (audio.Length / HOP_LENGTH) + 3; 
            Complex[][] frames = new Complex[numFrames][];

            for (int i = 0; i < numFrames; i++) {
                frames[i] = new Complex[N_FFT];
                int offset = i * HOP_LENGTH - N_FFT / 2; 
                
                for (int j = 0; j < N_FFT; j++) {
                    int idx = offset + j;
                    float sample = (idx >= 0 && idx < audio.Length) ? audio[idx] : 0f; 
                    frames[i][j] = new Complex(sample * window[j], 0);
                }
                Fourier.Forward(frames[i], FourierOptions.Default);
            }
            return frames;
        }

        private static float[] ComputeISTFT(Complex[][] stft, double[] window, int expectedLength) {
            int numFrames = stft.Length;
            int maxOutLength = numFrames * HOP_LENGTH + N_FFT;
            float[] output = new float[maxOutLength];
            double[] windowSum = new double[maxOutLength];

            for (int i = 0; i < numFrames; i++) {
                Complex[] frame = new Complex[N_FFT];
                Array.Copy(stft[i], frame, N_FFT);
                
                Fourier.Inverse(frame, FourierOptions.Default);

                int offset = i * HOP_LENGTH - N_FFT / 2; 
                for (int j = 0; j < N_FFT; j++) {
                    int idx = offset + j;
                    if (idx >= 0 && idx < maxOutLength) {
                        output[idx] += (float)(frame[j].Real * window[j]);
                        windowSum[idx] += window[j] * window[j];
                    }
                }
            }

            float[] finalOutput = new float[expectedLength];
            for (int i = 0; i < expectedLength; i++) {
                double wSum = windowSum[i];
                float val = 0f;
                if (wSum > 1e-8) val = (float)(output[i] / wSum);
                if (float.IsNaN(val) || float.IsInfinity(val)) val = 0f;
                finalOutput[i] = val;
            }
            return finalOutput;
        }
    }
}
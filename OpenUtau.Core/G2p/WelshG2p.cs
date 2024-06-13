using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using OpenUtau.Api;

namespace OpenUtau.Core.G2p {
    public class WelshG2p : G2pPack {
        private static readonly string[] graphemes = new string[] {
            "", "", "", "", "-", "a", "b", "c", "ch", "d", "dd", "e",
            "f", "ff", "g", "h", "i", "l", "ll", "m", "n", "ng", "o",
            "p", "ph", "r", "rh", "s", "t", "th", "u", "w", "y", "â",
            "ê", "î", "ô", "û", "ŵ", "ŷ"
        };

        private static readonly string[] phonemes = new string[] {
            "", "", "", "", "a", "A", "Ar", "b", "c", "ch", "d", "dd",
            "e", "E", "er", "f", "ff", "g", "h", "i", "I", "ir", "l",
            "ll", "m", "n", "ng", "o", "O", "or", "p", "q", "r", "rh",
            "s", "sh", "t", "th", "u", "U", "w", "W", "wr", "y", "Y"
        };

        private static object lockObj = new object();
        private static Dictionary<string, int> graphemeIndexes;
        private static IG2p dict;
        private static InferenceSession session;
        private static Dictionary<string, string[]> predCache = new Dictionary<string, string[]>();

        public WelshG2p() {
            lock (lockObj) {
                if (graphemeIndexes == null) {
                    graphemeIndexes = graphemes
                        .Skip(4)
                        .Select((g, i) => Tuple.Create(g, i))
                        .ToDictionary(t => t.Item1, t => t.Item2 + 4);
                    var tuple = LoadPack(Data.Resources.g2p_cym);
                    dict = tuple.Item1;
                    session = tuple.Item2;
                }
            }
            GraphemeIndexes = graphemeIndexes;
            Phonemes = phonemes;
            Dict = dict;
            Session = session;
            PredCache = predCache;
        }
    }
}

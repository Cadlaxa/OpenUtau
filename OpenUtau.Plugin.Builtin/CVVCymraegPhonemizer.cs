using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using Serilog;


namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Welsh CVVC Phonemizer", "CVVCymraeg", "Greta HaffandHawf & Mim & Ricecristpy & Cadlaxa", language: "CY")]
    // Contributed by Greta HaffandHawf with guidance from Mim & input from Ricecristpy (aka Liv) on their reclist
    public class CVVCymraegPhonemizer : SyllableBasedPhonemizer {
        private readonly string[] vowels = "a,A,e,E,i,I,o,O,u,U,w,W,y,Y,a',A',e',E',i',I',o',O',u',U',w',W',y',Y'".Split(",");
        private readonly string[] consonants = "b,c,ch,d,dd,f,ff,g,h,l,ll,m,n,ng,p,q,r,rh,s,sh,t,th".Split(",");
        private readonly string[] ShortConsonants = "r".Split(",");
        private readonly string[] longConsonants = "c,g,p,s,sh,t".Split(",");
        private readonly string[] hardConsonants = "b,c,d,g,p,t".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("aa=A;aar=Ar;ae=a;ah=U;ao=o;aor=or;eh=e;ehr=er;ey=E;ih=i;ihr=ir;iy=I;ow=O;uh=w;uhr=wr;uw=W;" +
            "dh=dd;f=ff;hh=h;k=c;rr=r;v=f;x=ch;").Split(';')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "cmudict-0_7b.txt";
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;


        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();
            // LOAD DICTIONARY FROM SINGER FOLDER
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, "arpasing.yaml");
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }
            // LOAD DICTIONARY FROM FOLDER
            string path = Path.Combine(PluginDir, "arpasing.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Data.Resources.arpasing_template);
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            g2ps.Add(new WelshG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }
        protected override List<string> ProcessSyllable(Syllable syllable) {
            string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            // --------------------------- STARTING V ------------------------------- //
            if (syllable.IsStartingV) {
                basePhoneme = $"- {v}";

                // --------------------------- is VV ------------------------------- //
            } else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable) || !AreTonesFromTheSameSubbank(syllable.tone, syllable.vowelTone)) {
                    basePhoneme = $"{prevV} {v}";
                } else {
                    // PREVIOUS ALIAS WILL EXTEND as [V V]
                    basePhoneme = null;
                }
                // --------------------------- STARTING CV ------------------------------- //
            } else if (syllable.IsStartingCVWithOneConsonant) {
                basePhoneme = $"- {cc[0]}{v}";
                if (!HasOto(basePhoneme, syllable.tone)) {
                    TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}");
                    basePhoneme = $"{cc[0]}{v}";
                }
                // --------------------------- STARTING CCV ------------------------------- //
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                if (!hardConsonants.Contains(cc[0])) {
                    phonemes.Add($"- {cc[0]}");
                }
                basePhoneme = $"{cc.Last()}{v}";
                // CC + CCV support
                var ccv = $"{cc[cc.Length - 2]}{cc.Last()}{v}";
                if (HasOto(ccv, syllable.tone)) {
                    basePhoneme = ccv;

                    for (int i = 0; i < cc.Length - 2; i++) {
                        var cci = $"{cc[i]} {cc[i + 1]}";

                        if (i == 0) {
                            cci = $"- {cc[i]}{cc[i + 1]}_";
                        }
                        if (!HasOto(cci, syllable.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}_";
                            if (i + 1 == cc.Length - 2 && HasOto($"_{ccv}", syllable.tone)) {
                                basePhoneme = $"_{ccv}";
                            }
                        }
                        TryAddPhoneme(phonemes, syllable.tone, cci);
                    }
                } else {
                    // CC + CV support
                    for (int i = 0; i < cc.Length - 1; i++) {
                        var cci = $"{cc[i]}{cc[i + 1]}_";

                        if (i == 0) {
                            cci = $"- {cc[i]}{cc[i + 1]}_";
                            if (!HasOto(cci, syllable.tone)) {
                                cci = $"{cc[i]}{cc[i + 1]}_";
                            }
                        }

                        if (HasOto(cci, syllable.tone)) {
                            phonemes.Add(cci);
                            if (i + 1 == cc.Length - 1 && HasOto($"_{cc.Last()}{v}", syllable.tone)) {
                                basePhoneme = $"_{cc.Last()}{v}";
                            }
                        } else {
                            cci = $"{cc[i]} {cc[i + 1]}";
                            TryAddPhoneme(phonemes, syllable.tone, cci);
                        }
                    }
                }
            }
            // --------------------------- IS VCV ------------------------------- //
            else if (syllable.IsVCVWithOneConsonant) {
                // try VCV
                var vc = $"{prevV} {cc[0]}";
                phonemes.Add(vc);
                basePhoneme = $"{cc[0]}{v}";
            } else {
                // ------------- IS VCV WITH MORE THAN ONE CONSONANT --------------- //
                var vc = $"{prevV} {cc[0]}";
                phonemes.Add(vc);
                basePhoneme = $"{cc.Last()}{v}";
                // CC + CCV support
                var ccv = $"{cc[cc.Length - 2]}{cc.Last()}{v}";
                if (HasOto(ccv, syllable.tone)) {
                    basePhoneme = ccv;

                    for (int i = 0; i < cc.Length - 2; i++) {
                        var cci = $"{cc[i]} {cc[i + 1]}";
                        if (!HasOto(cci, syllable.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}_";
                            if (i + 1 == cc.Length - 2 && HasOto($"_{ccv}", syllable.tone)) {
                                basePhoneme = $"_{ccv}";
                            }
                        }
                        TryAddPhoneme(phonemes, syllable.tone, cci);
                    }
                } else {
                    // CC + CV support
                    for (int i = 0; i < cc.Length - 1; i++) {
                        var cci = $"{cc[i]}{cc[i + 1]}_";

                        if (HasOto(cci, syllable.tone)) {
                            phonemes.Add(cci);
                            if (i + 1 == cc.Length - 1 && HasOto($"_{cc.Last()}{v}", syllable.tone)) {
                                basePhoneme = $"_{cc.Last()}{v}";
                            }
                        } else {
                            cci = $"{cc[i]} {cc[i + 1]}";
                            if (!HasOto(cci, syllable.tone)) {
                                cci = $"{cc[i]}{cc[i + 1]}";
                            }
                            TryAddPhoneme(phonemes, syllable.tone, cci);
                        }

                    }
                }
            }
            phonemes.Add(basePhoneme);
            return phonemes;
        }
        protected override List<string> ProcessEnding(Ending ending) {
            string[] cc = ending.cc;
            string v = ending.prevV;

            var phonemes = new List<string>();

            // --------------------------- ENDING V ------------------------------- //
            if (ending.IsEndingV) {
                var vE = $"{v} -";
                phonemes.Add(vE);

            } else {
                // --------------------------- ENDING VC ------------------------------- //
                if (ending.IsEndingVCWithOneConsonant) {
                    // try 'VC -' else 'V C' + 'C -'
                    var vc = $"{v}{cc[0]} -";
                    if (HasOto(vc, ending.tone)) {
                        phonemes.Add(vc);
                    } else {
                        vc = $"{v} {cc[0]}";
                        phonemes.Add(vc);

                        var cE = $"{cc[0]} -'";
                        phonemes.Add(cE);
                    }
                } else {
                    // --------------------------- ENDING VCC ------------------------------- //
                    var vc = $"{v} {cc[0]}";
                    phonemes.Add(vc);
                    bool hasEnding = false;

                    for (int i = 0; i < cc.Length - 1; i++) {
                        var cci = $"{cc[i]} {cc[i + 1]}";

                        if (i == cc.Length - 2) {
                            cci = $"{cc[i]}{cc[i + 1]} -";
                            hasEnding = true;
                        }
                        if (!HasOto(cci, ending.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}_";
                            hasEnding = false;
                        }
                        if (!HasOto(cci, ending.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}";
                            hasEnding = false;
                        }

                        TryAddPhoneme(phonemes, ending.tone, cci);
                    }

                    if (!hasEnding) {
                        var cE = $"{cc.Last()} -";
                        TryAddPhoneme(phonemes, ending.tone, cE);
                    }
                }
            }
         return phonemes;
        }
        
        protected override double GetTransitionBasicLengthMs(string alias = "") {
            //I wish these were automated instead :')
            double transitionMultiplier = 1.0; // Default multiplier

            foreach (var c in longConsonants) {
                return base.GetTransitionBasicLengthMs() * 2.5;
            }

            foreach (var c in hardConsonants) {
                return base.GetTransitionBasicLengthMs() * 1.3;
            }

            foreach (var c in ShortConsonants) {
                foreach (var v in vowels) {
                    if (alias.Contains($"{v} r")) {
                        return base.GetTransitionBasicLengthMs() * 0.5;
                    }
                }
            }

            return base.GetTransitionBasicLengthMs() * transitionMultiplier;
        }
    }
}

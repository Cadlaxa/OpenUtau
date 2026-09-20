using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Classic;
using OpenUtau.Api;
using OpenUtau.Classic;
using OpenUtau.Core.G2p;
using OpenUtau.Core.Ustx;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.RegularExpressions;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Spanish C+V Phonemizer", "ES C+V", "Cadlaxa", language: "ES")]
    public class SpanishCpVPhonemizer : SyllableBasedPhonemizer {
        protected override string YamlFileName => "es-cPv.yaml";
        protected override byte[] YamlTemplate => Spanish_CpV.data.Resources.template;
        protected override string YamlVersion => "1.1";
        private string[] vowels = Array.Empty<string>();
        private static string[] diphthongs = { "ay", "ey", "oy", "aw", "ow" };
        private static string[] c_cR = { "n" };
        private string[] consonants = Array.Empty<string>();
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        private Dictionary<string, string> dictionaryReplacements;
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;
        // Store the splitting replacements
        private List<Replacement> splittingReplacements = new List<Replacement>();
        // Store the merging replacements
        private List<Replacement> mergingReplacements = new List<Replacement>();

        // For banks with missing vowels
        private Dictionary<string, string> missingVphonemes = "R=-,ii=iy".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

        // TIMIT symbols
        private readonly Dictionary<string, string> timitphonemes = "axh=ax,bcl=b,dcl=d,eng=ng,gcl=g,hv=hh,kcl=k,pcl=p,tcl=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isTimitPhonemes = false;

        private static Dictionary<string, string> DiphthongExceptions = diphthongs.ToDictionary(
            key => key,
            value => value.Last().ToString()
        );

        private Dictionary<string, double> PhonemeOverrides = new Dictionary<string, double>();

        private readonly string[] ccvException = { "ch", "dh", "dx", "fh", "gh", "hh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };
        private readonly string[] RomajiException = { "a", "e", "i", "o", "u" };

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (original == null) {
                return null;
            }
            bool hasPhoneticHint = !string.IsNullOrEmpty(note.phoneticHint);
            for (int k = 1; k < original.Length; k++) {
                if (!hasPhoneticHint) {
                    if (original[k] == "ih" && consonants.Contains(original[k - 1]) && (original[k - 1] != "vtrash"  || original[k - 1] != "q")) {
                        original[k] = "ii";
                    }
                }
            }
            List<string> modified = new List<string>(original);
            List<string> finalPhonemes = new List<string>();
            int i = 0;
            bool hasReplacements = mergingReplacements.Any() == true || splittingReplacements.Any() == true; // Check for any replacements
            if (hasReplacements) {
                finalPhonemes = new List<string>();
                while (i < modified.Count) {
                    bool replaced = false;
                    foreach (var rule in mergingReplacements.Concat(splittingReplacements).Where(r => r.where == "inside")) {
                        if (rule.from is string[] fromArray && i + fromArray.Length <= modified.Count) {
                            bool match = true;
                            for (int j = 0; j < fromArray.Length; j++) {
                                if (modified[i + j] != fromArray[j]) {
                                    match = false;
                                    break;
                                }
                            }
                            if (match) {
                                if (rule.to is string toString) {
                                    finalPhonemes.Add(toString);
                                } else if (rule.to is string[] toArray) {
                                    finalPhonemes.AddRange(toArray);
                                }
                                i += fromArray.Length;
                                replaced = true;
                                break;
                            }
                        }
                    }

                    if (!replaced && splittingReplacements.Any()) {
                        string currentPhoneme = modified[i];
                        bool singleReplaced = false;
                        foreach (var rule in splittingReplacements.Where(r => r.where == "inside")) {
                            if (rule.from.ToString() == currentPhoneme && rule.to is string[] toArray) {
                                finalPhonemes.AddRange(toArray);
                                singleReplaced = true;
                                break;
                            }
                        }
                        if (!singleReplaced) {
                            finalPhonemes.Add(ReplacePhoneme(modified[i], note.tone));
                        }
                        i++;
                    } else if (!replaced) {
                        finalPhonemes.Add(ReplacePhoneme(modified[i], note.tone));
                        i++;
                    }
                }
            } else {
                finalPhonemes = new List<string>(modified);
            }
            List<string> finalProcessedPhonemes = new List<string>();

            IEnumerable<string> phonemes;
            if (hasReplacements) {
                phonemes = finalPhonemes;
            } else {
                phonemes = original;
            }
            
            foreach (string s in phonemes) {
                switch (s) {
                    default:
                        finalProcessedPhonemes.Add(s);
                        break;
                }
            }
            return finalProcessedPhonemes.ToArray();
        }

        protected override IG2p[] GetBaseG2ps() {
            return new IG2p[] { new ArpabetPlusG2p() };
        }

        public override void SetSinger(USinger singer) {
            base.SetSinger(singer);

            if (this.singer != null && this.singer.Loaded) {
                consExceptions.Clear();
                if (stop != null) consExceptions.AddRange(stop);
                if (tap != null) consExceptions.AddRange(tap);
                consExceptions = consExceptions.Distinct().ToList();
            }
        }

        protected override List<string> ProcessSyllable(Syllable syllable) {
            // Replacement for note boundaries
            List<string> currentPhonemes = new List<string>();
            bool hasPrevV = !string.IsNullOrEmpty(syllable.prevV);
            bool hasV = !string.IsNullOrEmpty(syllable.v);

            if (hasPrevV) currentPhonemes.Add(syllable.prevV);
            currentPhonemes.AddRange(syllable.cc);
            if (hasV) currentPhonemes.Add(syllable.v);

            List<string> finalPhonemes = new List<string>();
            int idx = 0;
            while (idx < currentPhonemes.Count) {
                bool replaced = false;
                foreach (var rule in mergingReplacements.Concat(splittingReplacements).Where(r => r.where == "all" || (hasPrevV && syllable.position == 0 && r.where == "boundary"))) {
                    if (rule.from is string[] fromArray && idx + fromArray.Length <= currentPhonemes.Count) {
                        bool match = true;
                        for (int j = 0; j < fromArray.Length; j++) {
                            if (currentPhonemes[idx + j] != fromArray[j]) {
                                match = false;
                                break;
                            }
                        }
                        if (match) {
                            if (rule.to is string toString) {
                                finalPhonemes.Add(toString);
                            } else if (rule.to is string[] toArray) {
                                finalPhonemes.AddRange(toArray);
                            }
                            idx += fromArray.Length;
                            replaced = true;
                            break;
                        }
                    }
                }
                
                if (!replaced && splittingReplacements.Any()) {
                    string currentPhoneme = currentPhonemes[idx];
                    bool singleReplaced = false;
                    foreach (var rule in splittingReplacements.Where(r => r.where == "all" || (hasPrevV && syllable.position == 0 && r.where == "boundary"))) {
                        if (rule.from.ToString() == currentPhoneme && rule.to is string[] toArray) {
                            finalPhonemes.AddRange(toArray);
                            singleReplaced = true;
                            break;
                        }
                    }
                    if (!singleReplaced) {
                        finalPhonemes.Add(ReplacePhoneme(currentPhonemes[idx], syllable.tone));
                    }
                    idx++;
                } else if (!replaced) {
                    finalPhonemes.Add(ReplacePhoneme(currentPhonemes[idx], syllable.tone));
                    idx++;
                }
            }

            string newPrevV = "";
            string newV = "";
            List<string> newCc = new List<string>();

            if (finalPhonemes.Count > 0) {
                if (hasPrevV) {
                    newPrevV = finalPhonemes[0];
                    finalPhonemes.RemoveAt(0);
                }
                if (hasV && finalPhonemes.Count > 0) {
                    newV = finalPhonemes.Last();
                    finalPhonemes.RemoveAt(finalPhonemes.Count - 1);
                }
                newCc.AddRange(finalPhonemes);
            }
            
            var prevV = string.IsNullOrEmpty(newPrevV) ? "" : newPrevV;
            string[] cc = newCc.ToArray();
            string v = newV;
            List<string> vowels = new List<string> { v };
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            string[] CurrentWordCc = syllable.CurrentWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string[] PreviousWordCc = syllable.PreviousWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            int prevWordConsonantsCount = syllable.prevWordConsonantsCount;

            // Check for missing vowel phonemes
            foreach (var entry in missingVphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Key, syllable.tone)) {
                    isMissingVPhonemes = true;
                    break;
                }
            }

            // Check for missing consonant phonemes
            foreach (var entry in missingCphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Value, syllable.tone)) {
                    isMissingCPhonemes = true;
                    break;
                }
            }

            // Check for missing TIMIT phonemes
            foreach (var entry in timitphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Value, syllable.tone)) {
                    isTimitPhonemes = true;
                    break;
                }
            }

            // STARTING V
            if (syllable.IsStartingV) {
                // TRIES - V THEN -V AND SO ON
                basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
            }
            // [V V] or [V C][- C/C][V]/[V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    basePhoneme = $"{prevV} {v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) && !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone) && DiphthongExceptions.ContainsKey(prevV)) {
                        // VV IS NOT PRESENT, CHECKS DiphthongExceptions LOGIC
                        var vc = $"{prevV} {DiphthongExceptions[prevV]}";
                        if (!HasOto(vc, syllable.vowelTone) && !HasOto(ValidateAlias(vc), syllable.vowelTone)) {
                            vc = AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", syllable.vowelTone, "");
                        }
                        TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc));
                        basePhoneme = AliasFormat(v, "vv", syllable.vowelTone, "");
                    } else {
                        {
                            if (!HasOto($"{prevV} {v}", syllable.vowelTone) || !HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone)) {
                                basePhoneme = AliasFormat(v, "vv", syllable.vowelTone, "");
                            } else {
                                basePhoneme = AliasFormat(v, "vv", syllable.vowelTone, "");
                            }
                        }
                    }
                } else {
                    basePhoneme = null;
                }

            } else if (syllable.IsStartingCVWithOneConsonant) {
                /// [- C/-C/C]
                if (cc.Length == 1 && cc[0].Contains("vtrash")) {
                    basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
                } else {
                    basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.tone, ""));
                }

            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                //// CCV with multiple starting consonants with cc's support
                basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                // TRY RCC [- CC] [-CC] [CC]
                for (var i = cc.Length; i > 1; i--) {
                    if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.tone, ""))) {
                        firstC = i - 1;
                    }
                    break;
                }
                // [- C] [-C] [C]
                if (phonemes.Count == 0) {
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.tone, ""));
                }
                /// VCV PART (not starting but in the middle phrase)
            } else {
                // [V] to [-V] to [- V]
                basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                // try [V C], [V CC], [VC C], [V -][- C]
                for (var i = lastC + 1; i >= 0; i--) {
                    var vr = $"_{prevV}";
                    var vr1 = $"{prevV}-";
                    var vc = $"{prevV} {cc[0]}";
                    bool CCV = false;
                    if (syllable.CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                        if (HasOto($"{string.Join("", cc)}", syllable.vowelTone) || HasOto($"- {string.Join("", cc)}", syllable.vowelTone) || HasOto($"-{string.Join("", cc)}", syllable.vowelTone)) {
                            CCV = true;
                        }
                    }
                    if (i == 0 && !HasOto(vc, syllable.tone)) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                        /// use vowel ending
                    } else if (DiphthongExceptions.ContainsKey(prevV) && ((HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone) || (HasOto(vr1, syllable.tone) || HasOto(ValidateAlias(vr1), syllable.tone)) && !HasOto(vc, syllable.tone)))) {
                        TryAddPhoneme(phonemes, syllable.vowelTone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", syllable.vowelTone, ""));
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                        /// use consonants for diphthongs if the vb doesn't have vowel endings
                    } else if (DiphthongExceptions.ContainsKey(prevV) && (!(HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone) || (HasOto(vr1, syllable.tone) || HasOto(ValidateAlias(vr1), syllable.tone)) && !HasOto(vc, syllable.tone)))) {
                        TryAddPhoneme(phonemes, syllable.vowelTone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", syllable.vowelTone, ""));
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));

                        break;
                    } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                        if (cc.Length >= 2) {
                            if (cc[1] == "y" || cc[1] == "w") {
                                TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                            } else {
                                TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc));
                                if (liquid.Contains(cc[0]) || semivowel.Contains(cc[0]) || nasal.Contains(cc[0])) {
                                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                                }
                            }
                        } else if (cc.Length == 1) {
                            TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        }
                        break;
                    } else if (CCV) {
                        //TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc));
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc)}", "cc", syllable.tone, ""));
                        firstC = 1;
                        break;
                    } else {
                        continue;
                    }
                }
            }

            for (var i = firstC; i < lastC; i++) {
                var cc1 = $"{cc.Skip(i)}";
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // [C1][C2]
                if (!HasOto(cc1, syllable.tone) || !HasOto(ValidateAlias(cc1), syllable.tone)) {
                    cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // CCV
                if (syllable.CurrentWordCc.Length >= 2) {
                    if ((HasOto($"{v}", syllable.vowelTone) || HasOto($"- {v}", syllable.vowelTone) || HasOto($"-{v}", syllable.vowelTone)) && HasOto(AliasFormat($"{string.Join("", cc.Skip(i + 1))}", "cc", syllable.tone, ""), syllable.vowelTone)) {
                        basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                        lastC = i;
                    } else if ((HasOto(AliasFormat(v, "cv", syllable.tone, ""), syllable.vowelTone)) && HasOto(cc1, syllable.vowelTone)) {
                        basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                    }
                    // [C2C3]
                    if (HasOto($"_{cc[i + 1]}", syllable.vowelTone) && !CurrentWordCc.Contains("I") && !CurrentWordCc.Contains("U")) {
                        cc1 = $"_{cc[i + 1]}";
                    }
                    // CV
                } else if (syllable.CurrentWordCc.Length == 1 && syllable.PreviousWordCc.Length == 1) {
                    basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                    // [C1] [C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");

                    }
                }
                if (i + 1 < lastC) {
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // [C1][C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // CCV
                    if (syllable.CurrentWordCc.Length >= 2) {
                        if ((HasOto($"{v}", syllable.vowelTone) || HasOto($"- {v}", syllable.vowelTone) || HasOto($"-{v}", syllable.vowelTone)) && HasOto(AliasFormat($"{string.Join("", cc.Skip(i + 1))}", "cc", syllable.tone, ""), syllable.vowelTone)) {
                            basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                            lastC = i;
                        } else if ((HasOto(AliasFormat(v, "cv", syllable.tone, ""), syllable.vowelTone)) && HasOto(cc1, syllable.vowelTone)) {
                            basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                        }
                        // [C2C3]
                        if (HasOto($"_{cc[i + 1]}", syllable.vowelTone) && !CurrentWordCc.Contains("I") && !CurrentWordCc.Contains("U")) {
                            cc1 = $"_{cc[i + 1]}";
                        }
                        // CV
                    } else if (syllable.CurrentWordCc.Length == 1 && syllable.PreviousWordCc.Length == 1) {
                        basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                        // [C1] [C2]
                        if (!HasOto(cc1, syllable.tone)) {
                            cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");

                        }
                    }
                    if (TryAddPhoneme(phonemes, syllable.tone, $"{cc[i]}{cc[i + 1]}{cc[i + 2]} -")) {
                        // if it exists, use [C1][C2][C3] -
                        i += 2;
                    } else if (HasOto(cc1, syllable.tone) && HasOto(cc1, syllable.tone)) {
                        // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // like [V C1] [C1 C2] [C2 ..]
                    }
                } else {
                    TryAddPhoneme(phonemes, syllable.tone, cc1);
                }
            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }

        protected override List<string> ProcessEnding(Ending ending) {
            string prevV = ReplacePhoneme(ending.prevV, ending.tone);
            string[] cc = ending.cc.Select(c => ReplacePhoneme(c, ending.tone)).ToArray();
            string v = ReplacePhoneme(ending.prevV, ending.tone);
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            if (tails.Contains(ending.prevV)) {
                return new List<string>();
            }
            if (ending.IsEndingV) {
                var vR = $"{v} -";
                var vR1 = $"{v} R";
                var vR2 = $"{v}-";
                var endV = AliasFormat(v, "ending", ending.tone, "");
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR), ending.tone) || (HasOto(vR1, ending.tone) || HasOto(ValidateAlias(vR1), ending.tone) || (HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)))) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat(v, "ending", ending.tone, ""));
                    /// split diphthong vowels
                } else if (DiphthongExceptions.ContainsKey(prevV) && !(HasOto(vR, ending.tone) && HasOto(ValidateAlias(vR), ending.tone) && (HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)))) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{DiphthongExceptions[prevV]}", "cv", ending.tone, ""));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{v} {cc[0]}";
                var vcr = $"{v} {cc[0]}-";
                var vcr2 = $"{v}{cc[0]} -";
                var vr = $"_{v}";
                var vr1 = $"{v}-";
                if (!RomajiException.Contains(cc[0])) {
                    if (HasOto(vcr, ending.tone) || HasOto(ValidateAlias(vcr), ending.tone)) {
                        TryAddPhoneme(phonemes, ending.tone, vcr);
                    } else if (!HasOto(vcr, ending.tone) && !HasOto(ValidateAlias(vcr), ending.tone) && (HasOto(vcr2, ending.tone) || HasOto(ValidateAlias(vcr2), ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, vcr2);
                        // double the consonants if has [C -]/[C-]
                    } else if (DiphthongExceptions.ContainsKey(prevV) && c_cR.Contains(cc.Last()) && (HasOto(AliasFormat(v, "ending_mix", ending.tone, ""), ending.tone) || HasOto($"{c_cR[0]} -", ending.tone) || HasOto($"{c_cR[0]}-", ending.tone))) {
                        // ex: [ow][ow-][z][z -]
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", ending.tone, ""));
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                    } else if (DiphthongExceptions.ContainsKey(prevV) && ((HasOto(AliasFormat(v, "ending_mix", ending.tone, ""), ending.tone)) && !HasOto(vc, ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", ending.tone, ""));
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                        /// use consonants for diphthongs if the vb doesn't have vowel endings
                    } else if (DiphthongExceptions.ContainsKey(prevV) && (!(HasOto(AliasFormat(v, "ending_mix", ending.tone, ""), ending.tone) && !HasOto(vc, ending.tone)))) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", ending.tone, ""));
                        if (c_cR.Contains(cc.Last())) {
                            if (HasOto(AliasFormat($"{c_cR[0]}", "cc_mix", ending.tone, ""), ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                            } else if (!(HasOto(AliasFormat($"{c_cR[0]}", "cc_mix", ending.tone, ""), ending.tone))) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                            } else {
                                TryAddPhoneme(phonemes, ending.tone, $"{cc[0]} -", $"{cc[0]}-");
                            }
                        } else if (!c_cR.Contains(cc.Last())) {
                            if (HasOto(AliasFormat($"{c_cR[0]}", "cc_mix", ending.tone, ""), ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                            } else if (!(HasOto(AliasFormat($"{c_cR[0]}", "cc_mix", ending.tone, ""), ending.tone))) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                            } else {
                                TryAddPhoneme(phonemes, ending.tone, $"{cc[0]} -", $"{cc[0]}-");
                            }
                        }
                        /// add additional c to those consonants on the top
                    } else if (c_cR.Contains(cc.Last())) {
                        if (HasOto(vc, ending.tone) || HasOto(ValidateAlias(vc), ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, vc);
                            //TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                        } else if (HasOto($"{c_cR[0]} -", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]} -"), ending.tone) || (HasOto($"{c_cR[0]}-", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]}-"), ending.tone))) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc1_mix", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                        } else if (!(HasOto($"{c_cR[0]} -", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]} -"), ending.tone) || (HasOto($"{c_cR[0]}-", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]}-"), ending.tone)))) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[0]} -", $"{cc[0]}-");
                        }
                    } else {
                        TryAddPhoneme(phonemes, ending.tone, vc);
                        if (vc.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[0]} -", $"{cc[0]}-");
                        }
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr = $"_{v}";
                    var vr1 = $"{v}-";
                    var vcc = $"{v} {string.Join("", cc.Take(2))}-";
                    var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} -";
                    var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                    var vcc4 = $"{v} {string.Join("", cc.Take(2))}";
                    var vc = $"{v} {cc[0]}";
                    if (!RomajiException.Contains(cc[0])) {
                        if (i == 0) {
                            if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) && !HasOto(vc, ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, vr);
                            }
                            break;
                        } else if ((HasOto(vcc, ending.tone) || HasOto(ValidateAlias(vcc), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc);
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc2, ending.tone) || HasOto(ValidateAlias(vcc2), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc2);
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc3, ending.tone) || HasOto(ValidateAlias(vcc3), ending.tone) && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc3);
                            if (vcc3.EndsWith(cc.Last()) && lastC == 1) {
                                if (consonants.Contains(cc.Last())) {
                                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                                }
                            }
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc4, ending.tone) || HasOto(ValidateAlias(vcc4), ending.tone) && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc4);
                            if (vcc4.EndsWith(cc.Last()) && lastC == 1) {
                                if (consonants.Contains(cc.Last())) {
                                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "cc_mix", ending.tone, ""));
                                }
                            }
                            firstC = 1;
                            break;
                        } else if (DiphthongExceptions.ContainsKey(prevV) && (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone)) || (HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone)) && !HasOto(vc, ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, vr1, vr);
                            break;
                            /// use consonants for diphthongs if the vb doesn't have vowel endings
                        } else if (DiphthongExceptions.ContainsKey(prevV) && (!(HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) || (HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone)) && !HasOto(vc, ending.tone)))) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{DiphthongExceptions[prevV]}", "diph_mix", ending.tone, ""));
                            break;
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, vc);
                            break;
                        }
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    var cc1 = $"{cc[i]}";
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]}";
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // CC FALLBACKS
                        if (!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1] [C2]
                            cc1 = $"{cc[i + 1]}";
                        } else if (!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone) && !HasOto($"{cc[i + 1]}", ending.tone)) {
                            // [- C1] [- C2]
                            cc1 = $"- {cc[i + 1]}";
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (HasOto(cc1, ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            // like [C1 C2][C2 ...]
                            phonemes.Add(cc1);
                        } else if ((HasOto(cc[i], ending.tone) || HasOto(ValidateAlias(cc[i]), ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone)))) {
                            // like [C1 C2-][C3 ...]
                            phonemes.Add(cc[i]);
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} {cc[i + 2]}-", ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"))) {
                            // like [C1 C2-][C3 ...]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else {
                            // like [C1][C2 ...]
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc1_mix", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc1_mix", ending.tone, ""));
                            i++;
                        }
                    } else {
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // CC FALLBACKS
                        if (!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone)) {
                            // [C1] [C2]
                            cc1 = AliasFormat($"{cc[i + 1]}", "cc_end", ending.tone, "");
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // CC FALLBACKS
                        if (!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1] [C2]
                            cc1 = AliasFormat($"{cc[i + 1]}", "cc1_mix", ending.tone, ""); ;
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-"))) {
                            // like [C1 C2-]
                            i++;
                        } else if (c_cR.Contains(cc.Last())) {
                            if (HasOto($"{c_cR[0]} -", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]} -"), ending.tone) || (HasOto($"{c_cR[0]}-", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]}-"), ending.tone))) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc1_mix", ending.tone, ""));
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc1_mix", ending.tone, ""));
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_mix", ending.tone, ""));
                                i++;
                            } else if (!(HasOto($"{c_cR[0]} -", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]} -"), ending.tone) || (HasOto($"{c_cR[0]}-", ending.tone) || HasOto(ValidateAlias($"{c_cR[0]}-"), ending.tone)))) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc1_mix", ending.tone, ""));
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_mix", ending.tone, ""));
                                i++;
                            }
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            // like [C1 C2][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_mix", ending.tone, ""));
                            i++;

                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1 -] [- C2]
                            TryAddPhoneme(phonemes, ending.tone, $"- {cc[i + 1]}", ValidateAlias($"- {cc[i + 1]}"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            phonemes.Add($"{cc[i]} -");
                            i++;
                        }

                    }
                }
            }
            return phonemes;
        }
        private string AliasFormat(string alias, string type, int tone, string prevV) {
            var aliasFormats = new Dictionary<string, string[]> {
                // Define alias formats for different types
                { "startingV", new string[] { "-", "- ", "_", "" } },
                { "vv", new string[] { "-", "", "_", "- " } },
                { "vvExtend", new string[] { "", "_", "-", "- " } },
                { "cv", new string[] { "-", "", "- ", "_" } },
                { "ending", new string[] { " -", "-", " R" } },
                { "ending_mix", new string[] { "-", " -", "R", " R", "_", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-", "", "_" } },
                { "cc_end", new string[] { " -", "-", "" } },
                { "cc_mix", new string[] { " -", " R", "-", "", "_", "- ", "-" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
                { "diph_mix", new string[] { "-", "_", "", " -", "", "_", "- ", "-" } },

            };

            // Check if the given type exists in the aliasFormats dictionary
            if (!aliasFormats.ContainsKey(type)) {
                return alias;
            }
            // Get the array of possible alias formats for the specified type
            var formatsToTry = aliasFormats[type];
            int counter = 0;
            foreach (var format in formatsToTry) {
                string aliasFormat;
                if (type.Contains("mix") && counter < 4) {
                    // Alternate between alias + format and format + alias for the first 4 iterations
                    aliasFormat = (counter % 2 == 0) ? alias + format : format + alias;
                    counter++;
                } else if (type.Contains("end")) {
                    aliasFormat = alias + format;
                } else {
                    aliasFormat = format + alias;
                }
                // Check if the formatted alias exists using HasOto and ValidateAlias
                if (HasOto(aliasFormat, tone)) {
                    alias = aliasFormat;
                    return alias;
                } else if (HasOto(ValidateAlias(aliasFormat), tone)) {
                    alias = aliasFormat;
                    return ValidateAlias(alias);
                }
            }
            return alias;
        }

        protected override string ValidateAlias(string alias, int tone = 0) {

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (HasOto(alias, tone)) return alias;

            string baseResolved = base.ValidateAlias(alias, tone);
            if (!string.IsNullOrEmpty(baseResolved) && baseResolved != alias) {
                if (HasOto(baseResolved, tone)) {
                    return baseResolved;
                }
                alias = baseResolved;
            }
            if (isTimitPhonemes) {
                foreach (var fb in timitphonemes.OrderByDescending(f => f.Key.Length)) {
                    alias =  alias.Replace(fb.Key, fb.Value);
                }
            }
            if (isMissingVPhonemes) {
                foreach (var fb in missingVphonemes.OrderByDescending(f => f.Key.Length)) {
                    alias = alias.Replace(fb.Key, fb.Value);
                }
            }
            if (isMissingCPhonemes) {
                foreach (var fb in missingCphonemes.OrderByDescending(f => f.Key.Length)) {
                    alias = alias.Replace(fb.Key, fb.Value);
                }
            }
            return alias;

        }

        bool PhonemeIsPresent(string alias, string phoneme) {
            if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(phoneme))
                return false;

            // Exact token match
            if (alias == phoneme)
                return true;

            return alias.EndsWith(phoneme);
        }
        
        protected override bool NoGap => true;

        private bool IsEndingAlias(string alias) {
            if (string.IsNullOrEmpty(alias)) return false;
            string trimmed = alias.Trim();
            if (trimmed.EndsWith("-") || trimmed.EndsWith("R")) return true;
            if (tails != null && tails.Any(t => !string.IsNullOrEmpty(t) && (trimmed.EndsWith(t) || trimmed.EndsWith($" {t}")))) return true;
            return false;
        }

        protected override double GetTransitionMultiplier(string alias) {
            double baseMultiplier = base.GetTransitionMultiplier(alias);

            if (IsEndingAlias(alias)) {
                return 1.0;
            }

            if (baseMultiplier != 1.0) {
                return baseMultiplier;
            }

            var fricative_def = 1.8;
            var aspirate_def = 1.2;
            var semivowel_def = 1.2;
            var liquid_def = 1.2;
            var nasal_def = 1.3;
            var stop_def = 1.3;
            var tap_def = 0.5;
            var affricate_def = 1.3;

            var sortedOverrides = PhonemeOverrides.OrderByDescending(kv => kv.Key.Length);
            foreach (var kvp in sortedOverrides) {
                var symbol = kvp.Key;
                var value = kvp.Value;

                if (IsEndingAlias(alias) && symbol != alias) {
                    continue;
                }

                if (Regex.IsMatch(alias, $@"(?<![a-zA-Z]){Regex.Escape(symbol)}(?![a-zA-Z])")) {
                    return baseMultiplier * value;
                }
            }

            foreach (var c in fricative) {
                if (PhonemeIsPresent(alias, c)) return fricative_def;
            }
            foreach (var c in aspirate) {
                if (PhonemeIsPresent(alias, c)) return aspirate_def;
            }
            foreach (var c in semivowel) {
                if (PhonemeIsPresent(alias, c)) return semivowel_def;
            }
            foreach (var c in liquid) {
                if (PhonemeIsPresent(alias, c)) return liquid_def;
            }
            foreach (var c in nasal) {
                if (PhonemeIsPresent(alias, c)) return nasal_def;
            }
            foreach (var c in stop) {
                if (PhonemeIsPresent(alias, c)) return stop_def;
            }
            foreach (var c in tap) {
                if (PhonemeIsPresent(alias, c)) return tap_def;
            }
            foreach (var c in affricate) {
                if (PhonemeIsPresent(alias, c)) return affricate_def;
            }

            return 1.0;
        }
    }
}

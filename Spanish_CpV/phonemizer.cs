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
    public class SpanishCpVPhonemizer : EnglishCpVPhonemizer {
        protected override string YamlFileName => "es-cPv.yaml";
        protected override byte[] YamlTemplate => Spanish_CpV.data.Resources.template;
        protected override string YamlVersion => "1.1";
        public SpanishCpVPhonemizer() {
            this.vowels = new string[] {"a", "e", "i", "o", "u" };
            this.consonants = Array.Empty<string>();
        }
        private static string[] c_cR = { "n" };
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        private Dictionary<string, string> dictionaryReplacements;
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

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

        private readonly string[] ccvException = { "ch", "dh", "dx", "fh", "gh", "hh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };
        private readonly string[] RomajiException = { "a", "e", "i", "o", "u" };

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (original == null) {
                return null;
            }
            List<string> finalProcessedPhonemes = new List<string>();
            
            foreach (string s in original) {
                switch (s) {
                    default:
                        finalProcessedPhonemes.Add(s);
                        break;
                }
            }
            return finalProcessedPhonemes.ToArray();
        }

        protected override IG2p[] GetBaseG2ps() {
            return new IG2p[] { new SpanishG2p() };
        }

        protected override List<string> ProcessSyllable(Syllable syllable) {
            syllable.prevV = tails.Contains(syllable.prevV) ? "" : syllable.prevV;
            var replacedPrevV = ReplacePhoneme(syllable.prevV, syllable.tone);
            var prevV = string.IsNullOrEmpty(replacedPrevV) ? "" : replacedPrevV;
            string[] cc = syllable.cc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string v = ReplacePhoneme(syllable.v, syllable.vowelTone);
            List<string> vowels = new List<string> { v };
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            string[] CurrentWordCc = syllable.CurrentWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string[] PreviousWordCc = syllable.PreviousWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            int prevWordConsonantsCount = syllable.prevWordConsonantsCount;

            // Check for missing consonant phonemes
            foreach (var entry in missingCphonemes) {
                if (!HasOto(entry.Key, syllable.tone) && !HasOto(entry.Value, syllable.tone)) {
                    isMissingCPhonemes = true;
                    break;
                }
            }

            // STARTING V
            if (syllable.IsStartingV) {
                basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
            }
            // [V V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    if (HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}", syllable.vowelTone), syllable.vowelTone)) {
                        basePhoneme = $"{prevV} {v}";
                    } 
                    else if (diphthongSplits.ContainsKey(prevV) || diphthongTails.ContainsKey(prevV)) {
                        string cv = "";

                        if (diphthongSplits.ContainsKey(prevV)) {
                            var splitOverride = diphthongSplits[prevV];
                            var vc = AliasFormat(splitOverride[0].Replace("{v}", v), "vcEx", syllable.tone, prevV);
                            cv = AliasFormat(splitOverride[1].Replace("{v}", v), "vv", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc, syllable.tone));
                        } 
                        else {
                            var tail = diphthongTails[prevV];
                            var vcSpace = $"{prevV} {tail}";
                            var vcNoSpace = $"{prevV}{tail}";
                            var vcMix = AliasFormat(tail, "diph_mix", syllable.vowelTone, "");

                            if (HasOto(vcSpace, syllable.vowelTone) || HasOto(ValidateAlias(vcSpace, syllable.vowelTone), syllable.vowelTone)) {
                                TryAddPhoneme(phonemes, syllable.tone, vcSpace, ValidateAlias(vcSpace, syllable.vowelTone));
                            } else if (HasOto(vcNoSpace, syllable.vowelTone) || HasOto(ValidateAlias(vcNoSpace, syllable.vowelTone), syllable.vowelTone)) {
                                TryAddPhoneme(phonemes, syllable.tone, vcNoSpace, ValidateAlias(vcNoSpace, syllable.vowelTone));
                            } else {
                                TryAddPhoneme(phonemes, syllable.tone, vcMix, ValidateAlias(vcMix, syllable.vowelTone));
                            }
                            cv = AliasFormat(v, "vv", syllable.vowelTone, "");
                        }

                        if (HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                            basePhoneme = cv;
                        } else if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v, syllable.vowelTone), syllable.vowelTone)) {
                            basePhoneme = v;
                        } else {
                            basePhoneme = AliasFormat(v, "vv", syllable.vowelTone, "");
                        }
                    } 
                    else {
                        if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v, syllable.vowelTone), syllable.vowelTone)) {
                            basePhoneme = v;
                        } else {
                            basePhoneme = AliasFormat(v, "vv", syllable.vowelTone, "");
                        }
                    }
                } 
                else {
                    basePhoneme = null;
                }
            } else if (syllable.IsStartingCVWithOneConsonant) {
                basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.tone, ""));

            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                for (var i = cc.Length; i > 1; i--) {
                    if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.tone, ""))) {
                        firstC = i - 1;
                        break;
                    }
                }
                if (phonemes.Count == 0) {
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.tone, ""));
                }
            } else { // VCV PART
                basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");

                string[] glideConsonants = { "w", "y", "I", "U" };

                bool cc0IsLiquid = cc.Length >= 1 && (
                    liquid.Contains(cc[0]) || 
                    liquid.Contains(ValidateAlias(cc[0], syllable.tone)));

                bool cc1IsSemivowel = cc.Length >= 2 && (
                    semivowel.Contains(cc[1]) || 
                    semivowel.Contains(ValidateAlias(cc[1], syllable.tone)));

                bool nextIsGlide = cc.Length >= 2 && (
                    glideConsonants.Contains(cc[1]) || 
                    glideConsonants.Contains(ValidateAlias(cc[1], syllable.tone)) || 
                    cc1IsSemivowel);

                for (var i = lastC + 1; i >= 0; i--) {
                    if (cc.Length == 0) break; // Guard against 0-consonant syllables

                    var vr = $"_{prevV}";
                    var vr1 = $"{prevV}-";
                    var vc = $"{prevV} {cc[0]}";
                    bool CCV = false;

                    if (syllable.CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                        if (HasOto($"{string.Join("", cc)}", syllable.vowelTone) || 
                            HasOto($"-{string.Join("", cc)}", syllable.vowelTone) || 
                            HasOto($"- {string.Join("", cc)}", syllable.vowelTone)) {
                            CCV = true;
                        }
                    }

                    if (i == 0 && !HasOto(vc, syllable.tone)) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                    } 
                    else if (cc.Length >= 2 && cc0IsLiquid && cc1IsSemivowel) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                    } 
                    else if (cc.Length >= 2 && (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc, syllable.tone), syllable.tone))) {
                        TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc, syllable.tone));
                        break;
                    }
                    else if (cc.Length >= 2 && nextIsGlide) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                    }
                    // 3. Diphthong tails handling
                    else if (diphthongTails.ContainsKey(prevV)) {
                        TryAddPhoneme(phonemes, syllable.vowelTone, AliasFormat($"{diphthongTails[prevV]}", "diph_mix", syllable.vowelTone, ""));
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc", syllable.tone, ""));
                        break;
                    } 
                    else if (CCV) {
                        TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc, syllable.tone));
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc)}", "cc", syllable.tone, ""));
                        firstC = 1;
                        break;
                    } 
                    else {
                        continue;
                    }
                }
            }

            // CC Transition Loop
            for (var i = firstC; i < lastC; i++) {
                var cc1 = $"{string.Join("", cc.Skip(i))}";
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }
                if (!HasOto(cc1, syllable.tone) || !HasOto(ValidateAlias(cc1, syllable.tone), syllable.tone)) {
                    cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }

                // CCV Resolution
                if (syllable.CurrentWordCc.Length >= 2) {
                    if ((HasOto($"{v}", syllable.vowelTone) || HasOto($"_{v}", syllable.vowelTone) || HasOto($"-{v}", syllable.vowelTone)) && HasOto(AliasFormat($"{string.Join("", cc.Skip(i + 1))}", "cc", syllable.tone, ""), syllable.vowelTone)) {
                        basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                        lastC = i;
                    } else if ((HasOto(AliasFormat(v, "cv", syllable.tone, ""), syllable.vowelTone)) && HasOto(cc1, syllable.vowelTone)) {
                        basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                    }

                    string[] glideVowels = { "y", "w", "U", "I" };
                    bool skipUnderscoreC = glideVowels.Contains(cc[i]) 
                        || glideVowels.Contains(ValidateAlias(cc[i], syllable.tone));

                    bool foundUnderscoreC = false;

                    if (!skipUnderscoreC && (HasOto(AliasFormat($"_{cc[i + 1]}", "cc", syllable.tone, ""), syllable.tone)
                        || HasOto(ValidateAlias(AliasFormat($"_{cc[i + 1]}", "cc", syllable.tone, ""), syllable.tone), syllable.tone))) {
                        cc1 = AliasFormat($"_{cc[i + 1]}", "cc", syllable.tone, "");
                        foundUnderscoreC = true;
                    }

                    if (!foundUnderscoreC && HasOto(AliasFormat($"{string.Join("", cc.Skip(i + 1))}", "cc", syllable.tone, ""), syllable.vowelTone)) {
                        cc1 = AliasFormat($"{string.Join("", cc.Skip(i + 1))}", "cc", syllable.tone, "");
                    }

                    if (liquid.Contains(cc[i + 1]) || semivowel.Contains(cc[i + 1])
                        || liquid.Contains(ValidateAlias(cc[i + 1], syllable.tone)) || semivowel.Contains(ValidateAlias(cc[i + 1], syllable.tone))) {
                        glides(cc1);
                    }
                } else if (syllable.CurrentWordCc.Length == 1 && syllable.PreviousWordCc.Length == 1) {
                    basePhoneme = AliasFormat(v, "cv", syllable.vowelTone, "");
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");
                    }
                }

                if (i + 1 < lastC) {
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = AliasFormat($"{cc[i + 1]}", "cc", syllable.tone, "");
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }

                    // Safe 3-consonant cluster check
                    if (i + 2 < cc.Length && TryAddPhoneme(phonemes, syllable.tone, $"{cc[i]}{cc[i + 1]}{cc[i + 2]} -")) {
                        i += 2;
                    } else if (HasOto(cc1, syllable.tone)) {
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // added cc1
                    }
                } else {
                    TryAddPhoneme(phonemes, syllable.tone, cc1);
                }
            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }

        private string AliasFormat(string alias, string type, int tone, string prevV, string t = "-") {
            var aliasFormats = new Dictionary<string, string[]> {
                // Define alias formats for different types
                { "startingV", new string[] { "-", "- ", "_", "" } },
                { "vv", new string[] { "-", "", "_", "- " } },
                { "vvExtend", new string[] { "", "_", "-", "- " } },
                { "cv", new string[] { "-", "", "*", "_" } },
                { "ending", new string[] { $" {t}", $"{t}" } },
                { "ending_mix", new string[] { $"{t}", $" {t}", "_", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-", "", "_" } },
                { "cc_end", new string[] { $" {t}", $"{t}", "" } },
                { "cc_mix", new string[] { $" {t}", $" {t}", $"{t}", "", "_", "- ", "-" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
                { "diph_mix", new string[] { $"{t}", "_", "", $" {t}", "", "_", "- ", "-" } },

            };

            if (!aliasFormats.ContainsKey(type) && !type.Contains("dynamic")) {
                return alias;
            }

            if (type.Contains("dynStart")) {
                string consonant = "";
                string vowel = "";
                // If the alias contains a space, split it into consonant and vowel
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }
                var dynamicVariations = new List<string> {
                    $"- {consonant}{vowel}",        // "- CV"
                    $"- {consonant} {vowel}",       // "- C V"
                    $"-{consonant} {vowel}",        // "-C V"
                    $"-{consonant}{vowel}",         // "-CV"
                    $"-{consonant}_{vowel}",        // "-C_V"
                    $"- {consonant}_{vowel}",       // "- C_V"
                };

                foreach (var variation in dynamicVariations) {
                    if (HasOto(variation, tone)) {
                        return variation;
                    } else if (HasOto(ValidateAlias(variation), tone)) {
                        return ValidateAlias(variation);
                    }
                }
            }

            if (type.Contains("dynMid")) {
                string consonant = "";
                string vowel = "";

                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }
                var dynamicVariations1 = new List<string> {
                    $"{consonant}{vowel}",    // "CV"
                    $"{consonant} {vowel}",    // "C V"
                    $"{consonant}_{vowel}",    // "C_V"
                };

                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone)) {
                        return variation1;
                    } else if (HasOto(ValidateAlias(variation1), tone)) {
                        return ValidateAlias(variation1);
                    }
                }
            }

            if (type.Contains("dynEnd")) {
                string consonant = "";
                string vowel = "";

                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[1];
                    vowel = parts[0];
                } else {
                    consonant = alias;
                }
                var dynamicVariations1 = new List<string> {
                    $"{vowel}{consonant} -",    // "VC -"
                    $"{vowel} {consonant}-",    // "V C-"
                    $"{vowel}{consonant}-",    // "VC-"
                    $"{vowel} {consonant} -",    // "V C -"
                };

                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone)) {
                        return variation1;
                    } else if (HasOto(ValidateAlias(variation1), tone)) {
                        return ValidateAlias(variation1);
                    }
                }
            }

            // Get the array of possible alias formats for the specified type if not dynamic
            var formatsToTry = aliasFormats[type];
            int counter = 0;
            foreach (var format in formatsToTry) {
                string aliasFormat;
                if (type.Contains("mix") && counter < 4) {
                    aliasFormat = (counter % 2 == 0) ? $"{alias}{format}" : $"{format}{alias}";
                    counter++;
                } else if (type.Contains("end") || type.Contains("End") && !(type.Contains("dynEnd"))) {
                    aliasFormat = $"{alias}{format}";
                } else {
                    aliasFormat = $"{format}{alias}";
                }

                if (HasOto(aliasFormat, tone)) {
                    return aliasFormat;
                } else if (HasOto(ValidateAlias(aliasFormat), tone)) {
                    return ValidateAlias(aliasFormat);
                }
            }
            return alias;
        }
    }
}

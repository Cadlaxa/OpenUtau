using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Classic;
using OpenUtau.Api;
using OpenUtau.Classic;
using OpenUtau.Core.G2p;
using OpenUtau.Core.Ustx;
using Serilog;
using YamlDotNet.Core.Tokens;
using System.Text.RegularExpressions;
using OpenUtau.Core;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("MCCR Mandarin Chinese Phonemizer", "ZH CVVC", "Cadlaxa", language: "ZH")]
    public class MandarinPhonemizer : SyllableBasedPhonemizer {
        protected override string YamlFileName => "zh-cvvc-mccr.yaml";
        protected override string YamlVersion => "1.1.1";
        protected override byte[] YamlTemplate => ZH_CVVC_MCCR.data.Resources.template;
        public MandarinPhonemizer() {
            this.vowels = Array.Empty<string>();
            this.consonants = Array.Empty<string>();
        }
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (original == null || original.Length == 0) {
                string[] romanizedArray = BaseChinesePhonemizer.Romanize(new string[] { note.lyric });
                string lyric = romanizedArray[0].ToLowerInvariant();

                List<string> fallbackSplit = new List<string>();
                string[] vowels = GetVowels();
                string[] consonants = GetConsonants();

                int ii = 0;
                while (ii < lyric.Length) {
                    string match = null;
                    
                    // Greedily match consonants first
                    foreach (var cons in consonants.OrderByDescending(c => c.Length)) {
                        if (lyric.Substring(ii).StartsWith(cons)) {
                            match = cons;
                            break;
                        }
                    }
                    
                    // If no consonant, greedily match vowels
                    if (match == null) {
                        foreach (var vow in vowels.OrderByDescending(v => v.Length)) {
                            if (lyric.Substring(ii).StartsWith(vow)) {
                                match = vow;
                                break;
                            }
                        }
                    }
                    
                    if (match != null) {
                        fallbackSplit.Add(match);
                        ii += match.Length;
                    } else {
                        fallbackSplit.Add(lyric[ii].ToString());
                        ii++;
                    }
                }
                
                original = fallbackSplit.ToArray();
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
        protected override IG2p[] GetBaseG2ps() => Array.Empty<IG2p>();

        public static Note[] ChangeLyric(Note[] group, string lyric) {
            var oldNote = group[0];
            group[0] = new Note {
                lyric = lyric,
                phoneticHint = oldNote.phoneticHint,
                tone = oldNote.tone,
                position = oldNote.position,
                duration = oldNote.duration,
                phonemeAttributes = oldNote.phonemeAttributes,
            };
            return group;
        }

        protected virtual string[] Romanize(IEnumerable<string> lyrics) {
            return BaseChinesePhonemizer.Romanize(lyrics);
        }

        protected void RomanizeNotes(Note[][] groups) {
            if (groups == null || groups.Length == 0) {
                return;
            }
            var ResultLyrics = Romanize(groups.Select(group => group[0].lyric));
            Enumerable.Zip(groups, ResultLyrics, ChangeLyric).ToArray();
        }

        public override void SetUp(Note[][] groups, UProject project, UTrack track) {
            RomanizeNotes(groups);
            base.SetUp(groups, project, track);
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

            // STARTING V
            if (syllable.IsStartingV) {
                basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
            }
            // [V V] or [V C][C V]/[V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    
                    string vvSpace = $"{prevV} {v}";
                    string vvNoSpace = $"{prevV}{v}";
                    string validVvSpace = ValidateAlias(vvSpace, syllable.vowelTone);
                    string validVvNoSpace = ValidateAlias(vvNoSpace, syllable.vowelTone);

                    // VV with space
                    if (HasOto(vvSpace, syllable.vowelTone)) {
                        basePhoneme = vvSpace;
                    } else if (HasOto(validVvSpace, syllable.vowelTone)) {
                        basePhoneme = validVvSpace;
                    } 
                    // VV without space
                    else if (HasOto(vvNoSpace, syllable.vowelTone)) {
                        basePhoneme = vvNoSpace;
                    } else if (HasOto(validVvNoSpace, syllable.vowelTone)) {
                        basePhoneme = validVvNoSpace;
                    } 
                    
                    // Diphthong Fallbacks & Splitting
                    else if (diphthongSplits.ContainsKey(prevV) || diphthongTails.ContainsKey(prevV)) {
                        string cv = "";
                        if (diphthongSplits.ContainsKey(prevV)) {
                            var splitOverride = diphthongSplits[prevV];
                            var vc = AliasFormat(splitOverride[0].Replace("{v}", v), "vcEx", syllable.tone, prevV);
                            cv = AliasFormat(splitOverride[1].Replace("{v}", v), "dynMid", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc, syllable.tone));
                        } 
                        else { // Default YAML diphthong logic
                            var tail = diphthongTails[prevV];
                            var vcSpace = AliasFormat($"{prevV} {tail}", "vcEx", syllable.tone, prevV);
                            var vcNoSpace = AliasFormat($"{prevV}{tail}", "vcEx", syllable.tone, prevV);
                            cv = AliasFormat($"{tail} {v}", "dynMid", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, vcSpace, ValidateAlias(vcSpace, syllable.tone), vcNoSpace, ValidateAlias(vcNoSpace, syllable.tone));
                        }

                        string validCv = ValidateAlias(cv, syllable.vowelTone);
                        string validV = ValidateAlias(v, syllable.vowelTone);
                        if (HasOto(cv, syllable.vowelTone)) {
                            basePhoneme = cv;
                        } else if (HasOto(validCv, syllable.vowelTone)) {
                            basePhoneme = validCv;
                        } else if (HasOto(v, syllable.vowelTone)) {
                            basePhoneme = v;
                        } else if (HasOto(validV, syllable.vowelTone)) {
                            basePhoneme = validV;
                        } else {
                            basePhoneme = ValidateAlias(AliasFormat($"- {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone);
                            phonemes.Add(ValidateAlias(AliasFormat($"{prevV} -", "dynMid", syllable.tone, ""), syllable.tone));
                        }
                    } else {
                        string validV = ValidateAlias(v, syllable.vowelTone);
                        if (HasOto(v, syllable.vowelTone)) {
                            basePhoneme = v;
                        } else if (HasOto(validV, syllable.vowelTone)) {
                            basePhoneme = validV;
                        } else {
                            basePhoneme = ValidateAlias(AliasFormat($"- {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone);
                            phonemes.Add(ValidateAlias(AliasFormat($"{prevV} -", "dynMid", syllable.tone, ""), syllable.tone));
                        }
                    }
                } 
                else {
                    basePhoneme = null;
                }
                // [- CV/C V] or [- C][CV/C V]
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var rcv = $"- {cc[0]} {v}";
                var rcv1 = $"- {cc[0]}{v}";
                var crv = $"{cc[0]} {v}";
                /// - CV
                if (HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv, syllable.vowelTone), syllable.vowelTone) || (HasOto(rcv1, syllable.vowelTone) || HasOto(ValidateAlias(rcv1, syllable.vowelTone), syllable.vowelTone))) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynStart", syllable.vowelTone, "");
                    /// CV
                } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone)) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    bool foundStart = false;
                    for (int len = cc[0].Length; len > 0; len--) {
                        string c = cc[0].Substring(0, len); // shr -> sh -> s
                        string targetStart = AliasFormat(c, "cc_start", syllable.vowelTone, "");
                        string validStart = ValidateAlias(targetStart, syllable.vowelTone);

                        if (HasOto(targetStart, syllable.vowelTone) || HasOto(validStart, syllable.vowelTone)) {
                            TryAddPhoneme(phonemes, syllable.tone, targetStart, validStart);
                            foundStart = true;
                            break;
                        }
                    }
                    if (!foundStart) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), ValidateAlias(AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), syllable.vowelTone));
                    }
                } else {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    bool foundStart = false;
                    for (int len = cc[0].Length; len > 0; len--) {
                        string c = cc[0].Substring(0, len); // shr -> sh -> s
                        string targetStart = AliasFormat(c, "cc_start", syllable.vowelTone, "");
                        string validStart = ValidateAlias(targetStart, syllable.vowelTone);

                        if (HasOto(targetStart, syllable.vowelTone) || HasOto(validStart, syllable.vowelTone)) {
                            TryAddPhoneme(phonemes, syllable.tone, targetStart, validStart);
                            foundStart = true;
                            break;
                        }
                    }
                    if (!foundStart) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), ValidateAlias(AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), syllable.vowelTone));
                    }
                }
                // [CCV/CC V] or [C C] + [CV/C V]
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                // TRY [- CCV]/[- CC V] or [- CC][CCV]/[CC V] or [- C][C C][C V]/[CV]
                var rccv = $"- {string.Join("", cc)} {v}";
                var rccv1 = $"- {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()} {v}";
                var crv1 = $"{cc.Last()}{v}";
                var ccv = $"{string.Join("", cc)} {v}";
                var ccv1 = $"{string.Join("", cc)}{v}";
                /// - CCV
                if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv, syllable.vowelTone), syllable.vowelTone) || HasOto(rccv1, syllable.vowelTone) || HasOto(ValidateAlias(rccv1, syllable.vowelTone), syllable.vowelTone)) {
                    basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynStart", syllable.vowelTone, "");
                    lastC = 0;
                } else {
                    /// CCV and CV
                    if ((HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv, syllable.vowelTone), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1, syllable.vowelTone), syllable.vowelTone))) {
                        basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                        lastC = 0;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(crv1, syllable.vowelTone) || HasOto(ValidateAlias(crv1, syllable.vowelTone), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // TRY RCC [- CC]
                    for (var i = cc.Length; i > 1; i--) {
                        if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.vowelTone, ""), ValidateAlias(AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.vowelTone, ""), syllable.vowelTone))) {
                            firstC = i - 1;
                            break;
                        }
                        var ccc = $"{string.Join("", cc.Take(i))}";
                        if (liquid.Contains(ccc) || semivowel.Contains(ccc)
                            || liquid.Contains(ValidateAlias(ccc)) || semivowel.Contains(ValidateAlias(ccc))) {
                            glides(ccc);
                        }
                    }
                    // [- C]
                    if (phonemes.Count == 0) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), ValidateAlias(AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""), syllable.vowelTone));
                    }
                }
            } else { // VCV
                var vcv = $"{prevV} {cc[0]}{v}";
                var vcv2 = $"{prevV}{cc[0]}{v}";
                var vcvEnd = $"{prevV}{cc[0]} {v}";
                var vccv = $"{prevV} {string.Join("", cc)}{v}";
                var vccv2 = $"{prevV} {string.Join("", cc)}";
                var vccv3 = $"{prevV}{string.Join("", cc)}";
                var crv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                bool sameSubbank = AreTonesFromTheSameSubbank(syllable.tone, syllable.vowelTone);
                // Use regular VCV if the current word starts with one consonant and the previous word ends with none
                if (sameSubbank && syllable.IsVCVWithOneConsonant && (HasOto(vcv, syllable.vowelTone) && HasOto(ValidateAlias(vcv), syllable.vowelTone)) && prevWordConsonantsCount == 0 && CurrentWordCc.Length == 1) {
                    basePhoneme = vcv;
                } else if (sameSubbank && syllable.IsVCVWithOneConsonant && (HasOto(vcv2, syllable.vowelTone) && HasOto(ValidateAlias(vcv2), syllable.vowelTone)) && prevWordConsonantsCount == 0 && CurrentWordCc.Length == 1) {
                    basePhoneme = vcv2;
                    // Use end VCV if current word does not start with a consonant but the previous word does end with one
                } else if (sameSubbank && syllable.IsVCVWithOneConsonant && prevWordConsonantsCount == 1 && CurrentWordCc.Length == 0 && (HasOto(vcvEnd, syllable.vowelTone) && HasOto(ValidateAlias(vcvEnd), syllable.vowelTone))) {
                    basePhoneme = vcvEnd;
                    // Use regular VCV if end VCV does not exist
                } else if (sameSubbank && syllable.IsVCVWithOneConsonant && (!HasOto(vcvEnd, syllable.vowelTone) && !HasOto(ValidateAlias(vcvEnd), syllable.vowelTone)) && (HasOto(vcv, syllable.vowelTone) && HasOto(ValidateAlias(vcv), syllable.vowelTone))) {
                    basePhoneme = vcv;
                    // VCV with multiple consonants, only for current word onset and null previous word ending
                } else if (sameSubbank && syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccv, syllable.vowelTone) && HasOto(ValidateAlias(vccv), syllable.vowelTone)) && prevWordConsonantsCount == 0) {
                    basePhoneme = vccv;
                    lastC = 0;
                } else if (sameSubbank && syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccv3, syllable.vowelTone) && HasOto(ValidateAlias(vccv3), syllable.vowelTone))) {
                    basePhoneme = AliasFormat($"{prevV} {string.Join("", cc)}{v}", "dynMid", syllable.vowelTone, "");
                    lastC = 0;
                } else {
                    /// CV
                    if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // try [CC V] or [CCV]
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        var ccv = $"{string.Join("", cc)} {v}";
                        var ccv1 = $"{string.Join("", cc)}{v}";
                        /// CCV
                        if (CurrentWordCc.Length >= 2) {
                            if ((HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone))) {
                                basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                                lastC = i;
                                break;
                            } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            } else {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            }
                            /// C-Last
                        } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                            if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            } else {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            }
                        }
                    }

                    // try [V C], [V CC], [VC C], [V -][- C]
                    for (var i = lastC + 1; i >= 0; i--) {
                        var vcc = $"{prevV} {string.Join("", cc.Take(2))}";
                        var vc = $"{prevV} {cc[0]}";
                        var vr = AliasFormat($"{v} -", "ending", syllable.tone, "");
                        // Boolean Triggers
                        bool CCV = false;
                        if (CurrentWordCc.Length >= 2) {
                            if (HasOto(AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                                CCV = true;
                            }
                        }

                        bool lastVC = false;
                        for (int len = cc[0].Length; len > 0; len--) {
                            string c = cc[0].Substring(0, len);   // shr → sh → s
                            string vcTry = $"{prevV} {c}";

                            bool hasVC =
                                HasOto(vc, syllable.tone) ||
                                HasOto(ValidateAlias(vc, syllable.tone), syllable.tone);

                            if (!hasVC && (HasOto(vcTry, syllable.tone) || HasOto(ValidateAlias(vcTry, syllable.tone), syllable.tone))) {
                                TryAddPhoneme(phonemes, syllable.tone, vcTry, ValidateAlias(vcTry, syllable.tone));
                                lastVC = true;
                                break;
                            }
                        }
                        if (lastVC) {
                            break;
                        }

                        if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc, syllable.tone), syllable.tone)) && CCV) {
                            TryAddPhoneme(phonemes, syllable.tone, vcc, ValidateAlias(vcc, syllable.tone));
                            firstC = 1;
                            break;
                        } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc, syllable.tone), syllable.tone)) {
                            TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc, syllable.tone));
                            break;
                        } else {
                            continue;
                        }
                    }
                }
            }
            for (var i = firstC; i < lastC; i++) {
                var ccv = $"{string.Join("", cc.Skip(i + 1))} {v}";
                var ccv1 = $"{string.Join("", cc.Skip(i + 1))}{v}";
                var cc1 = $"{string.Join(" ", cc.Skip(i))}";
                var lcv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                var crv = $"{cc.Last()} {v}";

                for (int len = cc[i + 1].Length; len > 0; len--) {
                    string c = cc[i + 1].Substring(0, len);   // shr → sh → s
                    string ccTry = $"{cc[i]} {c}";

                    if (HasOto(ccTry, syllable.tone) && !(HasOto(cc1, syllable.tone) || HasOto(ValidateAlias(cc1, syllable.tone), syllable.tone))) {
                        cc1 = ccTry;
                        break;
                    }
                }

                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }
                // [C1 C2]
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]} {cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]}{cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }
                // CC FALLBACKS
                if (!HasOto(cc1, syllable.tone) || (!HasOto(ValidateAlias(cc1, syllable.tone), syllable.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone))) {
                    var c1 = cc[i];
                    var c2 = cc[i + 1];
                    bool c1IsException = consExceptions.Contains(c1);
                    bool c2IsException = consExceptions.Contains(c2);

                    // Scenario 1: Both are NOT exceptions
                    if (!c1IsException && !c2IsException) {
                        //cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                        TryAddPhoneme(phonemes, syllable.tone, ValidateAlias(AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, ""), syllable.vowelTone));
                    }
                    // Scenario 2: C1 is an exception, C2 is NOT
                    else if (c1IsException && !c2IsException) {
                        cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                    }
                    // Scenario 3: C1 is NOT an exception, C2 is
                    else if (!c1IsException && c2IsException) {
                        cc1 = AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, "");
                    }
                    // Scenario 4: Both are exceptions
                    else if (c1IsException && c2IsException) {
                        cc1 = "";
                    }
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1, syllable.tone);
                }
                // CCV
                if (CurrentWordCc.Length >= 2) {
                    bool canGlide = true;
                    if ((HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv, syllable.vowelTone), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1, syllable.vowelTone), syllable.vowelTone))) {
                        basePhoneme = (AliasFormat($"{string.Join("", cc.Skip(i + 1))} {v}", "dynMid", syllable.vowelTone, ""));
                        lastC = i;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // [C1 C2C3]
                    if ((HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone))) {
                        cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                        lastC = i;
                    }
                    if (canGlide) {
                        if (liquid.Contains(cc[i + 1]) || semivowel.Contains(cc[i + 1])
                            || liquid.Contains(ValidateAlias(cc[i + 1])) || semivowel.Contains(ValidateAlias(cc[i + 1]))) {
                            glides(cc1);
                        }
                    }
                    // CV
                } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                    if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                }

                if (i + 1 < lastC) {
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]}{cc[i + 1]}";
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }
                    // CC FALLBACKS
                    if (!HasOto(cc1, syllable.tone) || (!HasOto(ValidateAlias(cc1, syllable.tone), syllable.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone))) {
                        var c1 = cc[i];
                        var c2 = cc[i + 1];
                        bool c1IsException = consExceptions.Contains(c1);
                        bool c2IsException = consExceptions.Contains(c2);

                        // Scenario 1: Both are NOT exceptions
                        if (!c1IsException && !c2IsException) {
                            // [C1 -] [- C2]
                            //cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, ValidateAlias(AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, ""), syllable.vowelTone));
                        }
                        // Scenario 2: C1 is an exception, C2 is NOT
                        else if (c1IsException && !c2IsException) {
                            cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                        }
                        // Scenario 3: C1 is NOT an exception, C2 is
                        else if (!c1IsException && c2IsException) {
                            cc1 = AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, "");
                        }
                        // Scenario 4: Both are exceptions
                        else if (c1IsException && c2IsException) {
                            cc1 = "";
                        }
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1, syllable.tone);
                    }
                    // CCV
                    if (CurrentWordCc.Length >= 2) {
                        bool canGlide = true;
                        if ((HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv, syllable.vowelTone), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1, syllable.vowelTone), syllable.vowelTone))) {
                            basePhoneme = (AliasFormat($"{string.Join("", cc.Skip(i + 1))} {v}", "dynMid", syllable.vowelTone, ""));
                            lastC = i;
                        } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        } else {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        }
                        // [C1 C2C3]
                        if ((HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone))) {
                            cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                        }
                        if (canGlide) {
                            if (liquid.Contains(cc[i + 1]) || semivowel.Contains(cc[i + 1])
                                || liquid.Contains(ValidateAlias(cc[i + 1])) || semivowel.Contains(ValidateAlias(cc[i + 1]))) {
                                glides(cc1);
                            }
                        }
                        // CV
                    } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                        if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv, syllable.vowelTone), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv, syllable.vowelTone), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        } else {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        }
                        // [C1 C2]
                        if (!HasOto(cc1, syllable.tone)) {
                            cc1 = $"{cc[i]} {cc[i + 1]}";
                            lastC = i;
                        }
                    }
                    if (HasOto(cc1, syllable.tone) && HasOto(cc1, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1, ValidateAlias(cc1, syllable.tone))) {
                        // like [V C1] [C1 C2] [C2 ..]
                        if (cc1.Contains($"{string.Join(" ", cc.Skip(i + 1))}")) {
                            i++;
                        }
                    } else {
                        // singular cc
                        if (PreviousWordCc.Contains(cc1) == CurrentWordCc.Contains(cc1)) {
                            cc1 = ValidateAlias(cc1, syllable.tone);
                        } else {
                            TryAddPhoneme(phonemes, syllable.tone, cc1, cc[i], ValidateAlias(cc[i], syllable.tone));
                        }
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
            string t = ending.HasTail ? ReplacePhoneme(ending.tail, ending.tone) : "-";

            
            if (ending.IsEndingV) {
                var vR = $"{prevV} {t}";
                var vR2 = $"{prevV}{t}";
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR, ending.tone), ending.tone) || HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2, ending.tone), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{prevV}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{prevV}", "ending", ending.tone, "", t), ending.tone));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{prevV} {cc[0]}";
                var vcr = $"{prevV} {cc[0]}{t}";
                var vcr2 = $"{prevV}{cc[0]} {t}";
                var vcr3 = $"{prevV} {cc[0]} {t}";
                var vcr4 = $"{prevV}{cc[0]}{t}";
                if (HasOto(vcr, ending.tone) || HasOto(ValidateAlias(vcr, ending.tone), ending.tone) || (HasOto(vcr2, ending.tone) || HasOto(ValidateAlias(vcr2, ending.tone), ending.tone))) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, "", t), ValidateAlias(AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, "", t), ending.tone));
                } else if (HasOto(vcr3, ending.tone) || HasOto(ValidateAlias(vcr3, ending.tone), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, vcr3, ValidateAlias(vcr3, ending.tone));
                } else if (HasOto(vcr4, ending.tone) || HasOto(ValidateAlias(vcr4, ending.tone), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, vcr4, ValidateAlias(vcr4, ending.tone));
                } else if (HasOto(vc, ending.tone) || HasOto(ValidateAlias(vc, ending.tone), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, vc, ValidateAlias(vc, ending.tone));
                    if (vc.Contains(cc[0])) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{cc[0]}", "ending", ending.tone, "", t), ending.tone));
                    }
                } else {
                    for (int len = cc[0].Length; len > 0; len--) {
                        string c = cc[0].Substring(0, len);   // shr → sh → s
                        string vcTry = $"{prevV} {c}";
                        if ( HasOto(vcTry, ending.tone) || HasOto(ValidateAlias(vcTry, ending.tone), ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, vcTry, ValidateAlias(vcTry, ending.tone));
                            break;
                        }
                    }
                    if (vc.Contains(cc[0])) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{cc[0]}", "ending", ending.tone, "", t), ending.tone));
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr = $"{v} {t}";
                    var vr1 = $"{v} R";
                    var vr2 = $"{v}{t}";
                    var vcc = $"{v} {string.Join("", cc.Take(2))}{t}";
                    var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} {t}";
                    var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                    var vcc4 = $"{v} {string.Join("", cc.Take(2))}";
                    var vc = $"{v} {cc[0]}";
                    if (i == 0) {
                        if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr, ending.tone), ending.tone) || HasOto(vr2, ending.tone) || HasOto(ValidateAlias(vr2, ending.tone), ending.tone) || HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1, ending.tone), ending.tone) && !HasOto(vc, ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{v}", "ending", ending.tone, "", t), ending.tone));
                        }
                        break;
                    } else if (HasOto(vcc, ending.tone) || HasOto(ValidateAlias(vcc, ending.tone), ending.tone) && lastC == 1) {
                        TryAddPhoneme(phonemes, ending.tone, vcc, ValidateAlias(vcc, ending.tone));
                        firstC = 1;
                        break;
                    } else if (HasOto(vcc2, ending.tone) || HasOto(ValidateAlias(vcc2, ending.tone), ending.tone) && lastC == 1) {
                        TryAddPhoneme(phonemes, ending.tone, vcc2, ValidateAlias(vcc2, ending.tone));
                        firstC = 1;
                        break;
                    } else if ((HasOto(vcc3, ending.tone) || HasOto(ValidateAlias(vcc3, ending.tone), ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, vcc3, ValidateAlias(vcc3, ending.tone));
                        if (vcc3.EndsWith(cc.Last()) && lastC == 1) {
                            if (consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{cc.Last()}", "ending", ending.tone, "", t), ending.tone));
                            }
                        }
                        firstC = 1;
                        break;
                    } else if ((HasOto(vcc4, ending.tone) || HasOto(ValidateAlias(vcc4, ending.tone), ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, vcc4, ValidateAlias(vcc4, ending.tone));
                        if (vcc4.EndsWith(cc.Last()) && lastC == 1) {
                            if (consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, "", t), ValidateAlias(AliasFormat($"{cc.Last()}", "ending", ending.tone, "", t), ending.tone));
                            }
                        }
                        firstC = 1;
                        break;
                    } else if (!!HasOto(vcc, ending.tone) && !HasOto(ValidateAlias(vcc, ending.tone), ending.tone)
                            || !HasOto(vcc2, ending.tone) && HasOto(ValidateAlias(vcc2, ending.tone), ending.tone)
                            || !HasOto(vcc3, ending.tone) && HasOto(ValidateAlias(vcc3, ending.tone), ending.tone)
                            || !HasOto(vcc4, ending.tone) && HasOto(ValidateAlias(vcc4, ending.tone), ending.tone)) {
                        TryAddPhoneme(phonemes, ending.tone, vc, ValidateAlias(vc, ending.tone));
                        break;
                    } else {
                        for (int len = cc[0].Length; len > 0; len--) {
                            string c = cc[0].Substring(0, len);   // shr → sh → s
                            string vcTry = $"{prevV} {c}";
                            if (HasOto(vcTry, ending.tone) || HasOto(ValidateAlias(vcTry, ending.tone), ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, vcTry, ValidateAlias(vcTry, ending.tone));
                                break;
                            }
                        }
                        break;
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    int remainingCount = cc.Length - i;
                    bool matchedEndingCluster = false;

                    // ([ccc-], [c cc-], [cc c-], [cc-], [c c-])
                    if (remainingCount >= 2) {
                        for (int clusterLength = Math.Min(3, remainingCount); clusterLength >= 2; clusterLength--) {
                            if (i + clusterLength == cc.Length) {
                                var cluster = cc.Skip(i).Take(clusterLength).ToArray();
                                var patterns = new List<string> {
                                    string.Join("", cluster) // "st", "str"
                                };

                                if (clusterLength == 3) {
                                    patterns.Add($"{cluster[0]} {cluster[1]}{cluster[2]}"); // "s tr"
                                    patterns.Add($"{cluster[0]}{cluster[1]} {cluster[2]}"); // "st r"
                                    patterns.Add($"{cluster[0]} {cluster[1]} {cluster[2]}"); // "s t r"
                                } else if (clusterLength == 2) {
                                    patterns.Add($"{cluster[0]} {cluster[1]}");             // "s t"
                                }

                                string[] hyphenVariations = { $"{t}", $" {t}" }; // "-", " -"

                                foreach (var consPattern in patterns) {
                                    foreach (var hyphen in hyphenVariations) {
                                        string candidate = $"{consPattern}{hyphen}";
                                        if (TryAddPhoneme(phonemes, ending.tone, candidate, ValidateAlias(candidate, ending.tone))) {
                                            matchedEndingCluster = true;
                                            i += clusterLength - 1;
                                            break;
                                        }
                                    }
                                    if (matchedEndingCluster) break;
                                }
                                if (matchedEndingCluster) break;
                            }
                        }
                    }

                    if (matchedEndingCluster) {
                        continue;
                    }

                    // [c1 c2]
                    var cc1 = $"{cc[i]} {cc[i + 1]}";
                    for (int len = cc[i + 1].Length; len > 0; len--) {
                        string c = cc[i + 1].Substring(0, len);
                        string ccTry = $"{cc[i]} {c}";
                        if (HasOto(ccTry, ending.tone) && !(HasOto(cc1, ending.tone) || HasOto(ValidateAlias(cc1, ending.tone), ending.tone))) {
                            cc1 = ccTry;
                            break;
                        }
                    }

                    bool hasCc1 = HasOto(cc1, ending.tone) || HasOto(ValidateAlias(cc1, ending.tone), ending.tone) ||
                                HasOto($"{cc[i]} {cc[i + 1]}", ending.tone) || HasOto(ValidateAlias($"{cc[i]} {cc[i + 1]}", ending.tone), ending.tone);

                    if (i < cc.Length - 2) {
                        if (hasCc1) {
                            TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1, ending.tone), $"{cc[i]} {cc[i + 1]}", ValidateAlias($"{cc[i]} {cc[i + 1]}", ending.tone));
                        } else {
                            // No [c c] available -> c1 fallback
                            TryAddPhoneme(phonemes, ending.tone,
                                ValidateAlias(AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""), ending.tone),
                                AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""),
                                $"{cc[i]} {t}",
                                cc[i]);
                        }
                    } else {
                        // Final consonant pair
                        if (hasCc1) {
                            TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1, ending.tone), $"{cc[i]} {cc[i + 1]}", ValidateAlias($"{cc[i]} {cc[i + 1]}", ending.tone));
                            
                            // Resolve final tail closure for c2
                            TryAddPhoneme(phonemes, ending.tone,
                                ValidateAlias(AliasFormat($"{cc[i + 1]}", "ending", ending.tone, "", t), ending.tone),
                                AliasFormat($"{cc[i + 1]}", "ending", ending.tone, "", t),
                                $"{cc[i + 1]} {t}",
                                ValidateAlias($"{cc[i + 1]} {t}", ending.tone),
                                cc[i + 1]);
                        } else {
                            TryAddPhoneme(phonemes, ending.tone,
                                ValidateAlias(AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""), ending.tone),
                                AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""),
                                $"{cc[i]} {t}",
                                cc[i]);

                            TryAddPhoneme(phonemes, ending.tone,
                                ValidateAlias(AliasFormat($"{cc[i + 1]}", "ending", ending.tone, "", t), ending.tone),
                                AliasFormat($"{cc[i + 1]}", "ending", ending.tone, "", t),
                                $"{cc[i + 1]} {t}",
                                ValidateAlias($"{cc[i + 1]} {t}", ending.tone),
                                cc[i + 1]);
                        }
                    }
                }
            }
            return phonemes;
        }
        private string AliasFormat(string alias, string type, int tone, string prevV, string t = "-") {
            var aliasFormats = new Dictionary<string, string[]> {
                { "dynStart", new string[] { "" } },
                { "dynMid", new string[] { "" } },
                { "dynMid_vv", new string[] { "" } },
                { "dynEnd", new string[] { "" } },
                { "startingV", new string[] { "-", "- ", "_", "" } },
                { "vcEx", new string[] { $"{prevV} ", $"{prevV}" } },
                { "vvExtend", new string[] { "", "_", "-", "- " } },
                { "cv", new string[] { "-", "", "- ", "_" } },
                { "cvStart", new string[] { "-", "- ", "_" } },
                { "ending", new string[] { $" {t}", $"{t}"} },
                { "ending_mix", new string[] { $"{t}", $" {t}", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-", "_" } },
                { "cc_end", new string[] { $" {t}", $"{t}", "" } },
                { "cc_inB", new string[] { "_", "-", "- " } },
                { "cc_endB", new string[] { "_", $"{t}", $" {t}" } },
                { "cc_mix", new string[] { $" {t}", " R", $"{t}", "", "_", $"{t} ", $"{t}" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
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
                    } else if (HasOto(ValidateAlias(variation, tone), tone)) {
                        return ValidateAlias(variation, tone);
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
                    } else if (HasOto(ValidateAlias(variation1, tone), tone)) {
                        return ValidateAlias(variation1, tone);
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
                    } else if (HasOto(ValidateAlias(variation1, tone), tone)) {
                        return ValidateAlias(variation1, tone);
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
                } else if (HasOto(ValidateAlias(aliasFormat, tone), tone)) {
                    return ValidateAlias(aliasFormat, tone);
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
            return alias;
        }
        protected override bool NoGap => true;
        protected override double GetTransitionBasicLengthMs(string alias, int tone, PhonemeAttributes attr) {
            double otoLength = GetTransitionBasicLengthMsByOto(alias, tone, attr);

            var sortedOverrides = PhonemeOverrides.OrderByDescending(kv => kv.Key.Length);
            foreach (var kvp in sortedOverrides) {
                var symbol = kvp.Key;
                var value = kvp.Value;

                if (Regex.IsMatch(alias, $@"(?<![a-zA-Z]){Regex.Escape(symbol)}(?![a-zA-Z])")) {
                    return GetTransitionBasicLengthMsByConstant() * value;
                }
            }

            return otoLength;
        }
    }
}
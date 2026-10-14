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
    [Phonemizer("TETO EN+JP Phonemizer", "EN TETO v102", "Cadlaxa", language: "EN")]
    public class TetoPhonemizer : ArpasingPlusPhonemizer {
        protected override string YamlFileName => "en_teto.yaml";
        protected override string YamlVersion => "1.2";
        protected override byte[] YamlTemplate => EN_TETO.Data.Resources.en_teto_template;
        public TetoPhonemizer() {
            this.vowels = Array.Empty<string>();
            this.consonants = Array.Empty<string>();
            this.diphthongTails = new Dictionary<string, string>() {
                { "ay", "y" },
                { "ey", "y" },
                { "oy", "y" },
                { "uy", "y" },
                { "aw", "w" },
                { "ew", "w" },
                { "ow", "w" },
                { "iw", "w" },
            };
        }
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        protected override IG2p[] GetBaseG2ps() {
            return new IG2p[] { new ArpabetPlusG2p() };
        }

        private bool HasOtoInColor(string alias, int tone, PhonemeAttributes attr) {
            if (singer == null || !singer.Loaded || string.IsNullOrEmpty(alias)) return false;

            string color = attr.voiceColor ?? GetParentVoiceColor() ?? "";

            if (string.IsNullOrEmpty(color)) {
                return singer.TryGetMappedOto(alias, tone, "", out _) 
                    || singer.TryGetMappedOto(alias, tone, out _);
            }

            if (!singer.TryGetMappedOto(alias, tone, color, out var colorOto)) {
                return false;
            }

            if (singer.TryGetMappedOto(alias, tone, "", out var defaultOto)) {
                if (string.Equals(colorOto.Alias, defaultOto.Alias, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(colorOto.File, defaultOto.File, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            if (singer.Subbanks != null) {
                var colorSubbanks = singer.Subbanks
                    .Where(s => string.Equals(s.Color, color, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (colorSubbanks.Count > 0) {
                    bool hasAffixes = colorSubbanks.Any(s => !string.IsNullOrEmpty(s.Suffix) || !string.IsNullOrEmpty(s.Prefix));
                    if (hasAffixes) {
                        bool matches = colorSubbanks.Any(s =>
                            (!string.IsNullOrEmpty(s.Suffix) && colorOto.Alias.Contains(s.Suffix)) ||
                            (!string.IsNullOrEmpty(s.Prefix) && colorOto.Alias.Contains(s.Prefix))
                        );

                        if (!matches) {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private string ValidateAliasForColor(string alias, int tone, PhonemeAttributes attr) {
            if (string.IsNullOrEmpty(alias) || singer == null || !singer.Loaded) return alias;

            if (HasOtoInColor(alias, tone, attr)) return alias;

            string defaultFallback = null;
            if (yamlFallbacks != null) {
                var singleRules = yamlFallbacks
                    .Where(r => r.FromList.Count == 1)
                    .OrderByDescending(r => r.FromList[0].Length)
                    .ToList();

                foreach (var rule in singleRules) {
                    string fromStr = rule.FromList[0].Trim('(', ')');
                    if (alias.Contains(fromStr)) {
                        foreach (var target in rule.ToList) {
                            string candidate = alias.Replace(fromStr, target);
                            
                            // Priority A: Candidate exists in THIS voice color
                            if (HasOtoInColor(candidate, tone, attr)) {
                                return candidate;
                            }

                            // Priority B: Candidate exists in default bank (with prefix.map)
                            if (defaultFallback == null && 
                                (singer.TryGetMappedOto(candidate, tone, "", out _) || 
                                 singer.TryGetMappedOto(candidate, tone, out _))) {
                                defaultFallback = candidate;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(defaultFallback)) {
                return defaultFallback;
            }

            return ValidateAlias(alias, tone);
        }

        protected override Phoneme[] AssignAllAffixes(List<Phoneme> phonemes, Note[] notes, Note[] prevs, List<PhonemeAttributes> dynamicAttributes = null) {
            int noteIndex = 0;

            for (int i = 0; i < phonemes.Count; i++) {
                var attr = dynamicAttributes?.FirstOrDefault(a => a.index == i) 
                    ?? notes[0].phonemeAttributes?.FirstOrDefault(a => a.index == i) 
                    ?? default;

                var phoneme = phonemes[i];

                int? altValue = attr.alternate ?? GetParentAlternate();
                string alt = altValue?.ToString() ?? string.Empty;

                if (string.IsNullOrEmpty(alt) && phoneme.expressions != null) {
                    var altExpr = phoneme.expressions.FirstOrDefault(e => e.abbr == "alt");
                    if (altExpr.abbr == "alt" && altExpr.value > 0) {
                        altValue = (int)altExpr.value;
                        alt = altValue.ToString();
                    }
                }

                // Strictly per-phoneme voice color isolation
                string color = attr.voiceColor ?? GetParentVoiceColor() ?? "";
                int toneShift = attr.toneShift ?? GetParentToneShift();

                while (noteIndex < notes.Length - 1 && notes[noteIndex].position - notes[0].position < phoneme.position) {
                    noteIndex++;
                }

                var noteStartPosition = notes[noteIndex].position - notes[0].position;
                int tone = (phoneme.position < noteStartPosition && i == 0)
                    ? ((noteIndex > 0) ? notes[noteIndex - 1].tone : (prevs != null && prevs.Length > 0 ? prevs.Last().tone : notes[noteIndex].tone))
                    : notes[noteIndex].tone;

                var validatedAlias = phoneme.phoneme;
                if (validatedAlias != null) {
                    validatedAlias = ValidateAliasForColor(validatedAlias, tone + toneShift, attr);
                    string mapped = MapPhoneme(validatedAlias, tone + toneShift, color, alt, singer);
                    if (mapped == validatedAlias && !string.IsNullOrEmpty(color)) {
                        mapped = MapPhoneme(validatedAlias, tone + toneShift, "", alt, singer);
                    }

                    // 4. Manual alternate resolution
                    if (!string.IsNullOrEmpty(alt) && alt != "0" && mapped == validatedAlias) {
                        if (singer.TryGetMappedOto($"{validatedAlias}{alt}", tone + toneShift, color, out var altOto)) {
                            mapped = altOto.Alias;
                        } else if (singer.TryGetMappedOto($"{validatedAlias}{alt}", tone + toneShift, "", out var defaultAltOto)) {
                            mapped = defaultAltOto.Alias;
                        }
                    }

                    phoneme.phoneme = mapped;

                    if (altValue.HasValue && altValue.Value > 0) {
                        var exprList = phoneme.expressions != null 
                            ? new List<PhonemeExpression>(phoneme.expressions) 
                            : new List<PhonemeExpression>();

                        exprList.RemoveAll(e => e.abbr == "alt");
                        exprList.Add(new PhonemeExpression { abbr = "alt", value = altValue.Value });
                        phoneme.expressions = exprList;
                    }
                } else {
                    phoneme.phoneme = null;
                    phoneme.position = 0;
                }

                phonemes[i] = phoneme;
            }
            return phonemes.ToArray();
        }

        protected override bool NoGap => true;

        protected override string ValidateAlias(string alias, int tone = 0) {
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
    }
}
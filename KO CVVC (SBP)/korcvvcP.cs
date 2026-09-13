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
using WanaKanaNet;
using YamlDotNet.Core.Tokens;
using System.Text.RegularExpressions;
using System.Text;
using OpenUtau.Core;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Korean SBP Phonemizer", "KO CVVC+", "Cadlaxa", language: "KO")]
    // Custom KO CVVC Phonemizer build in SBP style
    public class korcvvcP : SyllableBasedPhonemizer {
        private string[] vowels = {
        "a", "e", "i", "o", "u", "eo", "eu", "eui", "ae", "ui", "oe"
        };
        private readonly string[] consonants = "b,bb,ch,d,dd,f,g,gg,h,j,jj,k,kk,l,m,n,ng,p,q,r,s,ss,sh,t,tt,v,w,y,z,zh".Split(',');
        private readonly string[] affricates = "ch,jh,j".Split(',');
        private readonly string[] tapConsonant = "dx,nx,lx".Split(",");
        private readonly string[] semilongConsonants = "ng,n,m,v,z,q,hh,h".Split(",");
        private readonly string[] semiVowels = "y,w".Split(",");
        private readonly string[] connectingGlides = "l,r,ll".Split(",");
        private readonly string[] longConsonants = "f,s,sh,th,zh,dr,tr,ts,c,vf".Split(",");
        private readonly string[] normalConsonants = "b,d,dh,g,k,p,t,l,r".Split(',');
        private readonly string[] connectingNormCons = "b,d,g,k,p,t".Split(',');
        private static Dictionary<char, string> InitialConsonants = new Dictionary<char, string>
        {
            {'ᄀ', "g"}, {'ᄁ', "kk"}, {'ᄂ', "n"}, {'ᄃ', "d"}, {'ᄄ', "tt"}, {'ᄅ', "r"}, {'ᄆ', "m"},
            {'ᄇ', "b"}, {'ᄈ', "pp"}, {'ᄉ', "s"}, {'ᄊ', "ss"}, {'ᄋ', ""}, {'ᄌ', "j"}, {'ᄍ', "jj"},
            {'ᄎ', "ch"}, {'ᄏ', "k"}, {'ᄐ', "t"}, {'ᄑ', "p"}, {'ᄒ', "h"}
        };

        private static Dictionary<char, string> MedialVowels = new Dictionary<char, string>
        {
            {'ᅡ', "a"}, {'ᅢ', "ae"}, {'ᅣ', "ya"}, {'ᅤ', "yae"}, {'ᅥ', "eo"}, {'ᅦ', "e"}, {'ᅧ', "yeo"},
            {'ᅨ', "ye"}, {'ᅩ', "o"}, {'ᅪ', "wa"}, {'ᅫ', "wae"}, {'ᅬ', "oe"}, {'ᅭ', "yo"}, {'ᅮ', "u"},
            {'ᅯ', "wo"}, {'ᅰ', "we"}, {'ᅱ', "wi"}, {'ᅲ', "yu"}, {'ᅳ', "eu"}, {'ᅴ', "ui"}, {'ᅵ', "i"}
        };

        private static Dictionary<char, string> FinalConsonants = new Dictionary<char, string>
        {
            {' ', ""}, {'ᆨ', "k"}, {'ᆩ', "kk"}, {'ᆪ', "ks"}, {'ᆫ', "n"}, {'ᆮ', "n"}, {'ᆰ', "nk"},
            {'ᆱ', "lm"}, {'ᆲ', "lb"}, {'ᆳ', "ls"}, {'ᆴ', "lt"}, {'ᆵ', "lp"}, {'ᆶ', "lh"}, {'ᆷ', "m"},
            {'ᆸ', "p"}, {'ᆹ', "ps"}, {'ᆺ', "s"}, {'ᆻ', "ss"}, {'ᆼ', "ng"}, {'ᆽ', "j"}, {'ᆾ', "ch"},
            {'ᆿ', "k"}, {'ᇀ', "t"}, {'ᇁ', "p"}, {'ᇂ', "h"}
        };
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        private Dictionary<string, string> dictionaryReplacements;
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;
        // Store the splitting replacements
        private List<Replacement> splittingReplacements = new List<Replacement>();
        // Store the merging replacements
        private List<Replacement> mergingReplacements = new List<Replacement>();
        List<string> consExceptions = new List<string>();

        // For banks with missing vowels
        private readonly Dictionary<string, string> missingVphonemes = "ah=a".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "nx=n,dd=d,bb=b,pp=p,jj=j,kk=k,tt=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;
        private bool cPV_FallBack = false;
        private bool vc_FallBack = false;

        private readonly Dictionary<string, string> vcFallBacks =
            new Dictionary<string, string>() {
                {"eui","i"},
            };

        private readonly Dictionary<string, string> vvExceptions =
            new Dictionary<string, string>() {
                {"o","w"},
                {"eo","w"},
                {"u","w"},
                {"i","y"},
                {"ui","y"},
                {"eu","w"},
            };

        private readonly string[] ccvException = { "ch", "dh", "dx", "fh", "gh", "hh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };
        private readonly string[] RomajiException = { "a", "e", "i", "o", "u" };
        private string[] tails = "-,R".Split(',');
        private bool isTails = false;

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (tails.Contains(note.lyric)) {
                isTails = true;
                return new string[] { note.lyric };
            }
            if (original == null) {
                // First, handle the Hangul conversion
                string lyric = note.lyric;
                var romanizedLyric = new StringBuilder();

                foreach (char c in lyric) {
                    // Check if the character is a Hangul syllable
                    if (c >= 0xAC00 && c <= 0xD7A3) {
                        int baseCode = c - 0xAC00;
                        int initialIndex = baseCode / (21 * 28);
                        int medialIndex = (baseCode % (21 * 28)) / 28;
                        int finalIndex = baseCode % 28;

                        char initial = (char)(0x1100 + initialIndex);
                        char medial = (char)(0x1161 + medialIndex);
                        char finalConsonant = finalIndex > 0 ? (char)(0x11A7 + finalIndex) : ' ';

                        // Use the dictionaries to get the romanized parts
                        if (InitialConsonants.TryGetValue(initial, out string initialRoman)) {
                            romanizedLyric.Append(initialRoman);
                        }
                        if (MedialVowels.TryGetValue(medial, out string medialRoman)) {
                            romanizedLyric.Append(medialRoman);
                        }
                        if (FinalConsonants.TryGetValue(finalConsonant, out string finalRoman)) {
                            romanizedLyric.Append(finalRoman);
                        }
                    } else {
                        // If it's not Hangul, just append the original character (e.g., Roman letters, punctuation)
                        romanizedLyric.Append(c);
                    }
                }

                // Now, use the fully romanized string for the rest of the logic
                lyric = romanizedLyric.ToString().ToLowerInvariant();

                List<string> fallbackSplit = new List<string>();
                string[] vowels = GetVowels();
                string[] consonants = GetConsonants();

                // Handle apostrophes at the start or end
                bool hasLeadingApostrophe = lyric.StartsWith("'");
                bool hasTrailingApostrophe = lyric.EndsWith("'");

                if (hasLeadingApostrophe) {
                    lyric = lyric.Substring(1);
                }
                if (hasTrailingApostrophe && lyric.Length > 0) {
                    lyric = lyric.Substring(0, lyric.Length - 1);
                }

                int ii = 0;
                while (ii < lyric.Length) {
                    string match = null;
                    foreach (var cons in consonants.OrderByDescending(c => c.Length)) {
                        if (lyric.Substring(ii).StartsWith(cons)) {
                            match = cons;
                            break;
                        }
                    }
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
                // Add "-" at the beginning or end if needed
                if (hasLeadingApostrophe) {
                    fallbackSplit.Insert(0, "-");
                }
                if (hasTrailingApostrophe) {
                    fallbackSplit.Add("-");
                }
                original = fallbackSplit.ToArray();
            }
            List<string> modified = new List<string>(original);
            List<string> finalPhonemes = new List<string>();
            int i = 0;
            bool hasReplacements = mergingReplacements.Any() == true || splittingReplacements.Any() == true; // Check for any replacements
            if (hasReplacements) {
                finalPhonemes = new List<string>();
                while (i < modified.Count) {
                    bool replaced = false;
                    foreach (var rule in mergingReplacements.Concat(splittingReplacements)) {
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
                        foreach (var rule in splittingReplacements) {
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

            // SPLITS UP DR AND TR
            string[] eui = new[] { "eui" };
            string[] av_c = new[] { "al", "am", "an", "ang", "ar" };
            string[] ev_c = new[] { "el", "em", "en", "eng", "err" };
            string[] iv_c = new[] { "il", "im", "in", "ing", "ir" };
            string[] ov_c = new[] { "ol", "om", "on", "ong", "or" };
            string[] uv_c = new[] { "ul", "um", "un", "ung", "ur" };
            var consonatsV1 = new List<string> { "l", "m", "n", "r" };
            var consonatsV2 = new List<string> { "mm", "nn", "ng" };
            // SPLITS UP 2 SYMBOL VOWELS AND 1 SYMBOL CONSONANT
            List<string> vowel3S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV1) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            // SPLITS UP 2 SYMBOL VOWELS AND 2 SYMBOL CONSONANT
            List<string> vowel4S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV2) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            IEnumerable<string> phonemes;
            if (hasReplacements) {
                phonemes = finalPhonemes;
            } else {
                phonemes = original;
            }
            foreach (string s in phonemes) {
                switch (s) {
                    case var str when av_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "aa", s[1].ToString() });
                        break;
                    case var str when ev_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "eh", s[1].ToString() });
                        break;
                    case var str when iv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "iy", s[1].ToString() });
                        break;
                    case var str when ov_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "ao", s[1].ToString() });
                        break;
                    case var str when uv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "uw", s[1].ToString() });
                        break;
                    case var str when vowel3S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { s.Substring(0, 2), s[2].ToString() });
                        break;
                    case var str when vowel4S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { s.Substring(0, 2), s.Substring(2, 2) });
                        break;
                    default:
                        finalProcessedPhonemes.Add(s);
                        break;
                }
            }
            return finalProcessedPhonemes.ToArray();
        }

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();
            // LOAD DICTIONARY FROM FOLDER
            /*string path = Path.Combine(PluginDir, "ko_cvvc.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, KO_CVVCP.Data.Resources.kor_template);
            }*/
            // LOAD DICTIONARY FROM SINGER FOLDER
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, "ko_cvvc.yaml");
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }
            //g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            //g2ps.Add(new ArpabetPlusG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }
        public override void SetSinger(USinger singer) {
            if (this.singer != singer) {
                string file;
                if (singer != null && singer.Found && singer.Loaded && !string.IsNullOrEmpty(singer.Location)) {
                    file = Path.Combine(singer.Location, "ko_cvvc.yaml");
                    if (!File.Exists(file)) {
                        try {
                            File.WriteAllBytes(file, KO_CVVCP.Data.Resources.kor_template);
                        } catch (Exception e) {
                            Log.Error(e, $"Failed to write 'ko_cvvc.yaml' to singer folder at {file}");
                        }
                    }
                } else if (!string.IsNullOrEmpty(PluginDir)) {
                    file = Path.Combine(PluginDir, "ko_cvvc.yaml");
                } else {
                    Log.Error("Singer location and PluginDir are both null or empty. Cannot locate 'ko_cvvc.yaml'.");
                    return; // Exit early to avoid null file issues
                }

                if (File.Exists(file)) {
                    try {
                        var data = Core.Yaml.DefaultDeserializer.Deserialize<YAMLData>(File.ReadAllText(file));
                        // Load vowels
                        try {
                            var loadVowels = data.symbols
                                ?.Where(s => s.type == "vowel")
                                .Select(s => s.symbol)
                                .ToList() ?? new List<string>();

                            vowels = vowels.Concat(loadVowels).Distinct().ToArray();
                        } catch (Exception ex) {
                            Log.Error($"Failed to load vowels from ko_cvvc.yaml: {ex.Message}");
                        }
                        // Load stop and tap consonants
                        try {
                            var loadConsonants = data.symbols
                                ?.Where(s => s.type == "stop" || s.type == "tap")
                                .Select(s => s.symbol)
                                .ToList() ?? new List<string>();

                            consExceptions.AddRange(loadConsonants);
                        } catch (Exception ex) {
                            Log.Error($"Failed to load stop and tap consonants from filipino.yaml: {ex.Message}");
                        }
                        // Load replacements
                        try {
                            if (data?.replacements != null && data.replacements.Any() == true) {
                                dictionaryReplacements = new Dictionary<string, string>();
                                mergingReplacements = new List<Replacement>();
                                splittingReplacements = new List<Replacement>();

                                foreach (var replacement in data.replacements) {
                                    try {
                                        if (replacement.from != null && replacement.to != null) {
                                            if (replacement.from is IEnumerable<object> fromList) {
                                                // 'from' is a list (e.g., [ae, n])
                                                string[] fromArray = fromList.Select(item => item.ToString()).ToArray();
                                                if (replacement.to is string toString) {
                                                    mergingReplacements.Add(new Replacement { from = fromArray, to = toString });
                                                } else if (replacement.to is IEnumerable<object> toList) {
                                                    splittingReplacements.Add(new Replacement { from = fromArray, to = toList.Select(item => item.ToString()).ToArray() });
                                                } else {
                                                    Log.Error($"Error: Invalid 'to' type in replacement: {replacement}");
                                                }
                                            } else if (replacement.from is string fromString) {
                                                // 'from' is a single string (e.g., tr, aw, ae, m, ng)
                                                if (replacement.to is string toString) {
                                                    dictionaryReplacements[fromString] = toString;
                                                } else if (replacement.to is IEnumerable<object> toList) {
                                                    splittingReplacements.Add(new Replacement { from = fromString, to = toList.Select(item => item.ToString()).ToArray() });
                                                } else {
                                                    Log.Error($"Error: Invalid 'to' type in replacement: {replacement}");
                                                }
                                            } else {
                                                Log.Error($"Error: Invalid 'from' type in replacement: {replacement}");
                                            }
                                        } else {
                                            Log.Error($"Error: 'from' or 'to' is null in replacement: {replacement}");
                                        }
                                    } catch (Exception ex) {
                                        Log.Error($"Failed to process replacement entry: {replacement}. Error: {ex.Message}");
                                    }
                                }
                            } else {
                                dictionaryReplacements = new Dictionary<string, string>();
                                mergingReplacements = new List<Replacement>();
                                splittingReplacements = new List<Replacement>();
                            }
                        } catch (Exception ex) {
                            Log.Error($"Failed to load replacements from ko_cvvc.yaml: {ex.Message}");
                        }
                        // load fallbacks
                        try {
                            if (data?.fallbacks?.Any() == true) {
                                foreach (var df in data.fallbacks) {
                                    if (!string.IsNullOrEmpty(df.from) && !string.IsNullOrEmpty(df.to)) {
                                        missingVphonemes[df.from] = df.to;
                                    }
                                }
                            }
                        } catch (Exception ex) {
                            Log.Error($"Failed to load fallbacks from ko_cvvc.yaml: {ex.Message}");
                        }
                    } catch (Exception ex) {
                        Log.Error($"Failed to parse ko_cvvc.yaml: {ex.Message}, content: {File.ReadAllText(file)}, Exception Type: {ex.GetType()}");
                    }
                }

                ReadDictionaryAndInit();
                this.singer = singer;
            }
        }
        public class YAMLData {
            public SymbolData[] symbols { get; set; } = Array.Empty<SymbolData>();
            public Replacement[] replacements { get; set; } = Array.Empty<Replacement>();
            public Fallbacks[] fallbacks { get; set; } = Array.Empty<Fallbacks>();

            public struct SymbolData {
                public string symbol { get; set; }
                public string type { get; set; }
            }
            public struct Fallbacks {
                public string from { get; set; }
                public string to { get; set; }
            }
        }
        // can split or merge
        public class Replacement {
            public object from { get; set; }
            public object to { get; set; }

            public List<string> FromList {
                get {
                    if (from is string s) return new List<string> { s };
                    if (from is IEnumerable<object> list) return list.Select(x => x.ToString()).ToList();
                    return new List<string>();
                }
            }

            public List<string> ToList {
                get {
                    if (to is string s) return new List<string> { s };
                    if (to is IEnumerable<object> list) return list.Select(x => x.ToString()).ToList();
                    return new List<string>();
                }
            }
        }

        // prioritize yaml replacements over dictionary replacements
        private string ReplacePhoneme(string phoneme, int tone) {
            // If the original phoneme has an OTO, use it directly.
            if (HasOto(phoneme, tone) || HasOto(ValidateAlias(phoneme), tone)) {
                return phoneme;
            }
            // Otherwise, try to apply the dictionary replacement.
            if (dictionaryReplacements.TryGetValue(phoneme, out var replaced)) {
                return replaced;
            }
            return phoneme;
        }
        protected override List<string> ProcessSyllable(Syllable syllable) {
            var replacedPrevV = ReplacePhoneme(syllable.prevV, syllable.tone);
            var prevV = string.IsNullOrEmpty(replacedPrevV) ? "" : replacedPrevV;
            string[] cc = syllable.cc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            List<string> vowels = new List<string> { ReplacePhoneme(syllable.v, syllable.vowelTone) };
            string v = ReplacePhoneme(syllable.v, syllable.vowelTone);
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            string[] CurrentWordCc = syllable.CurrentWordCc.Select(ReplacePhoneme).ToArray();
            string[] PreviousWordCc = syllable.PreviousWordCc.Select(ReplacePhoneme).ToArray();
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
            
            // For VC Fallback phonemes
            foreach (var entry in vcFallBacks) {
                if (!HasOto($"{entry.Key} {cc}", syllable.tone) || (!HasOto($"ao {cc}", syllable.tone))) {
                    vc_FallBack = true;
                }
            }

            // STARTING V
            if (syllable.IsStartingV) {
                basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
            }
            // [V V] or [V C][C V]/[V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    basePhoneme = $"{prevV} {v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) && !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone) && vvExceptions.ContainsKey(prevV) && prevV != v) {
                        // VV IS NOT PRESENT, CHECKS VVEXCEPTIONS LOGIC
                        //var vc = $"{prevV}{vvExceptions[prevV]}";
                        var vc = AliasFormat($"{vvExceptions[prevV]}", "vcEx", syllable.vowelTone, prevV);
                        phonemes.Add(vc);
                        basePhoneme = AliasFormat($"{vvExceptions[prevV]} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        {
                            if (HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone)) {
                                basePhoneme = $"{prevV} {v}";
                            } else if (HasOto($"{prevV}{v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV}{v}"), syllable.vowelTone)) {
                                basePhoneme = $"{prevV}{v}";
                            } else if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) {
                                basePhoneme = v;
                            } else {
                                basePhoneme = v;
                            }
                        }
                    }
                } else {
                    // PREVIOUS ALIAS WILL EXTEND as [V V]
                    basePhoneme = null;
                }

                // [- CV/C V] or [- C][CV/C V]
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var rcv = $"- {cc[0]} {v}";
                var rcv1 = $"- {cc[0]}{v}";
                var crv = $"{cc[0]} {v}";
                /// - CV
                if (HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv), syllable.vowelTone) || (HasOto(rcv1, syllable.vowelTone) || HasOto(ValidateAlias(rcv1), syllable.vowelTone))) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynStart", syllable.vowelTone, "");
                    /// CV
                } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                } else {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                    TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
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
                if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone) || HasOto(rccv1, syllable.vowelTone) || HasOto(ValidateAlias(rccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                    basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynStart", syllable.vowelTone, "");
                    lastC = 0;
                } else {
                    /// CCV and CV
                    if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                        basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                        //lastC = 0;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(crv1, syllable.vowelTone) || HasOto(ValidateAlias(crv1), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                    }
                    // TRY RCC [- CC]
                    for (var i = cc.Length; i > 1; i--) {
                        if (!ccvException.Contains(cc[0])) {
                            if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.vowelTone, ""))) {
                                firstC = i - 1;
                            }
                        }
                        break;
                    }
                    // [- C]
                    if (phonemes.Count == 0) {
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""));
                    }
                }
            } else {
                string lastCPrevWord = PreviousWordCc.LastOrDefault() ?? "";
                string currentCWord = CurrentWordCc.FirstOrDefault() ?? "";
                // fortition
                /*if (!string.IsNullOrEmpty(lastCPrevWord) && cc.Length > 0 && CurrentWordCc.Length == 1) {
                    cc[1] = TenseConsonant(cc[1], lastCPrevWord, currentCWord);
                }
                // lentition
                if (!string.IsNullOrEmpty(lastCPrevWord) && cc.Length > 0 && CurrentWordCc.Length == 0) {
                    cc[0] = SoftConsonant(cc[0], lastCPrevWord);
                }
                if (!string.IsNullOrEmpty(lastCPrevWord) && cc.Length > 0 && CurrentWordCc.Length == 1) {
                    cc[0] = NasalConsonant(cc[0], lastCPrevWord, currentCWord);
                }*/
                var crv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";

                /// CV
                if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                    basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                } else {
                    basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                }
                // try [CC V] or [CCV]
                for (var i = firstC; i < cc.Length - 1; i++) {
                    var ccv = $"{string.Join("", cc.Skip(i + 1))} {v}";
                    var ccv1 = $"{string.Join("", cc.Skip(i + 1))}{v}";
                    /// CCV
                    if (CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[i])) {
                        if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                            lastC = i;
                            break;
                        }
                    } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                        if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        } else {
                            basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                        }
                    }
                }

                // CC Endings (trailing)
                if (isTails && basePhoneme.Contains("-")) {
                    for (int clusterLength = 3; clusterLength >= 2; clusterLength--) {
                        if (clusterLength > cc.Length) {
                            continue;
                        }

                        var cluster = new string[clusterLength];
                        Array.Copy(cc, 0, cluster, 0, clusterLength);

                        // All possible spacing patterns for the consonants.
                        var consonantPatterns = new List<string>();

                        if (clusterLength >= 3) {
                            consonantPatterns.Add($"{cluster[0]} {cluster[1]}{cluster[2]}");
                            consonantPatterns.Add($"{cluster[0]}{cluster[1]} {cluster[2]}");
                            consonantPatterns.Add($"{cluster[0]} {cluster[1]} {cluster[2]}");
                        } else if (clusterLength == 2) {
                            consonantPatterns.Add($"{cluster[0]} {cluster[1]}");
                            consonantPatterns.Add($"{cluster[0]}{cluster[1]}");
                        }

                        // Check for all possible patterns with the ending hyphen.
                        foreach (var consPattern in consonantPatterns) {
                            string[] endPatterns = { "-", " R", " -", };
                            foreach (var end in endPatterns) {
                                string endingcc = $"{consPattern}{end}";

                                if (HasOto(endingcc, syllable.tone)) {
                                    basePhoneme = endingcc;
                                    lastC = 0;
                                    goto FoundMatch;
                                } else {
                                    continue;
                                }
                            }
                        }
                    }
                }
                FoundMatch:;

                // try [V C], [V CC], [VC C], [V -][- C]
                for (var i = lastC + 1; i >= 0; i--) {
                    var vr = $"{prevV} -";
                    var vc_c = $"{prevV}{string.Join(" ", cc.Take(2))}";
                    var vcc = $"{prevV} {string.Join("", cc.Take(2))}";
                    var vc = $"{prevV} {cc[0]}";
                    // Boolean Triggers
                    bool CCV = false;
                    if (CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                        if (HasOto(AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                            CCV = true;
                        }
                    }

                    string originalFinalConsonant = cc[0];
                    string transformedFinalConsonant = originalFinalConsonant;

                    /*// lentition
                    if (!string.IsNullOrEmpty(cc[0]) && cc.Length > 0 && currentCWord.Length == 0) {
                        transformedFinalConsonant = SoftConsonant(cc[0], lastCPrevWord);
                    }
                    // nasal
                    if (!string.IsNullOrEmpty(cc.Last()) && cc.Length > 1 && CurrentWordCc.Length > 1) {
                        transformedFinalConsonant = NasalConsonant(cc[0], lastCPrevWord, currentCWord);
                    }*/

                    // Update vc to use the transformed final consonant
                    vc = $"{prevV} {transformedFinalConsonant}";

                    if (i == 0 && (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone))) {
                        phonemes.Add(vc);
                        break;
                    } else if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc), syllable.tone)) && CCV) {
                        phonemes.Add(vcc);
                        firstC = 1;
                        break;
                    } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                        TryAddPhoneme(phonemes, syllable.tone, vc, ValidateAlias(vc));
                        break;
                    } else {
                        continue;
                    }
                }
            }
            for (var i = firstC; i < lastC; i++) {
                var ccv = $"{string.Join("", cc.Skip(i + 1))} {v}";
                var ccv1 = $"{string.Join("", cc.Skip(i + 1))}{v}";
                var cc1 = $"{string.Join(" ", cc.Skip(i))}";
                var lcv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // [C1 C2]
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]} {cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // CC FALLBACKS
                if (!HasOto(cc1, syllable.tone) || (!HasOto(ValidateAlias(cc1), syllable.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone))) {
                    var c1 = cc[i];
                    var c2 = cc[i + 1];
                    bool c1IsException = consExceptions.Contains(c1);
                    bool c2IsException = consExceptions.Contains(c2);

                    // Scenario 1: Both are NOT exceptions
                    if (!c1IsException && !c2IsException) {
                        cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                        TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, ""));
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
                    cc1 = ValidateAlias(cc1);
                }
                // other cc formats
                if (HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                    // like [C1 C2C3]
                    cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                    lastC = i;
                }
                if (HasOto($"{cc[i]} {string.Join(" ", cc.Skip(i + 1))}", syllable.tone)) {
                    // like [C1 C2 C3]
                    cc1 = $"{cc[i]} {string.Join(" ", cc.Skip(i + 1))}";
                    lastC = i;
                }
                if (HasOto($"{cc[i]}{string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                    // like [C1C2C3]
                    cc1 = $"{cc[i]}{string.Join("", cc.Skip(i + 1))}";
                    lastC = i;
                }
                if (HasOto($"{cc[i]}{string.Join(" ", cc.Skip(i + 1))}", syllable.tone)) {
                    // like [C1C2 C3]
                    cc1 = $"{cc[i]}{string.Join(" ", cc.Skip(i + 1))}";
                    lastC = i;
                }
                // C+V
                if ((HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) && (!HasOto(lcv, syllable.vowelTone) && !HasOto(ValidateAlias(lcv), syllable.vowelTone) && (!HasOto(cv, syllable.vowelTone) && !HasOto(ValidateAlias(cv), syllable.vowelTone)))) {
                    cPV_FallBack = true;
                    basePhoneme = v;
                    cc1 = ValidateAlias(cc1);
                }

                if (i + 1 < lastC) {
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // CC FALLBACKS
                    if (!HasOto(cc1, syllable.tone) || (!HasOto(ValidateAlias(cc1), syllable.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone))) {
                        var c1 = cc[i];
                        var c2 = cc[i + 1];
                        bool c1IsException = consExceptions.Contains(c1);
                        bool c2IsException = consExceptions.Contains(c2);

                        // Scenario 1: Both are NOT exceptions
                        if (!c1IsException && !c2IsException) {
                            // [C1 -] [- C2]
                            cc1 = AliasFormat($"{c2}", "cc_inB", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{c1}", "cc_endB", syllable.vowelTone, ""));
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
                        cc1 = ValidateAlias(cc1);
                    }
                    // other cc formats
                    if (HasOto($"{cc[i]} {string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                        // like [C1 C2C3]
                        cc1 = $"{cc[i]} {string.Join("", cc.Skip(i + 1))}";
                        lastC = i;
                    }
                    if (HasOto($"{cc[i]} {string.Join(" ", cc.Skip(i + 1))}", syllable.tone)) {
                        // like [C1 C2 C3]
                        cc1 = $"{cc[i]} {string.Join(" ", cc.Skip(i + 1))}";
                        lastC = i;
                    }
                    if (HasOto($"{cc[i]}{string.Join("", cc.Skip(i + 1))}", syllable.tone)) {
                        // like [C1C2C3]
                        cc1 = $"{cc[i]}{string.Join("", cc.Skip(i + 1))}";
                        lastC = i;
                    }
                    if (HasOto($"{cc[i]}{string.Join(" ", cc.Skip(i + 1))}", syllable.tone)) {
                        // like [C1C2 C3]
                        cc1 = $"{cc[i]}{string.Join(" ", cc.Skip(i + 1))}";
                        lastC = i;
                    }
                    // C+V
                    if ((HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) && (!HasOto(lcv, syllable.vowelTone) && !HasOto(ValidateAlias(lcv), syllable.vowelTone) && (!HasOto(cv, syllable.vowelTone) && !HasOto(ValidateAlias(cv), syllable.vowelTone)))) {
                        cPV_FallBack = true;
                        basePhoneme = v;
                        cc1 = ValidateAlias(cc1);
                    }
                    if (HasOto(cc1, syllable.tone) && HasOto(cc1, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // like [V C1] [C1 C2] [C2 ..]
                        if (cc1.Contains($"{string.Join(" ", cc.Skip(i + 1))}")) {
                            i++;
                        }
                    } else {
                        // singular cc
                        if (PreviousWordCc.Contains(cc1) == CurrentWordCc.Contains(cc1)) {
                            cc1 = ValidateAlias(cc1);
                        } else {
                            TryAddPhoneme(phonemes, syllable.tone, cc1, cc[i], ValidateAlias(cc[i]));
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
            if (tails.Contains(ending.prevV)) {
                return new List<string>();
            }
            if (ending.IsEndingV) {
                var vR = $"{prevV} -";
                var vR1 = $"{prevV} R";
                var vR2 = $"{prevV}-";
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR), ending.tone) || HasOto(vR1, ending.tone) || HasOto(ValidateAlias(vR1), ending.tone) || HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{prevV}", "ending", ending.tone, ""));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{prevV} {cc[0]}";
                var vcr = $"{prevV} {cc[0]}-";
                var vcr2 = $"{prevV}{cc[0]} -";
                var vcr3 = $"{prevV} {cc[0]} -";
                var vcr4 = $"{prevV}{cc[0]}-";
                if (!RomajiException.Contains(cc[0])) {
                    if (HasOto(vcr, ending.tone) && HasOto(ValidateAlias(vcr), ending.tone) || (HasOto(vcr2, ending.tone) && HasOto(ValidateAlias(vcr2), ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, ""));
                    } else if (HasOto(vcr3, ending.tone) && HasOto(ValidateAlias(vcr3), ending.tone)) {
                        TryAddPhoneme(phonemes, ending.tone, vcr3);
                    } else if (HasOto(vcr4, ending.tone) && HasOto(ValidateAlias(vcr4), ending.tone)) {
                        TryAddPhoneme(phonemes, ending.tone, vcr4);
                    } else if (HasOto(vc, ending.tone) && HasOto(ValidateAlias(vc), ending.tone)) {
                        TryAddPhoneme(phonemes, ending.tone, vc);
                        if (vc.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, ""));
                        }
                    } else {
                        TryAddPhoneme(phonemes, ending.tone, vc);
                        if (vc.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, ""));
                        }
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr = $"{v} -";
                    var vr1 = $"{v} R";
                    var vr2 = $"{v}-";
                    var vcc = $"{v} {string.Join("", cc.Take(2))}-";
                    var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} -";
                    var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                    var vcc4 = $"{v} {string.Join("", cc.Take(2))}";
                    var vc = $"{v} {cc[0]}";
                    if (!RomajiException.Contains(cc[0])) {
                        if (i == 0) {
                            if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) || HasOto(vr2, ending.tone) || HasOto(ValidateAlias(vr2), ending.tone) || HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone) && !HasOto(vc, ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v}", "ending", ending.tone, ""));
                            }
                            break;
                        } else if (HasOto(vcc, ending.tone) && HasOto(ValidateAlias(vcc), ending.tone) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc);
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc2, ending.tone) && HasOto(ValidateAlias(vcc2), ending.tone) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc2);
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc3, ending.tone) && HasOto(ValidateAlias(vcc3), ending.tone) && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc3);
                            if (vcc3.EndsWith(cc.Last()) && lastC == 1) {
                                if (consonants.Contains(cc.Last())) {
                                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                                }
                            }
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc4, ending.tone) && HasOto(ValidateAlias(vcc4), ending.tone) && !ccvException.Contains(cc[0])) {
                            TryAddPhoneme(phonemes, ending.tone, vcc4);
                            if (vcc4.EndsWith(cc.Last()) && lastC == 1) {
                                if (consonants.Contains(cc.Last())) {
                                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                                }
                            }
                            firstC = 1;
                            break;
                        } else if (!!HasOto(vcc, ending.tone) && !HasOto(ValidateAlias(vcc), ending.tone)
                                || !HasOto(vcc2, ending.tone) && HasOto(ValidateAlias(vcc2), ending.tone)
                                || !HasOto(vcc3, ending.tone) && HasOto(ValidateAlias(vcc3), ending.tone)
                                || !HasOto(vcc4, ending.tone) && HasOto(ValidateAlias(vcc4), ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, vc);
                            break;
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, vc);
                            break;
                        }
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    var cc1 = $"{cc[i]} {cc[i + 1]}";
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc2, ending.tone)) {
                            cc2 = ValidateAlias(cc2);
                        }

                        if (HasOto(cc1, ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            // like [C1 C2][C2 ...]
                            TryAddPhoneme(phonemes, ending.tone, cc1);
                        } else if ((HasOto(cc[i], ending.tone) || HasOto(ValidateAlias(cc[i]), ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone)))) {
                            // like [C1 C2-][C3 ...]
                            TryAddPhoneme(phonemes, ending.tone, cc[i]);
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} {cc[i + 2]}-", ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"))) {
                            // like [C1 C2-][C3 ...]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]}{cc[i + 2]}", ValidateAlias($"{cc[i + 1]}{cc[i + 2]}"))) {
                            // like [C1C2][C2 ...]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else {
                            // like [C1][C2 ...]
                            TryAddPhoneme(phonemes, ending.tone, cc[i], ValidateAlias(cc[i]), $"{cc[i]} -", ValidateAlias($"{cc[i]} -"));
                            TryAddPhoneme(phonemes, ending.tone, cc[i + 1], ValidateAlias(cc[i + 1]), $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"));
                            i++;
                        }
                        // CC that ends with 3 clusters
                        for (int clusterLength = 3; clusterLength >= 2; clusterLength--) {
                            if (i + clusterLength > cc.Length) {
                                continue;
                            }
                            var cluster = new string[clusterLength];
                            for (int k = 0; k < clusterLength; k++) {
                                cluster[k] = cc[i + k].ToString();
                            }
                            // Generate all possible spacing patterns for the consonants.
                            var consonantPatterns = new List<string>();
                            consonantPatterns.Add(string.Join("", cluster));

                            // 3 CC.
                            if (clusterLength == 3) {
                                consonantPatterns.Add($"{cluster[0]} {cluster[1]}{cluster[2]}");
                                consonantPatterns.Add($"{cluster[0]}{cluster[1]} {cluster[2]}");
                                consonantPatterns.Add($"{cluster[0]} {cluster[1]} {cluster[2]}");
                            }
                            // 2 CC.
                            else if (clusterLength == 2) {
                                consonantPatterns.Add($"{cluster[0]} {cluster[1]}");
                                consonantPatterns.Add($"{cluster[0]}{cluster[1]}");
                            }

                            foreach (var consPattern in consonantPatterns) {
                                string[] hyphenPatterns = { "-", " -", " R"};
                                foreach (var hyphenPattern in hyphenPatterns) {
                                    string endingcc = $"{consPattern}{hyphenPattern}";

                                    if (TryAddPhoneme(phonemes, ending.tone, endingcc, ValidateAlias(endingcc))) {
                                        i += clusterLength - 1;
                                    }
                                }
                            }
                        }
                    } else {
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = $"{cc[i]} {cc[i + 1]}";
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // [C1 -] [- C2]
                        if (!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            cc1 = AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, "");
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""));
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // CC that ends with 2 clusters
                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-"))) {
                            // like [C1 C2-]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]} -", ValidateAlias($"{cc[i]} {cc[i + 1]} -"))) {
                            // like [C1 C2 -]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]}-", ValidateAlias($"{cc[i]}{cc[i + 1]}-"))) {
                            // like [C1C2-]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]} -", ValidateAlias($"{cc[i]}{cc[i + 1]} -"))) {
                            // like [C1C2 -]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            // like [C1 C2][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1 -] [- C2]
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 2]}", "cc_endB", ending.tone, ""));
                            i++;
                        }
                    }
                }
            }
            return phonemes;
        }
        // Fortition
        private string TenseConsonant(string consonant, string lastCPrevWord, string CurrentCWord) {
            // List of consonants that trigger tensing in the following consonant.
            var triggerConsonants = new HashSet<string> { "b", "bb", "d", "dd", "g", "gg", "k", "kk", "p", "pp", "t", "tt" };
            var currC = new HashSet<string> { "g", "d", "b", "s", "j" };

            // The tensing rules.
            var tensingMap = new Dictionary<string, string> {
                {"g", "kk"}, {"d", "tt"}, {"b", "pp"}, {"s", "ss"}, {"j", "jj"}
            };

            if (triggerConsonants.Contains(lastCPrevWord) && tensingMap.ContainsKey(consonant) && currC.Contains(CurrentCWord)) {
                return tensingMap[consonant];
            }
            return consonant;
        }
        // Lenition
        private string SoftConsonant(string consonant, string lastCPrevWord) {
            // List of consonants that trigger tensing in the following consonant.
            var triggerConsonants = new HashSet<string> { "k", "t", "p", "ch"};

            // The tensing rules.
            var tensingMap = new Dictionary<string, string> {
                {"k", "g"}, {"t", "d"}, {"p", "b"}, {"ch", "j"}
            };

            if (triggerConsonants.Contains(lastCPrevWord) && tensingMap.ContainsKey(consonant)) {
                return tensingMap[consonant];
            } else {
                return consonant;
            }
        }
        // Aspiration
        private string AspConsonant(string consonant, string lastCPrevWord) {
            // List of consonants that trigger tensing in the following consonant.
            var triggerConsonants = new HashSet<string> { "g", "d", "b", "j"};

            // The tensing rules.
            var tensingMap = new Dictionary<string, string> {
                {"g", "k"}, {"d", "t"}, {"b", "p"}, {"j", "ch"}
            };

            if (triggerConsonants.Contains(lastCPrevWord) && tensingMap.ContainsKey(consonant)) {
                return tensingMap[consonant];
            } else {
                return consonant;
            }
        }
        // Nasal not working properly
        private string NasalConsonant(string consonant, string lastCPrevWord, string CurrentVWord) {
            var nasalConsonants = new HashSet<string> { "n", "m", "ng" };
            var triggerConsonants = new HashSet<string> { "p", "b", "t", "d", "k", "g" };
            var nasalizationMap = new Dictionary<string, string> {
                {"p", "m"}, {"b", "m"}, {"t", "n"}, {"d", "n"}, {"k", "ng"}, {"g", "ng"}
            };

            if (triggerConsonants.Contains(lastCPrevWord) && nasalizationMap.ContainsKey(consonant) && nasalConsonants.Contains(CurrentVWord)) {
                return nasalizationMap[consonant];
            } else {
                return consonant;
            }
        }
        private string AliasFormat(string alias, string type, int tone, string prevV) {
            var aliasFormats = new Dictionary<string, string[]> {
            // Define alias formats for different types
                { "dynStart", new string[] { "" } },
                { "dynMid", new string[] { "" } },
                { "dynMid_vv", new string[] { "" } },
                { "dynEnd", new string[] { "" } },
                { "startingV", new string[] { "-", "- ", "_", "" } },
                { "vcEx", new string[] { $"{prevV} ", $"{prevV}" } },
                { "vvExtend", new string[] { "", "_", "-", "- " } },
                { "cv", new string[] { "-", "", "- ", "_" } },
                { "cvStart", new string[] { "-", "- ", "_" } },
                { "ending", new string[] { " -", "-", " R" } },
                { "ending_mix", new string[] { "-", " -", "R", " R", "_", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-", "_" } },
                { "cc_end", new string[] { " -", "-", "" } },
                { "cc_inB", new string[] { "_", "", "- " } },
                { "cc_endB", new string[] { "_", "", " -" } },
                { "cc_mix", new string[] { " -", " R", "-", "", "_", "- ", "-" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
            };

            // Check if the given type exists in the aliasFormats dictionary
            if (!aliasFormats.ContainsKey(type) && !type.Contains("dynamic")) {
                return alias;
            }

            // Handle dynamic variations when type contains "dynamic"
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

                // Handle the alias with space and without space
                var dynamicVariations = new List<string> {
                    // Variations with space, dash, and underscore
                    $"- {consonant}{vowel}",        // "- CV"
                    $"- {consonant} {vowel}",       // "- C V"
                    $"-{consonant} {vowel}",        // "-C V"
                    $"-{consonant}{vowel}",         // "-CV"
                    $"-{consonant}_{vowel}",        // "-C_V"
                    $"- {consonant}_{vowel}",       // "- C_V"
                };
                // Check each dynamically generated format
                foreach (var variation in dynamicVariations) {
                    if (HasOto(variation, tone) || HasOto(ValidateAlias(variation), tone)) {
                        return variation;
                    }
                }
            }

            if (type.Contains("dynMid")) {
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
                var dynamicVariations1 = new List<string> {
                    $"{consonant}{vowel}",    // "CV"
                    $"{consonant} {vowel}",    // "C V"
                    $"{consonant}_{vowel}",    // "C_V"
                };
                // Check each dynamically generated format
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
                    }
                }
            }

            if (type.Contains("dynEnd")) {
                string consonant = "";
                string vowel = "";
                // If the alias contains a space, split it into consonant and vowel
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
                // Check each dynamically generated format
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
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
                // Check if the formatted alias exists
                if (HasOto(aliasFormat, tone) || HasOto(ValidateAlias(aliasFormat), tone)) {
                    return aliasFormat;
                }
            }
            return alias;
        }

        protected override string ValidateAlias(string alias) {

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingVPhonemes || isMissingCPhonemes) {
                foreach (var phoneme in missingVphonemes.Concat(missingCphonemes)) {
                    alias = alias.Replace(phoneme.Key, phoneme.Value);
                }
            }

            foreach (var v1 in vcFallBacks) {
                foreach (var c1 in consonants) {
                    if (vc_FallBack) {
                        alias = alias.Replace(v1.Key + " " + c1, v1.Value + " " + c1);
                    }
                }
            }

            var CVMappings = new Dictionary<string, string[]> {
                { "ao", new[] { "ow" } },
                { "oy", new[] { "ow" } },
                { "aw", new[] { "ah" } },
                { "ay", new[] { "ah" } },
                { "eh", new[] { "ae" } },
                { "ey", new[] { "eh" } },
                { "ow", new[] { "ao" } },
                { "uh", new[] { "uw" } },
            };
            foreach (var kvp in CVMappings) {
                var v1 = kvp.Key;
                var vfallbacks = kvp.Value;
                foreach (var vfallback in vfallbacks) {
                    foreach (var c1 in consonants) {
                        alias = alias.Replace(c1 + " " + v1, c1 + " " + vfallback);
                    }
                }
            }

            return base.ValidateAlias(alias);
        }

        protected override double GetTransitionBasicLengthMs(string alias = "") {
            //I wish these were automated instead :')
            double transitionMultiplier = 1.0; // Default multiplier
            bool isEndingConsonant = false;
            bool isEndingVowel = false;
            bool hasCons = false;
            bool haslr = false;
            var excludedVowels = new List<string> { "a", "e", "i", "o", "u" };
            var GlideVCCons = new List<string> { $"{excludedVowels} {connectingGlides}" };
            var NormVCCons = new List<string> { $"{excludedVowels} {connectingNormCons}" };
            var arpabetFirstVDiphthong = new List<string> { "a", "e", "i", "o", "u" };
            var excludedEndings = new List<string> { $"{arpabetFirstVDiphthong}y -", $"{arpabetFirstVDiphthong}w -", $"{arpabetFirstVDiphthong}r -", };

            foreach (var c in longConsonants) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -")) {
                    return base.GetTransitionBasicLengthMs() * 2.5;
                }
            }

            foreach (var c in normalConsonants) {
                foreach (var v in normalConsonants.Except(GlideVCCons)) {
                    foreach (var b in normalConsonants.Except(NormVCCons)) {
                        if (alias.Contains(c) && !alias.StartsWith(c) &&
                        !alias.Contains("dx") && !alias.Contains($"{c} -")) {
                            if ("b,d,g,k,p,t".Split(',').Contains(c)) {
                                hasCons = true;
                            } else if ("l,r".Split(',').Contains(c)) {
                                haslr = true;
                            } else {
                                return base.GetTransitionBasicLengthMs() * 1.3;
                            }
                        }
                    }
                }
            }

            foreach (var c in connectingNormCons) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains(c) && !alias.Contains("- ") && alias.Contains($"{v} {c}")
                       && !alias.Contains("dx")) {
                        return base.GetTransitionBasicLengthMs() * 1.4;
                    }
                }
            }

            foreach (var c in tapConsonant) {
                foreach (var v in vowels) {
                    if (alias.Contains($"{v} {c}") || alias.Contains(c)) {
                        return base.GetTransitionBasicLengthMs() * 0.5;
                    }
                }
            }

            foreach (var c in affricates) {
                if (alias.Contains(c) && !alias.StartsWith(c)) {
                    return base.GetTransitionBasicLengthMs() * 1.5;
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains($"{v} {c}") && !alias.Contains($"{c} -") && !alias.Contains($"{v} -")) {
                        return base.GetTransitionBasicLengthMs() * 2.1;
                    }
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Where(v => excludedVowels.Contains(v))) {
                    if (alias.Contains($"{v} r")) {
                        return base.GetTransitionBasicLengthMs() * 0.6;

                    }
                }
            }

            foreach (var c in semilongConsonants) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -") && !alias.Contains($"- q")) {
                        return base.GetTransitionBasicLengthMs() * 1.4;
                    }
                }
            }

            foreach (var c in semiVowels) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -")) {
                        return base.GetTransitionBasicLengthMs() * 1.2;
                    }
                }
            }

            if (hasCons) {
                return base.GetTransitionBasicLengthMs() * 1.3; // Value for 'cons'
            } else if (haslr) {
                return base.GetTransitionBasicLengthMs() * 1.2; // Value for 'cons'
            }

            // Check if the alias ends with a consonant or vowel
            foreach (var c in GetConsonants()) {
                if (alias.Contains(c) || alias.EndsWith(" -") || alias.EndsWith(" R")) {
                    isEndingConsonant = true;
                    break;
                }
            }

            foreach (var v in GetVowels()) {
                if (alias.Contains(v) || alias.EndsWith('-') || alias.EndsWith('R')) {
                    isEndingVowel = true;
                    break;
                }
            }

            // If the alias ends with a consonant or vowel, return 0.5 ms
            if (isEndingConsonant || isEndingVowel) {
                return base.GetTransitionBasicLengthMs() * 0.5;
            }

            return base.GetTransitionBasicLengthMs() * transitionMultiplier;
        }
    }
}

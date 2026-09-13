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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.RegularExpressions;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Adrian EN VCV Legacy", "Adrian EN VCV", "Cadlaxa", language: "EN")]
    public class AdrianVCV : SyllableBasedPhonemizer {
        private const string YamlFile = "adrian_envcv.yaml";
        private const string LatestVersion = "1.3";
        private string[] vowels = Array.Empty<string>();
        private string[] consonants = Array.Empty<string>();
        private static string[] affricate = Array.Empty<string>();
        private static string[] fricative = Array.Empty<string>();
        private static string[] aspirate = Array.Empty<string>();
        private static string[] semivowel = Array.Empty<string>();
        private static string[] liquid = Array.Empty<string>();
        private static string[] nasal = Array.Empty<string>();
        private static string[] stop = Array.Empty<string>();
        private static string[] tap = Array.Empty<string>();
        private Dictionary<string, double> PhonemeOverrides = new Dictionary<string, double>();
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
        private readonly Dictionary<string, string> missingVphonemes = "ax=a".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "N=n,pp=p".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;
        private bool cPV_FallBack = false;

        private readonly Dictionary<string, string> vvDiphthongExceptions =
            new Dictionary<string, string>() {
                {"aw","a"},
                {"ow","o"},
                {"iw","i"},
                {"ay","a"},
                {"ey","e"},
                {"oy","o"},
                {"uy","u"},
                {"ew","e"},
            };
        
        private readonly Dictionary<string, string> vvExceptions =
            new Dictionary<string, string>() {
                {"aw","w"},
                {"ow","w"},
                {"iw","w"},
                {"ay","y"},
                {"ey","y"},
                {"oy","y"},
                {"uy","y"},
                {"ew","w"},
            };

        private readonly string[] ccvException = { "ch", "dh", "dx", "fh", "gh", "hh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };
        private readonly string[] RomajiException = { "a", "e", "i", "o", "u" };
        private static readonly string[] FinalConsonants = { "w", "y", "r", "l", "m", "n", "ng" };
        private string[] tails = "-,R".Split(',');
        bool isTails = false;

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (tails.Contains(note.lyric)) {
                isTails = true;
                return new string[] { note.lyric };
            }
            if (original == null) return Array.Empty<string>();

            List<string> modified = new List<string>(original);
            List<string> finalPhonemes = new List<string>();
            int i = 0;
            bool hasReplacements = mergingReplacements.Any() || splittingReplacements.Any();

            while (i < modified.Count) {
                if (i + 1 < modified.Count && modified[i + 1] == "w" && consonants.Contains(modified[i])) {
                    finalPhonemes.Add(ReplacePhoneme(modified[i], note.tone));
                    finalPhonemes.Add("w^");
                    i++;
                    continue;
                }

                bool replaced = false;

                if (hasReplacements) {
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
                                if (rule.to is string toString) finalPhonemes.Add(toString);
                                else if (rule.to is string[] toArray) finalPhonemes.AddRange(toArray);
                                i += fromArray.Length;
                                replaced = true;
                                break;
                            }
                        }
                    }
                }

                if (!replaced) {
                    string currentPhoneme = modified[i];
                    bool singleReplaced = false;

                    // Check for single phoneme splitting
                    foreach (var rule in splittingReplacements.Where(r => r.where == "inside")) {
                        if (rule.from.ToString() == currentPhoneme && rule.to is string[] toArray) {
                            finalPhonemes.AddRange(toArray);
                            singleReplaced = true;
                            break;
                        }
                    }

                    if (!singleReplaced) {
                        finalPhonemes.Add(ReplacePhoneme(currentPhoneme, note.tone));
                    }
                    i++;
                }
            }

            List<string> finalProcessedPhonemes = new List<string>();
            foreach (string s in finalPhonemes) {
                finalProcessedPhonemes.Add(s);
            }

            return finalProcessedPhonemes.ToArray();
        }

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();
            // LOAD DICTIONARY FROM FOLDER
            string path = Path.Combine(PluginDir, YamlFile);
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Adrian_EN_VCV.Data.Resources.template);
            }
            // LOAD DICTIONARY FROM SINGER FOLDER
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, YamlFile);
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            g2ps.Add(new ArpabetPlusG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }
        public override void SetSinger(USinger singer) {
            if (this.singer != singer) {
                string file;
                if (singer != null && singer.Found && singer.Loaded && !string.IsNullOrEmpty(singer.Location)) {
                    file = Path.Combine(singer.Location, YamlFile);
                } else if (!string.IsNullOrEmpty(PluginDir)) {
                    file = Path.Combine(PluginDir, YamlFile);
                } else {
                    Log.Error("Singer location and PluginDir are both null or empty. Cannot locate '" + YamlFile + "'.");
                    return;
                }
                try {
                    bool shouldWriteTemplate = false;
                    bool shouldBackupOldFile = false;

                    if (File.Exists(file)) {
                        try {
                            // Build YAML deserializer
                            var deserializer = new DeserializerBuilder()
                                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                .Build();

                            using var reader = new StreamReader(file);
                            var config = deserializer.Deserialize<Dictionary<string, object>>(reader);

                            if (config == null || !config.ContainsKey("version")) {
                                shouldWriteTemplate = true;
                                shouldBackupOldFile = true; // No version → backup old
                            } else {
                                string currentVersion = config["version"]?.ToString()?.Trim() ?? "";

                                // If version is missing OR outdated → backup old + write new
                                if (string.IsNullOrWhiteSpace(currentVersion) || currentVersion != LatestVersion) {
                                    shouldWriteTemplate = true;
                                    shouldBackupOldFile = true;
                                }
                            }
                        } catch (Exception ex) {
                            Log.Error(ex, $"Failed to read '{file}', backing up old file and writing a fresh one...");
                            shouldWriteTemplate = true;
                            shouldBackupOldFile = true;
                        }
                    } else {
                        shouldWriteTemplate = true;
                    }

                    // If needed, back up the old file
                    if (shouldBackupOldFile && File.Exists(file)) {
                        try {
                            string backupFile = Path.Combine(
                                Path.GetDirectoryName(file)!,
                                $"adrian_envcv_backup.yaml"
                            );
                            File.Move(file, backupFile);
                            Log.Warning($"Old {YamlFile} has been backed up as: {backupFile}");
                        } catch (Exception e) {
                            Log.Error(e, $"Failed to back up old {YamlFile}. Proceeding with new template anyway.");
                        }
                    }

                    // Write a fresh template if necessary
                    if (shouldWriteTemplate) {
                        try {
                            File.WriteAllBytes(file, Adrian_EN_VCV.Data.Resources.template);
                            Log.Information($"'{file}' created or updated to latest version {LatestVersion}");
                        } catch (Exception e) {
                            Log.Error(e, $"Failed to write '{YamlFile}' to {file}");
                        }
                    }
                } catch (Exception ex) {
                    Log.Error(ex, $"Unexpected error while ensuring {YamlFile} at {file}");
                }

                if (File.Exists(file)) {
                    try {
                        var data = Core.Yaml.DefaultDeserializer.Deserialize<AdrianYAMLData>(File.ReadAllText(file));
                        // Load vowels
                        try {
                            var loadVowels = data.symbols
                                ?.Where(s => s.type == "vowel")
                                .Select(s => s.symbol)
                                .ToList() ?? new List<string>();

                            vowels = vowels.Concat(loadVowels).Distinct().ToArray();
                        } catch (Exception ex) {
                            Log.Error($"Failed to load vowels from {YamlFile}: {ex.Message}");
                        }
                        // Load tails
                        try {
                            var loadTails = data.symbols
                                ?.Where(s => s.type == "tail")
                                .Select(s => s.symbol)
                                .ToList() ?? new List<string>();

                            tails = tails.Concat(loadTails).Distinct().ToArray();
                        } catch (Exception ex) {
                            Log.Error($"Failed to load tails from {YamlFile}: {ex.Message}");
                        }
                        // Load stop and tap consonants
                        try {
                            var loadConsonants = data.symbols
                                ?.Where(s => s.type == "stop" || s.type == "tap")
                                .Select(s => s.symbol)
                                .ToList() ?? new List<string>();

                            consExceptions.AddRange(loadConsonants);
                        } catch (Exception ex) {
                            Log.Error($"Failed to load stop and tap consonants from {YamlFile}: {ex.Message}");
                        }
                        // Load the various consonant types 
                        var fricatives = data.symbols
                            ?.Where(s => s.type == "fricative")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var aspirates = data.symbols
                            ?.Where(s => s.type == "aspirate")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var semivowels = data.symbols
                            ?.Where(s => s.type == "semivowel")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var liquids = data.symbols
                            ?.Where(s => s.type == "liquid")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var nasals = data.symbols
                            ?.Where(s => s.type == "nasal")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var stops = data.symbols
                            ?.Where(s => s.type == "stop")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var taps = data.symbols
                            ?.Where(s => s.type == "tap")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        var affricates = data.symbols
                            ?.Where(s => s.type == "affricate")
                            .Select(s => s.symbol)
                            .ToList() ?? new List<string>();

                        PhonemeOverrides = data.timings
                            ?.ToDictionary(t => t.symbol, t => t.value)
                            ?? new Dictionary<string, double>();

                       
                        // Load consonant types into their respective lists
                        fricative = fricatives.Distinct().ToArray();
                        aspirate = aspirates.Distinct().ToArray();
                        semivowel = semivowels.Distinct().ToArray();
                        liquid = liquids.Distinct().ToArray();
                        nasal = nasals.Distinct().ToArray();
                        stop = stops.Distinct().ToArray();
                        tap = taps.Distinct().ToArray();
                        affricate = affricates.Distinct().ToArray();
                        consonants = fricatives
                            .Concat(aspirates)
                            .Concat(semivowels)
                            .Concat(liquids)
                            .Concat(nasals)
                            .Concat(stop)
                            .Concat(tap)
                            .Concat(affricate)
                            .Distinct()
                            .ToArray();
                        // Load replacements
                        try {
                            if (data?.replacements != null && data.replacements.Any() == true) {
                                dictionaryReplacements = new Dictionary<string, string>();
                                mergingReplacements = new List<Replacement>();
                                splittingReplacements = new List<Replacement>();

                                foreach (var replacement in data.replacements) {
                                    try {
                                        string ruleScope = string.IsNullOrEmpty(replacement.where) ? "inside" : replacement.where.ToLowerInvariant();
                                        if (replacement.from != null && replacement.to != null) {
                                            if (replacement.from is IEnumerable<object> fromList) {
                                                // 'from' is a list (e.g., [ae, n])
                                                string[] fromArray = fromList.Select(item => item.ToString()).ToArray();
                                                if (replacement.to is string toString) {
                                                    mergingReplacements.Add(new Replacement { from = fromArray, to = toString, where = ruleScope });
                                                } else if (replacement.to is IEnumerable<object> toList) {
                                                    splittingReplacements.Add(new Replacement { from = fromArray, to = toList.Select(item => item.ToString()).ToArray(), where = ruleScope });
                                                } else {
                                                    Log.Error($"Error: Invalid 'to' type in replacement: {replacement}");
                                                }
                                            } else if (replacement.from is string fromString) {
                                                // 'from' is a single string (e.g., tr, aw, ae, m, ng)
                                                if (replacement.to is string toString) {
                                                    dictionaryReplacements[fromString] = toString;
                                                } else if (replacement.to is IEnumerable<object> toList) {
                                                    splittingReplacements.Add(new Replacement { from = fromString, to = toList.Select(item => item.ToString()).ToArray(), where = ruleScope });
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
                            Log.Error($"Failed to load replacements from {YamlFile}: {ex.Message}");
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
                            Log.Error($"Failed to load fallbacks from {YamlFile}: {ex.Message}");
                        }
                    } catch (Exception ex) {
                       Log.Error($"Failed to parse {YamlFile}: {ex.Message}, content: {File.ReadAllText(file)}, Exception Type: {ex.GetType()}");
                    }
                }
                this.singer = singer;
                ReadDictionaryAndInit();
            }
        }
        public class AdrianYAMLData {
            public SymbolData[] symbols { get; set; } = Array.Empty<SymbolData>();
            public Replacement[] replacements { get; set; } = Array.Empty<Replacement>();
            public Fallbacks[] fallbacks { get; set; } = Array.Empty<Fallbacks>();
            public Timings[] timings { get; set; } = Array.Empty<Timings>();

            public struct SymbolData {
                public string symbol { get; set; }
                public string type { get; set; }
            }
            public struct Fallbacks {
                public string from { get; set; }
                public string to { get; set; }
            }
            public struct Timings {
                public string symbol { get; set; }
                public double value { get; set; }
            }
        }
        // can split or merge
        public class Replacement {
            public object from { get; set; }
            public object to { get; set; }
            public string where { get; set; } = "inside";

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
            
            string[] CurrentWordCc = syllable.CurrentWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string[] PreviousWordCc = syllable.PreviousWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            int prevWordConsonantsCount = syllable.prevWordConsonantsCount;

            
            if (cc.Length > 1 && (cc[0] == "u" || cc[0] == "i" || cc[0] == "U" || cc[0] == "I")) {
                if (prevWordConsonantsCount == 0 && CurrentWordCc.Length == 0) {
                } else if (syllable.IsVV) {
                    
                } else if (prevWordConsonantsCount >= 0 && CurrentWordCc.Length >= 1) {
                    prevV = (cc[0] == "y") ? "i" : (cc[0] == "w") ? "u" : cc[0];
                    cc = cc.Skip(1).ToArray();
                } else if (prevWordConsonantsCount > 0 && CurrentWordCc.Length == 0 && cc.Length > 1) {
                    prevV = (cc[0] == "y") ? "i" : (cc[0] == "w") ? "u" : cc[0];
                    cc = cc.Skip(1).ToArray();
                } else if (prevWordConsonantsCount > 1 && CurrentWordCc.Length == 0 && cc.Length > 1) {
                    prevV = (cc[0] == "y") ? "i" : (cc[0] == "w") ? "u" : cc[0];
                    cc = cc.Skip(1).ToArray();
                }
            }

            var lastC = cc.Length - 1;
            var firstC = 0;

            string GetFallbackCV(string defaultCv) {
                if (cc.Length >= 2 && (cc[cc.Length - 2] == "w^" || cc[cc.Length - 2] == "u^") && cc.Last() == "w") {
                    string uwv = $"u {cc.Last()} {v}";
                    string uwv_nospace = $"u {cc.Last()}{v}";
                    
                    if (HasOto(uwv, syllable.vowelTone) || HasOto(ValidateAlias(uwv), syllable.vowelTone)) 
                        return uwv;
                    if (HasOto(uwv_nospace, syllable.vowelTone) || HasOto(ValidateAlias(uwv_nospace), syllable.vowelTone)) 
                        return uwv_nospace;
                }
                return AliasFormat(defaultCv, "dynMid", syllable.vowelTone, "");
            }

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

            // STARTING V
            if (syllable.IsStartingV) {
                basePhoneme = AliasFormat(v, "startingV", syllable.vowelTone, "");
            }
            // [V V] or [V C][C V]/[V]
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    basePhoneme = $"{prevV} {v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) || !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{v}", "dynMid", syllable.vowelTone, "");
                    } else {
                        if (HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone)) {
                            basePhoneme = $"{prevV} {v}";
                        } else if (HasOto($"{prevV}{v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV}{v}"), syllable.vowelTone)) {
                            basePhoneme = $"{prevV}{v}";
                        } else if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) {
                            basePhoneme = v;
                        } else {
                            basePhoneme = AliasFormat($"- {v}", "dynMid", syllable.vowelTone, "");
                            TryAddPhoneme(phonemes, syllable.vowelTone, AliasFormat($"{prevV} -", "dynMid", syllable.vowelTone, ""));
                        }
                    }
                } else {
                    // PREVIOUS ALIAS WILL EXTEND as [V V]
                    basePhoneme = null;
                }

                // [- CV/C V] or [- C][CV/C V]
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var space = $"{cc[0]} {v}";
                var noSpace = $"{cc[0]}{v}";
                var rcv = AliasFormat(space, "dynMid", syllable.vowelTone, "");
                var rcv1 = AliasFormat(noSpace, "dynMid", syllable.vowelTone, "");
                var crv = $"{cc[0]} {v}";
                /// - CV
                if (HasOto(rcv, syllable.vowelTone) && HasOto(ValidateAlias(rcv), syllable.vowelTone) || (HasOto(rcv1, syllable.vowelTone) && HasOto(ValidateAlias(rcv1), syllable.vowelTone))) {
                    basePhoneme = rcv;
                } else if (HasOto(rcv1, syllable.vowelTone) && HasOto(ValidateAlias(rcv1), syllable.vowelTone) || (HasOto(rcv, syllable.vowelTone) && HasOto(ValidateAlias(rcv), syllable.vowelTone))) {
                    basePhoneme = rcv1;
                    /// CV
                } else if (HasOto(crv, syllable.vowelTone) && HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                } else {
                    basePhoneme = AliasFormat($"{cc[0]} {v}", "dynMid", syllable.vowelTone, "");
                }
                // [CCV/CC V] or [C C] + [CV/C V]
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                var crv = $"-{cc.Last()}{v}";
                var crv1 = $"{cc.Last()}{v}";
                var ccv = $"-{string.Join("", cc)}{v}";
                var ccv1 = $"{string.Join("", cc)}{v}";
                var rccv = AliasFormat(ccv, "dynMid", syllable.vowelTone, "");
                var rccv1 = AliasFormat(ccv1, "dynMid", syllable.vowelTone, "");
                /// - CCV
                if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone) || HasOto(rccv1, syllable.vowelTone) || HasOto(ValidateAlias(rccv1), syllable.vowelTone) && !ccvException.Contains(cc[0])) {
                    basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                    lastC = 0;
                } else {
                    /// CCV and CV
                    if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                        basePhoneme = AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                        lastC = 0;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(crv1, syllable.vowelTone) || HasOto(ValidateAlias(crv1), syllable.vowelTone)) {
                        basePhoneme = GetFallbackCV($"-{cc.Last()}{v}");
                    } else {
                        basePhoneme = GetFallbackCV($"{cc.Last()}{v}"); 
                    }
                    // TRY RCC [- CC]
                    for (var i = cc.Length; i > 1; i--) {
                        if (!ccvException.Contains(cc[0])) {
                            if (TryAddPhoneme(phonemes, syllable.tone, AliasFormat($"{string.Join("", cc.Take(i))}", "cc_start", syllable.vowelTone, ""), AliasFormat($"{cc[0]}", "cc_start", syllable.vowelTone, ""))) {
                                firstC = i - 1;
                            }
                        }
                        break;
                    }
                    // try [CC V] or [CCV]
                    var cv = $"{cc.Last()}{v}";
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        /// CCV
                        if (CurrentWordCc.Length >= 2) {
                            if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"-{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                                lastC = i;
                                break;
                            }
                            /// C-Last
                        } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                            if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                                basePhoneme = GetFallbackCV($"-{cc.Last()} {v}");
                            } else {
                                basePhoneme = GetFallbackCV($"{cc.Last()} {v}");
                            }
                        }
                    }
                }
            } else { // VCV
                var vcv = $"{prevV} {cc[0]}{v}";
                var vcv1 = $"{cc[0]} {v}";
                var vcv2 = $"{prevV} {cc[0]}{v}";
                var vcvEnd = $"{prevV}{cc[0]} {v}";
                var vccv = $"{prevV} {string.Join("", cc)}{v}";
                var vccv2 = $"{prevV} {string.Join("", cc)}";
                var vccv3 = $"{prevV}{string.Join("", cc)}";
                var crv = $"-{cc.Last()}{v}";
                // Use regular VCV if the current word starts with one consonant and the previous word ends with none
                if (syllable.IsVCVWithOneConsonant && (HasOto(vcv, syllable.vowelTone) || HasOto(ValidateAlias(vcv), syllable.vowelTone))) {
                    basePhoneme = vcv;
                } else if (syllable.IsVCVWithOneConsonant && (HasOto(vcv1, syllable.vowelTone) || HasOto(ValidateAlias(vcv1), syllable.vowelTone))) {
                    basePhoneme = vcv1;
                    // Use end VCV if current word does not start with a consonant but the previous word does end with one
                } else if (syllable.IsVCVWithOneConsonant && prevWordConsonantsCount == 1 && CurrentWordCc.Length == 0 && (HasOto(vcvEnd, syllable.vowelTone) || HasOto(ValidateAlias(vcvEnd), syllable.vowelTone))) {
                    basePhoneme = vcvEnd;
                    // VCV with multiple consonants, only for current word onset and null previous word ending
                } else if (syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccv, syllable.vowelTone) || HasOto(ValidateAlias(vccv), syllable.vowelTone))) {
                    basePhoneme = vccv;
                    lastC = 0;
                } else {
                    var cv = $"-{cc.Last()}{v}";
                    /// CV
                    if (HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                        basePhoneme = GetFallbackCV($"-{cc.Last()} {v}"); // Modified
                    } else {
                        basePhoneme = GetFallbackCV($"{cc.Last()} {v}");  // Modified
                    }
                    // try [CC V] or [CCV]
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        var ccv = $"-{string.Join("", cc)}{v}";
                        var ccv1 = $"{string.Join("", cc)}{v}";
                        /// CCV
                        if (CurrentWordCc.Length >= 2) {
                            if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"-{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, "");
                                lastC = i;
                                break;
                            }
                            /// C-Last
                        } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                            if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone) || HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone)) {
                                basePhoneme = AliasFormat($"-{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            } else {
                                basePhoneme = AliasFormat($"{cc.Last()} {v}", "dynMid", syllable.vowelTone, "");
                            }
                        }
                    }
                    // try [V C], [V CC], [VC C], [V -][- C]
                    for (var i = lastC + 1; i >= 0; i--) {
                        var vr = $"{prevV} -";
                        var vc = cc.Length > 0 ? $"{prevV} {cc[0]}" : "";
                        var vc_c = cc.Length > 1 ? $"{prevV}{cc[0]} {cc[1]}" : "";
                        var vc_c2 = cc.Length > 1 ? $"{prevV} {cc[0]}{cc[1]}" : "";
                        var vcc = cc.Length > 1 ? $"{prevV} {cc[0]}{cc[1]}" : "";
                        // Boolean Triggers
                        bool CCV = false;
                        if (CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                            if (HasOto(AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                                CCV = true;
                            }
                        }

                        if (i == 0 && (HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone)) && !HasOto(vc, syllable.tone)) {
                            TryAddPhoneme(phonemes, syllable.tone, vr, ValidateAlias(vr));
                            break;
                        } else if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc), syllable.tone)) && CCV) {
                            TryAddPhoneme(phonemes, syllable.tone, vcc, ValidateAlias(vcc));
                            firstC = 1; 
                            break;
                        } else if (HasOto(vc_c, syllable.tone) || HasOto(ValidateAlias(vc_c), syllable.tone)) {
                            phonemes.Add(AliasFormat($"{prevV} {cc[0]} {cc[1]}", "dynMid", syllable.vowelTone, ""));
                            firstC = 1; 
                            break;
                        } else if (HasOto(vc_c2, syllable.tone) || HasOto(ValidateAlias(vc_c2), syllable.tone)) {
                            phonemes.Add(AliasFormat($"{prevV} {cc[0]}{cc[1]}", "dynMid", syllable.vowelTone, ""));
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

                // CC Endings (trailing)
                if (isTails && basePhoneme != null && basePhoneme.Contains("-")) {
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
                            string[] endPatterns = { "-", $" -" };
                            foreach (var end in endPatterns) {
                                string endingcc = $"{consPattern}{end}";

                                if (HasOto(endingcc, syllable.tone)) {
                                    basePhoneme = endingcc;
                                    lastC = 0;
                                } else {
                                    continue;
                                }
                            }
                        }
                    }
                }
            }

            for (var i = firstC; i < lastC; i++) {
                if (i < cc.Length - 2) {
                    var c_cc = $"{cc[i]} {cc[i + 1]}{cc[i + 2]}";
                    var c_cc_dash = $"{cc[i]} {cc[i + 1]}{cc[i + 2]}-";

                    if (HasOto(c_cc_dash, syllable.tone) || HasOto(ValidateAlias(c_cc_dash), syllable.tone)) {
                        TryAddPhoneme(phonemes, syllable.tone, c_cc_dash, ValidateAlias(c_cc_dash));
                        i++;
                        continue;
                    } else if (HasOto(c_cc, syllable.tone) || HasOto(ValidateAlias(c_cc), syllable.tone)) {
                        TryAddPhoneme(phonemes, syllable.tone, c_cc, ValidateAlias(c_cc));
                        i++; 
                        continue;
                    }
                }

                var ccv = $"{string.Join("", cc.Skip(i + 1))}{v}";
                var ccv1 = $"-{string.Join("", cc.Skip(i + 1))}{v}";
                var Nccv = $"{string.Join("", cc.Skip(i))}{v}";
                var Nccv1 = $"-{string.Join("", cc.Skip(i))}{v}";
                var cc1 = $"{string.Join(" ", cc.Skip(i))}";
                var lcv = $"{cc.Last()} {v}";
                var cv = $"-{cc.Last()}{v}";
                var vcv = $"{string.Join(" ", cc)}{v}";

                if (!HasOto(cc1, syllable.tone)) cc1 = ValidateAlias(cc1);
                
                // [C1 C2]
                if (!HasOto(cc1, syllable.tone)) cc1 = $"{cc[i]} {cc[i + 1]}";
                if (!HasOto(cc1, syllable.tone)) cc1 = ValidateAlias(cc1);
                if (!HasOto(cc1, syllable.tone)) cc1 = $"{cc[i]}{cc[i + 1]}";
                if (!HasOto(cc1, syllable.tone)) cc1 = ValidateAlias(cc1);
                
                // CCV
                if (CurrentWordCc.Length >= 2) {
                    if (HasOto(Nccv, syllable.vowelTone) || HasOto(ValidateAlias(Nccv), syllable.vowelTone) || HasOto(Nccv1, syllable.vowelTone) || HasOto(ValidateAlias(Nccv1), syllable.vowelTone)) {
                        basePhoneme = GetFallbackCV($"-{string.Join("", cc.Skip(i))}{v}");
                        lastC = i;
                    } else if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone) || HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                        basePhoneme = GetFallbackCV($"-{string.Join("", cc.Skip(i + 1))}{v}");
                        lastC = i;
                    } else if (HasOto(cv, syllable.vowelTone) || HasOto(ValidateAlias(cv), syllable.vowelTone) || HasOto(lcv, syllable.vowelTone) || HasOto(ValidateAlias(lcv), syllable.vowelTone) && HasOto(cc1, syllable.vowelTone) && !HasOto(ccv, syllable.vowelTone)) {
                        basePhoneme = GetFallbackCV($"-{cc.Last()}{v}");
                    }
                    // CV
                } else if (CurrentWordCc.Length == 1 && PreviousWordCc.Length == 1) {
                    basePhoneme = GetFallbackCV($"-{cc.Last()}{v}");
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                }
                
                // C+V
                if ((HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) && (!HasOto(lcv, syllable.vowelTone) && !HasOto(ValidateAlias(lcv), syllable.vowelTone) && (!HasOto(cv, syllable.vowelTone) && !HasOto(ValidateAlias(cv), syllable.vowelTone)))) {
                    cPV_FallBack = true;
                    basePhoneme = v;
                    cc1 = ValidateAlias(cc1);
                }

                if (i + 1 < lastC) {
                    if (HasOto(cc1, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        TryAddPhoneme(phonemes, syllable.vowelTone, cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        if (cc1.Contains($"{string.Join(" ", cc.Skip(i + 1))}")) {
                            i++;
                        }
                    } else {
                        TryAddPhoneme(phonemes, syllable.tone, cc[i], ValidateAlias(cc[i]));
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
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR), ending.tone) || 
                    HasOto(vR1, ending.tone) || HasOto(ValidateAlias(vR1), ending.tone) || 
                    HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)) {
                    TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v}", "ending", ending.tone, ""), ValidateAlias(AliasFormat($"{v}", "ending", ending.tone, "")));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{v} {cc[0]}";
                var vcr = $"{v} {cc[0]}-";
                var vcr2 = $"{v}{cc[0]} -";
                var vcr3 = $"{v}{cc[0]}-";
                
                if (!RomajiException.Contains(cc[0])) {
                    if ((HasOto(vcr, ending.tone) || HasOto(ValidateAlias(vcr), ending.tone)) || 
                        (HasOto(vcr2, ending.tone) || HasOto(ValidateAlias(vcr2), ending.tone)) || 
                        (HasOto(vcr3, ending.tone) || HasOto(ValidateAlias(vcr3), ending.tone))) {
                        TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, ""), ValidateAlias(AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, "")));
                    } else {
                        phonemes.Add(vc);
                        if (vc.Contains(cc[0])) {
                            if (cc[0] != "r" && cc[0] != "3") {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[0]}", "ending", ending.tone, ""));
                            }
                        }
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr = $"{v} -";
                    var vr1 = $"{v} R";
                    var vr2 = $"{v}-";
                    var vc = $"{v} {cc[0]}";
                    
                    if (!RomajiException.Contains(cc[0])) {
                        if (i == 0) {
                            if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) || 
                                HasOto(vr2, ending.tone) || HasOto(ValidateAlias(vr2), ending.tone) || 
                                HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone) && !HasOto(vc, ending.tone)) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{v}", "ending", ending.tone, ""));
                            }
                            break;
                        }

                        // Explicit formats to safely handle uh nt and uh nts
                        var vcc = $"{v} {string.Join("", cc.Take(2))}-";
                        var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} -";
                        var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                        var vcc4 = $"{v} {string.Join("", cc.Take(2))}"; // "uh nt"
                        var vcc5 = cc.Length >= 3 ? $"{v} {string.Join("", cc.Take(3))}" : ""; // "uh nts"
                        var vcc7 = $"{v} {string.Join("", cc.Take(3))}"; 
                        var vcc6 = $"{v} {string.Join(" ", cc.Take(2))}"; // "uh n t"

                        // Fixed && ValidateAlias bugs -> || ValidateAlias
                        if (cc.Length >= 3 && (HasOto(vcc5, ending.tone) || HasOto(ValidateAlias(vcc5), ending.tone)) && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc5);
                            firstC = 2;
                            break;
                        } else if ((HasOto(vcc7, ending.tone) || HasOto(ValidateAlias(vcc7), ending.tone))) {
                            phonemes.Add(vcc7);
                            firstC = 2;
                            break;
                        } else if ((HasOto(vcc, ending.tone) || HasOto(ValidateAlias(vcc), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc);
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc2, ending.tone) || HasOto(ValidateAlias(vcc2), ending.tone)) && lastC == 1 && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc2);
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc3, ending.tone) || HasOto(ValidateAlias(vcc3), ending.tone)) && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc3);
                            if (vcc3.EndsWith(cc.Last()) && lastC == 1 && consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            }
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc4, ending.tone) || HasOto(ValidateAlias(vcc4), ending.tone))) {
                            phonemes.Add(vcc4);
                            if (vcc4.EndsWith(cc.Last()) && lastC == 1 && consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            }
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc6, ending.tone) || HasOto(ValidateAlias(vcc6), ending.tone)) && !ccvException.Contains(cc[0])) {
                            phonemes.Add(vcc6);
                            if (vcc6.EndsWith(cc.Last()) && lastC == 1 && consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            }
                            firstC = 1;
                            break;
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, vc);
                            break;
                        }
                    }
                }
                
                for (var i = firstC; i < lastC; i++) {
                    if (i < cc.Length - 2) {
                        var c_cc = $"{cc[i]} {cc[i + 1]}{cc[i + 2]}";
                        var c_cc_dash = $"{cc[i]} {cc[i + 1]}{cc[i + 2]}-";

                        if (TryAddPhoneme(phonemes, ending.tone, c_cc_dash, ValidateAlias(c_cc_dash))) {
                            i++; 
                            continue;
                        } else if (TryAddPhoneme(phonemes, ending.tone, c_cc, ValidateAlias(c_cc))) {
                            i++; 
                            continue;
                        }
                    }

                    var cc1 = $"{cc[i]} {cc[i + 1]}";
                    
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                        if (!HasOto(cc1, ending.tone)) cc1 = ValidateAlias(cc1);
                        if (!HasOto(cc2, ending.tone)) cc2 = ValidateAlias(cc2);

                        if (!HasOto(cc2, ending.tone) && !HasOto($"{cc[i + 1]} {cc[i + 2]}", ending.tone)) {
                            cc2 = AliasFormat($"{cc[i + 2]}", "cc_inB", ending.tone, "");
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_endB", ending.tone, ""));
                        }
                        
                        if (HasOto(cc1, ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            TryAddPhoneme(phonemes, ending.tone, cc1);
                        } else if ((HasOto(cc[i], ending.tone) || HasOto(ValidateAlias(cc[i]), ending.tone)) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            TryAddPhoneme(phonemes, ending.tone, cc[i]);
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} {cc[i + 2]}-", ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"))) {
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_endB", ending.tone, ""));
                            i++;
                        } else {
                            TryAddPhoneme(phonemes, ending.tone, cc[i], ValidateAlias(cc[i]), $"{cc[i]} -", ValidateAlias($"{cc[i]} -"));
                            TryAddPhoneme(phonemes, ending.tone, cc[i + 1], ValidateAlias(cc[i + 1]), $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"));
                            i++;
                        }
                    } else {
                        if (!HasOto(cc1, ending.tone)) cc1 = ValidateAlias(cc1);
                        if (!HasOto(cc1, ending.tone)) cc1 = $"{cc[i]} {cc[i + 1]}";
                        if (!HasOto(cc1, ending.tone)) cc1 = ValidateAlias(cc1);

                        if ((!HasOto(cc1, ending.tone) || !HasOto(ValidateAlias(cc1), ending.tone)) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            cc1 = AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, "");
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i]}", "cc_endB", ending.tone, ""));
                        }
                        
                        if (!HasOto(cc1, ending.tone)) cc1 = ValidateAlias(cc1);

                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-"))) {
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]} -", ValidateAlias($"{cc[i]} {cc[i + 1]} -"))) {
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]}-", ValidateAlias($"{cc[i]}{cc[i + 1]}-"))) {
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]} -", ValidateAlias($"{cc[i]}{cc[i + 1]} -"))) {
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_endB", ending.tone, "")); // Fixed index out of bounds bug (was cc[i + 2])
                            i++;
                        }
                    }
                }
            }
            return phonemes;
        }

        private string AliasFormat(string alias, string type, int tone, string prevV) {
            // (Your existing AliasFormat code remains unchanged)
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

            if (!aliasFormats.ContainsKey(type) && !type.Contains("dynamic")) {
                return alias;
            }

            if (type.Contains("dynStart")) {
                string consonant = "";
                string vowel = "";
                if (alias.Contains(" ")) {
                    var parts = alias.Split(' ');
                    consonant = parts[0];
                    vowel = parts[1];
                } else {
                    consonant = alias;
                }

                var dynamicVariations = new List<string> {
                    $"- {consonant}{vowel}",
                    $"- {consonant} {vowel}",
                    $"-{consonant} {vowel}",
                    $"-{consonant}{vowel}",
                    $"-{consonant}_{vowel}",
                    $"- {consonant}_{vowel}",
                };
                foreach (var variation in dynamicVariations) {
                    if (HasOto(variation, tone) || HasOto(ValidateAlias(variation), tone)) {
                        return variation;
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
                    $"{consonant}{vowel}",
                    $"{consonant} {vowel}",
                    $"{consonant}_{vowel}",
                };
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
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
                    $"{vowel}{consonant} -",
                    $"{vowel} {consonant}-",
                    $"{vowel}{consonant}-",
                    $"{vowel} {consonant} -",
                };
                foreach (var variation1 in dynamicVariations1) {
                    if (HasOto(variation1, tone) || HasOto(ValidateAlias(variation1), tone)) {
                        return variation1;
                    }
                }
            }

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
                if (HasOto(aliasFormat, tone) || HasOto(ValidateAlias(aliasFormat), tone)) {
                    return aliasFormat;
                }
            }
            return alias;
        }

        protected override string ValidateAlias(string alias) {

            // VALIDATE ALIAS DEPENDING ON METHOD
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

            if (alias.Contains("w^")) {
                alias = alias.Replace("w^", "u");
            }
            if (alias.Contains("y^")) {
                alias = alias.Replace("y^", "i");
            }
            return base.ValidateAlias(alias);
        }

        protected override bool NoGap => true;
        protected override double GetTransitionBasicLengthMs(string alias, int tone, PhonemeAttributes attr) {
            double otoLength = GetTransitionBasicLengthMsByOto(alias, tone, attr);

            var parts = alias.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool isVcv = false;

            if (parts.Length == 2) {
                var startingVowels = vowels; 
                var endingVowels = vowels.ToArray();
                
                if (startingVowels.Contains(parts[0])) {
                    string cv = parts[1];
                    
                    
                    bool isRomajiVcv = endingVowels.Any(v => cv.EndsWith(v));
                    bool isJapaneseVcv = cv.Any(c => c > 0xFF);

                    if (isRomajiVcv || isJapaneseVcv) {
                        isVcv = true;
                    }
                }
            }

            if (isVcv) {
                return GetTransitionBasicLengthMsByConstant() * 1.3;
            }

            return otoLength;
        }
    }
}

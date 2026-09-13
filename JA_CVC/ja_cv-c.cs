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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Text;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Japanese CV-C Phonemizer", "JA CV-C", "Cadlaxa", language: "JA")]
    public class JACVC : SyllableBasedPhonemizer {
        protected override string YamlFileName => "ja-cvc.yaml";
        protected override byte[] YamlTemplate => JA_CVC.Data.Resources.template;
        protected override string YamlVersion => "1.4";
        private static readonly Dictionary<string, string> hiraToRoma = new Dictionary<string, string> {
            {"りゃ","rya"}, {"りぇ","rye"}, {"りゅ","ryu"}, {"りょ","ryo"},
            {"ぴゃ","pya"}, {"ぴぇ","pye"}, {"ぴゅ","pyu"}, {"ぴょ","pyo"},
            {"ふゃ","ffya"}, {"ふゅ","ffyu"}, {"ふぇ","fe"},
            {"ぎゃ","gya"}, {"ぎゅ","gyu"}, {"ぎぇ","gye"}, {"ぎょ","gyo"},
            {"ひゃ","hhya"}, {"ひゅ","hhyu"}, {"ひぇ","hhye"}, {"ひょ","hhyo"},
            {"きゃ","kya"}, {"きぇ","kye"}, {"きゅ","kyu"}, {"きょ","kyo"},
            {"びゃ","bya"}, {"びゅ","byu"}, {"びぇ","bye"}, {"びょ","byo"},
            {"にゃ","nya"}, {"にゅ","nyu"}, {"にぇ","nye"}, {"にょ","nyo"},
            {"みゃ","mya"}, {"みゅ","myu"}, {"みぇ","mye"}, {"みょ","myo"},
            {"てぃ","ti"}, {"てゅ","tyu"}, {"てぇ","tye"}, {"てょ","tyo"}, {"てゃ","tya"},
            {"でぃ","di"}, {"でゅ","dyu"}, {"でぇ","dye"}, {"でょ","dyo"}, {"でゃ","dya"},
            {"づぁ","zwa"}, {"づぇ","zwe"}, {"づぉ","zwo"},
            {"づ","zu"}, {"ぢ","zwi"},
            {"か","ka"}, {"き","ki"}, {"く","ku"}, {"け","ke"}, {"こ","ko"},
            {"た","ta"}, {"とぅ","tu"}, {"て","te"}, {"と","to"},
            {"ど","do"}, {"だ","da"}, {"どぅ","du"}, {"で","de"},
            {"つぁ","tsa"}, {"つぃ","tsi"}, {"つぇ","tse"}, {"つぉ","tso"}, {"つ","tsu"},
            {"さ","sa"}, {"すぃ","si"}, {"す","su"}, {"せ","se"}, {"そ","so"},
            {"ふぁ","fa"}, {"ふぃ","fi"}, {"ふぉ","fo"}, {"ふ","fu"},
            {"が","ga"}, {"ぎ","gi"}, {"ぐ","gu"}, {"げ","ge"}, {"ご","go"},
            {"じゃ","ja"}, {"じゅ","ju"}, {"じぇ","je"}, {"じょ","jo"}, {"じ","ji"},
            {"ざ","dza"}, {"ずぃ","dzi"}, {"ず","dzu"}, {"ぜ","dze"}, {"ぞ","dzo"},
            {"ヴぁ","va"}, {"ヴぃ","vi"}, {"ヴぇ","ve"}, {"ヴぉ","vo"}, {"ヴ","vu"},
            {"ば","ba"}, {"び","bi"}, {"ぶ","bu"}, {"べ","be"}, {"ぼ","bo"},
            {"ぱ","pa"}, {"ぴ","pi"}, {"ぷ","pu"}, {"ぺ","pe"}, {"ぽ","po"},
            {"な","na"}, {"に","ni"}, {"ぬ","nu"}, {"ね","ne"}, {"の","no"},
            {"ま","ma"}, {"み","mi"}, {"む","mu"}, {"め","me"}, {"も","mo"},
            {"ら","ra"}, {"り","ri"}, {"る","ru"}, {"れ","re"}, {"ろ","ro"},
            {"わ","wa"},
            {"うぃ","wi"}, {"うぅ","wu"}, {"うぇ","we"}, {"うぉ","wo"},
            {"や","ya"}, {"ゆ","yu"}, {"いぇ","ye"}, {"よ","yo"}, {"いぃ","yi"},
            {"しゃ","sha"}, {"しゅ","shu"}, {"しぇ","she"}, {"しょ","sho"}, {"し","shi"},
            {"ちゃ","cha"}, {"ちゅ","chu"}, {"ちぇ","che"}, {"ちょ","cho"}, {"ち","chi"},
            {"は","ha"}, {"ひ","hhi"}, {"へ","he"}, {"ほぅ","hu"}, {"ほ","ho"},
            {"あ","a"}, {"い","i"}, {"う","u"}, {"え","e"}, {"お","o"},
            {"ん","ん"},

            {"ガ","nga"}, {"ギ","ngi"}, {"グ","ngu"}, {"ゲ","nge"}, {"ゴ","ngo"},
            {"ザ","za"}, {"ジ","zi"}, {"ズ","zu"}, {"ゼ","ze"}, {"ゾ","zo"},
            {"くぁ","kwa"},{"くぃ","kwi"},{"くぅ","kwu"},{"くぇ","kwe"},{"くぉ","kwo"},

            // s + y
            {"すゃ","sya"}, {"すゅ","syu"}, {"すぃぇ","sye"}, {"すょ","syo"},
            {"ずゃ","dzya"}, {"ずゅ","dzyu"}, {"ずぃぇ","dzye"}, {"ずょ","dzyo"},

            // s + w
            {"すぁ","swa"}, {"すぅぃ","swi"}, {"すぇ","swe"}, {"すぉ","swo"},
            {"ずぁ","dzwa"}, {"ずぅぃ","dzwi"}, {"ずぇ","dzwe"}, {"ずぉ","dzwo"},

            // zh
            {"ぢゃ","zha"}, {"ぢゅ","zhu"}, {"ぢぇ","zhe"}, {"ぢょ","zho"},
            {"ジゃ","zha"}, {"ジゅ","zhu"}, {"ジぇ","zhe"}, {"ジょ","zho"},
            {"ジャ","zha"}, {"ジュ","zhu"}, {"ジェ","zhe"}, {"ジョ","zho"},

            // z + y
            {"づゃ","zya"}, {"づゅ","zyu"}, {"づぃぇ","zye"}, {"づょ","zyo"},
            {"ズゃ","zya"}, {"ズゅ","zyu"}, {"ズぃぇ","zye"}, {"ズょ","zyo"},
            {"ズャ","zya"}, {"ズュ","zyu"}, {"ズィェ","zye"}, {"ズョ","zyo"},

            // t + y
            {"てぃぇ","tye"},

            // d + y
            {"でぃぇ","dye"},

            // n
            {"ぬぁ","nwa"}, {"ぬぃ","nwi"}, {"ぬぇ","nwe"}, {"ぬょ","nwo"},

            // f
            {"ふぃぇ","ffye"}, {"ふょ","ffyo"},
            {"ファ","ffwa"}, {"フィ","ffwi"}, {"フェ","ffwe"}, {"フォ","ffwo"},
            {"フぁ","ffwa"}, {"フぃ","ffwi"}, {"フぇ","ffwe"}, {"フぉ","ffwo"},

            // b
            {"ぶぁ","bwa"}, {"ぶぃ","bwi"}, {"ぶぇ","bwe"}, {"ぶぉ","bwo"},

            // p
            {"ぷぁ","pwa"}, {"ぷぃ","pwi"}, {"ぷぇ","pwe"}, {"ぷぉ","pwo"},

            // m
            {"むぁ","mwa"}, {"むぃ","mwi"}, {"むぇ","mwe"}, {"むぉ","mwo"},

            // r
            {"るぁ","rwa"}, {"るぃ","rwi"}, {"るぇ","rwe"}, {"るぉ","rwo"},

            // v
            {"ヴゃ","vya"}, {"ヴゅ","vyu"}, {"ヴぃぇ","vye"}, {"ヴょ","vyo"},

            //g
            {"ぐぁ","gwa"}, {"ぐぃ","gwi"}, {"ぐぇ","gwe"}, {"ぐぉ","gwo"},


        };
        private static readonly Dictionary<string, string> hiraToKana = new Dictionary<string, string> {
            {"ガ","nga"}, {"ギ","ngi"}, {"グ","ngu"}, {"ゲ","nge"}, {"ゴ","ngo"},
            {"ザ","za"}, {"ジ","zi"}, {"ズ","zu"}, {"ゼ","ze"}, {"ゾ","zo"},
            {"ジゃ","zha"}, {"ジゅ","zhu"}, {"ジぇ","zhe"}, {"ジょ","zho"},
            {"ジャ","zha"}, {"ジュ","zhu"}, {"ジェ","zhe"}, {"ジョ","zho"},
            {"ズゃ","zya"}, {"ズゅ","zyu"}, {"ズぃぇ","zye"}, {"ズょ","zyo"},
            {"ズャ","zya"}, {"ズュ","zyu"}, {"ズィェ","zye"}, {"ズョ","zyo"},
            {"ファ","ffwa"}, {"フィ","ffwi"}, {"フェ","ffwe"}, {"フォ","ffwo"},
            {"フぁ","ffwa"}, {"フぃ","ffwi"}, {"フぇ","ffwe"}, {"フぉ","ffwo"},
            
        };
        private string[] vowels = {
        "a", "i", "u", "e", "o", "N", "ん"
        };
        private string[] JAvowels = {
        "a", "i", "u", "e", "o", "N", "ん"
        };
        private string[] consonants = "b,ch,d,dz,f,g,h,hh,j,k,l,m,n,ng,p,r,s,sh,t,ts,v,w,y,z".Split(',');
        private Dictionary<string, double> PhonemeOverrides = new Dictionary<string, double>();
       protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "";
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

        // For banks with missing vowels
        private Dictionary<string, string> missingVphonemes = "A=a".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private Dictionary<string, string> missingCphonemes = "ん=n".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

        // TIMIT symbols
        private Dictionary<string, string> timitphonemes = "axh=ax,bcl=b,dcl=d,eng=ng,gcl=g,hv=hh,kcl=k,pcl=p,tcl=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isTimitPhonemes = false;
        private bool cPV_FallBack = false;
        private Dictionary<string, string> dictionaryReplacements;
        
        private readonly Dictionary<string, string> vcFallBacks =
        new Dictionary<string, string>() {
            {"aw","u"},
            {"ow","u"},
            {"uh","u"},
            {"ay","i"},
            {"ey","i"},
            {"oy","i"},
            {"aa","a"},
            {"ae","h"},
            {"ao","a"},
            {"i","i"},
            {"u","u"},
            {"a","aa"},
            {"e","eh"},
            {"o","ao"},
            //{"eh","ah"},
            //{"er","ah"},
        };

        private readonly Dictionary<string, string> vvExceptions =
        new Dictionary<string, string>() {
            {"aw","w"},
            {"ow","w"},
            {"uw","w"},
            {"uh","w"},
            {"ay","y"},
            {"ey","y"},
            {"iy","y"},
            {"oy","y"},
            {"ih","y"},
            {"er","r"},
            {"aar","r"},
            {"aen","n"},
            {"aeng","ng"},
            {"aor","r"},
            {"ehr","r"},
            {"ihng","ng"},
            {"ihr","r"},
            {"uwr","r"},
            {"awn","n"},
            {"awng","ng"},
            {"ean","n"},
            {"eam","m"},
            {"eang","ng"},
            // r-colored vowel and l
            {"ar","r"},
            {"or","r"},
            {"air","r"},
            {"ir","r"},
            {"ur","r"},
            {"al","l"},
            {"ol","l"},
            {"il","l"},
            {"el","l"},
            {"ul","l"},
        };


        private readonly string[] ccvException = { "ch", "dh", "fh", "gh", "jh", "kh", "ph", "ng", "sh", "th", "vh", "wh", "zh" };

        private static char GetLastVowel(string romaji) {
            for (int i = romaji.Length - 1; i >= 0; i--) {
                char c = romaji[i];
                if ("aeioun".IndexOf(c) >= 0)
                    return c;
            }
            return '\0';
        }

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (tails.Contains(note.lyric)) {
                return new string[] { note.lyric };
            }
            if (original == null) {
                // Normalize lyric
                string lyric = note.lyric.Trim().ToLowerInvariant();
                string romaji = "";

                // Detect Hiragana / Katakana
                if (Regex.IsMatch(lyric, @"\p{IsHiragana}+")) {
                    int ja = 0;
                    while (ja < lyric.Length) {

                        // LONG VOWEL MARK
                        if (lyric[ja] == 'ー') {
                            char v = GetLastVowel(romaji);
                            if (v != '\0')
                                romaji += v;
                            ja++;
                            continue;
                        }

                        string match = null;
                        foreach (var kv in hiraToRoma.OrderByDescending(k => k.Key.Length)) {
                            if (ja + kv.Key.Length <= lyric.Length &&
                                lyric.Substring(ja, kv.Key.Length) == kv.Key) {
                                match = kv.Key;
                                romaji += kv.Value;
                                ja += kv.Key.Length;
                                break;
                            }
                        }

                        if (match == null) {
                            romaji += WanaKana.ToRomaji(lyric[ja].ToString());
                            ja++;
                        }
                    }
                } else if (Regex.IsMatch(lyric, @"\p{IsKatakana}+")) {
                    int ja = 0;
                    while (ja < lyric.Length) {

                        // LONG VOWEL MARK
                        if (lyric[ja] == 'ー') {
                            char v = GetLastVowel(romaji);
                            if (v != '\0')
                                romaji += v;
                            ja++;
                            continue;
                        }

                        string match = null;
                        foreach (var kv in hiraToKana.OrderByDescending(k => k.Key.Length)) {
                            if (ja + kv.Key.Length <= lyric.Length &&
                                lyric.Substring(ja, kv.Key.Length) == kv.Key) {
                                match = kv.Key;
                                romaji += kv.Value;
                                ja += kv.Key.Length;
                                break;
                            }
                        }

                        if (match == null) {
                            romaji += WanaKana.ToRomaji(lyric[ja].ToString());
                            ja++;
                        }
                    }
                } else {
                    // Fallback for other text
                    romaji = lyric;
                }

                // Split Romaji into consonant/vowel sequence
                List<string> split = new List<string>();
                int ii = 0;
                while (ii < romaji.Length) {
                    string match = null;

                    // Try matching consonants first (longest first)
                    foreach (var cons in consonants.OrderByDescending(c => c.Length)) {
                        if (romaji.Substring(ii).StartsWith(cons)) {
                            match = cons;
                            split.Add(cons);
                            ii += cons.Length;
                            break;
                        }
                    }

                    if (match != null) continue;

                    // Then vowels
                    foreach (var vow in JAvowels.OrderByDescending(v => v.Length)) {
                        if (romaji.Substring(ii).StartsWith(vow)) {
                            match = vow;
                            split.Add(vow);
                            ii += vow.Length;
                            break;
                        }
                    }

                    // If no match, just add raw character
                    if (match == null) {
                        split.Add(romaji[ii].ToString());
                        ii++;
                    }
                }
                original = split.ToArray();
            }

            // SPLITS UP DR AND TR
            string[] tr = new[] { "tr" };
            string[] dr = new[] { "dr" };
            string[] diphthongs = new[] { "aI", "eI", "aU", "oU", "VI", "VU", "@U", "ai", "ei", "Oi", "au", "ou", "Ou", "@u" };
            string[] diphthongs1 = new[] { "OI"};
            string[] diphthongs2 = new[] { "uy", "ew", "iw"};
            
            List<string> modified = new List<string>(original);
            List<string> finalPhonemes = new List<string>();
            int i = 0;
            
            List<string> finalProcessedPhonemes = new List<string>();
            foreach (string s in original) {
                switch (s) {
                    case var str when dr.Contains(str):
                        finalProcessedPhonemes.AddRange(new string[] { "j", s[1].ToString() });
                        break;
                    case var str when tr.Contains(str):
                        finalProcessedPhonemes.AddRange(new string[] { "ch", s[1].ToString() });
                        break;
                    case var str when diphthongs.Contains(str) && !HasOto($"- {str}", note.tone) && !HasOto(s, note.tone) && !HasOto(ValidateAlias($"- {str}"), note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { s[0].ToString(), s[1].ToString() + '^'.ToString() });
                        break;
                    case var str when diphthongs1.Contains(str) && !HasOto($"- {str}", note.tone) && !HasOto(s, note.tone) && !HasOto(ValidateAlias($"- {str}"), note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { "o", s[1].ToString() + '^'.ToString() });
                        break;
                    case var str when diphthongs2.Contains(str) && !HasOto($"- {str}", note.tone) && !HasOto(s, note.tone) && !HasOto(ValidateAlias($"- {str}"), note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        finalProcessedPhonemes.AddRange(new string[] { s[0].ToString(), s[1].ToString() });
                        break;
                    default:
                        finalProcessedPhonemes.Add(s);
                        break;
                }
            }
            return finalProcessedPhonemes.ToArray();
        }

        protected override IG2p[] GetBaseG2ps() => Array.Empty<IG2p>();
        
        protected override List<string> ProcessSyllable(Syllable syllable) {
            syllable.prevV = tails.Contains(syllable.prevV) ? "" : syllable.prevV;
            var replacedPrevV = ReplacePhoneme(syllable.prevV, syllable.tone);
            var prevV = string.IsNullOrEmpty(replacedPrevV) ? "" : replacedPrevV;
            string[] cc = syllable.cc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string v = ReplacePhoneme(syllable.v, syllable.tone);
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            string[] CurrentWordCc = syllable.CurrentWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            string[] PreviousWordCc = syllable.PreviousWordCc.Select(c => ReplacePhoneme(c, syllable.tone)).ToArray();
            int prevWordConsonantsCount = syllable.prevWordConsonantsCount;
            var rv = $"- {v}";

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

            if (syllable.IsStartingV) {
                if (HasOto(rv, syllable.vowelTone) || HasOto(ValidateAlias(rv), syllable.vowelTone)) {
                    basePhoneme = rv;
                } else {
                    basePhoneme = v;
                }
            } else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    basePhoneme = $"{prevV} {v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) && !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone) && vvExceptions.ContainsKey(prevV) && prevV != v) {
                        // VV IS NOT PRESENT, CHECKS VVEXCEPTIONS LOGIC
                        //var vc = $"{prevV}{vvExceptions[prevV]}";
                        var vc = AliasFormat($"{vvExceptions[prevV]}", "vcEx", syllable.vowelTone, prevV);
                        phonemes.Add(vc);
                        basePhoneme = ValidateAlias(AliasFormat($"{vvExceptions[prevV]} {v}", "dynMid", syllable.vowelTone, ""));
                    } else {
                        {
                            if (HasOto($"{prevV} {v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV} {v}"), syllable.vowelTone)) {
                                basePhoneme = $"{prevV} {v}";
                            } else if (HasOto($"{prevV}{v}", syllable.vowelTone) || HasOto(ValidateAlias($"{prevV}{v}"), syllable.vowelTone)) {
                                basePhoneme = $"{prevV}{v}";
                            } else if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) {
                                basePhoneme = v;
                            } else {
                                basePhoneme = AliasFormat($"- {v}", "dynMid", syllable.vowelTone, "");
                                phonemes.Add(AliasFormat($"{prevV} -", "dynMid", syllable.vowelTone, ""));
                            }
                        }
                    }
                    // EXTEND AS [V]
                } else if (HasOto($"{v}", syllable.vowelTone) && HasOto(ValidateAlias($"{v}"), syllable.vowelTone) || missingVphonemes.ContainsKey(prevV)) {
                    basePhoneme = v;
                } else {
                    // PREVIOUS ALIAS WILL EXTEND as [V V]
                    basePhoneme = null;
                }

                // [- CV/C V] or [- C][CV/C V]
            } else if (syllable.IsStartingCVWithOneConsonant) {
                // TODO: move to config -CV or -C CV
                var rcv = $"- {cc[0]}{v}";
                var crv = $"{cc[0]}{v}";
                var cv = $"{cc[0]}{v}";
                if (HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv), syllable.vowelTone)) {
                    basePhoneme = rcv;
                } else if ((!HasOto(rcv, syllable.vowelTone) && !HasOto(ValidateAlias(rcv), syllable.vowelTone)) && (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone))) {
                    basePhoneme = crv;
                    TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                } else {
                    basePhoneme = cv;
                    TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                }
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                // try RCCV
                var rccv = $"- {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()}{v}";
                var ucv = $"_{cc.Last()}{v}";
                var ccv1 = $"{string.Join("", cc)}{v}";
                if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone)) {
                    basePhoneme = rccv;
                    lastC = 0;
                } else {
                    if (HasOto(ccv1, syllable.vowelTone) || HasOto(ValidateAlias(ccv1), syllable.vowelTone)) {
                        basePhoneme = ccv1;
                    } else if (HasOto(ucv, syllable.vowelTone) || HasOto(ValidateAlias(ucv), syllable.vowelTone)) {
                        basePhoneme = ucv;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                        basePhoneme = crv;
                    } else {
                        basePhoneme = $"{cc.Last()}{v}";
                    }
                    // try RCC
                    for (var i = cc.Length; i > 1; i--) {
                        if (TryAddPhoneme(phonemes, syllable.tone, $"- {string.Join("", cc.Take(i))}", ValidateAlias($"- {string.Join("", cc.Take(i))}"))) {
                            firstC = i - 1;
                            break;
                        }
                    }
                    if (phonemes.Count == 0) {
                        TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                    }
                    // try CCV
                    for (var i = firstC; i < cc.Length - 1; i++) {
                        var ccv = string.Join("", cc.Skip(i)) + v;
                        if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone)) {
                            basePhoneme = ccv;
                            lastC = i;
                            break;
                        } else {
                            if (HasOto(ucv, syllable.vowelTone) || HasOto(ValidateAlias(ucv), syllable.vowelTone)) {
                                basePhoneme = ucv;
                                break;
                            } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                                basePhoneme = crv;
                                break;
                            } else {
                                basePhoneme = $"{cc.Last()}{v}";
                                break;
                            }
                        }
                    }
                }
            } else { // VCV
                var vcv = $"{prevV} {cc[0]}{v}";
                var vcvEnd = $"{prevV}{cc[0]} {v}";
                var vccv = $"{prevV} {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()} {v}";
                // Use regular VCV if the current word starts with one consonant and the previous word ends with none
                if (syllable.IsVCVWithOneConsonant && (HasOto(vcv, syllable.vowelTone) || HasOto(ValidateAlias(vcv), syllable.vowelTone)) && prevWordConsonantsCount == 0 && CurrentWordCc.Length == 1) {
                    basePhoneme = vcv;
                    // Use end VCV if current word does not start with a consonant but the previous word does end with one
                } else if (syllable.IsVCVWithOneConsonant && prevWordConsonantsCount == 1 && CurrentWordCc.Length == 0 && (HasOto(vcvEnd, syllable.vowelTone) || HasOto(ValidateAlias(vcvEnd), syllable.vowelTone))) {
                    basePhoneme = vcvEnd;
                    // Use regular VCV if end VCV does not exist
                } else if (syllable.IsVCVWithOneConsonant && !HasOto(vcvEnd, syllable.vowelTone) && !HasOto(ValidateAlias(vcvEnd), syllable.vowelTone) && (HasOto(vcv, syllable.vowelTone) || HasOto(ValidateAlias(vcv), syllable.vowelTone))) {
                    basePhoneme = vcv;
                    // VCV with multiple consonants, only for current word onset and null previous word ending
                    // TODO: multi-VCV for words ending with one or more consonants?
                } else if (syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccv, syllable.vowelTone) || HasOto(ValidateAlias(vccv), syllable.vowelTone)) && prevWordConsonantsCount == 0) {
                    basePhoneme = vccv;
                    lastC = 0;
                } else {
                    var cv = cc.Last() + v;
                    basePhoneme = cv;
                    if ((!HasOto(cv, syllable.vowelTone) && !HasOto(ValidateAlias(cv), syllable.vowelTone)) && (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone))) {
                        basePhoneme = crv;
                    }
                    // try CCV
                    if ((cc.Length - firstC > 1) && CurrentWordCc.Length >= 2) {
                        for (var i = firstC; i < cc.Length; i++) {
                            var ccv = $"{string.Join("", cc.Skip(0))}{v}";
                            var rccv = $"- {string.Join("", cc.Skip(0))}{v}";
                            var ucv = $"_{cc.Last()}{v}";
                            if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone)) {
                                lastC = 0;
                                basePhoneme = ccv;
                                break;
                            } else if (!HasOto(ccv, syllable.vowelTone) && !HasOto(ValidateAlias(ccv), syllable.vowelTone) && (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone))) {
                                lastC = 0;
                                basePhoneme = rccv;
                                break;
                            } else if ((!HasOto(rccv, syllable.vowelTone) || !HasOto(ValidateAlias(rccv), syllable.vowelTone)) && (HasOto(ucv, syllable.vowelTone) || HasOto(ValidateAlias(ucv), syllable.vowelTone))) {
                                basePhoneme = ucv;
                                break;
                            }
                        }
                    }
                    
                    // try vcc
                    for (var i = lastC + 1; i >= 0; i--) {
                        var vr = $"{prevV} -";
                        var vcc = $"{prevV} {string.Join("", cc.Take(2))}";
                        var vcc2 = $"{prevV}{string.Join(" ", cc.Take(2))}";
                        var vc = $"{prevV} {cc[0]}";
                        bool CCV = false;
                        if (CurrentWordCc.Length >= 2 && !ccvException.Contains(cc[0])) {
                            if (HasOto(AliasFormat($"{string.Join("", cc)} {v}", "dynMid", syllable.vowelTone, ""), syllable.vowelTone)) {
                                CCV = true;
                            }
                        }

                        if (i == 0) {
                            if (HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone)) {
                                phonemes.Add(vr);
                            }
                        } else if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc), syllable.tone)) && CCV) {
                            phonemes.Add(vcc);
                            firstC = 1;
                            break;
                        } else if ((HasOto(vcc, syllable.tone) || HasOto(ValidateAlias(vcc), syllable.tone)) && !affricate.Contains(string.Join("", cc.Take(2)))) {
                            phonemes.Add(vcc);
                            firstC = 1;
                            break;
                        } else if (HasOto(vcc2, syllable.tone) || HasOto(ValidateAlias(vcc2), syllable.tone)) {
                            phonemes.Add(vcc2);
                            firstC = 1;
                            break;
                        } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                            phonemes.Add(vc);
                            break;
                        } else {
                            continue;
                        }
                    }
                }
            }

            for (var i = firstC; i < lastC; i++) {
                // we could use some CCV, so lastC is used
                // we could use -CC so firstC is used
                var cc1 = $"{cc[i]} {cc[i + 1]}";
                var ccv = string.Join("", cc.Skip(i + 1)) + v;
                var rccv = $"- {string.Join("", cc.Skip(i + 1)) + v}";
                var ucv = $"_{cc.Last()}{v}";
                var crv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                // Use [C1C2...] when current word starts with 2 consonants or more
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                if (CurrentWordCc.Length >= 2 && !PreviousWordCc.Contains(cc1)) {
                    cc1 = $"{string.Join("", cc.Skip(i))}";
                }
                if (CurrentWordCc.Length >= 2) {
                    if (liquid.Contains(cc.Last()) || semivowel.Contains(cc.Last())
                        || liquid.Contains(ValidateAlias(cc.Last())) || semivowel.Contains(ValidateAlias(cc.Last()))) {
                        glides(cc1);
                    }
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // Use [C1C2] when current word has 2 consonants or more and [C1C2C3...] does not exist
                if (!HasOto(cc1, syllable.tone) && CurrentWordCc.Length >= 2 && CurrentWordCc.Contains(cc1)) {
                    cc1 = $"{cc[i]}{cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // Use [C1 C2] when either [C1C2] does not exist, or current word has 1 consonant or less and previous word has 1 consonant or more
                if ((!HasOto(cc1, syllable.tone)) || PreviousWordCc.Contains(cc1)) {
                    cc1 = $"{cc[i]} {cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // Use UCV if it exists
                if ((HasOto(ucv, syllable.vowelTone) || HasOto(ValidateAlias(ucv), syllable.vowelTone)) && !cc1.Contains($"{cc[i]} {cc[i + 1]}")) {
                    basePhoneme = ucv;
                }
                if (i + 1 < lastC) {
                    var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    // Use [C2C3...] when current word starts with 2 consonants or more
                    if (CurrentWordCc.Length >= 2 && !PreviousWordCc.Contains(cc2)) {
                        cc2 = $"{string.Join("", cc.Skip(i))}";
                    }
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    if (CurrentWordCc.Length >= 2) {
                        if (liquid.Contains(cc[i + 1]) || semivowel.Contains(cc[i + 1])
                            || liquid.Contains(ValidateAlias(cc[i + 1])) || semivowel.Contains(ValidateAlias(cc[i + 1]))) {
                            glides(cc1);
                        }
                    }
                    // Use [C2C3] when current word has 2 consonants or more and [C2C3C4...] does not exist
                    if (!HasOto(cc2, syllable.tone) && CurrentWordCc.Length >= 2 && CurrentWordCc.Contains(cc2)) {
                        cc2 = $"{cc[i + 1]}{cc[i + 2]}";
                    }
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    // Use [C2 C3] when either [C2C3] does not exist, or current word has 1 consonant or less and previous word has 2 consonants or more
                    if ((!HasOto(cc2, syllable.tone)) || PreviousWordCc.Contains(cc2)) {
                        cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                    }
                    if (!HasOto(cc2, syllable.tone)) {
                        cc2 = ValidateAlias(cc2);
                    }
                    //Use CCV if it exists
                    if ((HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone)) && CurrentWordCc.Length >= 2 && !PreviousWordCc.Contains(string.Join("", cc.Skip(i + 1)))) {
                        lastC = i;
                        basePhoneme = ccv;
                        // Use RCCV if it exists
                    } else if ((HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone)) && CurrentWordCc.Length >= 2 && !PreviousWordCc.Contains(string.Join("", cc.Skip(i + 1)))) {
                        lastC = i;
                        basePhoneme = rccv;
                        // Use _CV if it exists
                    } else if ((HasOto(ucv, syllable.vowelTone) || HasOto(ValidateAlias(ucv), syllable.vowelTone)) && HasOto(cc2, syllable.vowelTone) && !cc2.Contains($"{cc[i + 1]} {cc[i + 2]}") && CurrentWordCc.Length >= 2) {
                        basePhoneme = ucv;
                        // Use spaced CV if it exists
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                        basePhoneme = crv;
                        // Use normal CV
                    } else {
                        basePhoneme = cv;
                    }
                    if (HasOto(cc1, syllable.tone) && HasOto(cc2, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // like [V C1] [C1 C2] [C2 ..]
                        if (cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                            i++;
                        }
                    } else {
                        // singular cc
                        if ((PreviousWordCc.Contains(cc1) == CurrentWordCc.Contains(cc1)) && !affricate.Contains(cc1)) {
                            cc1 = ValidateAlias(cc1);
                        } else {
                            TryAddPhoneme(phonemes, syllable.tone, cc1, cc[i], ValidateAlias(cc[i]));
                        }
                    }
                } else {
                    // like [V C1] [C1 C2]  [C2 ..] or like [V C1] [C1 -] [C3 ..]
                    TryAddPhoneme(phonemes, syllable.tone, cc1, cc[i], ValidateAlias(cc[i]));
                }
            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }

        protected override List<string> ProcessEnding(Ending ending) {
            // Pass ending.tone to ReplacePhoneme
            string prevV = ReplacePhoneme(ending.prevV, ending.tone);
            // Pass ending.tone to ReplacePhoneme for each consonant
            string[] cc = ending.cc.Select(c => ReplacePhoneme(c, ending.tone)).ToArray();
            // Assuming 'v' here refers to ending.prevV as 'Ending' class doesn't have a 'v' property directly
            string v = ReplacePhoneme(ending.prevV, ending.tone);
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            var vr = $"{v} -";
            if (tails.Contains(ending.prevV)) {
                return new List<string>();
            }
            if (ending.IsEndingV) {
                var vR = $"{v} -";
                var vR2 = $"{v}-";
                if (HasOto(vR, ending.tone) || HasOto(ValidateAlias(vR), ending.tone) || HasOto(vR2, ending.tone) || HasOto(ValidateAlias(vR2), ending.tone)) {
                    phonemes.Add(AliasFormat($"{v}", "ending", ending.tone, ""));
                }
            } else if (ending.IsEndingVCWithOneConsonant) {
                var vc = $"{prevV} {cc[0]}";
                var vcr = $"{prevV} {cc[0]}-";
                var vcr2 = $"{prevV}{cc[0]} -";
                var vcr3 = $"{prevV} {cc[0]} -";
                var vcr4 = $"{prevV}{cc[0]}-";
                if (HasOto(vcr, ending.tone) && HasOto(ValidateAlias(vcr), ending.tone) || (HasOto(vcr2, ending.tone) && HasOto(ValidateAlias(vcr2), ending.tone))) {
                    phonemes.Add(AliasFormat($"{v} {cc[0]}", "dynEnd", ending.tone, ""));
                } else if (HasOto(vcr3, ending.tone) && HasOto(ValidateAlias(vcr3), ending.tone)) {
                    phonemes.Add(vcr3);
                } else if (HasOto(vcr4, ending.tone) && HasOto(ValidateAlias(vcr4), ending.tone)) {
                    phonemes.Add(vcr4);
                } else if (HasOto(vc, ending.tone) && HasOto(ValidateAlias(vc), ending.tone)) {
                    phonemes.Add(vc);
                    if (vc.Contains(cc[0])) {
                        phonemes.Add(AliasFormat($"{cc[0]}", "ending", ending.tone, ""));
                    }
                } else {
                    for (int len = cc[0].Length; len > 0; len--) {
                        string c = cc[0].Substring(0, len);   // shr → sh → s
                        string vcTry = $"{prevV} {c}";
                        if ( HasOto(vcTry, ending.tone) || HasOto(ValidateAlias(vcTry), ending.tone)) {
                            phonemes.Add(vcTry);
                            break;
                        }
                    }
                    if (vc.Contains(cc[0])) {
                        phonemes.Add(AliasFormat($"{cc[0]}", "ending", ending.tone, ""));
                    }
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vr1 = $"{v} R";
                    var vr2 = $"{v}-";
                    var vcc = $"{v} {string.Join("", cc.Take(2))}-";
                    var vcc2 = $"{v}{string.Join(" ", cc.Take(2))} -";
                    var vcc3 = $"{v}{string.Join(" ", cc.Take(2))}";
                    var vcc4 = $"{v} {string.Join("", cc.Take(2))}";
                    var vc = $"{v} {cc[0]}";
                    if (i == 0) {
                        if (HasOto(vr, ending.tone) || HasOto(ValidateAlias(vr), ending.tone) || HasOto(vr2, ending.tone) || HasOto(ValidateAlias(vr2), ending.tone) || HasOto(vr1, ending.tone) || HasOto(ValidateAlias(vr1), ending.tone) && !HasOto(vc, ending.tone)) {
                            phonemes.Add(AliasFormat($"{v}", "ending", ending.tone, ""));
                        }
                        break;
                    } else if (HasOto(vcc, ending.tone) && HasOto(ValidateAlias(vcc), ending.tone) && lastC == 1 && !ccvException.Contains(cc[0])) {
                        phonemes.Add(vcc);
                        firstC = 1;
                        break;
                    } else if (HasOto(vcc2, ending.tone) && HasOto(ValidateAlias(vcc2), ending.tone) && lastC == 1 && !ccvException.Contains(cc[0])) {
                        phonemes.Add(vcc2);
                        firstC = 1;
                        break;
                    } else if ((HasOto(vcc3, ending.tone) && HasOto(ValidateAlias(vcc3), ending.tone) && !ccvException.Contains(cc[0]))) {
                        phonemes.Add(vcc3);
                        if (vcc3.EndsWith(cc.Last()) && lastC == 1) {
                            if (consonants.Contains(cc.Last())) {
                                phonemes.Add(AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            }
                        }
                        firstC = 1;
                        break;
                    } else if ((HasOto(vcc4, ending.tone) && HasOto(ValidateAlias(vcc4), ending.tone) && !ccvException.Contains(cc[0]))) {
                        phonemes.Add(vcc4);
                        if (vcc4.EndsWith(cc.Last()) && lastC == 1) {
                            if (consonants.Contains(cc.Last())) {
                                phonemes.Add(AliasFormat($"{cc.Last()}", "ending", ending.tone, ""));
                            }
                        }
                        firstC = 1;
                        break;
                    } else if (!!HasOto(vcc, ending.tone) && !HasOto(ValidateAlias(vcc), ending.tone)
                            || !HasOto(vcc2, ending.tone) && HasOto(ValidateAlias(vcc2), ending.tone)
                            || !HasOto(vcc3, ending.tone) && HasOto(ValidateAlias(vcc3), ending.tone)
                            || !HasOto(vcc4, ending.tone) && HasOto(ValidateAlias(vcc4), ending.tone)) {
                        phonemes.Add(vc);
                        break;
                    } else {
                        for (int len = cc[0].Length; len > 0; len--) {
                            string c = cc[0].Substring(0, len);   // shr → sh → s
                            string vcTry = $"{prevV} {c}";
                            if (HasOto(vcTry, ending.tone) || HasOto(ValidateAlias(vcTry), ending.tone)) {
                                phonemes.Add(vcTry);
                                break;
                            }
                        }
                        break;
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    var cc1 = $"{cc[i]} {cc[i + 1]}";
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]} {cc[i + 2]}";

                        for (int len = cc[i + 2].Length; len > 0; len--) {
                            string c = cc[i + 2].Substring(0, len);   // shr → sh → s
                            string ccTry = $"{cc[i + 1]} {c}";

                            if (HasOto(ccTry, ending.tone) && !(HasOto(cc1, ending.tone) || HasOto(ValidateAlias(cc1), ending.tone))) {
                                cc1 = ccTry;
                                break;
                            }
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc2, ending.tone)) {
                            cc2 = ValidateAlias(cc2);
                        }

                        if (!HasOto(cc2, ending.tone) && !HasOto($"{cc[i + 1]} {cc[i + 2]}", ending.tone)) {
                            // [C1 -] [- C2]
                            cc2 = AliasFormat($"{cc[i + 2]}", "cc_inB", ending.tone, "");
                            TryAddPhoneme(phonemes, ending.tone, ValidateAlias(AliasFormat($"{cc[i + 1]}", "cc_endB", ending.tone, "")));
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
                        } else if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]}{cc[i + 2]}", ValidateAlias($"{cc[i + 1]}{cc[i + 2]}"))) {
                            // like [C1C2][C2 ...]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1 -] [- C2]
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_inB", ending.tone, ""));
                            TryAddPhoneme(phonemes, ending.tone, AliasFormat($"{cc[i + 1]}", "cc_endB", ending.tone, ""));
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
                                string[] hyphenPatterns = { "-", " -" };
                                foreach (var hyphenPattern in hyphenPatterns) {
                                    string endingcc = $"{consPattern}{hyphenPattern}";

                                    if (TryAddPhoneme(phonemes, ending.tone, endingcc, ValidateAlias(endingcc))) {
                                        i += clusterLength - 1;
                                    }
                                }
                            }
                        }
                    } else {
                        for (int len = cc[i + 1].Length; len > 0; len--) {
                            string c = cc[i + 1].Substring(0, len);   // shr → sh → s
                            string ccTry = $"{cc[i]} {c}";

                            if (HasOto(ccTry, ending.tone) && !(HasOto(cc1, ending.tone) || HasOto(ValidateAlias(cc1), ending.tone))) {
                                cc1 = ccTry;
                                break;
                            }
                        }
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
                            TryAddPhoneme(phonemes, ending.tone, ValidateAlias(AliasFormat($"{cc[i]}", "cc_endB", ending.tone, "")));
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        // CC that ends with 2 clusters
                        if ((TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-")))) {
                            // like [C1 C2-]
                            i++;
                        } else if ((TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]} -", ValidateAlias($"{cc[i]} {cc[i + 1]} -")))) {
                            // like [C1 C2 -]
                            i++;
                        } else if ((TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]}-", ValidateAlias($"{cc[i]}{cc[i + 1]}-")))) {
                            // like [C1C2-]
                            i++;
                        } else if ((TryAddPhoneme(phonemes, ending.tone, $"{cc[i]}{cc[i + 1]} -", ValidateAlias($"{cc[i]}{cc[i + 1]} -")))) {
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
        private string ToHiragana(string alias, int tone) {
            // Check if the alias or its validated version has an OTO
            if (HasOto(WanaKana.ToHiragana(alias), tone) || HasOto(ValidateAlias(WanaKana.ToHiragana(alias)), tone)) {
                return WanaKana.ToHiragana(alias);
            }

            // Convert the alias to Hiragana
            var hiragana = WanaKana.ToHiragana(alias);

            // Apply specific character replacements
            hiragana = hiragana.Replace("ゔ", "ヴ");
            hiragana = hiragana.Replace("q", "-");

            // Return the modified Hiragana
            return hiragana;
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
                { "consEn", new string[] { "_", "- ", "_" } },
                { "ending", new string[] { " -", "-"} },
                { "ending_mix", new string[] { "-", " -", "R", " R", "_", "--" } },
                { "cc", new string[] { "", "-", "- ", "_" } },
                { "cc_start", new string[] { "- ", "-"} },
                { "cc_end", new string[] { " -", "-", "" } },
                { "cc_mix", new string[] { " -", " R", "-", "", "_", "- ", "-" } },
                { "cc1_mix", new string[] { "", " -", "-", " R", "_", "- ", "-" } },
                { "cc_teto", new string[] { "_", ""} },
                { "cc_teto_end", new string[] { "_", ""} }
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

            if (type.Contains("dynMid_vv")) {
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
                    $"{consonant} {vowel}",    // "C V"
                    $"{consonant}{vowel}",    // "CV"
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
                } else if (type.Contains("end") && !(type.Contains("dynEnd"))) {
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

        protected override string ValidateAlias(string alias, int tone = 0) {
            if (HasOto(alias, tone)) return alias;

            string baseResolved = base.ValidateAlias(alias, tone);
            if (!string.IsNullOrEmpty(baseResolved) && baseResolved != alias) {
                if (HasOto(baseResolved, tone)) {
                    return baseResolved;
                }
                alias = baseResolved;
            }

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingVPhonemes || isMissingCPhonemes || isTimitPhonemes) {
                foreach (var phoneme in missingVphonemes.Concat(missingCphonemes).Concat(timitphonemes)) {
                    alias = alias.Replace(phoneme.Key, phoneme.Value);
                }
            }

            //CC (C R)
            foreach (var c2 in consonants) {
                if (!(alias.Contains($"ay {c2}") || alias.Contains($"ey {c2}") || alias.Contains($"iy {c2}") || alias.Contains($"oy {c2}"))) {
                    alias = alias.Replace($"{c2} R", $"{c2} -");
                }
            }

            //VC's
            foreach (var v1 in vcFallBacks) {
                foreach (var c1 in consonants) {
                    alias = alias.Replace(v1.Key + " " + c1, v1.Value + " " + c1);
                }
            }

            // glottal
            foreach (var v1 in vowels) {
                if (!alias.Contains("cl " + v1) || !alias.Contains("q " + v1)) {
                    alias = alias.Replace("q " + v1, "- " + v1);
                }
            }
            foreach (var c2 in consonants) {
                if (!alias.Contains(c2 + " cl") || !alias.Contains(c2 + " q")) {
                    alias = alias.Replace(c2 + " q", $"{c2} -");
                }
            }
            foreach (var c2 in consonants) {
                if (!alias.Contains("cl " + c2) || !alias.Contains("q " + c2)) {
                    alias = alias.Replace("q " + c2, "- " + c2);
                }
            }

            if (alias.Contains("y -")) {
                alias = alias.Replace("y", "I");
            }

            if (alias.Contains("w -")) {
                alias = alias.Replace("w", "U");
            }

            // Split diphthongs adjuster
            if (alias.Contains("U^")) {
                alias = alias.Replace("U^", "U");
            }
            if (alias.Contains("I^")) {
                alias = alias.Replace("I^", "I");
            }
            if (alias.Contains("u^")) {
                alias = alias.Replace("u^", "u");
            }
            if (alias.Contains("i^")) {
                alias = alias.Replace("i^", "i");
            }

            return base.ValidateAlias(alias);
        }

        // Endings has 50 ticks gap
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

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
    public class MandarinPhonemizer : ArpasingPlusPhonemizer {
        protected override string YamlFileName => "zh-cvvc-mccr.yaml";
        protected override string YamlVersion => "1.0";
        protected override byte[] YamlTemplate => ZH_CVVC_MCCR.data.Resources.template;
        public MandarinPhonemizer() {
            this.vowels = new string[] {
                "a", "e", "i", "o", "E", "Ei", "3", "1", "u", "v"
            };
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
        protected override bool NoGap => true;
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
    }
}
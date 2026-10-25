using System.Collections.Generic;

using OpenUtau.Api;

namespace OpenUtau.Core.DiffSinger {
    [Phonemizer("DiffSinger Chinese Phonemizer", "DIFFS ZH FLOP", language: "ZH")]
    public class FLDiffSingerChinesePhonemizer : DiffSingerRefinedPhonemizer {
        protected override string GetDictionaryName()=>"fl-dsdict-zh.yaml";
        public override string GetLangCode()=>"zh";
        protected override string[] Romanize(IEnumerable<string> lyrics) {
            return BaseChinesePhonemizer.Romanize(lyrics);
        }
    }
}

# YAML Dictionary

**YAML (short for YAML Ain't Markup Language)** is a human-readable data serialization format. In the context of OpenUtau phonemizers, YAML files act as structured configuration files that tell the phonemizer how a voicebank is structured, which phonemes are available, and how words map to sounds without requiring complex code.

YAML offers two main structural styles: **Block Style** (multiline and indentation-based) and **Flow Style** (compact and inline, using brackets and braces).

* **Block Style:**

  ```yaml
  replacements:
    - from: dr
      to:
        - d
        - r
      where: all
    - from: ia
      to:
        - ih
        - ax
      where: all
  ```

* **Flow Style:**

  ```yaml
  replacements:
    - {from: dr, to: [d, r], where: all}
    - {from: ia, to: [ih, ax], where: all}
    - {from: ea, to: [eh, ax], where: inside}
  ```

For dictionary editing and creation, OpenUtau voicebank developers recommend using **inline flow mappings within block sequences** to keep dictionaries readable, compact, and easy to scan.

All YAML examples below use Flow Style.

In YAML, strings generally do not require quotation marks. However, you must enclose values in quotation marks (`"..."` or `'...'`) whenever a string contains characters or patterns that YAML reserves for its own syntax. Failing to do so may cause parsing errors, unintended data corruption, or unexpected type conversions.

---

## DIFFSinger Supported YAML Configuration

In OpenUtau's DiffSinger phonemizers, configuration files such as `dsdict-en.yaml` define how phonemes, phoneme types, and G2P phoneme remappings are configured.

### Symbols

* The `symbols` section lists every valid phoneme token available in the UTAU voicebank. The DiffSinger model uses this list to map acoustic feature predictions to exact phoneme durations.
* The `symbols` section supports only the following keys:

  * `symbol`: The definition of the phoneme itself.
  * `type`: The type of the phoneme (`vowel`, `affricate`, `fricative`, `nasal`, `liquid`, `stop`, `tap`, `aspirate`, or `semivowel`).

  ```yaml
  symbols:
    - {symbol: AP, type: vowel}
    - {symbol: SP, type: vowel}
    - {symbol: cl, type: stop}
    - {symbol: en/aa, type: vowel}
    - {symbol: en/ae, type: vowel}
    - {symbol: en/ah, type: vowel}
    - {symbol: en/ao, type: vowel}
    - {symbol: en/aw, type: vowel}
    - {symbol: en/ax, type: vowel}
    - {symbol: en/ay, type: vowel}
  ```

### Replacements

* The `replacements` section remaps G2P output to another user-defined phoneme set or symbol.

  **Note:** DiffSinger phonemizer replacements support only 1:1 phoneme remapping.

* The `replacements` section supports only the following keys:

  * `from`: The target phoneme.
  * `to`: The replacement phoneme for the target symbol.

  ```yaml
  replacements:
    - {from: aa, to: en/aa}
    - {from: ae, to: en/ae}
    - {from: ah, to: en/ah}
    - {from: ao, to: en/ao}
    - {from: aw, to: en/aw}
    - {from: ax, to: en/ax}
    - {from: ay, to: en/ay}
    - {from: eh, to: en/eh}
    - {from: er, to: en/er}
  ```

### Entries

* The `entries` section allows you to override the G2P output with a user-defined pronunciation for a target word.
* The `entries` section supports the following keys:

  * `grapheme`: The word itself.
  * `phoneme`: An array of phonemes representing how the word should be pronounced.

  **Note:** The `phonemes` key must always contain a list. Even if a word has only one phoneme, you must still use brackets so that OpenUtau can parse it correctly.

  ```yaml
  entries:
    - {grapheme: thinking, phonemes: [th, ih, ng, k, ix, ng]}
    - {grapheme: probably, phonemes: [p, r, aa, b, ax, b, l, iy]}
    - {grapheme: beautiful, phonemes: [b, y, uw, dx, ix, f, el]}
    - {grapheme: full, phonemes: [f, ux, l]}
    - {grapheme: hand, phonemes: [hh, ea, n, d]}
  ```

---

## SBP (Syllable-Based Phonemizer) Supported YAML Configuration

In SBP phonemizers, YAML dictionaries use the same general structure and provide similar functionality to DiffSinger YAML dictionaries.

### Global Dictionary

* Child phonemizers within the SBP API will **no longer write their dictionaries directly into the voicebank folder**. This change is intended to prevent the folder from becoming cluttered and to ensure that only necessary changes are recorded when the dictionary is updated, even if the modification is minor.
* The global dictionary, located in the plugins folder and containing all YAML dictionary files, serves as the single authoritative resource for all voicebanks. Each voicebank can override the global dictionary by using its own local YAML file in its root directory.
* To override the global dictionary for a specific voicebank, simply copy the relevant YAML file into the voicebank's root directory and make your edits there.
* **How can I tell whether my voicebank is using an overridden YAML dictionary?** You can verify this by checking whether the entry's value has changed and whether the change takes effect.

  ```yaml
  # Global YAML:
  replacements:
    - {from: ax, to: ah}
    - {from: [d, r], to: dr, where: inside}
    - {from: [t, r], to: tr, where: inside}

  # Overridden YAML file:
  replacements:
    - {from: ax, to: ax}
    - {from: [d, r], to: [jh, r], where: inside}
    - {from: [t, r], to: [ch, r], where: inside}
  ```

### Version

* In SBP phonemizers, some phonemizers update their default YAML dictionaries over time. To distinguish between dictionary versions, a root-level scalar entry is added to the YAML file to specify the dictionary's version.
* When a phonemizer updates its dictionary version, it backs up the old dictionary using its previous version number and writes the new dictionary to the file.

  ```yaml
  version: 1.0
  ```

### Isglides

* In certain SBP phonemizers, the `isglides` function anchors liquid or semivowel consonant clusters to the starting position of a note. Examples include `d(r)a`, `t(w)i`, `s(l)e`, and `n(y)a`.
* The default value is `true`.

  ```yaml
  isglides: true
  ```

### Symbols

* The `symbols` section lists every valid phoneme token available in the UTAU voicebank. In SBP, you can add custom or extra phonemes to this list so that child phonemizers can recognize them by their phoneme types.
* The `symbols` section supports only the following keys:

  * `symbol`: The definition of the phoneme itself.
  * `type`: The type of the phoneme (`vowel`, `affricate`, `fricative`, `nasal`, `liquid`, `stop`, `tap`, `aspirate`, `semivowel`, `tail`, or `diphthong`).

  ```yaml
  symbols:
    - {symbol: aa, type: vowel}
    - {symbol: ae, type: vowel}
    - {symbol: ah, type: vowel}
    - {symbol: ao, type: vowel}
    - {symbol: aw, type: diphthong}
    - {symbol: ax, type: vowel}
    - {symbol: ay, type: diphthong}
    - {symbol: R, type: tail}
    - {symbol: "-", type: tail}
  ```

### Diphthongs

* In some SBP child phonemizers, this section defines how a diphthong symbol is split from its diphthong tail.
* The `diphthongs` section supports only the following keys:

  * `from`: The target phoneme.
  * `to`: The diphthong tail to use when splitting the phoneme.

  ```yaml
  diphthongs:
    - {from: ay, to: y}
    - {from: OI, to: j}
    - {from: e@n, to: e@n_}
    - {from: air, to: air_}
  ```

### Replacements

* The `replacements` section remaps G2P output to another user-defined phoneme set or symbol.

  **Note:** SBP phonemizers support 1:1, 1:M, M:1, and M:M phoneme replacements.

* The `replacements` section supports the following keys:

  * `from`: The target phoneme.
  * `to`: The replacement phoneme or phoneme sequence.
  * `where` (optional): Specifies where the replacement occurs.

    * `inside`: Applies only within the boundaries of a note.
    * `boundary`: Applies only during transitions from one note to another, usually at the connecting transition.
    * `all`: Applies in both contexts.

  ```yaml
  replacements:
    # 1:1 (one-to-one mappings)
    - {from: ax, to: ah}
    - {from: cl, to: q}
    - {from: dd, to: dx}

    # 1:M (one-to-many mappings) — splitting
    - {from: dr, to: [d, r]}
    - {from: tr, to: [t, sh, r]}
    - {from: aw, to: [aa, w]}

    # M:1 (many-to-one mappings) — merging
    - {from: [ih, ng], to: ing}
    - {from: [E, `@`, m], to: eam}
    - {from: [sh, r], to: Sr}

    # M:M (many-to-many mappings)
    - {from: [ih, ng], to: [ix, ng]}
    - {from: [ay, el], to: [ay, ax, l]}
    - {from: [h, E], to: [hh, E]}

    # All
    - {from: [n, b], to: [m, b], where: all}

    # Inside
    - {from: [s, t], to: [s, tcl], where: inside}

    # Boundary
    - {from: [t, r], to: [t, cl, r], where: boundary}
  ```

* **Phoneme groups** are a replacement syntax that allows you to target multiple phonemes and phoneme types in a single entry.

  ```yaml
  # = indicates that only the specified phoneme(s) are included in the group.
  - {from: ["vowel=ay,ey", l], to: ["vowel=ay,ey", ax, l]}

  # & includes matching phonemes from the search.
  - {from: [ae, "nasal&liquid"], to: [ea, "nasal&liquid"]}

  # ! excludes the specified phonemes.
  - {from: [vowel, "stop!cl,q,vf"], to: [vowel, "stop!cl,q,vf", exh]}

  # + concatenates symbols. For example, b+h = bh.
  - {from: [stop, vowel], to: [stop+h, vowel]}

  # () specifies groupings.
  - {from: ["affricate=j&c", vowel], to: ["(affricate=j&c)+h", vowel]}
  ```

### Timings

* In this section, users can specify custom phoneme lengths using the `GetTransitionBasicLengthMsByConstant` value.
* The `timings` section supports only the following keys:

  * `symbol`: The target phoneme or alias.
  * `value`: The numerical value used to override the phoneme length in `GetTransitionBasicLengthMsByConstant`.

  ```yaml
  timings:
    - {symbol: "ax r", value: 1.5}
    - {symbol: "a r", value: 0.5}
    - {symbol: "ey s", value: 2.4}
  ```

### Fallbacks

* In this section, users can specify fallback phonemes for missing phonemes. Fallbacks are triggered only when a specified phoneme or alias is missing, in which case it is replaced with the defined fallback value.
* The `fallbacks` section supports only the following keys:

  * `from`: The target phoneme or alias.
  * `to`: The fallback phoneme or alias.

  **Note:** Fallbacks are phonemizer-dependent. For them to work properly, the phonemizer's code must implement and use the `ValidateAlias` function.

  ```yaml
  fallbacks:
    - {from: "@r -", to: "er -"}
    - {from: dx, to: d}
    - {from: "ia ", to: ih}
    - {from: " ia", to: ax}
    - {from: "a ky", to: "a k"}
    - {from: "m by", to: "m b"}
  ```

### Vowelsustains

* In this section, SBP phonemizers can specify which aliases or vowels should have an additional alias attached immediately after the base phoneme's position.
* The `vowelsustains` section supports only the following keys:

  * `symbol`: The target vowel or alias (CV/CCV), or the base vowel.
  * `sustain`: The additional alias to attach.
  * `offset`: The distance of the sustain alias from the base phoneme, using the `getMsLengthByConstant` value.

  ```yaml
  vowelsustains:
    - {symbol: ay, sustain: '_ayL', offset: 0.5}
    - {symbol: axr, sustain: axr, offset: 1.0}
    - {symbol: 'dr eh', sustain: eh5, offset: 1.4}
    - {symbol: 'bV', sustain: '* V', offset: 0.8}
  ```

### Entries

* The `entries` section allows you to override the G2P output with a user-defined pronunciation for a target word.
* The `entries` section supports the following keys:

  * `grapheme`: The word itself.
  * `phoneme`: An array of phonemes representing how the word should be pronounced.

  **Note:** The `phonemes` key must always contain a list. Even if a word has only one phoneme, you must still use brackets so that OpenUtau can parse it correctly.

  ```yaml
  entries:
    - {grapheme: thinking, phonemes: [th, ih, ng, k, ix, ng]}
    - {grapheme: probably, phonemes: [p, r, aa, b, ax, b, l, iy]}
    - {grapheme: beautiful, phonemes: [b, y, uw, dx, ix, f, el]}
    - {grapheme: full, phonemes: [f, ux, l]}
    - {grapheme: hand, phonemes: [hh, ea, n, d]}
  ```
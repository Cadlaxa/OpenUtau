This proposal describes a json-based API for SVS backends. This API is flexible. It defines some data structs and a single API `ops`, and allows the backend to define and describe its API structure itself.

The preferred transport of these json objects is ZeroMQ. The choice is based on ZeroMQ's simplicity and flexibility. Without going into too many details here, it's a very easy IPC setup, and works locally or remotely.

## Data Structs
### `note_sequence`
```
{
 time_unit: "ms", // s, ms or us
 notes: [
   {
     "lyric": "", // rest note. practically all note sequences should start with a rest note since the voice usually starts before the note.
     "duration": 500,
     "key": 60
   }, {
     "lyric": "花",
     "duration": 500,
     "key": 60 // midi spec, C4 = 60
   }
 ]
}
```
### `phoneme_sequence`
```
{
 time_unit: "ms",
 phonemes: [
   {
     "phoneme": "", // rest note
     "duration": 400
   }, {
     "phoneme": "h", // h from hua (花), starts 100ms before first note
     "duration": 200
   }, {
     "phoneme": "ua", // ua from hua (花)
     "duration": 400
   },, {
     "phoneme": "", // rest note
     "duration": 500
   } 
 ]
}
```
### `f0`
```
{
 "time_unit": "ms",
 "frame_duration": "5", // 5ms per frame
 "f0": [261, 261, 261, 261, 261, ...] // Hz, each number is a frame
}
```
### `world_mgc`, `world_sp`, `world_bap`, `world_ap`
```
{
 "time_unit": "ms",
 "frame_duration": 5, // 5ms per frame
 "width": 60,
 "world_mgc": [
   [0, 0, 0, ...], // first frame, length = 60
   [0, 0, 0, ...], // second frame, length = 60
   ...
  ]
}
### `audio_samples`
```
{
  "channels": 1, // probably should always be 1
  "sample_rate": 44100, // or 22500, 48000, etc.
  "sample_format": "int16", // "int8", "int16", "int32" or "float"
  "channels": [
    "samples": [0, 0, 0, ...]
  ]
}

## API
### `ops`:
This API describes all available operations of a backend. This helps the frontend to understand how to call the APIs to transform note sequences to audios.

Example request:
```
{
  "op": "ops"
}
```
Example Response (NNSVS):
```
{
  "ops": [
    {
      "op": "phonemize", // op names are free for backends to define
      "inputs": ["note_sequence"], // input and output data structs are predefined as above
      "outputs": ["phoneme_sequence"]
    },
    {
      "op": "synth_world_features",
      "inputs": ["phoneme_sequence"],
      "outputs": ["world_mgc", "world_bap", "f0"],
    }
  ]
}
```
Example Response (Renderer with ML vocoder):
```
{
  "ops": [
    {
      "op": "phonemize",
      "inputs": ["note_sequence"],
      "outputs": ["f0", "phoneme_sequence"]
    },
    {
      "op": "synth_audio_samples",
      "inputs": ["f0", "phoneme_sequence"],
      "outputs": ["audio_samples"],
    }
  ]
}
```

- At minimal, a renderer should implement a direct or indirect path from `note_sequence` to:
  - Either (`world_mgc` or `world_sp`) and (`world_bap` or `world_ap`)
  - Or `audio_samples`
- `f0` is optional. The frontend can use it as a base for further user editing. Otherwise, the frontend still can use `f0` created by the user.
- This is the only required API for a backend. Any other APIs are defined by the backend and returned via this API.
- This API can also be used to check if the backend is started, or still alive. If the backend does not return in 1 second, it is considered offline.

## Example API: NNSVS `phonemize`

Example request:
```
{
  "op": "phonemize",
  "note_sequence": {
    "phoneme_sequence": [...] // as defined earlier
  }
}
```
Example Response:
```
{
  "phoneme_sequence": [...] // as defined earlier
}
```

## Example API: NNSVS `synth_world_features`

Example request:
```
{
  "op": "synth_world_features",
  "phoneme_sequence": [...] // as defined earlier
}
```
Example Response:
```
{
  "world_mgc": [...],
  "world_bap": [...],
  "f0": [...]
}
```

import os
import math
import struct
import wave

SOUNDS_DIR = r"d:\Projects\Unity\GameProj\WordsOfWonders\Assets\WordPuzzle\Sounds"
os.makedirs(SOUNDS_DIR, exist_ok=True)

SAMPLE_RATE = 44100

def create_audio_meta(path):
    meta = f"""fileFormatVersion: 2
guid: {os.urandom(16).hex()}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    preloadAudioData: 1
    loadInBackground: 0
    ambisonic: 0
  3D: 0
  forceToMono: 1
  normalize: 1
  preloadAudioData: 1
  loadInBackground: 0
"""
    with open(path + ".meta", "w") as f:
        f.write(meta)

def write_wav(filename, samples):
    p1 = os.path.join(SOUNDS_DIR, filename)
    with wave.open(p1, "w") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2) # 16-bit
        wf.setframerate(SAMPLE_RATE)
        packed = bytearray()
        for s in samples:
            val = int(max(-32768, min(32767, s * 32767)))
            packed.extend(struct.pack("<h", val))
        wf.writeframes(packed)
    create_audio_meta(p1)
    print(f"Generated sound asset: {filename}")

# 1. Swipe Char (Short Pop/Chime)
def gen_swipe_char():
    duration = 0.06
    count = int(SAMPLE_RATE * duration)
    samples = []
    freq = 587.33
    for i in range(count):
        t = i / SAMPLE_RATE
        env = math.exp(-t * 40.0)
        s = math.sin(2 * math.pi * freq * t) * env * 0.6
        samples.append(s)
    write_wav("swipe_char.wav", samples)

# 2. Word Matched
def gen_word_matched():
    notes = [523.25, 659.25, 783.99, 1046.50]
    note_dur = 0.08
    samples = []
    for note in notes:
        count = int(SAMPLE_RATE * note_dur)
        for i in range(count):
            t = i / SAMPLE_RATE
            env = math.exp(-t * 12.0)
            s = (math.sin(2 * math.pi * note * t) + 0.3 * math.sin(4 * math.pi * note * t)) * env * 0.5
            samples.append(s)
    write_wav("word_matched.wav", samples)

# 3. Wrong Word
def gen_wrong_word():
    duration = 0.18
    count = int(SAMPLE_RATE * duration)
    samples = []
    freq = 150.0
    for i in range(count):
        t = i / SAMPLE_RATE
        env = math.exp(-t * 8.0)
        s = (math.sin(2 * math.pi * freq * t) + 0.5 * (1 if math.sin(2 * math.pi * freq * 0.5 * t) > 0 else -1)) * env * 0.4
        samples.append(s)
    write_wav("wrong_word.wav", samples)

# 4. Bonus Word
def gen_bonus_word():
    notes = [659.25, 880.00, 1046.50, 1318.51]
    note_dur = 0.06
    samples = []
    for note in notes:
        count = int(SAMPLE_RATE * note_dur)
        for i in range(count):
            t = i / SAMPLE_RATE
            env = math.exp(-t * 15.0)
            s = math.sin(2 * math.pi * note * t) * env * 0.5
            samples.append(s)
    write_wav("bonus_word.wav", samples)

# 5. Hint Sound
def gen_hint_sound():
    duration = 0.25
    count = int(SAMPLE_RATE * duration)
    samples = []
    freq_start = 880.0
    freq_end = 1760.0
    for i in range(count):
        t = i / SAMPLE_RATE
        freq = freq_start + (freq_end - freq_start) * (t / duration)
        env = math.sin(math.pi * (t / duration))
        s = math.sin(2 * math.pi * freq * t) * env * 0.5
        samples.append(s)
    write_wav("hint.wav", samples)

# 6. Shuffle Sound
def gen_shuffle_sound():
    duration = 0.12
    count = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(count):
        t = i / SAMPLE_RATE
        freq = 300.0 + 400.0 * math.sin(t * 50.0)
        env = math.exp(-t * 10.0)
        s = math.sin(2 * math.pi * freq * t) * env * 0.4
        samples.append(s)
    write_wav("shuffle.wav", samples)

# 7. Level Complete
def gen_level_complete():
    chords = [
        [523.25, 659.25, 783.99],
        [587.33, 698.46, 880.00],
        [659.25, 783.99, 987.77],
        [1046.50, 1318.51, 1567.98]
    ]
    chord_dur = 0.2
    samples = []
    for chord in chords:
        count = int(SAMPLE_RATE * chord_dur)
        for i in range(count):
            t = i / SAMPLE_RATE
            mix = sum(math.sin(2 * math.pi * f * t) for f in chord) / len(chord)
            env = math.exp(-t * 6.0)
            samples.append(mix * env * 0.6)
    write_wav("level_complete.wav", samples)

# 8. Button Click
def gen_button_click():
    duration = 0.04
    count = int(SAMPLE_RATE * duration)
    samples = []
    freq = 650.0
    for i in range(count):
        t = i / SAMPLE_RATE
        env = math.exp(-t * 50.0)
        s = math.sin(2 * math.pi * freq * t) * env * 0.5
        samples.append(s)
    write_wav("button_click.wav", samples)

if __name__ == "__main__":
    gen_swipe_char()
    gen_word_matched()
    gen_wrong_word()
    gen_bonus_word()
    gen_hint_sound()
    gen_shuffle_sound()
    gen_level_complete()
    gen_button_click()
    print("Sound generation completed inside Assets/WordPuzzle/Sounds!")

import wave
import math
import struct
import os

AUDIO_DIR = r"d:\3d\unity\project\vrmine\Assets\SexyDistanceGimmick\Audio"
FILES = [
    "Whisper_Default.wav", "Whisper_Chest.wav", "Whisper_Neck.wav",
    "Whisper_Ear.wav", "Whisper_Waist.wav", "Whisper_Thigh.wav", "Touch.wav"
]

if not os.path.exists(AUDIO_DIR):
    os.makedirs(AUDIO_DIR)

def generate_wav(filename, duration=1.0, freq=440.0):
    path = os.path.join(AUDIO_DIR, filename)
    if os.path.exists(path):
        print(f"Skipping {filename} (already exists)")
        return

    sample_rate = 44100
    n_frames = int(sample_rate * duration)
    
    with wave.open(path, 'w') as wav_file:
        wav_file.setparams((1, 2, sample_rate, n_frames, 'NONE', 'not compressed'))
        
        data = []
        for i in range(n_frames):
            value = int(32767.0 * 0.1 * math.sin(2.0 * math.pi * freq * i / sample_rate))
            data.append(struct.pack('<h', value))
            
        wav_file.writeframes(b''.join(data))
    print(f"Generated {filename}")

for f in FILES:
    is_touch = "Touch" in f
    generate_wav(f, duration=0.5 if is_touch else 2.0, freq=880.0 if is_touch else 440.0)

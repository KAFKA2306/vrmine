from __future__ import annotations

import json
import queue
import random

from .app import Pet


def listen(
    pet: Pet,
    model_path: str,
    device: int | None = None,
    speak_probability: float = 0.18,
) -> None:
    import sounddevice as sd
    from vosk import KaldiRecognizer, Model

    audio = queue.Queue()
    model = Model(model_path)
    recognizer = KaldiRecognizer(model, 16000)
    rng = random.Random()

    def callback(indata, frames, time_info, status):
        audio.put(bytes(indata))

    with sd.RawInputStream(
        samplerate=16000,
        blocksize=8000,
        device=device,
        dtype="int16",
        channels=1,
        callback=callback,
    ):
        while True:
            data = audio.get()
            if not recognizer.AcceptWaveform(data):
                continue
            text = json.loads(recognizer.Result()).get("text", "").strip()
            if not text:
                continue
            pet.learn(text)
            if rng.random() < speak_probability:
                pet.speak()

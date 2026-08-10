from __future__ import annotations

from pathlib import Path

from .core import Sampler, Store, SudachiNounExtractor, encode_reading
from .osc import VrchatOsc


class Pet:
    def __init__(
        self,
        db_path: str,
        host: str = "127.0.0.1",
        port: int = 9000,
        seed: int | None = None,
    ) -> None:
        Path(db_path).expanduser().parent.mkdir(parents=True, exist_ok=True)
        self.store = Store(str(Path(db_path).expanduser()))
        self.extractor = SudachiNounExtractor()
        self.sampler = Sampler(seed=seed)
        self.osc = VrchatOsc(host, port)

    def learn(self, text: str) -> int:
        return self.store.learn(self.extractor.nouns(text))

    def sample(self):
        return self.sampler.choose(self.store.words())

    def speak(self):
        word = self.sample()
        if word is None:
            return None
        self.osc.speak(encode_reading(word.reading))
        return word

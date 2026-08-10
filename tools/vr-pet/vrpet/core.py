from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import math
import random
import sqlite3
from typing import Iterable, Protocol

KANA = "アイウエオカキクケコガギグゲゴサシスセソザジズゼゾタチツテトダヂヅデドナニヌネノハヒフヘホバビブベボパピプペポマミムメモヤユヨラリルレロワヲンァィゥェォャュョッーヴ"
KANA_TO_INT = {c: i + 1 for i, c in enumerate(KANA)}


@dataclass(frozen=True)
class Lexeme:
    text: str
    reading: str


@dataclass(frozen=True)
class Word:
    text: str
    reading: str
    count: int
    last_seen: float


class Extractor(Protocol):
    def nouns(self, text: str) -> list[Lexeme]:
        ...


class SudachiNounExtractor:
    def __init__(self) -> None:
        from sudachipy import dictionary, tokenizer

        self._tokenizer = dictionary.Dictionary().create()
        self._mode = tokenizer.Tokenizer.SplitMode.C

    def nouns(self, text: str) -> list[Lexeme]:
        result = []
        for morpheme in self._tokenizer.tokenize(text, self._mode):
            if morpheme.part_of_speech()[0] != "名詞":
                continue
            reading = morpheme.reading_form()
            if not reading or reading == "*":
                continue
            result.append(Lexeme(morpheme.surface(), reading))
        return result


class Store:
    def __init__(self, path: str) -> None:
        self.db = sqlite3.connect(path)
        self.db.execute(
            "create table if not exists words("
            "text text primary key,"
            "reading text not null,"
            "count integer not null,"
            "last_seen real not null)"
        )
        self.db.commit()

    def learn(self, lexemes: Iterable[Lexeme], now: float | None = None) -> int:
        ts = now if now is not None else datetime.now(timezone.utc).timestamp()
        learned = 0
        for lexeme in lexemes:
            if not lexeme.text.strip() or not lexeme.reading.strip():
                continue
            self.db.execute(
                "insert into words(text, reading, count, last_seen) values(?, ?, 1, ?)"
                " on conflict(text) do update set"
                " reading=excluded.reading,"
                " count=words.count+1,"
                " last_seen=excluded.last_seen",
                (lexeme.text, lexeme.reading, ts),
            )
            learned += 1
        self.db.commit()
        return learned

    def words(self) -> list[Word]:
        rows = self.db.execute(
            "select text, reading, count, last_seen from words order by text"
        ).fetchall()
        return [Word(*row) for row in rows]


class Sampler:
    def __init__(
        self,
        alpha: float = 0.5,
        beta: float = 0.8,
        decay_per_day: float = 0.035,
        seed: int | None = None,
    ) -> None:
        self.alpha = alpha
        self.beta = beta
        self.decay_per_day = decay_per_day
        self.rng = random.Random(seed)

    def choose(self, words: list[Word], now: float | None = None) -> Word | None:
        if not words:
            return None
        ts = now if now is not None else datetime.now(timezone.utc).timestamp()
        weights = []
        for word in words:
            age_days = max(0.0, ts - word.last_seen) / 86400.0
            weight = (word.count + self.alpha) ** self.beta
            weight *= math.exp(-self.decay_per_day * age_days)
            weights.append(weight)
        return self.rng.choices(words, weights=weights, k=1)[0]


def katakana(text: str) -> str:
    out = []
    for c in text:
        code = ord(c)
        if 0x3041 <= code <= 0x3096:
            out.append(chr(code + 0x60))
        else:
            out.append(c)
    return "".join(out)


def encode_reading(reading: str, slots: int = 8) -> list[int]:
    values = [KANA_TO_INT.get(c, 0) for c in katakana(reading)[:slots]]
    return values + [0] * (slots - len(values))

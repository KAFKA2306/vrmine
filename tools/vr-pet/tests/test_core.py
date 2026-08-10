import tempfile
import unittest

from vrpet.core import Lexeme, Sampler, Store, Word, encode_reading, katakana
from vrpet.osc import osc_message


class CoreTests(unittest.TestCase):
    def test_store_counts(self):
        with tempfile.NamedTemporaryFile() as f:
            store = Store(f.name)
            store.learn([Lexeme("猫", "ネコ")], now=100)
            store.learn([Lexeme("猫", "ネコ")], now=200)
            word = store.words()[0]
            self.assertEqual(word.count, 2)
            self.assertEqual(word.last_seen, 200)

    def test_sampler_prefers_frequency(self):
        sampler = Sampler(seed=7)
        words = [
            Word("a", "ア", 1, 1000),
            Word("b", "ビ", 50, 1000),
        ]
        picks = [sampler.choose(words, now=1000).text for _ in range(200)]
        self.assertGreater(picks.count("b"), picks.count("a"))

    def test_kana_encoding(self):
        self.assertEqual(katakana("ねこ"), "ネコ")
        encoded = encode_reading("ネコ", slots=4)
        self.assertEqual(len(encoded), 4)
        self.assertNotEqual(encoded[0], 0)
        self.assertEqual(encoded[-1], 0)

    def test_osc_int_message(self):
        msg = osc_message("/avatar/parameters/PetMood", 3)
        self.assertIn(b",i", msg)
        self.assertTrue(msg.endswith(b"\x00\x00\x00\x03"))

    def test_osc_bool_message(self):
        self.assertIn(b",T", osc_message("/avatar/parameters/PetSpeak", True))
        self.assertIn(b",F", osc_message("/avatar/parameters/PetSpeak", False))


if __name__ == "__main__":
    unittest.main()

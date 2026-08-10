from __future__ import annotations

import argparse

from .app import Pet
from .voice import listen


DEFAULT_DB = "~/.vrmine/vrpet.sqlite3"


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="vrpet")
    p.add_argument("--db", default=DEFAULT_DB)
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=9000)
    commands = p.add_subparsers(dest="command", required=True)

    learn = commands.add_parser("learn")
    learn.add_argument("text")

    commands.add_parser("sample")

    voice = commands.add_parser("listen")
    voice.add_argument("--model", required=True)
    voice.add_argument("--device", type=int)
    voice.add_argument("--speak-probability", type=float, default=0.18)
    return p


def main() -> None:
    args = parser().parse_args()
    pet = Pet(args.db, args.host, args.port)

    if args.command == "learn":
        print(pet.learn(args.text))
        return

    if args.command == "sample":
        word = pet.speak()
        if word is not None:
            print(f"{word.text}\t{word.reading}")
        return

    listen(pet, args.model, args.device, args.speak_probability)


if __name__ == "__main__":
    main()

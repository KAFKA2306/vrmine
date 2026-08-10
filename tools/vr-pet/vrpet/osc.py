from __future__ import annotations

import socket
import struct
import time


def _osc_string(value: str) -> bytes:
    data = value.encode("utf-8") + b"\0"
    return data + b"\0" * ((4 - len(data) % 4) % 4)


def osc_message(address: str, value: int | bool) -> bytes:
    if isinstance(value, bool):
        return _osc_string(address) + _osc_string(",T" if value else ",F")
    return _osc_string(address) + _osc_string(",i") + struct.pack(">i", value)


class VrchatOsc:
    def __init__(self, host: str = "127.0.0.1", port: int = 9000) -> None:
        self.target = (host, port)
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    def send_int(self, name: str, value: int) -> None:
        self.socket.sendto(
            osc_message(f"/avatar/parameters/{name}", int(value)), self.target
        )

    def send_bool(self, name: str, value: bool) -> None:
        self.socket.sendto(
            osc_message(f"/avatar/parameters/{name}", bool(value)), self.target
        )

    def speak(self, chars: list[int], mood: int = 0, pulse_seconds: float = 0.18) -> None:
        for index, value in enumerate(chars):
            self.send_int(f"PetChar{index}", value)
        self.send_int("PetMood", max(0, min(255, mood)))
        self.send_bool("PetSpeak", True)
        time.sleep(pulse_seconds)
        self.send_bool("PetSpeak", False)

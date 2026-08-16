#!/usr/bin/env python3
"""Generate disposable large-file fixtures in this directory."""

from pathlib import Path

HERE = Path(__file__).resolve().parent


def main() -> None:
    many_lines = HERE / "many-lines-10mb.txt"
    long_line = HERE / "single-line-5mb.txt"
    line = "0123456789 abcdefghijklmnopqrstuvwxyz\n"

    with many_lines.open("w", encoding="utf-8", newline="") as handle:
        while handle.tell() < 10 * 1024 * 1024:
            handle.write(line)

    with long_line.open("w", encoding="utf-8", newline="") as handle:
        handle.write("x" * (5 * 1024 * 1024))

    print(many_lines)
    print(long_line)


if __name__ == "__main__":
    main()

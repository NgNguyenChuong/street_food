#!/usr/bin/env python3
"""
Edge-TTS Wrapper Script
Su dung edge-tts de generate audio tu text voi voice chi dinh
"""
import sys
import asyncio
import io

# Bao ve stderr/stdout truoc ky tu Unicode de tranh UnicodeEncodeError tren Windows
if hasattr(sys.stderr, 'buffer'):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')
if hasattr(sys.stdout, 'buffer'):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import edge_tts

def err(msg):
    """In loi ra stderr (an toan, khong raise)."""
    try:
        print(msg, file=sys.stderr, flush=True)
    except Exception:
        pass

async def main():
    if len(sys.argv) < 4:
        err("Usage: python tts_wrapper.py <voice> <text_or_@file> <output_path> [rate] [volume]")
        sys.exit(1)

    voice       = sys.argv[1]
    text_input  = sys.argv[2]
    output_path = sys.argv[3]
    rate        = sys.argv[4] if len(sys.argv) > 4 else "+0%"
    volume      = sys.argv[5] if len(sys.argv) > 5 else "+0%"

    # Neu text bat dau bang @, doc tu file
    if text_input.startswith("@"):
        file_path = text_input[1:]
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                text = f.read()
        except Exception as e:
            err(f"Error reading file '{file_path}': {e}")
            sys.exit(1)
    else:
        text = text_input

    if not text or not text.strip():
        err("Error: text is empty after reading")
        sys.exit(1)

    # Generate audio
    try:
        communicate = edge_tts.Communicate(text, voice, rate=rate, volume=volume)
        await communicate.save(output_path)
        print(f"OK: audio saved to {output_path}", flush=True)
    except Exception as e:
        err(f"Error generating audio (voice={voice}): {e}")
        sys.exit(1)

if __name__ == "__main__":
    asyncio.run(main())

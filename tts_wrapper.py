#!/usr/bin/env python3
"""
Edge-TTS Wrapper Script
Su dung edge-tts de generate audio tu text voi voice chi dinh
"""
import sys
import asyncio
import io
import random
import tempfile
import edge_tts
from pathlib import Path

# Bao ve stderr/stdout truoc ky tu Unicode de tranh UnicodeEncodeError tren Windows
if hasattr(sys.stderr, 'buffer'):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')
if hasattr(sys.stdout, 'buffer'):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')


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

    voice       = sys.argv[1].strip()
    text_input  = sys.argv[2]
    output_path = sys.argv[3]
    rate        = (sys.argv[4].strip() if len(sys.argv) > 4 else "+0%")
    volume      = (sys.argv[5].strip() if len(sys.argv) > 5 else "+0%")

    # Voice fallback chain for intermittent provider-side failures.
    fallback_voices = {
        "vi-VN-HoaiMyNeural": ["vi-VN-NamMinhNeural"],
        "en-US-JennyNeural": ["en-US-GuyNeural"],
        "zh-CN-XiaoxiaoNeural": ["zh-CN-YunxiNeural"],
    }

    voice_candidates = [voice]
    for candidate in fallback_voices.get(voice, []):
        if candidate not in voice_candidates:
            voice_candidates.append(candidate)

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

    # Generate audio (retry for intermittent Edge-TTS empty-audio responses)
    attempts = 3
    last_error = None
    output_file = Path(output_path)
    output_file.parent.mkdir(parents=True, exist_ok=True)

    for current_voice in voice_candidates:
        for attempt in range(1, attempts + 1):
            tmp_file = None
            try:
                with tempfile.NamedTemporaryFile(
                    prefix="tts_tmp_",
                    suffix=".mp3",
                    dir=str(output_file.parent),
                    delete=False,
                ) as tmp:
                    tmp_file = Path(tmp.name)

                communicate = edge_tts.Communicate(text, current_voice, rate=rate, volume=volume)
                await communicate.save(str(tmp_file))

                if not tmp_file.exists() or tmp_file.stat().st_size <= 0:
                    raise RuntimeError("Output file is empty")

                tmp_file.replace(output_file)
                print(f"OK: audio saved to {output_path} (voice={current_voice})", flush=True)
                return
            except Exception as e:
                last_error = e
                try:
                    if tmp_file is not None and tmp_file.exists():
                        tmp_file.unlink()
                except Exception:
                    pass

                if attempt < attempts:
                    base_sleep = 0.7 * attempt
                    await asyncio.sleep(base_sleep + random.uniform(0.05, 0.25))

    err(
        f"Error generating audio (voice={voice}, tried={','.join(voice_candidates)}, "
        f"rate={rate}, volume={volume}): {last_error}"
    )
    sys.exit(1)

if __name__ == "__main__":
    asyncio.run(main())

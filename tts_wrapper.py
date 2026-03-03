#!/usr/bin/env python3
"""
Edge-TTS Wrapper Script
Sử dụng edge-tts để generate audio từ text với voice chỉ định
"""
import sys
import asyncio
import edge_tts

async def main():
    if len(sys.argv) < 4:
        print("Usage: python tts_wrapper.py <voice> <text_or_@file> <output_path>")
        sys.exit(1)
    
    voice = sys.argv[1]
    text_input = sys.argv[2]
    output_path = sys.argv[3]
    
    # If text starts with @, read from file
    if text_input.startswith("@"):
        file_path = text_input[1:]
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                text = f.read()
        except Exception as e:
            print(f"Error reading file: {e}")
            sys.exit(1)
    else:
        text = text_input
    
    # Generate audio using edge-tts
    try:
        communicate = edge_tts.Communicate(text, voice)
        await communicate.save(output_path)
        print(f"Audio saved to: {output_path}")
    except Exception as e:
        print(f"Error generating audio: {e}")
        sys.exit(1)

if __name__ == "__main__":
    asyncio.run(main())

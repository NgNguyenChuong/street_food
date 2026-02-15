"""
Simple Edge-TTS wrapper for C# to call
Usage: python tts_wrapper.py <voice> <text> <output_file>
"""
import sys
import asyncio
import edge_tts

async def generate_tts(voice, text, output_file):
    try:
        communicate = edge_tts.Communicate(text, voice)
        await communicate.save(output_file)
        print(f"SUCCESS: Audio saved to {output_file}")
        return 0
    except Exception as e:
        print(f"ERROR: {str(e)}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    if len(sys.argv) != 4:
        print("Usage: python tts_wrapper.py <voice> <text|@file> <output_file>")
        sys.exit(1)
    
    voice = sys.argv[1]
    text = sys.argv[2]
    output_file = sys.argv[3]
    
    # If text starts with @, read from file
    if text.startswith('@'):
        text_file = text[1:]  # Remove @ prefix
        try:
            with open(text_file, 'r', encoding='utf-8') as f:
                text = f.read()
        except Exception as e:
            print(f"ERROR: Failed to read text file: {e}", file=sys.stderr)
            sys.exit(1)
    
    exit_code = asyncio.run(generate_tts(voice, text, output_file))
    sys.exit(exit_code)

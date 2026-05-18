"""
0.Data/Audio 의 WAV 클립에서 앞뒤 무음을 제거해 0.Data/ClearAudio 에 저장합니다.

Requirements:
    pip install pydub
    ffmpeg 설치 후 PATH 등록 (https://ffmpeg.org/)

Usage:
    python trim_silence.py
    python trim_silence.py --threshold -45 --padding 50
"""

import argparse
from pathlib import Path
from pydub import AudioSegment
from pydub.silence import detect_nonsilent


def trim_silence(audio: AudioSegment, threshold_db: float, padding_ms: int) -> AudioSegment:
    chunks = detect_nonsilent(audio, min_silence_len=100, silence_thresh=threshold_db)
    if not chunks:
        return audio
    start = max(0, chunks[0][0] - padding_ms)
    end = min(len(audio), chunks[-1][1] + padding_ms)
    return audio[start:end]


def process(input_dir: Path, output_dir: Path, threshold_db: float, padding_ms: int):
    wav_files = list(input_dir.glob("*.wav")) + list(input_dir.glob("*.WAV"))
    if not wav_files:
        print(f"[!] {input_dir} 에 WAV 파일이 없습니다.")
        return

    output_dir.mkdir(parents=True, exist_ok=True)
    ok, skipped = 0, 0

    for src in sorted(wav_files):
        try:
            audio = AudioSegment.from_wav(src)
            trimmed = trim_silence(audio, threshold_db, padding_ms)
            dst = output_dir / src.name
            trimmed.export(dst, format="wav")
            saved_ms = len(audio) - len(trimmed)
            print(f"[OK] {src.name}  {len(audio)}ms → {len(trimmed)}ms  (제거: {saved_ms}ms)")
            ok += 1
        except Exception as e:
            print(f"[SKIP] {src.name}: {e}")
            skipped += 1

    print(f"\n완료: {ok}개 처리, {skipped}개 건너뜀")
    print(f"저장 위치: {output_dir.resolve()}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WAV 무음 구간 자동 제거")
    parser.add_argument("--input",     default="../Assets/0.Data/Audio",      help="입력 폴더")
    parser.add_argument("--output",    default="../Assets/0.Data/ClearAudio", help="출력 폴더")
    parser.add_argument("--threshold", type=float, default=-45.0,      help="무음 판정 dBFS (기본: -45)")
    parser.add_argument("--padding",   type=int,   default=50,         help="앞뒤 여유 ms (기본: 50)")
    args = parser.parse_args()

    base = Path(__file__).parent
    process(
        input_dir=Path(args.input) if Path(args.input).is_absolute() else base / args.input,
        output_dir=Path(args.output) if Path(args.output).is_absolute() else base / args.output,
        threshold_db=args.threshold,
        padding_ms=args.padding,
    )

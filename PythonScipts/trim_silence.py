"""
WAV 파일에서 소리가 나는 구간(첫 소리 ~ 마지막 소리)만 잘라 저장합니다.
단일 파일 또는 폴더(하위 폴더 포함)를 입력받습니다.

Requirements:
    pip install pydub
    ffmpeg 설치 후 PATH 등록 (https://ffmpeg.org/)

Usage:
    python trim_silence.py --input path/to/file.wav
    python trim_silence.py --input path/to/dir/
    python trim_silence.py --input path/to/dir/ --output path/to/out/ --threshold -40 --padding 100
"""

import argparse
from pathlib import Path
from pydub import AudioSegment
from pydub.silence import detect_nonsilent


def trim_sound(audio: AudioSegment, threshold_db: float, padding_ms: int, min_silence_ms: int) -> AudioSegment:
    chunks = detect_nonsilent(audio, min_silence_len=min_silence_ms, silence_thresh=threshold_db)
    if not chunks:
        return audio
    start = max(0, chunks[0][0] - padding_ms)
    end = min(len(audio), chunks[0][1] + padding_ms)
    return audio[start:end]


def collect_wav_files(input_path: Path) -> list[Path]:
    if input_path.is_file():
        return [input_path]
    return sorted(
        {p.resolve() for p in input_path.rglob("*.wav")}
        | {p.resolve() for p in input_path.rglob("*.WAV")}
    )


def process(input_path: Path, output_dir: Path | None, threshold_db: float, padding_ms: int, min_silence_ms: int):
    files = collect_wav_files(input_path)
    if not files:
        print(f"[!] WAV 파일이 없습니다: {input_path}")
        return

    ok, skipped = 0, 0
    base_dir = input_path if input_path.is_dir() else input_path.parent

    for src in files:
        try:
            if output_dir is not None:
                rel = src.relative_to(base_dir)
                dst = output_dir / rel
            else:
                dst = src

            audio = AudioSegment.from_wav(src)
            trimmed = trim_sound(audio, threshold_db, padding_ms, min_silence_ms)
            dst.parent.mkdir(parents=True, exist_ok=True)
            trimmed.export(dst, format="wav")
            removed_ms = len(audio) - len(trimmed)
            print(f"[OK] {src.name}  {len(audio)}ms → {len(trimmed)}ms  (제거: {removed_ms}ms)")
            ok += 1
        except Exception as e:
            print(f"[SKIP] {src.name}: {e}")
            skipped += 1

    print(f"\n완료: {ok}개 처리, {skipped}개 건너뜀")
    out_repr = output_dir.resolve() if output_dir else "(원본 덮어쓰기)"
    print(f"저장 위치: {out_repr}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WAV 앞뒤 무음 자동 제거")
    parser.add_argument("--input",       required=True,             help="입력 WAV 파일 또는 폴더")
    parser.add_argument("--output",      default=None,              help="출력 폴더 (생략 시 원본 덮어쓰기)")
    parser.add_argument("--threshold",   type=float, default=-65.0, help="무음 판정 dBFS (기본: -45)")
    parser.add_argument("--padding",     type=int,   default=50,    help="앞뒤 여유 ms (기본: 50)")
    parser.add_argument("--min_silence", type=int,   default=100,   help="무음 판정 최소 길이 ms (기본: 100)")
    args = parser.parse_args()

    input_path = Path(args.input).resolve()
    output_dir = Path(args.output).resolve() if args.output else None

    process(input_path, output_dir, args.threshold, args.padding, args.min_silence)

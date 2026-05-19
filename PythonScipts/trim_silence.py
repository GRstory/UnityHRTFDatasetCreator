"""
0.Data/Audio 의 WAV 클립에서 소리 이벤트를 개별 파일로 분리해 0.Data/ClearAudio 에 저장합니다.

Requirements:
    pip install pydub
    ffmpeg 설치 후 PATH 등록 (https://ffmpeg.org/)

Usage:
    python trim_silence.py
    python trim_silence.py --threshold -45 --padding 50 --min_silence 300
"""

import argparse
from pathlib import Path
from pydub import AudioSegment
from pydub.silence import detect_nonsilent


def split_sounds(audio: AudioSegment, threshold_db: float, padding_ms: int, min_silence_ms: int) -> list[AudioSegment]:
    chunks = detect_nonsilent(audio, min_silence_len=min_silence_ms, silence_thresh=threshold_db)
    result = []
    for start, end in chunks:
        s = max(0, start - padding_ms)
        e = min(len(audio), end + padding_ms)
        result.append(audio[s:e])
    return result


def process(input_dir: Path, output_dir: Path, threshold_db: float, padding_ms: int, min_silence_ms: int):
    wav_files = list({p.resolve() for p in input_dir.glob("*.wav")} | {p.resolve() for p in input_dir.glob("*.WAV")})
    if not wav_files:
        print(f"[!] {input_dir} 에 WAV 파일이 없습니다.")
        return

    output_dir.mkdir(parents=True, exist_ok=True)
    ok, skipped = 0, 0

    for src in sorted(wav_files):
        try:
            audio = AudioSegment.from_wav(src)
            segments = split_sounds(audio, threshold_db, padding_ms, min_silence_ms)
            if not segments:
                print(f"[SKIP] {src.name}: 소리 구간 없음")
                skipped += 1
                continue
            for i, seg in enumerate(segments):
                dst = output_dir / f"{src.stem}_{i+1:03d}.wav"
                seg.export(dst, format="wav")
            print(f"[OK] {src.name}  → {len(segments)}개 클립")
            ok += len(segments)
        except Exception as e:
            print(f"[SKIP] {src.name}: {e}")
            skipped += 1

    print(f"\n완료: {ok}개 클립 저장, {skipped}개 건너뜀")
    print(f"저장 위치: {output_dir.resolve()}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="WAV 소리 이벤트 분리")
    parser.add_argument("--input",       default="../Assets/0.Data/Audio",      help="입력 폴더")
    parser.add_argument("--output",      default="../Assets/0.Data/ClearAudio", help="출력 폴더")
    parser.add_argument("--threshold",   type=float, default=-45.0, help="무음 판정 dBFS (기본: -45)")
    parser.add_argument("--padding",     type=int,   default=50,    help="앞뒤 여유 ms (기본: 50)")
    parser.add_argument("--min_silence", type=int,   default=300,   help="소리 간 최소 무음 길이 ms (기본: 300)")
    args = parser.parse_args()

    base = Path(__file__).parent
    process(
        input_dir=Path(args.input) if Path(args.input).is_absolute() else base / args.input,
        output_dir=Path(args.output) if Path(args.output).is_absolute() else base / args.output,
        threshold_db=args.threshold,
        padding_ms=args.padding,
        min_silence_ms=args.min_silence,
    )

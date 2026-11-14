import math
import os
import struct
import wave

SAMPLE_RATE = 44100
DURATION = 32.4  # seconds (covers 32s video + tail)
BPM = 114.0
BEAT_SEC = 60.0 / BPM


def clamp(v: float, lo: float = -1.0, hi: float = 1.0) -> float:
    return max(lo, min(hi, v))


def soft_clip(v: float, drive: float = 2.2) -> float:
    return math.tanh(v * drive) / math.tanh(drive)


def midi_to_freq(midi: float) -> float:
    return 440.0 * (2.0 ** ((midi - 69.0) / 12.0))


def generate_wav(out_path: str) -> None:
    total_samples = int(SAMPLE_RATE * DURATION)
    # Dark hype progression in A minor / F / G / E: Am -> F -> C -> G (or Am -> F -> Dm -> E)
    bass_roots = [33, 29, 36, 31]  # A1, F1, C2, G1
    chords = [
        [57, 60, 64, 69],  # Am
        [53, 57, 60, 65],  # F
        [55, 60, 64, 67],  # C
        [55, 59, 62, 67],  # G
    ]
    arp_seq = [0, 2, 1, 3, 2, 1, 3, 2]
    drop_time = 5.2

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with wave.open(out_path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)

        frames = bytearray()
        for i in range(total_samples):
            t = i / SAMPLE_RATE
            beat_pos = t / BEAT_SEC
            bar_idx = int(beat_pos // 4) % len(chords)
            beat_in_bar = beat_pos % 4.0
            step_16 = (beat_pos * 4.0) % 1.0  # 16th-note grid

            chord = chords[bar_idx]
            bass_midi = bass_roots[bar_idx]

            # Deterministic pseudo-noise
            noise = math.sin(i * 12.9898) * 43758.5453
            noise = (noise - math.floor(noise)) * 2.0 - 1.0

            # --- 0.0s to 5.2s: HYPE BUILD-UP & TENSION RISER ---
            if t < drop_time:
                prog = t / drop_time
                # Pulsing filtered saw-like bass
                bf = midi_to_freq(33)
                pulse_env = math.exp(-4.5 * ((beat_pos * 2.0) % 1.0))
                b = (
                    math.sin(2.0 * math.pi * bf * t)
                    + 0.5 * math.sin(4.0 * math.pi * bf * t)
                    + 0.25 * math.sin(6.0 * math.pi * bf * t)
                ) * pulse_env * (0.18 + 0.28 * prog)

                # Accelerating snare/clap build
                roll_rate = 2.0 if t < 2.6 else (4.0 if t < 4.2 else 8.0)
                rt = (beat_pos * roll_rate) % 1.0
                snare = (
                    noise * math.exp(-22.0 * rt)
                    + math.sin(2.0 * math.pi * 185.0 * t) * math.exp(-18.0 * rt) * 0.4
                ) * (0.12 + 0.35 * prog)

                # Warm low-end tension swell (stays deep, never high-pitched) + subtle whoosh at end
                riser_f = 72.0 + prog * 115.0
                riser = math.sin(2.0 * math.pi * riser_f * t) * (prog ** 1.3) * 0.14
                if t > drop_time - 0.55:
                    wt = (t - (drop_time - 0.55)) / 0.55
                    riser += noise * (wt ** 2.0) * 0.10

                fade_in = min(1.0, t / 0.15)
                mix = clamp(soft_clip((b + snare + riser) * fade_in, 1.5) * 0.88)
                frames += struct.pack("<hh", int(mix * 32767), int(mix * 32767))
                continue

            # --- 5.2s onwards: HEAVY HYPE ELECTRO DROP ---
            td = t - drop_time
            pump = 0.22 + 0.78 * min(1.0, (beat_pos % 1.0) * 3.0)

            # 1. Punchy 4-on-the-floor Kick
            bt = t % BEAT_SEC
            kick = 0.0
            if t < 29.8 and bt < 0.20:
                kf = 145.0 * math.exp(-24.0 * bt) + 46.0
                kick = math.sin(2.0 * math.pi * kf * bt) * math.exp(-12.0 * bt) * 0.62

            # 2. Snappy Clap on beats 2 & 4
            clap = 0.0
            if t < 29.8 and (beat_in_bar >= 1.0 and beat_in_bar < 1.16 or beat_in_bar >= 3.0 and beat_in_bar < 3.16):
                ct = (beat_in_bar % 2.0 - 1.0) * BEAT_SEC
                if ct >= 0:
                    clap = noise * math.exp(-32.0 * ct) * 0.32

            # 3. Crisp off-beat Hi-Hat
            hat = 0.0
            if t < 29.8:
                ht = (t + BEAT_SEC * 0.5) % BEAT_SEC
                if ht < 0.05:
                    hat = noise * math.exp(-85.0 * ht) * 0.12

            # 4. Driving Midtempo / Electro Bassline (16th-note groove + grit)
            bf = midi_to_freq(bass_midi)
            bass_env = math.exp(-3.2 * step_16)
            raw_bass = (
                math.sin(2.0 * math.pi * bf * t) * 0.55
                + math.sin(4.0 * math.pi * bf * t) * 0.32
                + math.sin(6.0 * math.pi * bf * t) * 0.18
                + math.sin(8.0 * math.pi * bf * t) * 0.10
            )
            bass = soft_clip(raw_bass * 1.4, 2.5) * bass_env * pump * 0.42

            # 5. Wide Synth Stab / Pad
            pad = 0.0
            for idx, m in enumerate(chord):
                f = midi_to_freq(m)
                pad += math.sin(2.0 * math.pi * f * t) * 0.11
                pad += math.sin(2.0 * math.pi * f * 2.0 * t + idx) * 0.04
            pad *= pump

            # 6. Energetic 16th-note Hype Arp Lead (kicks in full on drop & chorus)
            arp_idx = int(beat_pos * 4.0) % len(arp_seq)
            arp_midi = chord[arp_seq[arp_idx]] + 12
            af = midi_to_freq(arp_midi)
            arp_env = math.exp(-7.5 * step_16)
            arp = (
                math.sin(2.0 * math.pi * af * t) * 0.16
                + math.sin(4.0 * math.pi * af * t) * 0.06
            ) * arp_env * (1.0 if t < 28.5 else 0.7)

            # Drop impact boom at 5.2s
            impact = 0.0
            if td < 0.45:
                impact = math.sin(2.0 * math.pi * 55.0 * td) * math.exp(-6.0 * td) * 0.45 + noise * math.exp(-14.0 * td) * 0.25

            fade_out = 1.0 if t < DURATION - 1.4 else max(0.0, (DURATION - t) / 1.4)
            stereo_w = math.sin(2.0 * math.pi * 0.6 * t) * 0.08

            left = clamp(soft_clip((kick + clap + hat + bass + pad * (1.0 - stereo_w) + arp * (1.0 + stereo_w) + impact) * fade_out, 1.6) * 0.88)
            right = clamp(soft_clip((kick + clap + hat + bass + pad * (1.0 + stereo_w) + arp * (1.0 - stereo_w) + impact) * fade_out, 1.6) * 0.88)

            frames += struct.pack("<hh", int(left * 32767), int(right * 32767))

        wf.writeframes(frames)


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(__file__), "public", "uppy-soundtrack.wav")
    generate_wav(out)
    print(f"Generated {out}")

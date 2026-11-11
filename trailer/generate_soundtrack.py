import math
import struct
import wave
import os

SAMPLE_RATE = 48000
DURATION = 40.0  # seconds
TOTAL_SAMPLES = int(SAMPLE_RATE * DURATION)

left_channel = [0.0] * TOTAL_SAMPLES
right_channel = [0.0] * TOTAL_SAMPLES

def clamp(v, min_v=-1.0, max_v=1.0):
    return max(min_v, min(max_v, v))

def add_drone(start_s, end_s, freq_left, freq_right, amp, attack_s=1.5, decay_s=2.0):
    start_idx = int(start_s * SAMPLE_RATE)
    end_idx = min(TOTAL_SAMPLES, int(end_s * SAMPLE_RATE))
    dur = end_idx - start_idx
    att_samples = int(attack_s * SAMPLE_RATE)
    dec_samples = int(decay_s * SAMPLE_RATE)

    for i in range(dur):
        idx = start_idx + i
        if idx >= TOTAL_SAMPLES:
            break
        # Envelope
        env = 1.0
        if i < att_samples:
            env = 0.5 * (1.0 - math.cos(math.pi * i / att_samples))
        elif i > dur - dec_samples:
            env = 0.5 * (1.0 + math.cos(math.pi * (i - (dur - dec_samples)) / dec_samples))

        t = i / SAMPLE_RATE
        # Rich harmonic content (fundamental + 2nd + 3rd harmonic)
        val_l = (math.sin(2 * math.pi * freq_left * t) +
                 0.4 * math.sin(2 * math.pi * freq_left * 2 * t) +
                 0.15 * math.sin(2 * math.pi * freq_left * 3 * t)) * amp * env
        val_r = (math.sin(2 * math.pi * freq_right * t) +
                 0.4 * math.sin(2 * math.pi * freq_right * 2 * t) +
                 0.15 * math.sin(2 * math.pi * freq_right * 3 * t)) * amp * env

        # Subtle analog saturation
        val_l = math.tanh(val_l * 1.2)
        val_r = math.tanh(val_r * 1.2)

        left_channel[idx] += val_l
        right_channel[idx] += val_r

def add_pad_chord(start_s, end_s, freqs, amp, attack_s=2.0, decay_s=2.5):
    start_idx = int(start_s * SAMPLE_RATE)
    end_idx = min(TOTAL_SAMPLES, int(end_s * SAMPLE_RATE))
    dur = end_idx - start_idx
    att_samples = int(attack_s * SAMPLE_RATE)
    dec_samples = int(decay_s * SAMPLE_RATE)

    for i in range(dur):
        idx = start_idx + i
        if idx >= TOTAL_SAMPLES:
            break
        env = 1.0
        if i < att_samples:
            env = 0.5 * (1.0 - math.cos(math.pi * i / att_samples))
        elif i > dur - dec_samples:
            env = 0.5 * (1.0 + math.cos(math.pi * (i - (dur - dec_samples)) / dec_samples))

        t = i / SAMPLE_RATE
        # LFO filter sweep effect
        lfo = 0.7 + 0.3 * math.sin(2 * math.pi * 0.25 * t)
        chord_l = 0.0
        chord_r = 0.0
        for fi, f in enumerate(freqs):
            pan = 0.3 + 0.4 * (fi / max(1, len(freqs) - 1))
            wave_val = math.sin(2 * math.pi * f * t) + 0.25 * math.sin(2 * math.pi * f * 2 * t)
            chord_l += wave_val * (1.0 - pan)
            chord_r += wave_val * pan

        scale = (amp / len(freqs)) * env * lfo
        left_channel[idx] += chord_l * scale
        right_channel[idx] += chord_r * scale

def add_tick(time_s, amp=0.25, freq=2800):
    idx = int(time_s * SAMPLE_RATE)
    dur = int(0.045 * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        t = i / SAMPLE_RATE
        env = math.exp(-i / (0.006 * SAMPLE_RATE))
        noise = (math.sin(i * 12345.67) % 1.0) * 0.4
        tonal = math.sin(2 * math.pi * freq * t) * 0.6
        val = (noise + tonal) * env * amp
        left_channel[target] += val * 0.9
        right_channel[target] += val * 1.1

def add_impact(time_s, amp=0.7, start_freq=120, end_freq=38, duration_s=1.8):
    idx = int(time_s * SAMPLE_RATE)
    dur = int(duration_s * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        frac = i / dur
        env = math.exp(-frac * 4.5)
        # Pitch sweep down
        curr_freq = start_freq + (end_freq - start_freq) * (frac ** 0.5)
        phase = 2 * math.pi * (start_freq * (i / SAMPLE_RATE) - (start_freq - end_freq) * ((i / SAMPLE_RATE) ** 1.5) / 2.0)
        sub = math.sin(phase) * env * amp
        # Saturation warmth
        sub = math.tanh(sub * 1.5) * amp
        # Sub stereo spread
        left_channel[target] += sub * 0.98
        right_channel[target] += sub * 1.02

def add_whoosh(start_s, duration_s=1.2, amp=0.35, pan_dir=1):
    idx = int(start_s * SAMPLE_RATE)
    dur = int(duration_s * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        frac = i / dur
        # Bell curve envelope
        env = math.sin(math.pi * frac) ** 2
        # Filtered noise sweep
        noise = (math.sin(i * 9876.54) % 1.0) * 2.0 - 1.0
        f = 400 + 1600 * frac
        val = noise * math.sin(2 * math.pi * f * (i / SAMPLE_RATE)) * env * amp
        pan = 0.2 + 0.6 * (frac if pan_dir > 0 else 1.0 - frac)
        left_channel[target] += val * (1.0 - pan)
        right_channel[target] += val * pan

def add_key_click(time_s, amp=0.4):
    idx = int(time_s * SAMPLE_RATE)
    dur = int(0.06 * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        t = i / SAMPLE_RATE
        env = math.exp(-i / (0.008 * SAMPLE_RATE))
        thock = math.sin(2 * math.pi * 320 * t) * 0.7 + math.sin(2 * math.pi * 1800 * t) * 0.3
        val = thock * env * amp
        left_channel[target] += val * 1.1
        right_channel[target] += val * 0.9

def add_riser(start_s, duration_s=4.0, amp=0.3):
    idx = int(start_s * SAMPLE_RATE)
    dur = int(duration_s * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        frac = i / dur
        env = frac ** 2.2 * amp
        freq = 120 * (18.0 ** frac)  # 120Hz to ~2160Hz
        val = math.sin(2 * math.pi * freq * (i / SAMPLE_RATE))
        saw = (2 * ((freq * (i / SAMPLE_RATE)) % 1.0) - 1.0) * 0.3
        sound = (val + saw) * env
        left_channel[target] += sound * 0.95
        right_channel[target] += sound * 1.05

def add_pulse_beat(time_s, amp=0.35, freq=55):
    idx = int(time_s * SAMPLE_RATE)
    dur = int(0.25 * SAMPLE_RATE)
    for i in range(dur):
        target = idx + i
        if target >= TOTAL_SAMPLES:
            break
        t = i / SAMPLE_RATE
        env = math.exp(-t * 18.0)
        val = math.sin(2 * math.pi * freq * t) * env * amp
        left_channel[target] += val
        right_channel[target] += val

print("Synthesizing cinematic score...")

# 1. Base atmospheric sub-drones across the film
add_drone(0.0, 15.0, 32.7, 32.9, 0.45, attack_s=2.0, decay_s=3.0)  # C1 deep drone
add_drone(14.0, 28.0, 38.9, 39.1, 0.40, attack_s=2.0, decay_s=2.0) # Eb1 deep drone
add_drone(27.0, 35.5, 43.6, 43.8, 0.45, attack_s=1.5, decay_s=1.5) # F1 deep drone
add_drone(34.5, 40.0, 32.7, 32.8, 0.50, attack_s=1.0, decay_s=3.5) # Final C1 resolution

# 2. Harmonic chord pads
# Act I Intro (C minor mysterious pad: C3 130.81, G3 196.00, D4 293.66, Eb4 311.13)
add_pad_chord(0.5, 7.5, [130.81, 196.00, 293.66, 311.13], 0.28, attack_s=2.5, decay_s=2.0)
# Act II Reveal (Rich C sus2: C3 130.81, G3 196.00, D4 293.66, G4 392.00)
add_pad_chord(7.0, 15.5, [130.81, 196.00, 293.66, 392.00], 0.32, attack_s=1.5, decay_s=2.5)
# Act III Pillars progression (Eb -> F -> G)
add_pad_chord(15.0, 20.0, [155.56, 233.08, 311.13, 392.00], 0.30, attack_s=1.2, decay_s=1.5)
add_pad_chord(19.5, 24.0, [174.61, 261.63, 349.23, 440.00], 0.32, attack_s=1.0, decay_s=1.5)
add_pad_chord(23.5, 28.0, [196.00, 293.66, 392.00, 493.88], 0.34, attack_s=1.0, decay_s=1.5)
# Act IV Rhythmic crescendo (C minor power chord: C3, G3, C4, Eb4, G4)
add_pad_chord(27.5, 35.0, [130.81, 196.00, 261.63, 311.13, 392.00], 0.38, attack_s=0.8, decay_s=1.0)
# Act V Grand resolve (Warm C Major with natural 9th: C3, G3, D4, E4, G4)
add_pad_chord(35.0, 40.0, [130.81, 196.00, 293.66, 329.63, 392.00], 0.42, attack_s=0.4, decay_s=3.8)

# 3. Synchronized Sound Design & Micro-SFX
# Act I Clock ticks
for sec in [1.0, 2.0, 3.0, 4.0, 5.0, 6.0]:
    add_tick(sec, amp=0.18, freq=3200)

# Act II Reveal impact & whoosh
add_whoosh(6.2, duration_s=1.0, amp=0.35, pan_dir=1)
add_impact(7.0, amp=0.75, start_freq=130, end_freq=35, duration_s=2.2)

# Act II Uptime metric counting pulses
for sec in [8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0]:
    add_tick(sec, amp=0.15, freq=2400)
    add_pulse_beat(sec, amp=0.20, freq=65)

# Act III TimeRuler sweep (15.0 - 18.0)
add_whoosh(15.0, duration_s=1.4, amp=0.30, pan_dir=-1)
for i in range(16):
    add_tick(15.2 + i * 0.18, amp=0.12, freq=2000 + i * 80)

# Act III Hotkey press at 19.2s (Win + Alt + U)
add_key_click(19.18, amp=0.45)
add_key_click(19.24, amp=0.50)
add_impact(19.30, amp=0.55, start_freq=95, end_freq=45, duration_s=1.2)

# Act III Stability forensic transition (23.0s)
add_impact(23.0, amp=0.60, start_freq=110, end_freq=40, duration_s=1.6)

# Act IV Rhythmic Drive (27.0 - 35.0)
for beat in range(16):
    t_beat = 27.0 + beat * 0.5
    add_pulse_beat(t_beat, amp=0.38 if beat % 2 == 0 else 0.25, freq=55 if beat % 2 == 0 else 70)
    add_tick(t_beat + 0.25, amp=0.16, freq=3400)

# Act IV Riser (30.0 - 34.8)
add_riser(30.0, duration_s=4.8, amp=0.35)

# Act IV Zero Telemetry sub drop at 29.3s
add_impact(29.3, amp=0.85, start_freq=150, end_freq=32, duration_s=2.5)

# Act IV Badge shimmer at 32.5s
add_whoosh(32.3, duration_s=1.0, amp=0.28, pan_dir=1)

# Act V Final Logo Lockup at 35.0s
add_whoosh(34.4, duration_s=0.7, amp=0.40, pan_dir=1)
add_impact(35.0, amp=0.90, start_freq=140, end_freq=30, duration_s=3.0)
# Precision mechanical lock latch
add_key_click(35.02, amp=0.55)
add_tick(35.05, amp=0.35, freq=4200)

# Master Limiter / Normalization
max_peak = 0.0
for i in range(TOTAL_SAMPLES):
    max_peak = max(max_peak, abs(left_channel[i]), abs(right_channel[i]))

print(f"Peak amplitude before master limiting: {max_peak:.3f}")
target_peak = 0.92
gain = target_peak / max(max_peak, 0.001)

out_path = os.path.join(os.path.dirname(__file__), "public", "soundtrack.wav")
with wave.open(out_path, "wb") as wf:
    wf.setnchannels(2)
    wf.setsampwidth(2)  # 16-bit
    wf.setframerate(SAMPLE_RATE)
    frames = bytearray()
    for i in range(TOTAL_SAMPLES):
        vl = clamp(left_channel[i] * gain)
        vr = clamp(right_channel[i] * gain)
        sample_l = int(vl * 32767)
        sample_r = int(vr * 32767)
        frames.extend(struct.pack("<hh", sample_l, sample_r))
    wf.writeframes(frames)

print(f"Successfully generated soundtrack: {out_path} ({DURATION}s, 48kHz Stereo)")

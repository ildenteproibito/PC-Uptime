import React from 'react';
import { interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Background } from '../components/Background';
import { WindowFrame } from '../components/WindowFrame';
import { MiniHud } from '../components/MiniHud';
import { theme } from '../theme';

export const Act3Experience: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Part 1: 0 - 240 frames (TimeRuler & Telemetry)
  // Part 2: 230 - 480 frames (Mini-HUD & Shortcut)
  // Part 3: 470 - 720 frames (Stability & Forensics)

  const sceneOpacity = interpolate(frame, [0, 15, 705, 720], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // PART 1 CAMERA (0 - 240)
  const part1Scale = interpolate(frame, [0, 240], [1.18, 1.24], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // PART 2 SPRINGS & HUD (230 - 480)
  const hudSpring = spring({
    frame: frame - 250,
    fps,
    config: { damping: 16, stiffness: 110 },
  });

  // PART 3 CAMERA (470 - 720)
  const part3Scale = interpolate(frame, [470, 720], [1.18, 1.24], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <div
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        fontFamily: theme.fonts.main,
        opacity: sceneOpacity,
      }}
    >
      <Background
        glowX={frame < 240 ? 45 : frame < 480 ? 50 : 55}
        glowY={45}
        glowIntensity={0.5}
        accentIntensity={0.25}
      />

      {/* PART 1: TimeRuler & Active Session (0 - 240 frames) */}
      {frame < 250 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: interpolate(frame, [0, 15, 230, 250], [0, 1, 1, 0]),
          }}
        >
          <WindowFrame
            screenshotSrc="monitor.png"
            scale={part1Scale}
            glow={true}
          />

          {/* Minimalist Top Feature Pill */}
          <div
            style={{
              position: 'absolute',
              top: 60,
              padding: '8px 22px',
              borderRadius: 30,
              backgroundColor: 'rgba(27, 30, 36, 0.85)',
              border: `1px solid ${theme.colors.outline}`,
              backdropFilter: 'blur(12px)',
              fontSize: 13,
              letterSpacing: '2px',
              textTransform: 'uppercase',
              color: theme.colors.text,
              fontWeight: 600,
            }}
          >
            CHRONO RULER & SESSION BREAKDOWN
          </div>

          {/* Clean Lower Pill */}
          <div
            style={{
              position: 'absolute',
              bottom: 60,
              display: 'flex',
              alignItems: 'center',
              gap: 24,
              padding: '12px 28px',
              backgroundColor: 'rgba(15, 17, 20, 0.92)',
              backdropFilter: 'blur(16px)',
              border: `1px solid ${theme.colors.outlineBright}`,
              borderRadius: 30,
              boxShadow: '0 15px 40px rgba(0, 0, 0, 0.75)',
              opacity: interpolate(frame, [20, 45], [0, 1]),
            }}
          >
            <span style={{ fontSize: 13, color: theme.colors.accent, fontWeight: 600 }}>
              168-HOUR HORIZONTAL RULER
            </span>
            <div style={{ width: 1, height: 16, backgroundColor: theme.colors.line }} />
            <span style={{ fontSize: 13, color: theme.colors.text }}>
              Active vs. Idle session metrics · Kernel thread monitoring
            </span>
          </div>
        </div>
      )}

      {/* PART 2: The Instant Mini-HUD (230 - 480 frames) */}
      {frame >= 230 && frame < 480 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: interpolate(frame, [230, 250, 460, 480], [0, 1, 1, 0]),
          }}
        >
          {/* Defocused Background Window */}
          <div
            style={{
              position: 'absolute',
              filter: 'blur(14px) brightness(0.35)',
              transform: 'scale(1.15)',
              opacity: 0.65,
            }}
          >
            <WindowFrame screenshotSrc="monitor.png" />
          </div>

          {/* Hotkey Indicator */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 12,
              marginBottom: 36,
              opacity: interpolate(frame, [235, 260, 440, 470], [0, 1, 1, 0]),
              transform: `translateY(${interpolate(frame, [235, 260], [15, 0], { extrapolateRight: 'clamp' })}px)`,
              zIndex: 20,
            }}
          >
            <span style={{ color: theme.colors.muted, fontSize: 13, letterSpacing: '2px', textTransform: 'uppercase', fontWeight: 600 }}>
              GLOBAL SHORTCUT
            </span>
            <div style={{ display: 'flex', gap: 8 }}>
              {['⊞ Win', 'Alt', 'U'].map((k) => (
                <div
                  key={k}
                  style={{
                    backgroundColor: '#1E222A',
                    border: '1px solid #3E4552',
                    borderRadius: 8,
                    padding: '8px 16px',
                    fontSize: 15,
                    fontWeight: 600,
                    color: '#FFFFFF',
                    boxShadow: '0 4px 12px rgba(0, 0, 0, 0.5)',
                  }}
                >
                  {k}
                </div>
              ))}
            </div>
          </div>

          {/* The Mini-HUD Widget */}
          <div
            style={{
              transform: `scale(${hudSpring})`,
              zIndex: 20,
            }}
          >
            <MiniHud
              primaryTime="14d 06h"
              secondaryTime="42m 38s"
              statusText="Nominal"
              targetHours="168h"
              progressPercent={82}
              glow={true}
            />
          </div>

          {/* Subtitle */}
          <div
            style={{
              marginTop: 28,
              fontSize: 15,
              color: theme.colors.muted,
              letterSpacing: '0.6px',
              opacity: interpolate(frame, [270, 300], [0, 1]),
              zIndex: 20,
            }}
          >
            Always-on-top translucent HUD · Instant glance, zero friction
          </div>
        </div>
      )}

      {/* PART 3: Stability & Diagnostics (470 - 720 frames) */}
      {frame >= 470 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: interpolate(frame, [470, 490, 700, 720], [0, 1, 1, 0]),
          }}
        >
          <WindowFrame
            screenshotSrc="stability.png"
            scale={part3Scale}
            glow={true}
          />

          {/* Minimalist Top Feature Pill */}
          <div
            style={{
              position: 'absolute',
              top: 60,
              padding: '8px 22px',
              borderRadius: 30,
              backgroundColor: 'rgba(27, 30, 36, 0.85)',
              border: `1px solid ${theme.colors.outline}`,
              backdropFilter: 'blur(12px)',
              fontSize: 13,
              letterSpacing: '2px',
              textTransform: 'uppercase',
              color: theme.colors.text,
              fontWeight: 600,
            }}
          >
            STABILITY & INCIDENT FORENSICS
          </div>

          {/* Clean Lower Pill */}
          <div
            style={{
              position: 'absolute',
              bottom: 60,
              display: 'flex',
              alignItems: 'center',
              gap: 24,
              padding: '12px 28px',
              backgroundColor: 'rgba(15, 17, 20, 0.92)',
              backdropFilter: 'blur(16px)',
              border: `1px solid ${theme.colors.outlineBright}`,
              borderRadius: 30,
              boxShadow: '0 15px 40px rgba(0, 0, 0, 0.75)',
              opacity: interpolate(frame, [490, 520], [0, 1]),
            }}
          >
            <span style={{ fontSize: 13, color: theme.colors.accent, fontWeight: 600 }}>
              99.4% STABILITY INDEX
            </span>
            <div style={{ width: 1, height: 16, backgroundColor: theme.colors.line }} />
            <span style={{ fontSize: 13, color: theme.colors.text }}>
              Windows Event diagnostics · Unexpected shutdown forensics
            </span>
          </div>
        </div>
      )}
    </div>
  );
};

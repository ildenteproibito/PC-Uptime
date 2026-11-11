import React from 'react';
import { interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Background } from '../components/Background';
import { WindowFrame } from '../components/WindowFrame';
import { theme } from '../theme';

export const Act4Impact: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Cut 1: 0 - 140 frames (Operations Power Actions)
  // Cut 2: 135 - 330 frames (Zero Telemetry Monolith)
  // Cut 3: 320 - 480 frames (Shareable Badges & Exports)

  const sceneOpacity = interpolate(frame, [0, 15, 465, 480], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Cut 1 Camera
  const cut1Scale = interpolate(frame, [0, 140], [1.18, 1.24], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Cut 2 Monolith Typography Springs
  const monoSpring1 = spring({
    frame: frame - 140,
    fps,
    config: { damping: 14, stiffness: 140 },
  });
  const monoSpring2 = spring({
    frame: frame - 165,
    fps,
    config: { damping: 14, stiffness: 140 },
  });
  const monoSpring3 = spring({
    frame: frame - 190,
    fps,
    config: { damping: 14, stiffness: 140 },
  });

  // Cut 3 Badge Spring
  const badgeSpring = spring({
    frame: frame - 330,
    fps,
    config: { damping: 16, stiffness: 110 },
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
        glowX={frame < 140 ? 50 : frame < 330 ? 50 : 50}
        glowY={45}
        glowIntensity={frame >= 140 && frame < 330 ? 0.65 : 0.5}
        accentIntensity={0.25}
      />

      {/* CUT 1: Operations Suite (0 - 140 frames) */}
      {frame < 140 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: interpolate(frame, [0, 15, 125, 140], [0, 1, 1, 0]),
          }}
        >
          <WindowFrame
            screenshotSrc="operations.png"
            scale={cut1Scale}
            glow={true}
          />

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
            POWER TOOLS & SYSTEM OPERATIONS
          </div>

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
            }}
          >
            <span style={{ fontSize: 13, color: theme.colors.accent, fontWeight: 600 }}>
              POWER ORCHESTRATION
            </span>
            <div style={{ width: 1, height: 16, backgroundColor: theme.colors.line }} />
            <span style={{ fontSize: 13, color: theme.colors.text }}>
              Clean Restart · Bypass Fast Startup · Restart direct to UEFI / BIOS
            </span>
          </div>
        </div>
      )}

      {/* CUT 2: Zero Telemetry Monolith (135 - 330 frames) */}
      {frame >= 135 && frame < 330 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            textAlign: 'center',
            opacity: interpolate(frame, [135, 145, 315, 330], [0, 1, 1, 0]),
          }}
        >
          <div
            style={{
              fontSize: 16,
              letterSpacing: '5px',
              color: theme.colors.accent,
              textTransform: 'uppercase',
              fontWeight: 700,
              marginBottom: 24,
              opacity: monoSpring1,
            }}
          >
            THE PRIVACY GUARANTEE
          </div>

          <h2
            style={{
              fontSize: 72,
              fontWeight: 800,
              letterSpacing: '-1.5px',
              color: '#FFFFFF',
              margin: 0,
              lineHeight: 1.1,
              transform: `scale(${interpolate(monoSpring1, [0, 1], [0.94, 1])})`,
              opacity: monoSpring1,
            }}
          >
            ZERO TELEMETRY.
          </h2>

          <h2
            style={{
              fontSize: 72,
              fontWeight: 800,
              letterSpacing: '-1.5px',
              color: theme.colors.accent,
              margin: '10px 0',
              lineHeight: 1.1,
              transform: `scale(${interpolate(monoSpring2, [0, 1], [0.94, 1])})`,
              opacity: monoSpring2,
            }}
          >
            100% LOCAL PROCESSING.
          </h2>

          <h2
            style={{
              fontSize: 32,
              fontWeight: 300,
              letterSpacing: '0.8px',
              color: theme.colors.muted,
              margin: '16px 0 0 0',
              transform: `scale(${interpolate(monoSpring3, [0, 1], [0.94, 1])})`,
              opacity: monoSpring3,
            }}
          >
            Your system event data never leaves your computer.
          </h2>
        </div>
      )}

      {/* CUT 3: Shareable Badges & Exports (320 - 480 frames) */}
      {frame >= 320 && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: interpolate(frame, [320, 335, 465, 480], [0, 1, 1, 0]),
          }}
        >
          <div
            style={{
              fontSize: 14,
              letterSpacing: '3px',
              color: theme.colors.muted,
              textTransform: 'uppercase',
              fontWeight: 600,
              marginBottom: 28,
            }}
          >
            SHAREABLE BADGES & INSTANT EXPORT
          </div>

          {/* Elegant Uptime Milestone Badge */}
          <div
            style={{
              transform: `scale(${badgeSpring})`,
              display: 'inline-flex',
              alignItems: 'center',
              backgroundColor: '#111418',
              borderRadius: 14,
              border: '1px solid #2F363F',
              boxShadow: '0 25px 70px rgba(0, 0, 0, 0.9), 0 0 40px rgba(123, 196, 168, 0.35)',
              overflow: 'hidden',
              padding: '8px',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                backgroundColor: '#1B1E24',
                padding: '14px 28px',
                borderRadius: 9,
              }}
            >
              <div
                style={{
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  backgroundColor: theme.colors.accent,
                  boxShadow: '0 0 8px rgba(123, 196, 168, 0.9)',
                }}
              />
              <span
                style={{
                  color: '#9AA0A9',
                  fontSize: 17,
                  fontWeight: 600,
                  letterSpacing: '1px',
                  textTransform: 'uppercase',
                }}
              >
                UPTIME
              </span>
            </div>

            <div
              style={{
                padding: '14px 34px',
                color: '#FFFFFF',
                fontSize: 24,
                fontWeight: 600,
                letterSpacing: '-0.5px',
                fontFamily: theme.fonts.mono,
              }}
            >
              14d 06h 42m
            </div>
          </div>

          <div
            style={{
              marginTop: 36,
              fontSize: 18,
              color: theme.colors.textDim,
              letterSpacing: '0.5px',
            }}
          >
            JSON Export · CSV Export · Dynamic Status Badges
          </div>
        </div>
      )}
    </div>
  );
};

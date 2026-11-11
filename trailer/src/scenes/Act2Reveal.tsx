import React from 'react';
import { interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Background } from '../components/Background';
import { WindowFrame } from '../components/WindowFrame';
import { theme } from '../theme';

export const Act2Reveal: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Smooth entrance fade
  const sceneOpacity = interpolate(frame, [0, 20, 460, 480], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Continuous, silky-smooth cinematic camera movement
  const rotateX = interpolate(frame, [0, 240], [7, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const rotateY = interpolate(frame, [0, 240], [-12, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Smooth, gradual zoom: from 1.12 to 1.25 over 480 frames (no jerky jumping!)
  const scale = interpolate(frame, [0, 480], [1.12, 1.25], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const translateY = interpolate(frame, [0, 480], [10, -10], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Header pill animation
  const pillSpring = spring({
    frame: frame - 25,
    fps,
    config: { damping: 18, stiffness: 100 },
  });

  // Bottom caption animation
  const captionSpring = spring({
    frame: frame - 200,
    fps,
    config: { damping: 18, stiffness: 90 },
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
        glowX={interpolate(frame, [0, 480], [45, 55])}
        glowY={45}
        glowIntensity={0.55}
        accentIntensity={0.25}
      />

      {/* Floating Category Pill at Top */}
      <div
        style={{
          position: 'absolute',
          top: 60,
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          padding: '8px 22px',
          borderRadius: 30,
          backgroundColor: 'rgba(27, 30, 36, 0.85)',
          border: `1px solid ${theme.colors.outline}`,
          backdropFilter: 'blur(12px)',
          opacity: interpolate(frame, [15, 45, 430, 460], [0, 1, 1, 0]),
          transform: `translateY(${(1 - pillSpring) * 15}px)`,
          zIndex: 20,
        }}
      >
        <div
          style={{
            width: 7,
            height: 7,
            borderRadius: '50%',
            backgroundColor: theme.colors.accent,
            boxShadow: '0 0 8px rgba(123, 196, 168, 0.9)',
          }}
        />
        <span
          style={{
            fontSize: 13,
            letterSpacing: '2px',
            textTransform: 'uppercase',
            color: theme.colors.text,
            fontWeight: 600,
          }}
        >
          DESKTOP MONITOR VIEW
        </span>
      </div>

      {/* Authentic Uptime Window (Single authentic header, perfectly scaled) */}
      <div style={{ position: 'relative', zIndex: 10 }}>
        <WindowFrame
          screenshotSrc="monitor.png"
          scale={scale}
          rotateX={rotateX}
          rotateY={rotateY}
          translateY={translateY}
          glow={frame > 60}
        />
      </div>

      {/* Elegant, restrained lower-third bar (frames 200 - 460) */}
      {frame >= 190 && (
        <div
          style={{
            position: 'absolute',
            bottom: 60,
            display: 'flex',
            alignItems: 'center',
            gap: 24,
            padding: '14px 28px',
            backgroundColor: 'rgba(15, 17, 20, 0.9)',
            backdropFilter: 'blur(16px)',
            border: `1px solid ${theme.colors.outlineBright}`,
            borderRadius: 30,
            boxShadow: '0 15px 40px rgba(0, 0, 0, 0.75)',
            opacity: interpolate(frame, [190, 220, 440, 470], [0, 1, 1, 0]),
            transform: `translateY(${(1 - captionSpring) * 20}px)`,
            zIndex: 20,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                backgroundColor: theme.colors.accent,
              }}
            />
            <span style={{ fontSize: 14, color: theme.colors.text, fontWeight: 500 }}>
              Live Session Telemetry
            </span>
          </div>
          <div style={{ width: 1, height: 16, backgroundColor: theme.colors.line }} />
          <span style={{ fontSize: 13, color: theme.colors.muted }}>
            Accurate boot tracking · Continuous system health
          </span>
        </div>
      )}
    </div>
  );
};

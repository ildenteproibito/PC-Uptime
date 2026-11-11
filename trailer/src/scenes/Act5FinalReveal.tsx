import React from 'react';
import { interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Background } from '../components/Background';
import { VectorLogo } from '../components/VectorLogo';
import { theme } from '../theme';

export const Act5FinalReveal: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Logo assembly spring
  const logoSpring = spring({
    frame,
    fps,
    config: { damping: 15, stiffness: 95 },
  });

  // Typography spring
  const textSpring = spring({
    frame: frame - 25,
    fps,
    config: { damping: 16, stiffness: 100 },
  });

  const ctaOpacity = interpolate(frame, [45, 80], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Final master fade to black (frames 265 - 295)
  const masterFade = interpolate(frame, [265, 295], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <div
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        overflow: 'hidden',
        fontFamily: theme.fonts.main,
        opacity: masterFade,
      }}
    >
      <Background />

      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          transform: `scale(${interpolate(logoSpring, [0, 1], [0.88, 1])})`,
        }}
      >
        {/* Official Uptime Logo */}
        <VectorLogo
          size={160}
          showBackground={true}
        />

        {/* Wordmark */}
        <div
          style={{
            marginTop: 28,
            opacity: textSpring,
            transform: `translateY(${interpolate(textSpring, [0, 1], [15, 0])}px)`,
            textAlign: 'center',
          }}
        >
          <h1
            style={{
              fontSize: 70,
              fontWeight: 600,
              letterSpacing: '-1.5px',
              color: '#FFFFFF',
              margin: 0,
              lineHeight: 1,
            }}
          >
            Uptime
          </h1>
          <div
            style={{
              fontSize: 20,
              fontWeight: 300,
              letterSpacing: '1.2px',
              color: theme.colors.muted,
              marginTop: 14,
            }}
          >
            Know your machine. Master your time.
          </div>
        </div>

        {/* GitHub Repository CTA & Website Link */}
        <div
          style={{
            marginTop: 44,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: 16,
            opacity: ctaOpacity,
          }}
        >
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 16,
            }}
          >
            {/* Official GitHub Pill with Original GitHub Logo */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                backgroundColor: 'rgba(27, 30, 36, 0.95)',
                border: '1px solid #3E4552',
                padding: '10px 24px',
                borderRadius: 24,
                boxShadow: '0 8px 24px rgba(0,0,0,0.6)',
              }}
            >
              {/* Official GitHub Octocat SVG mark */}
              <svg width="20" height="20" viewBox="0 0 24 24" fill="#FFFFFF">
                <path
                  fillRule="evenodd"
                  clipRule="evenodd"
                  d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z"
                />
              </svg>
              <span style={{ fontSize: 14, fontWeight: 500, color: '#FFFFFF', letterSpacing: '0.2px' }}>
                ildenteproibito/PC-Uptime
              </span>
            </div>

            {/* Official Website */}
            <div
              style={{
                fontSize: 15,
                color: theme.colors.accent,
                fontWeight: 500,
                letterSpacing: '0.5px',
              }}
            >
              uptime-pc.vercel.app
            </div>
          </div>

          <div
            style={{
              fontSize: 12,
              color: theme.colors.dim,
              letterSpacing: '0.8px',
              marginTop: 4,
            }}
          >
            Open Source for Windows 10 & 11 · 100% Local & Free of Telemetry
          </div>
        </div>
      </div>
    </div>
  );
};

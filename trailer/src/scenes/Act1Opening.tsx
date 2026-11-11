import React from 'react';
import { interpolate, spring, useCurrentFrame, useVideoConfig } from 'remotion';
import { Background } from '../components/Background';
import { VectorLogo } from '../components/VectorLogo';
import { theme } from '../theme';

export const Act1Opening: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Shot 1: 0 - 200 frames ("Every second your machine runs...")
  const shot1Opacity = interpolate(frame, [0, 30, 160, 195], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const shot1Scale = interpolate(frame, [0, 180], [0.98, 1.02], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // Shot 2: 195 - 420 frames ("Every session leaves a trace." -> Logo Emergence)
  const shot2Opacity = interpolate(frame, [200, 230, 400, 420], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const logoSpring = spring({
    frame: frame - 230,
    fps,
    config: { damping: 16, stiffness: 95 },
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
      }}
    >
      <Background />

      {/* Shot 1: The Monolith Question */}
      {frame < 200 && (
        <div
          style={{
            position: 'absolute',
            textAlign: 'center',
            opacity: shot1Opacity,
            transform: `scale(${shot1Scale})`,
          }}
        >
          <div
            style={{
              fontSize: 13,
              letterSpacing: '5px',
              color: theme.colors.accent,
              textTransform: 'uppercase',
              fontWeight: 600,
              marginBottom: 20,
            }}
          >
            PRECISION DESKTOP MONITORING
          </div>
          <h1
            style={{
              fontSize: 56,
              fontWeight: 300,
              letterSpacing: '2px',
              color: theme.colors.text,
              margin: 0,
              lineHeight: 1.25,
            }}
          >
            Every second your machine runs.
          </h1>
          <div
            style={{
              fontSize: 22,
              fontWeight: 300,
              color: theme.colors.muted,
              marginTop: 18,
              letterSpacing: '1px',
            }}
          >
            A silent pulse beneath the surface.
          </div>
        </div>
      )}

      {/* Shot 2: The Official Logo & Transition */}
      {frame >= 195 && (
        <div
          style={{
            position: 'absolute',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: shot2Opacity,
          }}
        >
          <div
            style={{
              transform: `scale(${logoSpring})`,
              marginBottom: 32,
            }}
          >
            <VectorLogo
              size={130}
              showBackground={true}
            />
          </div>

          <h2
            style={{
              fontSize: 48,
              fontWeight: 300,
              letterSpacing: '1.5px',
              color: theme.colors.text,
              margin: 0,
              textAlign: 'center',
            }}
          >
            Every session leaves a trace.
          </h2>

          <p
            style={{
              fontSize: 20,
              color: theme.colors.muted,
              marginTop: 16,
              letterSpacing: '0.8px',
              textAlign: 'center',
            }}
          >
            Know your uptime. Understand your stability.
          </p>
        </div>
      )}
    </div>
  );
};

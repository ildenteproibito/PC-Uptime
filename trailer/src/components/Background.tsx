import React from 'react';
import { theme } from '../theme';

interface BackgroundProps {
  glowIntensity?: number; // kept for compatibility
  accentIntensity?: number;
  glowX?: number;
  glowY?: number;
  gridOpacity?: number;
}

export const Background: React.FC<BackgroundProps> = () => {
  return (
    <div
      style={{
        position: 'absolute',
        inset: 0,
        backgroundColor: '#0A0C0E',
        overflow: 'hidden',
        pointerEvents: 'none',
      }}
    >
      {/* 
        Single, static, ultra-smooth radial lighting:
        Carefully tuned color stops to eliminate 8-bit quantization banding.
        No animated radius, no pulsing, no grid moiré.
      */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          background: 'radial-gradient(ellipse 1200px 800px at 50% 50%, #15181E 0%, #0D0F13 55%, #08090B 100%)',
        }}
      />

      {/* Subtle, soft emerald accent illumination in the center */}
      <div
        style={{
          position: 'absolute',
          top: '30%',
          left: '35%',
          width: '30%',
          height: '40%',
          background: 'radial-gradient(circle at center, rgba(114, 192, 164, 0.08) 0%, rgba(114, 192, 164, 0.02) 50%, transparent 80%)',
          pointerEvents: 'none',
        }}
      />

      {/* High-frequency micro-dither noise overlay to eliminate gradient banding completely */}
      <svg
        style={{
          position: 'absolute',
          inset: 0,
          width: '100%',
          height: '100%',
          opacity: 0.035,
          pointerEvents: 'none',
        }}
      >
        <filter id="ditherNoise">
          <feTurbulence
            type="fractalNoise"
            baseFrequency="0.85"
            numOctaves="3"
            stitchTiles="stitch"
          />
          <feColorMatrix type="saturate" values="0" />
        </filter>
        <rect width="100%" height="100%" filter="url(#ditherNoise)" />
      </svg>
    </div>
  );
};

import React from 'react';
import { staticFile, Img } from 'remotion';
import { theme } from '../theme';

interface VectorLogoProps {
  size?: number;
  glowIntensity?: number; // 0 to 1
  opacity?: number;
  showBackground?: boolean;
  scale?: number;
  arcProgress?: number;
  handsProgress?: number;
  ticksOpacity?: number;
}

export const VectorLogo: React.FC<VectorLogoProps> = ({
  size = 180,
  opacity = 1,
  showBackground = true,
  scale = 1,
}) => {
  return (
    <div
      style={{
        position: 'relative',
        width: size,
        height: size,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        opacity,
        transform: `scale(${scale})`,
      }}
    >
      {/* Dark obsidian squircle chassis with subtle rim */}
      {showBackground && (
        <div
          style={{
            position: 'absolute',
            inset: 0,
            borderRadius: size * 0.22,
            backgroundColor: '#111418',
            border: `1px solid ${theme.colors.outline}`,
            boxShadow: '0 16px 40px rgba(0, 0, 0, 0.85), 0 0 25px rgba(123, 196, 168, 0.12)',
          }}
        />
      )}

      {/* Official Uptime Logo directly from brand assets */}
      <Img
        src={staticFile('uptime-logo-official.png')}
        style={{
          position: 'relative',
          width: showBackground ? size * 0.72 : size,
          height: showBackground ? size * 0.72 : size,
          objectFit: 'contain',
        }}
      />
    </div>
  );
};

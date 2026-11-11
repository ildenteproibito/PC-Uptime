import React from 'react';
import { staticFile, Img } from 'remotion';
import { theme } from '../theme';

interface WindowFrameProps {
  screenshotSrc: string;
  width?: number;
  height?: number;
  scale?: number;
  rotateX?: number;
  rotateY?: number;
  rotateZ?: number;
  translateX?: number;
  translateY?: number;
  translateZ?: number;
  opacity?: number;
  glow?: boolean;
  activeTab?: string; // Kept for backwards compatibility
}

export const WindowFrame: React.FC<WindowFrameProps> = ({
  screenshotSrc,
  width = 960,
  height = 640,
  scale = 1,
  rotateX = 0,
  rotateY = 0,
  rotateZ = 0,
  translateX = 0,
  translateY = 0,
  translateZ = 0,
  opacity = 1,
  glow = false,
}) => {
  return (
    <div
      style={{
        width,
        height,
        opacity,
        transform: `perspective(1600px) translate3d(${translateX}px, ${translateY}px, ${translateZ}px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) rotateZ(${rotateZ}deg) scale(${scale})`,
        transformStyle: 'preserve-3d',
        borderRadius: 14,
        border: `1px solid ${theme.colors.outline}`,
        boxShadow: glow
          ? `0 40px 100px rgba(0, 0, 0, 0.9), 0 0 50px ${theme.colors.accentGlow}, inset 0 1px 0 rgba(255, 255, 255, 0.12)`
          : '0 30px 80px rgba(0, 0, 0, 0.85), inset 0 1px 0 rgba(255, 255, 255, 0.08)',
        backgroundColor: theme.colors.bg,
        overflow: 'hidden',
        position: 'relative',
        display: 'flex',
      }}
    >
      {/* The authentic window screenshot (contains the real header, tabs, and content with ZERO duplication) */}
      <Img
        src={staticFile(screenshotSrc)}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'cover',
          display: 'block',
        }}
      />
    </div>
  );
};

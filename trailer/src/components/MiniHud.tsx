import React from 'react';
import { staticFile, Img } from 'remotion';
import { theme } from '../theme';

interface MiniHudProps {
  primaryTime?: string;
  secondaryTime?: string;
  statusText?: string;
  targetHours?: string;
  progressPercent?: number; // 0 to 100
  scale?: number;
  opacity?: number;
  glow?: boolean;
}

export const MiniHud: React.FC<MiniHudProps> = ({
  primaryTime = '14d 06h',
  secondaryTime = '42m 38s',
  statusText = 'Nominal',
  targetHours = '168h',
  progressPercent = 82,
  scale = 1,
  opacity = 1,
  glow = true,
}) => {
  return (
    <div
      style={{
        width: 440,
        height: 220,
        backgroundColor: '#111418EE',
        backdropFilter: 'blur(16px)',
        border: '1px solid #30363D',
        borderRadius: 16,
        padding: '22px 24px',
        boxShadow: glow
          ? '0 25px 60px rgba(0, 0, 0, 0.8), 0 0 35px rgba(123, 196, 168, 0.25)'
          : '0 20px 50px rgba(0, 0, 0, 0.75)',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        fontFamily: theme.fonts.main,
        transform: `scale(${scale})`,
        opacity,
        position: 'relative',
        boxSizing: 'border-box',
      }}
    >
      {/* Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Img
            src={staticFile('uptime.png')}
            style={{ width: 22, height: 22, borderRadius: 5 }}
          />
          <span style={{ fontSize: 14, fontWeight: 600, color: '#E8EAED' }}>
            Uptime
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          {/* Status Badge */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 7,
              backgroundColor: '#20242A',
              padding: '4px 10px',
              borderRadius: 12,
              border: '1px solid #2F363F',
            }}
          >
            <div
              style={{
                width: 7,
                height: 7,
                borderRadius: '50%',
                backgroundColor: theme.colors.accent,
                boxShadow: '0 0 6px rgba(123, 196, 168, 0.8)',
              }}
            />
            <span style={{ fontSize: 11, color: '#DCE8E2', fontWeight: 500 }}>
              {statusText}
            </span>
          </div>

          {/* Close mark */}
          <span style={{ color: theme.colors.muted, fontSize: 16, cursor: 'pointer' }}>
            ×
          </span>
        </div>
      </div>

      {/* Center Metrics */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginTop: 4,
        }}
      >
        <div>
          <div
            style={{
              fontSize: 48,
              fontWeight: 300,
              letterSpacing: '-1.5px',
              color: '#FFFFFF',
              lineHeight: 1,
            }}
          >
            {primaryTime}
          </div>
          <div
            style={{
              fontSize: 14,
              color: '#AEB5BD',
              marginTop: 4,
              fontWeight: 400,
            }}
          >
            {secondaryTime}
          </div>
        </div>

        <div style={{ textAlign: 'right' }}>
          <div
            style={{
              fontSize: 10,
              letterSpacing: '1px',
              color: theme.colors.dim,
              fontWeight: 600,
              textTransform: 'uppercase',
              marginBottom: 4,
            }}
          >
            TARGET
          </div>
          <div
            style={{
              fontSize: 22,
              fontWeight: 500,
              color: '#E8EAED',
            }}
          >
            {targetHours}
          </div>
        </div>
      </div>

      {/* Progress Bar & Subtitle */}
      <div>
        <div
          style={{
            height: 6,
            backgroundColor: '#292F35',
            borderRadius: 3,
            overflow: 'hidden',
            position: 'relative',
          }}
        >
          <div
            style={{
              width: `${Math.min(100, Math.max(0, progressPercent))}%`,
              height: '100%',
              backgroundColor: theme.colors.accent,
              borderRadius: 3,
              boxShadow: '0 0 8px rgba(123, 196, 168, 0.6)',
            }}
          />
        </div>

        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            marginTop: 6,
            fontSize: 10,
            color: theme.colors.muted,
          }}
        >
          <span>Boot (0h)</span>
          <span>Target ({targetHours})</span>
        </div>
      </div>
    </div>
  );
};

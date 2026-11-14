import React from 'react';
import {
  AbsoluteFill,
  Audio,
  Img,
  Sequence,
  interpolate,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';
import { Background } from './components/Background';
import { theme } from './theme';

// Real Uppy sparkle-heart matching Uptime's actual petting particle
const HeartSvg: React.FC<{ size: number; opacity?: number }> = ({
  size,
  opacity = 1,
}) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    style={{ opacity, filter: 'drop-shadow(0 3px 8px rgba(0,0,0,0.45))' }}
  >
    <path
      d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
      fill="#EAECEF"
      stroke="#8D939C"
      strokeWidth={1.3}
    />
    <path d="M8.5 7.5L9 9L10.5 9.5L9 10L8.5 11.5L8 10L6.5 9.5L8 9Z" fill="#5B616A" />
    <path d="M15.2 10.2L15.6 11.3L16.7 11.7L15.6 12.1L15.2 13.2L14.8 12.1L13.7 11.7L14.8 11.3Z" fill="#5B616A" />
  </svg>
);

const HeadphonesSvg: React.FC<{ color: string }> = ({ color }) => (
  <svg width={26} height={26} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 18v-6a9 9 0 0 1 18 0v6" />
    <path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z" />
  </svg>
);

const CoffeeSvg: React.FC<{ color: string }> = ({ color }) => (
  <svg width={26} height={26} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
    <path d="M17 8h1a4 4 0 1 1 0 8h-1" />
    <path d="M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4Z" />
    <line x1="6" y1="2" x2="6" y2="4" />
    <line x1="10" y1="2" x2="10" y2="4" />
    <line x1="14" y1="2" x2="14" y2="4" />
  </svg>
);

const PulseSvg: React.FC<{ color: string }> = ({ color }) => (
  <svg width={26} height={26} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);

const MusicNoteSvg: React.FC<{ color: string }> = ({ color }) => (
  <svg width={16} height={16} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round">
    <path d="M9 18V5l12-2v13" />
    <circle cx="6" cy="18" r="3" />
    <circle cx="18" cy="16" r="3" />
  </svg>
);

const CursorSvg: React.FC<{ grabbing?: boolean }> = ({ grabbing }) => (
  <svg
    width={38}
    height={38}
    viewBox="0 0 28 28"
    style={{
      filter: 'drop-shadow(0 6px 14px rgba(0,0,0,0.65))',
      transform: grabbing ? 'scale(0.88)' : 'scale(1)',
    }}
  >
    <path
      d="M5 3L22 14L13.5 16L10 24L5 3Z"
      fill="#FFFFFF"
      stroke="#0A0C0E"
      strokeWidth={2}
      strokeLinejoin="round"
    />
  </svg>
);

const PillBadge: React.FC<{
  text: string;
  accent?: string;
  opacity?: number;
  translateY?: number;
}> = ({ text, accent = theme.colors.accent, opacity = 1, translateY = 0 }) => (
  <div
    style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: 10,
      padding: '10px 22px',
      borderRadius: 999,
      backgroundColor: 'rgba(27, 30, 36, 0.92)',
      border: `1px solid ${accent}55`,
      boxShadow: `0 8px 28px rgba(0,0,0,0.45), 0 0 24px ${accent}22`,
      color: theme.colors.text,
      fontFamily: theme.fonts.main,
      fontSize: 22,
      fontWeight: 600,
      letterSpacing: 0.3,
      opacity,
      transform: `translateY(${translateY}px)`,
    }}
  >
    <span
      style={{
        width: 10,
        height: 10,
        borderRadius: '50%',
        backgroundColor: accent,
        boxShadow: `0 0 10px ${accent}`,
      }}
    />
    {text}
  </div>
);

// Beat 1: Hype Kinetic Hook synced to 0.0s - 5.2s build-up (0 - 312 frames)
const HypeHookBeat: React.FC = () => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const phase = frame < 104 ? 0 : frame < 208 ? 1 : 2;
  const localFrame = frame - phase * 104;
  const pop = spring({
    frame: localFrame,
    fps,
    config: { damping: 14, stiffness: 160 },
  });
  const exit = interpolate(localFrame, [86, 104], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const pulseGlow =
    phase === 2
      ? interpolate(localFrame, [25, 95], [0.12, 0.45], {
          extrapolateLeft: 'clamp',
          extrapolateRight: 'clamp',
        })
      : 0.08;

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: '#07080A',
      }}
    >
      <div
        style={{
          position: 'absolute',
          width: 700,
          height: 700,
          borderRadius: '50%',
          background: `radial-gradient(circle, rgba(123,196,168,${pulseGlow}) 0%, transparent 68%)`,
        }}
      />

      <div
        style={{
          fontFamily: theme.fonts.main,
          fontSize: 76,
          fontWeight: 800,
          color: theme.colors.text,
          letterSpacing: -1.8,
          textAlign: 'center',
          transform: `scale(${interpolate(pop, [0, 1], [0.9, 1])})`,
          opacity: pop * exit,
        }}
      >
        {phase === 0 && "This isn't just a mascot."}
        {phase === 1 && 'I rebuilt everything.'}
        {phase === 2 && (
          <>
            To make it{' '}
            <span
              style={{
                color: theme.colors.accent,
                textShadow: '0 0 38px rgba(123,196,168,0.55)',
              }}
            >
              alive.
            </span>
          </>
        )}
      </div>
    </AbsoluteFill>
  );
};

// Beat 2: THE DROP — Meet Uppy 2.0 Reveal (312 - 492 frames | 5.2s - 8.2s)
const IntroBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 180
  const { fps } = useVideoConfig();

  const enter = spring({ frame, fps, config: { damping: 13, stiffness: 140 } });
  const mascotPop = spring({
    frame: Math.max(0, frame - 6),
    fps,
    config: { damping: 12, stiffness: 135 },
  });
  const bubblePop = spring({
    frame: Math.max(0, frame - 26),
    fps,
    config: { damping: 14, stiffness: 140 },
  });
  const exitOpacity = interpolate(frame, [158, 180], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const floatY = Math.sin(frame * 0.045) * 6;

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        opacity: exitOpacity,
      }}
    >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 22,
          transform: `scale(${interpolate(enter, [0, 1], [0.92, 1])})`,
        }}
      >
        <PillBadge text="UPTIME 2.0 UPDATE" />

        <div
          style={{
            position: 'relative',
            marginTop: 12,
            transform: `scale(${mascotPop}) translateY(${floatY}px)`,
          }}
        >
          <div
            style={{
              position: 'absolute',
              inset: -40,
              borderRadius: '50%',
              background:
                'radial-gradient(circle, rgba(123,196,168,0.32) 0%, transparent 70%)',
            }}
          />
          <Img
            src={staticFile(frame > 72 ? 'mascot/good.png' : 'mascot/idle.png')}
            style={{ width: 230, height: 230, objectFit: 'contain', position: 'relative' }}
          />
          <div
            style={{
              position: 'absolute',
              top: -18,
              right: -250,
              padding: '12px 20px',
              borderRadius: 16,
              backgroundColor: theme.colors.surfaceElevated,
              border: `1px solid ${theme.colors.accent}55`,
              color: theme.colors.text,
              fontFamily: theme.fonts.main,
              fontSize: 20,
              fontWeight: 600,
              boxShadow: '0 12px 30px rgba(0,0,0,0.45)',
              transform: `scale(${bubblePop})`,
              transformOrigin: 'bottom left',
              whiteSpace: 'nowrap',
            }}
          >
            Hey! Ready to check your PC?
          </div>
        </div>

        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 88,
            fontWeight: 800,
            color: theme.colors.text,
            letterSpacing: -2,
            lineHeight: 1.02,
            textAlign: 'center',
          }}
        >
          Meet <span style={{ color: theme.colors.accent }}>Uppy 2.0</span>
        </div>

        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 30,
            fontWeight: 500,
            color: theme.colors.muted,
            textAlign: 'center',
          }}
        >
          Your PC&apos;s interactive desktop companion
        </div>
      </div>
    </AbsoluteFill>
  );
};

// Beat 3: Literal Real Uptime Screenshot + Real Sparkle Hearts + Slow Bounce (492 - 870 frames | 8.2s - 14.5s)
const InteractiveBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 378
  const { fps } = useVideoConfig();

  const winEnter = spring({ frame, fps, config: { damping: 16, stiffness: 120 } });
  const isFlingPhase = frame >= 115;
  const flingFrame = Math.max(0, frame - 115); // 0 to 263

  const restX = 1236;
  const restY = 626;
  let uppyX = restX;
  let uppyY = restY;

  if (!isFlingPhase) {
    uppyY = restY + Math.sin(frame * 0.055) * 3;
  } else {
    // Touches left edge (x=58), top edge (y=66), and bottom edge (y=632) before settling
    if (flingFrame < 75) {
      const t = flingFrame / 75;
      uppyX = interpolate(t, [0, 1], [restX, 58]);
      uppyY = interpolate(t, [0, 1], [restY, 300]);
    } else if (flingFrame < 140) {
      const t = (flingFrame - 75) / 65;
      uppyX = interpolate(t, [0, 1], [58, 480]);
      uppyY = interpolate(t, [0, 1], [300, 66]);
    } else if (flingFrame < 210) {
      const t = (flingFrame - 140) / 70;
      uppyX = interpolate(t, [0, 1], [480, 920]);
      uppyY = interpolate(t, [0, 1], [66, 632]);
    } else {
      const settle = spring({
        frame: flingFrame - 210,
        fps,
        config: { damping: 18, stiffness: 80 },
      });
      uppyX = interpolate(settle, [0, 1], [920, restX]);
      uppyY = interpolate(settle, [0, 1], [632, restY]);
    }
  }

  let cursorX = restX + Math.sin(frame * 0.11) * 26;
  let cursorY = restY - 42 + Math.cos(frame * 0.13) * 8;
  let cursorOpacity = 1;
  if (isFlingPhase) {
    if (flingFrame < 16) {
      cursorX = interpolate(flingFrame, [0, 16], [restX, restX - 110]);
      cursorY = interpolate(flingFrame, [0, 16], [restY - 28, restY - 88]);
    } else {
      cursorX = restX - 110 - (flingFrame - 16) * 5;
      cursorY = restY - 88 - (flingFrame - 16) * 2.5;
      cursorOpacity = interpolate(flingFrame, [16, 40], [1, 0], {
        extrapolateLeft: 'clamp',
        extrapolateRight: 'clamp',
      });
    }
  }

  const hearts = [
    { start: 10, dx: -36, size: 26 },
    { start: 18, dx: 20, size: 24 },
    { start: 26, dx: -12, size: 30 },
    { start: 34, dx: 30, size: 22 },
    { start: 42, dx: -28, size: 28 },
    { start: 50, dx: 8, size: 25 },
    { start: 58, dx: -18, size: 27 },
    { start: 66, dx: 26, size: 24 },
    { start: 74, dx: -6, size: 29 },
  ];

  const exitOpacity = interpolate(frame, [358, 378], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        opacity: exitOpacity,
      }}
    >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 20,
          transform: `scale(${interpolate(winEnter, [0, 1], [0.95, 1])})`,
        }}
      >
        <PillBadge
          text={
            isFlingPhase
              ? 'Grab, fling & bounce off window edges'
              : 'Pet Uppy for animated hearts'
          }
          accent={isFlingPhase ? theme.colors.amber : '#FF6B8B'}
        />

        <div
          style={{
            width: 1300,
            height: 699,
            borderRadius: 12,
            border: `1px solid ${theme.colors.outlineBright}`,
            boxShadow:
              '0 32px 90px rgba(0, 0, 0, 0.78), 0 0 0 1px rgba(255,255,255,0.04) inset',
            overflow: 'hidden',
            position: 'relative',
            backgroundColor: '#0F1114',
          }}
        >
          <Img
            src={staticFile('real-monitor-hq.png')}
            style={{
              width: '100%',
              height: '100%',
              objectFit: 'contain',
              display: 'block',
            }}
          />

          <Img
            src={staticFile('real-corner-clean.png')}
            style={{
              position: 'absolute',
              right: 0,
              bottom: 0,
              width: 122,
              height: 122,
              display: 'block',
            }}
          />

          {hearts.map((h, i) => {
            const hf = frame - h.start;
            if (hf < 0 || hf > 80) return null;
            const progress = hf / 80;
            const hy = restY - 40 - progress * 145;
            const hx = restX + h.dx + Math.sin(progress * Math.PI * 2.5) * 10;
            const hop = interpolate(progress, [0, 0.15, 0.8, 1], [0, 1, 1, 0]);
            return (
              <div
                key={i}
                style={{
                  position: 'absolute',
                  left: hx - h.size / 2,
                  top: hy - h.size / 2,
                  transform: `scale(${interpolate(progress, [0, 0.2], [0.45, 1], {
                    extrapolateRight: 'clamp',
                  })})`,
                }}
              >
                <HeartSvg size={h.size} opacity={hop} />
              </div>
            );
          })}

          <div
            style={{
              position: 'absolute',
              left: uppyX - 50,
              top: uppyY - 50,
              width: 100,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 4,
            }}
          >
            {(!isFlingPhase && frame > 14) || (isFlingPhase && flingFrame < 184) ? (
              <div
                style={{
                  position: 'absolute',
                  top: -34,
                  right: !isFlingPhase ? 4 : undefined,
                  padding: '4px 11px',
                  borderRadius: 999,
                  backgroundColor: 'rgba(18, 20, 24, 0.92)',
                  border: `1px solid ${theme.colors.accent}66`,
                  fontFamily: theme.fonts.main,
                  fontSize: 11,
                  fontWeight: 600,
                  color: theme.colors.text,
                  whiteSpace: 'nowrap',
                  boxShadow: '0 4px 12px rgba(0,0,0,0.45)',
                }}
              >
                {isFlingPhase ? 'Wheee! Bounce me on edges!' : 'Hehe, that tickles!'}
              </div>
            ) : null}
            <Img
              src={staticFile(
                isFlingPhase ? 'mascot/attention.png' : 'mascot/good.png'
              )}
              style={{ width: 80, height: 80, objectFit: 'contain' }}
            />
            <div
              style={{
                padding: '3px 10px',
                borderRadius: 999,
                backgroundColor: 'rgba(18, 20, 24, 0.92)',
                border: `1px solid ${theme.colors.outlineBright}`,
                fontFamily: theme.fonts.main,
                fontSize: 11,
                fontWeight: 600,
                color: theme.colors.textDim,
                whiteSpace: 'nowrap',
              }}
            >
              Uppy · Happy
            </div>
          </div>

          <div
            style={{
              position: 'absolute',
              left: cursorX,
              top: cursorY,
              opacity: cursorOpacity,
            }}
          >
            <CursorSvg grabbing={isFlingPhase && flingFrame < 16} />
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

// Beat 4: NEW — All 6 Live OLED Moods (870 - 1170 frames | 14.5s - 19.5s)
const SixMoodsBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 300
  const { fps } = useVideoConfig();

  const enter = spring({ frame, fps, config: { damping: 14, stiffness: 120 } });
  const exitOpacity = interpolate(frame, [280, 300], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  const moods = [
    { name: 'Idle', trigger: 'Standing by calmly', img: 'mascot/idle.png', color: theme.colors.accent },
    { name: 'Happy', trigger: 'Petting & healthy PC', img: 'mascot/good.png', color: theme.colors.accent },
    { name: 'Attention', trigger: 'Fling, bounce & alerts', img: 'mascot/attention.png', color: theme.colors.amber },
    { name: 'Smart Caffeine', trigger: 'Late-night sessions', img: 'mascot/coffee.png', color: theme.colors.amber },
    { name: 'Spotify', trigger: 'Dancing to your music', img: 'mascot/spotify.png', color: '#1DB954' },
    { name: 'Critical', trigger: 'High load or warnings', img: 'mascot/critical.png', color: theme.colors.orange },
  ];

  const activeIdx = Math.min(5, Math.floor(frame / 45));

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        opacity: exitOpacity,
      }}
    >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 36,
          transform: `translateY(${interpolate(enter, [0, 1], [30, 0])}px)`,
        }}
      >
        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 56,
            fontWeight: 800,
            color: theme.colors.text,
            letterSpacing: -1,
          }}
        >
          <span style={{ color: theme.colors.accent }}>6 Live OLED Moods</span>
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 360px)',
            gap: 24,
          }}
        >
          {moods.map((m, idx) => {
            const cardPop = spring({
              frame: Math.max(0, frame - idx * 6),
              fps,
              config: { damping: 14, stiffness: 130 },
            });
            const isActive = idx === activeIdx;
            return (
              <div
                key={m.name}
                style={{
                  padding: '24px 26px',
                  borderRadius: 22,
                  backgroundColor: isActive ? theme.colors.surfaceElevated : theme.colors.surface,
                  border: isActive
                    ? `1.5px solid ${m.color}`
                    : `1px solid ${theme.colors.outline}`,
                  boxShadow: isActive ? `0 16px 40px ${m.color}26` : '0 10px 28px rgba(0,0,0,0.35)',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 20,
                  transform: `scale(${cardPop * (isActive ? 1.03 : 1)})`,
                  opacity: cardPop,
                }}
              >
                <Img
                  src={staticFile(m.img)}
                  style={{ width: 84, height: 84, objectFit: 'contain' }}
                />
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <div
                    style={{
                      fontFamily: theme.fonts.main,
                      fontSize: 22,
                      fontWeight: 700,
                      color: isActive ? m.color : theme.colors.text,
                    }}
                  >
                    {m.name}
                  </div>
                  <div
                    style={{
                      fontFamily: theme.fonts.main,
                      fontSize: 15,
                      color: theme.colors.muted,
                    }}
                  >
                    {m.trigger}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </AbsoluteFill>
  );
};

// Beat 5: Spotify Dance (Clarity) & Live PC Reactions (1170 - 1440 frames | 19.5s - 24.0s)
const ReactionsBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 270
  const { fps } = useVideoConfig();

  const enter = spring({ frame, fps, config: { damping: 14, stiffness: 120 } });
  const danceBounce = Math.abs(Math.sin(frame * 0.12)) * -12;
  const danceTilt = Math.sin(frame * 0.06) * 5;

  const reactions = [
    {
      icon: <HeadphonesSvg color="#1DB954" />,
      title: 'Spotify Dance',
      sub: 'Dances to your track + shows Now Playing bubble',
      color: '#1DB954',
      img: 'mascot/spotify.png',
    },
    {
      icon: <CoffeeSvg color={theme.colors.amber} />,
      title: 'Night Owl Mode',
      sub: 'Reacts when you pull late-night sessions',
      color: theme.colors.amber,
      img: 'mascot/coffee.png',
    },
    {
      icon: <PulseSvg color={theme.colors.accent} />,
      title: 'Live PC Reactions',
      sub: 'Stability detective · Operations RAM snack',
      color: theme.colors.accent,
      img: 'mascot/good.png',
    },
  ];

  const activeIdx = frame < 100 ? 0 : frame < 185 ? 1 : 2;
  const exitOpacity = interpolate(frame, [250, 270], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        opacity: exitOpacity,
      }}
    >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 36,
          transform: `translateY(${interpolate(enter, [0, 1], [30, 0])}px)`,
        }}
      >
        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 54,
            fontWeight: 800,
            color: theme.colors.text,
            letterSpacing: -1,
          }}
        >
          Reacts live to your <span style={{ color: '#1DB954' }}>Spotify</span> &amp;{' '}
          <span style={{ color: theme.colors.accent }}>PC</span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 64 }}>
          <div
            style={{
              width: 430,
              height: 430,
              borderRadius: 32,
              backgroundColor: theme.colors.surface,
              border: `1px solid ${reactions[activeIdx].color}55`,
              boxShadow: `0 24px 70px rgba(0,0,0,0.55), 0 0 60px ${reactions[activeIdx].color}18`,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              position: 'relative',
              padding: 28,
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: 24,
                padding: '10px 18px',
                borderRadius: 999,
                backgroundColor: '#12151A',
                border: '1px solid rgba(29,185,84,0.45)',
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                fontFamily: theme.fonts.main,
                fontSize: 16,
                fontWeight: 600,
                color: theme.colors.text,
              }}
            >
              <MusicNoteSvg color="#1DB954" />
              <span>Now Playing · Clarity</span>
              <span style={{ display: 'inline-flex', gap: 3, alignItems: 'flex-end', height: 14 }}>
                {[0.7, 1.3, 0.9].map((spd, bi) => (
                  <span
                    key={bi}
                    style={{
                      width: 3,
                      height: 5 + Math.abs(Math.sin(frame * 0.2 * spd + bi)) * 10,
                      backgroundColor: '#1DB954',
                      borderRadius: 2,
                    }}
                  />
                ))}
              </span>
            </div>

            <Img
              src={staticFile(reactions[activeIdx].img)}
              style={{
                width: 240,
                height: 240,
                objectFit: 'contain',
                marginTop: 32,
                transform: `translateY(${danceBounce}px) rotate(${danceTilt}deg)`,
              }}
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 20, width: 560 }}>
            {reactions.map((r, idx) => {
              const isActive = idx === activeIdx;
              return (
                <div
                  key={r.title}
                  style={{
                    padding: '22px 26px',
                    borderRadius: 20,
                    backgroundColor: isActive
                      ? theme.colors.surfaceElevated
                      : 'rgba(27,30,36,0.55)',
                    border: isActive
                      ? `1.5px solid ${r.color}`
                      : `1px solid ${theme.colors.outline}`,
                    boxShadow: isActive ? `0 12px 36px ${r.color}22` : 'none',
                    transform: isActive ? 'scale(1.03)' : 'scale(1)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 18,
                  }}
                >
                  <div
                    style={{
                      width: 52,
                      height: 52,
                      borderRadius: 14,
                      backgroundColor: `${r.color}1E`,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    {r.icon}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                    <div
                      style={{
                        fontFamily: theme.fonts.main,
                        fontSize: 24,
                        fontWeight: 700,
                        color: theme.colors.text,
                      }}
                    >
                      {r.title}
                    </div>
                    <div
                      style={{
                        fontFamily: theme.fonts.main,
                        fontSize: 17,
                        color: theme.colors.muted,
                      }}
                    >
                      {r.sub}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

// Beat 6: Real Guided Tour Screenshot + Discord Rich Presence (1440 - 1710 frames | 24.0s - 28.5s)
const TourAndDiscordBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 270
  const { fps } = useVideoConfig();

  const leftCard = spring({ frame, fps, config: { damping: 14, stiffness: 120 } });
  const rightCard = spring({
    frame: Math.max(0, frame - 14),
    fps,
    config: { damping: 14, stiffness: 120 },
  });

  const exitOpacity = interpolate(frame, [250, 270], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <AbsoluteFill
      style={{
        alignItems: 'center',
        justifyContent: 'center',
        opacity: exitOpacity,
      }}
    >
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 34,
        }}
      >
        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 52,
            fontWeight: 800,
            color: theme.colors.text,
            letterSpacing: -1,
          }}
        >
          Guided Tour &amp;{' '}
          <span style={{ color: '#5865F2' }}>Discord Rich Presence</span>
        </div>

        <div style={{ display: 'flex', gap: 36, alignItems: 'center' }}>
          <div
            style={{
              width: 580,
              borderRadius: 20,
              overflow: 'hidden',
              boxShadow: '0 24px 60px rgba(0,0,0,0.6)',
              transform: `translateY(${interpolate(leftCard, [0, 1], [40, 0])}px)`,
              opacity: leftCard,
            }}
          >
            <Img
              src={staticFile('real-tour-card.png')}
              style={{ width: '100%', height: 'auto', display: 'block' }}
            />
          </div>

          <div
            style={{
              width: 540,
              padding: 32,
              borderRadius: 24,
              backgroundColor: '#111214',
              border: '1.5px solid rgba(88, 101, 242, 0.55)',
              boxShadow: '0 24px 60px rgba(0,0,0,0.55), 0 0 50px rgba(88,101,242,0.16)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between',
              gap: 20,
              transform: `translateY(${interpolate(rightCard, [0, 1], [40, 0])}px)`,
              opacity: rightCard,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <Img
                src={staticFile('mascot/discord-logo.png')}
                style={{ width: 26, height: 26, objectFit: 'contain' }}
              />
              <span
                style={{
                  fontFamily: theme.fonts.mono,
                  fontSize: 14,
                  fontWeight: 700,
                  color: '#8EA1E1',
                  letterSpacing: 0.6,
                }}
              >
                PLAYING A GAME · RICH PRESENCE
              </span>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
              <div
                style={{
                  width: 96,
                  height: 96,
                  borderRadius: 18,
                  backgroundColor: '#1E2128',
                  border: '1px solid #2F333D',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  position: 'relative',
                }}
              >
                <Img
                  src={staticFile('uptime-logo-official.png')}
                  style={{ width: 64, height: 64, objectFit: 'contain' }}
                />
                <div
                  style={{
                    position: 'absolute',
                    right: -8,
                    bottom: -8,
                    width: 38,
                    height: 38,
                    borderRadius: '50%',
                    backgroundColor: '#111214',
                    border: '2px solid #5865F2',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    overflow: 'hidden',
                  }}
                >
                  <Img
                    src={staticFile('mascot/spotify.png')}
                    style={{ width: 30, height: 30, objectFit: 'contain' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                <div
                  style={{
                    fontFamily: theme.fonts.main,
                    fontSize: 24,
                    fontWeight: 800,
                    color: '#FFFFFF',
                  }}
                >
                  Uptime
                </div>
                <div
                  style={{
                    fontFamily: theme.fonts.main,
                    fontSize: 17,
                    color: '#DCDDDE',
                  }}
                >
                  Uptime: 6h 48m · Healthy
                </div>
                <div
                  style={{
                    fontFamily: theme.fonts.main,
                    fontSize: 16,
                    color: '#7BC4A8',
                    fontWeight: 600,
                  }}
                >
                  Uppy is vibing to Spotify
                </div>
              </div>
            </div>

            <div
              style={{
                fontFamily: theme.fonts.main,
                fontSize: 15,
                color: theme.colors.muted,
              }}
            >
              Zero dependencies · Live status &amp; Uppy mood
            </div>
          </div>
        </div>

        <PillBadge text="Available in English · Italiano · Русский" />
      </div>
    </AbsoluteFill>
  );
};

// Beat 7: Finale CTA (1710 - 1920 frames | 28.5s - 32.0s)
const FinaleBeat: React.FC = () => {
  const frame = useCurrentFrame(); // 0 to 210
  const { fps } = useVideoConfig();

  const pop = spring({ frame, fps, config: { damping: 12, stiffness: 120 } });
  const floatY = Math.sin(frame * 0.045) * 6;

  return (
    <AbsoluteFill style={{ alignItems: 'center', justifyContent: 'center' }}>
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 18,
          transform: `scale(${interpolate(pop, [0, 1], [0.9, 1])})`,
          opacity: pop,
        }}
      >
        <div style={{ position: 'relative', transform: `translateY(${floatY}px)` }}>
          <div
            style={{
              position: 'absolute',
              inset: -45,
              borderRadius: '50%',
              background:
                'radial-gradient(circle, rgba(123,196,168,0.32) 0%, transparent 70%)',
            }}
          />
          <Img
            src={staticFile('mascot/good.png')}
            style={{ width: 220, height: 220, objectFit: 'contain', position: 'relative' }}
          />
        </div>

        <div
          style={{
            fontFamily: theme.fonts.main,
            fontSize: 58,
            fontWeight: 800,
            color: theme.colors.text,
            letterSpacing: -1,
          }}
        >
          Meet <span style={{ color: theme.colors.accent }}>Uppy 2.0</span>
        </div>

        <PillBadge text="Free · Lightweight · Windows" />
      </div>
    </AbsoluteFill>
  );
};

export const UppyShowcase: React.FC = () => {
  return (
    <AbsoluteFill style={{ backgroundColor: '#0A0C0E' }}>
      <Background />
      <Audio src={staticFile('uppy-soundtrack.wav')} volume={0.88} />

      <Sequence from={0} durationInFrames={312}>
        <HypeHookBeat />
      </Sequence>
      <Sequence from={312} durationInFrames={180}>
        <IntroBeat />
      </Sequence>
      <Sequence from={492} durationInFrames={378}>
        <InteractiveBeat />
      </Sequence>
      <Sequence from={870} durationInFrames={300}>
        <SixMoodsBeat />
      </Sequence>
      <Sequence from={1170} durationInFrames={270}>
        <ReactionsBeat />
      </Sequence>
      <Sequence from={1440} durationInFrames={270}>
        <TourAndDiscordBeat />
      </Sequence>
      <Sequence from={1710} durationInFrames={210}>
        <FinaleBeat />
      </Sequence>
    </AbsoluteFill>
  );
};

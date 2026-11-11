import React from 'react';
import { Audio, Sequence, staticFile } from 'remotion';
import { Act1Opening } from './scenes/Act1Opening';
import { Act2Reveal } from './scenes/Act2Reveal';
import { Act3Experience } from './scenes/Act3Experience';
import { Act4Impact } from './scenes/Act4Impact';
import { Act5FinalReveal } from './scenes/Act5FinalReveal';

export const Trailer: React.FC = () => {
  return (
    <div
      style={{
        position: 'relative',
        width: 1920,
        height: 1080,
        backgroundColor: '#0F1114',
        overflow: 'hidden',
      }}
    >
      {/* Synchronized Procedural Cinematic Master Audio */}
      <Audio src={staticFile('soundtrack.wav')} volume={1.0} />

      {/* Act I — The Opening (0.0s - 7.0s | frames 0 - 420) */}
      <Sequence from={0} durationInFrames={420} name="Act 1 — Opening">
        <Act1Opening />
      </Sequence>

      {/* Act II — The Reveal (7.0s - 15.0s | frames 420 - 900) */}
      <Sequence from={420} durationInFrames={480} name="Act 2 — The Reveal">
        <Act2Reveal />
      </Sequence>

      {/* Act III — The Experience (15.0s - 27.0s | frames 900 - 1620) */}
      <Sequence from={900} durationInFrames={720} name="Act 3 — The Experience">
        <Act3Experience />
      </Sequence>

      {/* Act IV — The Impact (27.0s - 35.0s | frames 1620 - 2100) */}
      <Sequence from={1620} durationInFrames={480} name="Act 4 — The Impact">
        <Act4Impact />
      </Sequence>

      {/* Act V — The Final Reveal (35.0s - 40.0s | frames 2100 - 2400) */}
      <Sequence from={2100} durationInFrames={300} name="Act 5 — Final Reveal">
        <Act5FinalReveal />
      </Sequence>
    </div>
  );
};

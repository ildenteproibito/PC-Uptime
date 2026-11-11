import React from 'react';
import { Composition, Still } from 'remotion';
import { Trailer } from './Trailer';
import { Poster } from './Poster';

export const Root: React.FC = () => {
  return (
    <>
      <Composition
        id="Trailer"
        component={Trailer}
        durationInFrames={2400}
        fps={60}
        width={1920}
        height={1080}
      />
      <Still
        id="Poster"
        component={Poster}
        width={1920}
        height={1080}
      />
    </>
  );
};

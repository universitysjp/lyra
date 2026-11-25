import { useState } from 'react';

import { artworkFor } from '../../utils/artwork';

interface IProps {
  src?: string | null;
  seed: string;
  initials?: string;
  alt?: string;
  className?: string;
}

/**
 * Cover art with a deterministic local fallback. When a track or album has no artwork,
 * or the image fails to load, the fallback renders the initials on a generated gradient.
 */
const Artwork: React.FC<IProps> = ({ src, seed, initials, alt = '', className = '' }) => {
  const [failed, setFailed] = useState(false);
  const fallback = artworkFor(seed, initials ?? seed);
  const image = src && !failed ? src : fallback;

  return (
    <img
      className={className}
      src={image}
      alt={alt}
      draggable={false}
      loading='lazy'
      onError={() => setFailed(true)}
    />
  );
};

export default Artwork;

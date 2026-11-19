import type { ReactNode } from 'react';

// interfaces
interface IProps {
  image: string;
  children?: ReactNode;
}

/** Blurred full-bleed backdrop behind a page, from the upstream template. */
const Cover: React.FC<IProps> = ({ image, children }) => (
  <>
    <div
      style={{
        backgroundImage: `url('${image}')`,
      }}
      className='cover'
    />
    {children}
  </>
);

export default Cover;

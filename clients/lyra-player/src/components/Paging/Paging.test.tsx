import { describe, expect, it, vi } from 'vite-plus/test';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import Paging from './Paging';

type Props = React.ComponentProps<typeof Paging>;

const withPager = (props: Partial<Props> = {}): void => {
  // Paging is presentational, so it renders without the app providers.
  render(
    <Paging page={2} totalPages={3} totalCount={120} pageSize={50} onChange={vi.fn()} {...props} />,
  );
};

describe('Paging', () => {
  it('describes the slice of the library that is on screen', () => {
    withPager();

    expect(screen.getByText('51–100 of 120')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
  });

  it('marks the edges of the result set', () => {
    withPager({ page: 1 });

    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
  });

  it('hides itself for an empty library', () => {
    withPager({ totalCount: 0 });

    expect(screen.queryByRole('button', { name: 'Next' })).not.toBeInTheDocument();
  });

  it('requests the page the user clicked', async () => {
    const onChange = vi.fn();

    withPager({ onChange });

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Previous' }));

    expect(onChange).toHaveBeenNthCalledWith(1, 3);
    expect(onChange).toHaveBeenNthCalledWith(2, 1);
  });
});

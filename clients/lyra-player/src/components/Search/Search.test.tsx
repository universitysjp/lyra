import { describe, expect, it } from 'vite-plus/test';

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useLocation } from 'react-router-dom';

import Search from './Search';
import { renderWithProviders } from '../../test/render';

const LocationDisplay: React.FC = () => {
  const location = useLocation();

  return <div data-testid='trail'>{`${location.pathname}${location.search}`}</div>;
};

const render = (route: string) =>
  renderWithProviders(
    <>
      <Search />
      <LocationDisplay />
    </>,
    { route },
  );

describe('Search', () => {
  it('submits the query into the search route', async () => {
    render('/library');

    await userEvent.type(screen.getByRole('searchbox'), 'bohemain rhapsody{Enter}');

    expect(screen.getByTestId('trail')).toHaveTextContent('/search?q=bohemain%20rhapsody');
  });

  it('keeps the url in sync while typing on the search page', async () => {
    render('/search');

    await userEvent.type(screen.getByRole('searchbox'), 'queen');

    expect(screen.getByTestId('trail')).toHaveTextContent('/search?q=queen');
  });

  it('clears the query from the url', async () => {
    render('/search?q=queen');

    expect(screen.getByRole('searchbox')).toHaveValue('queen');

    await userEvent.clear(screen.getByRole('searchbox'));
    await userEvent.keyboard('{Enter}');

    expect(screen.getByTestId('trail')).toHaveTextContent('/search');
  });
});

import { ApiError } from './http';

/** Human readable failure text for the UI, derived from problem+json where available. */
export const describeError = (error: unknown): string => {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof TypeError) {
    return `Could not reach the Lyra gateway: ${error.message}`;
  }

  if (error instanceof Error && error.name === 'AbortError') {
    return 'Request cancelled.';
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'Something went wrong.';
};

export const isAbortError = (error: unknown): boolean =>
  error instanceof Error && error.name === 'AbortError';

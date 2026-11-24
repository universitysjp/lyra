interface IProps {
  kind?: 'error' | 'info' | 'success';
  message: string | null;
  actionLabel?: string;
  onRetry?: () => void;
  onDismiss?: () => void;
}

const bannerClass = (kind: NonNullable<IProps['kind']>): string => {
  if (kind === 'error') {
    return 'banner banner-error';
  }

  return kind === 'success' ? 'banner banner-success' : 'banner';
};

/** Inline status message for errors, confirmations and missing files. */
const Banner: React.FC<IProps> = ({
  kind = 'error',
  message,
  actionLabel = 'Retry',
  onRetry,
  onDismiss,
}) => {
  if (!message) {
    return null;
  }

  return (
    <div className={bannerClass(kind)}>
      <span className='material-symbols-outlined' aria-hidden='true'>
        {kind === 'error' ? 'report_problem' : kind === 'success' ? 'check_circle' : 'info'}
      </span>
      <p>{message}</p>
      {onRetry && (
        <button type='button' className='btn btn-ghost btn-sm' onClick={onRetry}>
          {actionLabel}
        </button>
      )}
      {onDismiss && (
        <button type='button' className='icon-btn' onClick={onDismiss} aria-label='Dismiss'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            close
          </span>
        </button>
      )}
    </div>
  );
};

export default Banner;

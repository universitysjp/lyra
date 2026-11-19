// interfaces
interface IProps {
  kind?: 'error' | 'info';
  message: string | null;
  onRetry?: () => void;
  onDismiss?: () => void;
}

/** Inline problem report, mostly surfaced from backend problem+json payloads. */
const Banner: React.FC<IProps> = ({ kind = 'error', message, onRetry, onDismiss }) => {
  if (!message) {
    return null;
  }

  return (
    <div className={kind === 'error' ? 'banner banner-error flex flex-gap flex-v-center' : 'banner flex flex-gap flex-v-center'}>
      <span className='material-symbols-outlined' aria-hidden='true'>
        {kind === 'error' ? 'report_problem' : 'info'}
      </span>
      <p>{message}</p>
      {onRetry && (
        <button type='button' className='button black' onClick={onRetry}>
          Retry
        </button>
      )}
      {onDismiss && (
        <button type='button' className='icon-button' onClick={onDismiss} title='Dismiss'>
          <span className='material-symbols-outlined' aria-hidden='true'>
            close
          </span>
        </button>
      )}
    </div>
  );
};

export default Banner;

interface IProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  loading?: boolean;
  onChange: (page: number) => void;
}

/** Page controls for the library. */
const Paging: React.FC<IProps> = ({
  page,
  totalPages,
  totalCount,
  pageSize,
  loading = false,
  onChange,
}) => {
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(totalCount, page * pageSize);

  if (totalCount === 0) {
    return null;
  }

  return (
    <div className='pager'>
      <span className='pager-info'>
        {from}–{to} of {totalCount}
      </span>
      <div className='pager-actions'>
        <button
          type='button'
          className='btn btn-ghost btn-sm'
          disabled={loading || page <= 1}
          onClick={() => onChange(page - 1)}
        >
          Previous
        </button>
        <span className='pager-page'>
          Page {page} of {totalPages}
        </span>
        <button
          type='button'
          className='btn btn-ghost btn-sm'
          disabled={loading || page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
};

export default Paging;

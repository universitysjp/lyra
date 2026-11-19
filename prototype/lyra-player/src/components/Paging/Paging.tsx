// interfaces
interface IProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  loading?: boolean;
  onChange: (page: number) => void;
}

/** Pager for the backend-paged library endpoint (page / pageSize query params). */
const Paging: React.FC<IProps> = ({ page, totalPages, totalCount, pageSize, loading = false, onChange }) => {
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(totalCount, page * pageSize);

  if (totalCount === 0) {
    return null;
  }

  return (
    <div className='pager flex flex-gap flex-v-center flex-space-between'>
      <span className='chip'>
        {from}-{to} of {totalCount}
      </span>
      <div className='flex flex-gap-small flex-v-center'>
        <button
          type='button'
          className='button black'
          disabled={loading || page <= 1}
          onClick={() => onChange(page - 1)}
        >
          Previous
        </button>
        <span className='chip'>
          page {page} / {totalPages}
        </span>
        <button
          type='button'
          className='button black'
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

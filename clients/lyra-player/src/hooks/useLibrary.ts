import { use } from 'react';

import LibraryContext from '../contexts/LibraryContext';
import type { ILibraryContext } from '../contexts/LibraryContext';

const useLibrary = (): ILibraryContext => use(LibraryContext);

export default useLibrary;

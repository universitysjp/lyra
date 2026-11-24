import { use } from 'react';

import ApiContext from '../contexts/ApiContext';
import type { IApiContext } from '../contexts/ApiContext';

const useApi = (): IApiContext => use(ApiContext);

export default useApi;

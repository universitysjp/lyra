import { use } from 'react';

import AuthContext from '../contexts/AuthContext';
import type { IAuthContext } from '../contexts/AuthContext';

const useAuth = (): IAuthContext => use(AuthContext);

export default useAuth;

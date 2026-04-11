import { createContext, useContext, useState, useEffect } from 'react';
import { jwtDecode } from './jwtDecode';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      try {
        const decoded = jwtDecode(token);
        if (decoded.exp && decoded.exp * 1000 < Date.now()) {
          localStorage.removeItem('token');
        } else {
          setUser({
            token,
            username: decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"] 
                      || decoded.unique_name 
                      || decoded.name,
            role: decoded["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] 
                  || decoded.role,
            userId: decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] 
                    || decoded.nameid,
          });
        }
      } catch {
        localStorage.removeItem('token');
      }
    }
    setIsLoading(false);
  }, []);

  const login = (token, username, role) => {
    localStorage.setItem('token', token);
    const decoded = jwtDecode(token);
    setUser({ token, username, role, userId: decoded.nameid });
  };

  const logout = () => {
    localStorage.removeItem('token');
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, isAuth: !!user, isLoading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}

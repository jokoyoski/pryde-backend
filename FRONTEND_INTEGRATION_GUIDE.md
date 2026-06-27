# Pryde Authentication API - Frontend Integration Guide

## Quick Start

### Environment Setup

Create `.env` file in your frontend project:

```env
REACT_APP_API_BASE_URL=https://localhost:5001/api/v1
REACT_APP_API_TIMEOUT=30000
```

---

## Registration Implementation

### React Example

```javascript
import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'https://localhost:5001/api/v1';

export const registerUser = async (formData) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/auth/register`, {
      email: formData.email,
      phoneNumber: formData.phoneNumber,
      password: formData.password,
      firstName: formData.firstName,
      lastName: formData.lastName,
      roles: formData.roles
    }, {
      headers: {
        'Content-Type': 'application/json'
      }
    });

    return {
      success: true,
      data: response.data,
      message: 'Registration successful!'
    };
  } catch (error) {
    return {
      success: false,
      error: error.response?.data?.message || 'Registration failed',
      statusCode: error.response?.status
    };
  }
};
```

### React Component Example

```jsx
import React, { useState } from 'react';
import { registerUser } from '../services/authService';

const RegisterForm = () => {
  const [formData, setFormData] = useState({
    email: '',
    phoneNumber: '',
    password: '',
    firstName: '',
    lastName: '',
    roles: []
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  const handleInputChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
  };

  const handleRoleChange = (roleId) => {
    setFormData(prev => ({
      ...prev,
      roles: prev.roles.includes(roleId)
        ? prev.roles.filter(r => r !== roleId)
        : [...prev.roles, roleId]
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    // Validation
    if (!formData.email || !formData.password || !formData.firstName || !formData.lastName) {
      setError('All fields are required');
      setLoading(false);
      return;
    }

    if (formData.roles.length === 0) {
      setError('Please select at least one role');
      setLoading(false);
      return;
    }

    const result = await registerUser(formData);

    if (result.success) {
      setSuccess(true);
      setFormData({
        email: '',
        phoneNumber: '',
        password: '',
        firstName: '',
        lastName: '',
        roles: []
      });
      // Redirect to login or dashboard
      setTimeout(() => window.location.href = '/login', 2000);
    } else {
      setError(result.error);
    }

    setLoading(false);
  };

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label>Email *</label>
        <input
          type="email"
          name="email"
          value={formData.email}
          onChange={handleInputChange}
          required
        />
      </div>

      <div>
        <label>Phone Number *</label>
        <input
          type="tel"
          name="phoneNumber"
          value={formData.phoneNumber}
          onChange={handleInputChange}
          required
        />
      </div>

      <div>
        <label>First Name *</label>
        <input
          type="text"
          name="firstName"
          value={formData.firstName}
          onChange={handleInputChange}
          required
        />
      </div>

      <div>
        <label>Last Name *</label>
        <input
          type="text"
          name="lastName"
          value={formData.lastName}
          onChange={handleInputChange}
          required
        />
      </div>

      <div>
        <label>Password *</label>
        <input
          type="password"
          name="password"
          value={formData.password}
          onChange={handleInputChange}
          required
        />
        <small>Min 8 chars, uppercase, lowercase, number, special char</small>
      </div>

      <div>
        <label>User Type *</label>
        <div>
          <label>
            <input
              type="checkbox"
              checked={formData.roles.includes(1)}
              onChange={() => handleRoleChange(1)}
            />
            Passenger
          </label>
          <label>
            <input
              type="checkbox"
              checked={formData.roles.includes(2)}
              onChange={() => handleRoleChange(2)}
            />
            Driver
          </label>
        </div>
      </div>

      {error && <div className="error">{error}</div>}
      {success && <div className="success">Registration successful! Redirecting...</div>}

      <button type="submit" disabled={loading}>
        {loading ? 'Registering...' : 'Register'}
      </button>
    </form>
  );
};

export default RegisterForm;
```

---

## Login Implementation

### React Example

```javascript
export const loginUser = async (emailOrPhone, password) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/auth/login`, {
      emailOrPhone,
      password
    });

    // Store token
    localStorage.setItem('authToken', response.data.accessToken);
    localStorage.setItem('userId', response.data.userId);
    localStorage.setItem('userEmail', response.data.email);

    return {
      success: true,
      data: response.data,
      message: 'Login successful!'
    };
  } catch (error) {
    return {
      success: false,
      error: error.response?.data?.message || 'Login failed',
      statusCode: error.response?.status
    };
  }
};
```

### React Component Example

```jsx
import React, { useState } from 'react';
import { loginUser } from '../services/authService';

const LoginForm = () => {
  const [emailOrPhone, setEmailOrPhone] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    const result = await loginUser(emailOrPhone, password);

    if (result.success) {
      // Redirect to dashboard
      window.location.href = '/dashboard';
    } else {
      setError(result.error);
    }

    setLoading(false);
  };

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label>Email or Phone *</label>
        <input
          type="text"
          value={emailOrPhone}
          onChange={(e) => setEmailOrPhone(e.target.value)}
          required
        />
      </div>

      <div>
        <label>Password *</label>
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
      </div>

      {error && <div className="error">{error}</div>}

      <button type="submit" disabled={loading}>
        {loading ? 'Logging in...' : 'Login'}
      </button>
    </form>
  );
};

export default LoginForm;
```

---

## Authentication Context / State Management

### Using Context API

```javascript
import React, { createContext, useState, useCallback } from 'react';
import { loginUser, registerUser } from '../services/authService';

export const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(localStorage.getItem('authToken'));
  const [loading, setLoading] = useState(false);

  const login = useCallback(async (emailOrPhone, password) => {
    setLoading(true);
    const result = await loginUser(emailOrPhone, password);

    if (result.success) {
      setUser({
        userId: result.data.userId,
        email: result.data.email
      });
      setToken(result.data.accessToken);
    }

    setLoading(false);
    return result;
  }, []);

  const register = useCallback(async (formData) => {
    setLoading(true);
    const result = await registerUser(formData);

    if (result.success) {
      // Auto login after registration (optional)
      // const loginResult = await login(formData.email, formData.password);
      // return loginResult;
    }

    setLoading(false);
    return result;
  }, []);

  const logout = useCallback(() => {
    setUser(null);
    setToken(null);
    localStorage.removeItem('authToken');
    localStorage.removeItem('userId');
    localStorage.removeItem('userEmail');
  }, []);

  const value = {
    user,
    token,
    loading,
    login,
    register,
    logout,
    isAuthenticated: !!token
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
```

---

## API Interceptor Setup

### Axios Interceptor

```javascript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL || 'https://localhost:5001/api/v1'
});

// Request interceptor
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expired or invalid
      localStorage.removeItem('authToken');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default apiClient;
```

---

## Protected Routes

```jsx
import React from 'react';
import { useContext } from 'react';
import { AuthContext } from '../context/AuthContext';
import { Navigate } from 'react-router-dom';

const ProtectedRoute = ({ component: Component, ...rest }) => {
  const { isAuthenticated, loading } = useContext(AuthContext);

  if (loading) {
    return <div>Loading...</div>;
  }

  return isAuthenticated ? <Component {...rest} /> : <Navigate to="/login" />;
};

export default ProtectedRoute;
```

---

## Error Handling

```javascript
const handleAuthError = (error) => {
  switch (error.statusCode) {
    case 400:
      return 'Validation error: ' + error.error;
    case 401:
      return 'Invalid credentials';
    case 403:
      return 'Account restricted: ' + error.error;
    case 409:
      return 'Email or phone already registered';
    case 500:
      return 'Server error. Please try again later.';
    default:
      return 'An error occurred';
  }
};
```

---

## Password Requirements

Display to user during registration:

```javascript
const passwordRequirements = {
  minLength: 8,
  hasUppercase: /[A-Z]/.test(password),
  hasLowercase: /[a-z]/.test(password),
  hasNumber: /[0-9]/.test(password),
  hasSpecial: /[!@#$%^&*]/.test(password)
};

const isPasswordValid = Object.values(passwordRequirements).every(req => req === true || typeof req === 'boolean' && req);
```

---

## Testing

### Unit Test Example

```javascript
import { registerUser, loginUser } from '../services/authService';

describe('Auth Service', () => {
  test('should register user successfully', async () => {
    const formData = {
      email: 'test@example.com',
      phoneNumber: '+1234567890',
      password: 'TestPass123!',
      firstName: 'Test',
      lastName: 'User',
      roles: [1]
    };

    const result = await registerUser(formData);

    expect(result.success).toBe(true);
    expect(result.data.userId).toBeDefined();
  });

  test('should login user successfully', async () => {
    const result = await loginUser('test@example.com', 'TestPass123!');

    expect(result.success).toBe(true);
    expect(result.data.accessToken).toBeDefined();
  });
});
```

---

## Common Issues & Solutions

### Issue: CORS Error
**Solution**: Ensure backend has CORS configured for your frontend URL

### Issue: Token not persisting
**Solution**: Use localStorage and refresh token on app load

### Issue: 401 Unauthorized on protected routes
**Solution**: Check token format in Authorization header (should be `Bearer <token>`)

### Issue: Password validation fails
**Solution**: Ensure password meets all requirements (8+ chars, upper, lower, number, special)

---

## Support & Resources

- API Documentation: See `API_DOCUMENTATION.md`
- Postman Collection: Import `Pryde_Auth_API_Postman_Collection.json`
- Backend Repository: https://github.com/jokoyoski/pryde-backend

---

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0.0 | 2024-01-15 | Initial release |

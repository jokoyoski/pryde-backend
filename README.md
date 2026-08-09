# Pryde Backend - API Documentation & Frontend Integration

## ?? Documentation Files

This package contains comprehensive API documentation and frontend integration guides for the Pryde Authentication system.

## Trip booking cutoff

Trips close for new bookings 15 minutes before their UTC departure time by
default. Drivers can override the default per one-time or recurring trip with
`bookingWindowMinutes`. Trip creation and updates require the calculated cutoff
(`departureUtc - bookingWindowMinutes`) to remain in the future; discovery,
booking, and recurring occurrence generation use the same cutoff rule.

The application default is configured in `appsettings.json`:

```json
{
  "Trips": {
    "DefaultBookingWindowMinutes": 15
  }
}
```

For deployments, override it with
`Trips__DefaultBookingWindowMinutes`. Nearby discovery uses the validated
request `pickupRadiusKm` when supplied and otherwise falls back to
`Pricing__PickupRadiusKm`.

### 1. **API_DOCUMENTATION.md** 
Complete API reference for Registration and Login endpoints.

**Contains:**
- Base URL and endpoints
- Request/response schemas
- All error codes and messages
- cURL and JavaScript examples
- Security considerations
- Status codes reference
- Authentication flow diagram

**Use this for:**
- Developers integrating the API
- QA testing endpoints
- Understanding API contract
- Error handling reference

---

### 2. **FRONTEND_INTEGRATION_GUIDE.md**
Step-by-step guide for frontend developers to integrate authentication.

**Contains:**
- React component examples
- State management patterns
- API interceptor setup
- Protected routes implementation
- Error handling strategies
- Password validation logic
- Testing examples
- Common issues and solutions

**Use this for:**
- Building registration forms
- Building login forms
- Managing auth state
- Protecting routes
- Handling auth errors

---

### 3. **Pryde_Auth_API_Postman_Collection.json**
Ready-to-import Postman collection for testing API endpoints.

**Contains:**
- Pre-configured requests for Register and Login
- Example requests for different scenarios
- Test scripts for token management
- Environment variables setup
- Response examples

**How to use:**
1. Open Postman
2. Click "Import"
3. Select `Pryde_Auth_API_Postman_Collection.json`
4. Set `base_url` variable in environment (default: `https://localhost:5001/api/v1`)
5. Start testing endpoints

---

## ?? Quick Start

### For Backend Developers
1. Read `API_DOCUMENTATION.md` for complete endpoint reference
2. Use `Pryde_Auth_API_Postman_Collection.json` to test locally

### For Frontend Developers
1. Start with `FRONTEND_INTEGRATION_GUIDE.md`
2. Reference `API_DOCUMENTATION.md` for request/response formats
3. Use `Pryde_Auth_API_Postman_Collection.json` to understand API behavior

### For QA/Testers
1. Review `API_DOCUMENTATION.md` for all test scenarios
2. Import and use `Pryde_Auth_API_Postman_Collection.json` for testing
3. Check error cases and edge cases sections

---

## ?? Endpoints Overview

### Authentication Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/auth/register` | Create new user account |
| POST | `/auth/login` | Authenticate user and get JWT token |

---

## ?? Authentication Flow

```
User ? Register ? Verify Credentials ? Create Account ? Return UserID
                                                              ?
User ? Login ? Validate Password ? Generate JWT Token ? Return Token
                                                          ?
                                              Use in Authorization Header
```

---

## ?? Request/Response Examples

### Register Request
```json
{
  "email": "user@example.com",
  "phoneNumber": "+1234567890",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "roles": [1, 2]
}
```

### Register Response (201 Created)
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "status": 1,
  "roles": [1, 2]
}
```

### Login Request
```json
{
  "emailOrPhone": "user@example.com",
  "password": "SecurePassword123!"
}
```

### Login Response (200 OK)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com"
}
```

---

## ??? Security Checklist

- ? HTTPS only in production
- ? Password: 8+ chars, uppercase, lowercase, number, special char
- ? Bcrypt password hashing with salt
- ? JWT token authentication
- ? Rate limiting on auth endpoints
- ? Token expiration after 24 hours
- ? Input validation on all fields
- ? Unique email and phone constraints
- ? Account status validation
- ? CORS properly configured

---

## ?? Testing the API

### Using Postman
1. Import the collection
2. Set environment variables
3. Send requests and verify responses
4. Check test results

### Using cURL
```bash
# Register
curl -X POST https://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","phoneNumber":"+1234567890","password":"Pass123!","firstName":"Test","lastName":"User","roles":[1]}'

# Login
curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"emailOrPhone":"test@example.com","password":"Pass123!"}'
```

---

## ?? Role Types

| Role | Value | Description |
|------|-------|-------------|
| Passenger | 1 | User can request rides |
| Driver | 2 | User can offer rides |
| Admin | 3 | Cannot be self-assigned during registration |

---

## ?? User Status Types

| Status | Value | Description |
|--------|-------|-------------|
| Pending | 1 | Awaiting verification |
| Active | 2 | Account is active |
| Suspended | 3 | Temporarily restricted |
| Deactivated | 4 | Permanently disabled |

---

## ?? Common Error Scenarios

### 400 - Validation Error
- Empty password
- Invalid email format
- No roles selected
- Duplicate roles
- Admin role selected during registration

### 401 - Unauthorized
- Invalid credentials
- Expired token
- Missing token

### 403 - Forbidden
- Account suspended
- Account deactivated
- Insufficient permissions

### 409 - Conflict
- Email already registered
- Phone number already registered

### 500 - Server Error
- Database connection error
- Internal server exception

---

## ?? Support Contacts

- **API Support**: api-support@pryde.com
- **Backend Team**: backend@pryde.com
- **Frontend Team**: frontend@pryde.com
- **QA Team**: qa@pryde.com

---

## ?? Version Information

- **API Version**: 1.0.0
- **.NET Version**: .NET 9
- **Documentation Version**: 1.0.0
- **Last Updated**: 2024-01-15

---

## ?? Related Resources

- Backend Repository: https://github.com/jokoyoski/pryde-backend
- API Endpoint: https://api.pryde.com/api/v1
- Swagger/OpenAPI: https://api.pryde.com/swagger

---

## ?? Checklist for Integration

- [ ] Review API_DOCUMENTATION.md
- [ ] Import Postman collection
- [ ] Test Register endpoint
- [ ] Test Login endpoint
- [ ] Test error scenarios
- [ ] Set up frontend auth context
- [ ] Implement registration form
- [ ] Implement login form
- [ ] Set up API interceptors
- [ ] Test protected routes
- [ ] Implement token refresh (if needed)
- [ ] Add loading states
- [ ] Add error handling
- [ ] Test in production environment

---

**Made with ?? by Pryde Team**

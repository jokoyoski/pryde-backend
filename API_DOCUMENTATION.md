# Pryde Backend API Documentation

## Authentication Endpoints

### Base URL
```
https://api.pryde.com/api/v1
```

---

## 1. User Registration

### Endpoint
```
POST /auth/register
```

### Description
Register a new user with email/phone, password, and role selection (Passenger or Driver).

### Request Headers
```
Content-Type: application/json
```

### Request Body
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

### Request Parameters

| Field | Type | Required | Description | Validation |
|-------|------|----------|-------------|-----------|
| `email` | string | Yes | User's email address | Must be valid email format, unique |
| `phoneNumber` | string | Yes | User's phone number | Must be unique, international format recommended |
| `password` | string | Yes | User's password | Minimum 8 characters, must include uppercase, lowercase, number, special char |
| `firstName` | string | Yes | User's first name | Max 100 characters |
| `lastName` | string | Yes | User's last name | Max 100 characters |
| `roles` | array[integer] | Yes | User role types | At least one role required. Values: `1` = Passenger, `2` = Driver. Cannot include `3` (Admin) |

### Role Enum Values
```json
{
  "Passenger": 1,
  "Driver": 2,
  "Admin": 3
}
```

### Response (201 Created)
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "status": 1,
  "roles": [1, 2]
}
```

### Response Parameters

| Field | Type | Description |
|-------|------|-------------|
| `userId` | string (UUID) | Unique identifier for the created user |
| `email` | string | Registered email address |
| `status` | integer | User status (1 = Pending, 2 = Active, 3 = Suspended, 4 = Deactivated) |
| `roles` | array[integer] | Assigned roles to the user |

### Error Responses

#### 400 - Bad Request (Validation Error)
```json
{
  "error": "ValidationError",
  "message": "Password cannot be empty.",
  "statusCode": 400
}
```

**Possible Validation Messages:**
- `"Password cannot be empty."`
- `"At least one role (Passenger or Driver) must be selected."`
- `"Duplicate roles are not allowed."`
- `"Admin role cannot be self-assigned during registration."`

#### 409 - Conflict
```json
{
  "error": "ConflictError",
  "message": "A user with this email or phone number already exists.",
  "statusCode": 409
}
```

#### 500 - Internal Server Error
```json
{
  "error": "InternalServerError",
  "message": "An unexpected error occurred.",
  "statusCode": 500
}
```

### Example Request (cURL)
```bash
curl -X POST https://api.pryde.com/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "phoneNumber": "+1234567890",
    "password": "SecurePass123!",
    "firstName": "John",
    "lastName": "Doe",
    "roles": [1]
  }'
```

### Example Request (JavaScript/Fetch)
```javascript
fetch('https://api.pryde.com/api/v1/auth/register', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    email: 'john.doe@example.com',
    phoneNumber: '+1234567890',
    password: 'SecurePass123!',
    firstName: 'John',
    lastName: 'Doe',
    roles: [1]
  })
})
.then(response => response.json())
.then(data => console.log(data))
.catch(error => console.error('Error:', error));
```

### Example Response (Success)
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com",
  "status": 1,
  "roles": [1]
}
```

---

## 2. User Login

### Endpoint
```
POST /auth/login
```

### Description
Authenticate a user using email/phone number and password to receive an access token.

### Request Headers
```
Content-Type: application/json
```

### Request Body
```json
{
  "emailOrPhone": "user@example.com",
  "password": "SecurePassword123!"
}
```

### Request Parameters

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `emailOrPhone` | string | Yes | User's email or phone number |
| `password` | string | Yes | User's password |

### Response (200 OK)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com"
}
```

### Response Parameters

| Field | Type | Description |
|-------|------|-------------|
| `accessToken` | string (JWT) | Bearer token for authenticated requests. Include in `Authorization` header |
| `userId` | string (UUID) | User's unique identifier |
| `email` | string | User's registered email address |

### Error Responses

#### 401 - Unauthorized
```json
{
  "error": "UnauthorizedError",
  "message": "Invalid email/phone number or password.",
  "statusCode": 401
}
```

#### 403 - Forbidden (Account Suspended/Deactivated)
```json
{
  "error": "ForbiddenError",
  "message": "This account has been suspended.",
  "statusCode": 403
}
```

**Possible Messages:**
- `"This account has been suspended."`
- `"This account has been deactivated."`

#### 500 - Internal Server Error
```json
{
  "error": "InternalServerError",
  "message": "An unexpected error occurred.",
  "statusCode": 500
}
```

### Example Request (cURL)
```bash
curl -X POST https://api.pryde.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "emailOrPhone": "john.doe@example.com",
    "password": "SecurePass123!"
  }'
```

### Example Request (JavaScript/Fetch)
```javascript
fetch('https://api.pryde.com/api/v1/auth/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    emailOrPhone: 'john.doe@example.com',
    password: 'SecurePass123!'
  })
})
.then(response => response.json())
.then(data => {
  if (data.accessToken) {
    localStorage.setItem('authToken', data.accessToken);
    console.log('Login successful');
  }
})
.catch(error => console.error('Error:', error));
```

### Example Response (Success)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJlbWFpbCI6ImpvaG4uZG9lQGV4YW1wbGUuY29tIiwicm9sZXMiOlsiUGFzc2VuZ2VyIl0sImV4cCI6MTcwMzA2MTMzMn0...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "john.doe@example.com"
}
```

### Using Access Token in Requests
Include the `accessToken` in subsequent API requests:

```bash
curl -X GET https://api.pryde.com/api/v1/profile \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## Authentication Flow Diagram

```
???????????????
?   Client    ?
???????????????
       ?
       ???? POST /auth/register ???????????????
       ?                                       ?
       ?                           ????????????????????????
       ?                           ?  Create User         ?
       ?                           ?  Hash Password       ?
       ?                           ?  Create Profile      ?
       ?                           ?  Assign Roles        ?
       ?                           ?  Create KYC Record   ?
       ?                           ????????????????????????
       ?                                       ?
       ??? RegisterResponseDto ?????????????????
       ?
       ?
       ???? POST /auth/login ??????????????????
       ?                                       ?
       ?                           ????????????????????????
       ?                           ?  Validate Email      ?
       ?                           ?  Verify Password     ?
       ?                           ?  Check Status        ?
       ?                           ?  Generate JWT        ?
       ?                           ????????????????????????
       ?                                       ?
       ??? LoginResponseDto (JWT Token)????????
       ?
       ???? Authenticated Requests
            Authorization: Bearer <JWT>
```

---

## Status Codes

| Status | Value | Description |
|--------|-------|-------------|
| Pending | 1 | User account created, awaiting verification |
| Active | 2 | User account is active and verified |
| Suspended | 3 | User account is temporarily suspended |
| Deactivated | 4 | User account has been deactivated |

---

## Error Handling

### Standard Error Response Format
```json
{
  "error": "ErrorType",
  "message": "Detailed error message",
  "statusCode": 400,
  "timestamp": "2024-01-15T10:30:45.123Z"
}
```

### Exception Types

| Exception Type | HTTP Status | Description |
|---|---|---|
| ValidationException | 400 | Input validation failed |
| ConflictException | 409 | Resource already exists or conflict occurred |
| UnauthorizedException | 401 | Authentication failed or invalid credentials |
| ForbiddenException | 403 | User doesn't have permission or account restricted |
| NotFoundException | 404 | Resource not found |

---

## Security Considerations

1. **HTTPS Only**: Always use HTTPS in production
2. **Password Requirements**:
   - Minimum 8 characters
   - Must contain uppercase letter (A-Z)
   - Must contain lowercase letter (a-z)
   - Must contain number (0-9)
   - Must contain special character (!@#$%^&*)

3. **Token Expiration**: Access tokens expire after 24 hours
4. **Rate Limiting**: 5 login attempts per 15 minutes per IP
5. **Password Hashing**: Bcrypt with salt rounds = 12

---

## Testing Credentials

```json
{
  "test_email": "test@pryde.com",
  "test_phone": "+1234567890",
  "test_password": "TestPass123!",
  "test_roles": [1, 2]
}
```

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-01-15 | Initial API documentation |

---

## Support

For technical support or questions, contact: **api-support@pryde.com**

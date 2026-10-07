# Архітектура сервісу автентифікації

Документ показує структуру сервісу `auth`, основні залежності та життєві цикли
автентифікації. Діаграми можна переглядати у VS Code з Mermaid-розширенням або
у GitHub/іншому Markdown-рендері з підтримкою Mermaid.

## 1. Загальна структура та залежності шарів

```mermaid
flowchart TB
    Client["Клієнт / інший сервіс"]
    Api["auth.Api<br/>ASP.NET Core"]
    App["auth.Application<br/>Commands, Queries, Handlers"]
    Domain["auth.Domain<br/>Entities, Result, Errors, repository contracts"]
    Infra["auth.Infrastructure<br/>EF Core, PostgreSQL, security implementations"]
    DB[("PostgreSQL<br/>users, sessions, api_keys,<br/>api_key_permissions")]
    Jwt["RSA JWT key pair<br/>private key: environment<br/>public key: JWKS endpoint"]

    Client -->|HTTP JSON| Api
    Api -->|ProjectReference| App
    Api -->|ProjectReference| Infra
    Infra -->|ProjectReference| App
    App -->|ProjectReference| Domain
    Infra -->|implements contracts| App
    App -->|uses repository interfaces| Domain
    Infra -->|EF Core / Npgsql| DB
    Infra --> Jwt

    subgraph API["Вхідний HTTP-шар"]
        Controllers["AuthController<br/>ApiKeysController<br/>JwksController"]
        Middleware["ExceptionMiddleware<br/>ASP.NET Authentication<br/>CurrentUserMiddleware"]
        Controllers --> Middleware
    end

    subgraph APPLICATION["Шар прикладної логіки"]
        Pipeline["MediatR + ValidationBehavior"]
        Handlers["Auth / ApiKeys / Jwks handlers"]
        Contracts["IUserRepository<br/>ISessionRepository<br/>IApiKeyRepository<br/>IUnitOfWork<br/>IJwtProvider та інші"]
        Pipeline --> Handlers
        Handlers --> Contracts
    end

    subgraph INFRASTRUCTURE["Реалізації"]
        Repositories["EF repositories"]
        Context["AppDbContext<br/>transaction boundary + mappings"]
        Security["JwtProvider<br/>ApiKeyGenerator<br/>PasswordHasher"]
        Repositories --> Context
    end

    Api -.-> Controllers
    Controllers --> Pipeline
    Contracts -.-> Repositories
    Contracts -.-> Security
    Context --> DB
```

> **Нотатка:** `Application` залежить лише від контрактів, а не від EF Core чи
> PostgreSQL. Реальні repository/security-реалізації підключаються в
> `Program.cs` через `AddInfrastructure()` і DI.

## 2. HTTP-конвеєр запиту

```mermaid
flowchart LR
    Request["HTTP request"] --> Exceptions["ExceptionMiddleware"]
    Exceptions --> Swagger["Swagger / Swagger UI<br/>(документація)"]
    Swagger --> AuthN["UseAuthentication<br/>JWT Bearer validation"]
    AuthN --> CurrentUser["CurrentUserMiddleware<br/>sub claim -> ICurrentUser"]
    CurrentUser --> AuthZ["UseAuthorization"]
    AuthZ --> Controller["Controller action"]
    Controller --> Send["ISender.Send(command/query)"]
    Send --> Validation["ValidationBehavior<br/>FluentValidation"]
    Validation -->|valid| Handler["MediatR handler"]
    Validation -->|invalid Result| Controller
    Handler --> Result["Result / Result<T>"]
    Result --> Base["BaseApiController.HandleResult"]
    Base --> Response["200 / ProblemDetails"]

    Note1["Валідація виконується до handler.<br/>Помилки домену не кидаються як винятки:<br/>вони перетворюються на HTTP ProblemDetails."]:::note
    Validation -.-> Note1

    Note2["CurrentUserMiddleware запускається після JWT authentication,<br/>тому читає вже перевірений sub claim."]:::note
    CurrentUser -.-> Note2

    classDef note fill:#fff8dc,stroke:#b8860b,color:#333;
```

## 3. Публічні HTTP-поверхні

```mermaid
flowchart TB
    subgraph PUBLIC["Публічні endpoints"]
        Register["POST /auth/register"]
        Login["POST /auth/login"]
        Refresh["POST /auth/refresh"]
        Logout["POST /auth/logout"]
        Exchange["POST /auth/token"]
        Jwks["GET /.well-known/jwks"]
    end

    subgraph PROTECTED["JWT-required endpoints"]
        List["GET /auth/keys"]
        Permissions["GET /auth/keys/permissions"]
        Paged["GET /auth/keys/paged"]
        Create["POST /auth/keys"]
        Revoke["POST /auth/keys/{id}/revoke"]
    end

    Register --> RegisterHandler["RegisterHandler"]
    Login --> LoginHandler["LoginHandler"]
    Refresh --> RefreshHandler["RefreshHandler"]
    Logout --> LogoutHandler["LogoutHandler"]
    Exchange --> ExchangeHandler["ExchangeApiKeyHandler"]
    Jwks --> JwksHandler["GetJwksHandler"]

    List --> ListHandler["ListApiKeysHandler"]
    Permissions --> PermissionHandler["ListApiKeyPermissionsHandler"]
    Paged --> PagedHandler["ListApiKeysPagedQuery handler"]
    Create --> CreateHandler["CreateApiKeyHandler"]
    Revoke --> RevokeHandler["RevokeApiKeyHandler"]

    ProtectedAuth["JWT Bearer authentication<br/>+ CurrentUser"] -.-> PROTECTED
```

## 4. Реєстрація та login

```mermaid
sequenceDiagram
    autonumber
    participant C as Клієнт
    participant API as AuthController
    participant M as MediatR + ValidationBehavior
    participant H as Register/LoginHandler
    participant U as IUserRepository
    participant P as PasswordHasher
    participant J as JwtProvider
    participant S as ISessionRepository
    participant TX as IUnitOfWork
    participant DB as PostgreSQL

    C->>API: POST /auth/register або /auth/login
    API->>M: Send(command)
    M->>M: FluentValidation
    alt register
        M->>H: RegisterCommand
        H->>U: GetByLoginAsync(login)
        U->>DB: SELECT users
        H->>P: Hash(password)
        H->>J: GenerateAccessToken(userId)
        H->>J: GenerateRefreshToken(userId)
        H->>TX: ExecuteInTransactionAsync
        TX->>U: AddAsync(user)
        TX->>S: AddAsync(session with refresh JTI)
        TX->>DB: SaveChanges + COMMIT
    else login
        M->>H: LoginCommand
        H->>U: GetByLoginAsync(login)
        U->>DB: SELECT users
        H->>P: Verify(password, PasswordHash)
        H->>J: GenerateAccessToken(userId)
        H->>J: GenerateRefreshToken(userId)
        H->>S: AddAsync(session with refresh JTI)
        H->>TX: SaveChangesAsync
        TX->>DB: INSERT sessions
    end
    H-->>M: accessToken + refreshToken
    M-->>API: Result<T>
    API-->>C: 200 OK

    Note over H,DB: Register створює user і session в одній транзакції.<br/>Login додає session після успішної перевірки пароля.
    Note over J: Access token короткоживучий (типово 15 хв).<br/>Refresh token довгоживучий (типово 30 днів).
```

## 5. Refresh rotation та logout

```mermaid
sequenceDiagram
    autonumber
    participant C as Клієнт
    participant API as AuthController
    participant H as Refresh/LogoutHandler
    participant J as JwtProvider
    participant S as SessionRepository
    participant TX as AppDbContext (IUnitOfWork)
    participant DB as PostgreSQL

    C->>API: POST /auth/refresh з refresh token
    API->>H: RefreshCommand
    H->>J: ValidateRefreshToken(token)
    J-->>H: subjectId + jti
    H->>S: GetByRefreshTokenJtiAsync(jti)
    S->>DB: SELECT sessions
    H->>TX: ExecuteInTransactionAsync
    TX->>S: RevokeIfActiveAsync(jti, userId, now)
    S->>DB: UPDATE ... WHERE active AND not expired
    alt рівно один session відкликано
        H->>J: GenerateAccessToken(userId)
        H->>J: GenerateRefreshToken(userId)
        H->>S: AddAsync(new session)
        H->>TX: SaveChangesAsync + COMMIT
        H-->>API: new access + refresh tokens
        API-->>C: 200 OK
    else token повторно використано / протерміновано
        H-->>API: Unauthorized
        API-->>C: 401 ProblemDetails
    end

    Note over S,DB: RevokeIfActiveAsync — захист від повторного використання:<br/>атомарне UPDATE має зачепити рівно один активний запис.
    Note over C,API: Logout використовує той самий JTI lookup,<br/>але лише виставляє RevokedAtUtc і не створює нову session.
```

## 6. API-ключі: створення, обмін і відкликання

```mermaid
sequenceDiagram
    autonumber
    participant C as Клієнт
    participant API as ApiKeys/AuthController
    participant CU as CurrentUser
    participant H as API-key handler
    participant G as ApiKeyGenerator
    participant R as API-key repository
    participant U as User/Permission repositories
    participant DB as PostgreSQL
    participant J as JwtProvider

    C->>API: POST /auth/keys (JWT)
    API->>CU: userId з перевіреного sub
    API->>H: CreateApiKeyCommand
    H->>U: Перевірити user + permission readwrite
    H->>G: Generate(new key id)
    G-->>H: повний key + prefix + SHA-256 hash + last4
    H->>R: AddAsync(ApiKey)
    R->>DB: INSERT api_keys (без plaintext secret)
    H-->>C: повний API key (показується один раз)

    C->>API: POST /auth/token з API key
    API->>H: ExchangeApiKeyCommand
    H->>R: GetByPrefixAsync("ak_{id}")
    R->>DB: SELECT api_keys
    H->>G: Verify(id, key, SecretHash)
    H->>R: Update LastUsedAtUtc
    H->>DB: SaveChanges
    H->>J: GenerateAccessToken(ApiKey.UserId)
    H-->>C: access token

    C->>API: POST /auth/keys/{id}/revoke (JWT)
    API->>H: RevokeApiKeyCommand
    H->>R: GetByIdAsync(id)
    H->>R: Update RevokedAtUtc
    H->>DB: SaveChanges
    H-->>C: 200 OK

    Note over G,DB: У базі зберігається лише SHA-256 hash secret.<br/>Пошук спочатку звужується унікальним prefix, потім Verify використовує constant-time comparison.
    Note over API,CU: Створення/перелік/відкликання API-ключів вимагають JWT<br/>і перевіряють ownership через CurrentUser.
```

## 7. Модель даних і зв'язки

```mermaid
erDiagram
    USERS {
        uuid id PK
        varchar login UK
        varchar password_hash
    }

    SESSIONS {
        uuid id PK
        uuid user_id FK
        varchar refresh_token_jti UK
        timestamptz expires_at_utc
        timestamptz revoked_at_utc
    }

    API_KEYS {
        uuid id PK
        uuid user_id FK
        varchar name
        varchar prefix UK
        varchar secret_hash
        varchar last4
        uuid api_key_permission_id FK
        timestamptz last_used_at_utc
        timestamptz created_at_utc
        timestamptz revoked_at_utc
    }

    API_KEY_PERMISSIONS {
        uuid id PK
        varchar name UK
    }

    USERS ||--o{ SESSIONS : "owns"
    USERS ||--o{ API_KEYS : "owns"
    API_KEY_PERMISSIONS ||--o{ API_KEYS : "classifies"
```

> **Нотатка:** видалення `User` каскадно видаляє його `Session` і `ApiKey`.
> Видалення permission заборонене (`Restrict`), тому permission не може
> залишити ключі без класифікації.

## 8. Безпекові особливості

```mermaid
flowchart TB
    Env["Environment variables"]
    Env --> RSA["Jwt__PrivateKeyPem<br/>RSA >= 2048 bits"]
    Env --> DBEnv["DB_HOST / DB_PORT / DB_NAME<br/>DB_USER / DB_PASSWORD"]
    RSA --> Provider["JwtProvider"]
    Provider --> Sign["RS256 signing"]
    Provider --> Validate["Bearer validation"]
    Provider --> Jwks["Public JWK / JWKS"]
    Jwks --> Consumers["Сервіси, що перевіряють JWT"]

    Password["Пароль"] --> Hasher["PasswordHasher"]
    Hasher --> PasswordHash["PasswordHash у users"]

    PlainKey["API key: ak_id_secret"] --> Split["prefix + id + secret"]
    Split --> Lookup["Lookup за prefix"]
    Split --> Hash["SHA-256 secret"]
    Hash --> Compare["FixedTimeEquals"]
    Compare --> Decision{"valid + not revoked?"}
    Decision -->|так| Access["Access JWT"]
    Decision -->|ні| Deny["401 Unauthorized"]

    Note1["Приватний RSA-ключ не зберігається в БД.<br/>JWKS віддає лише public key material."]:::note
    Provider -.-> Note1
    Note2["Plaintext API key повертається тільки під час створення;<br/>у БД залишається hash."]:::note
    PlainKey -.-> Note2

    classDef note fill:#fff8dc,stroke:#b8860b,color:#333;
```

## 9. Швидка навігація по коду

| Область | Основні файли |
|---|---|
| HTTP і middleware | `src/Api/Program.cs`, `src/Api/Controllers/`, `src/Api/Middleware/` |
| Команди, queries і handlers | `src/Application/Commands/`, `src/Application/Queries/`, `src/Application/Handlers/` |
| Валідація | `src/Application/Validators/`, `src/Application/Behaviors/ValidationBehavior.cs` |
| Контракти | `src/Application/Abstractions/`, `src/Domain/Repositories/` |
| Модель домену | `src/Domain/Entities/`, `src/Domain/Result.cs`, `src/Domain/Errors/` |
| Persistence | `src/Infrastructure/Persistence/AppDbContext.cs`, `src/Infrastructure/Persistence/Repositories/` |
| Security | `src/Infrastructure/Security/` |


# Audit report

Дата аудиту: 2026-10-07

Перевірено поточний стан проєкту `auth` без змін у коді та базі даних. Окремо перевірено API, application handlers, persistence, security-код і Docker-конфігурацію.

## Підсумок

| # | Severity | File | Lines | Проблема | Confidence |
|---|---|---|---:|---|---:|
| 1 | CRITICAL | [src/Api/Controllers/ApiKeysController.cs](src/Api/Controllers/ApiKeysController.cs#L13) | 13-30 | API-ключі можна створювати, переглядати й відкликати без автентифікації та перевірки власника | 10/10 |
| 2 | HIGH | [docker-compose.yml](docker-compose.yml#L17) | 17-24 | PostgreSQL опублікований назовні зі статичними слабкими credentials | 9/10 |
| 3 | RESOLVED | [Dockerfile](Dockerfile#L17) | 17-20 | Auth API налаштовано на HTTPS; TLS-сертифікат передається в контейнер через volume та secret environment variable | 9/10 |
| 4 | HIGH | [src/Api/Program.cs](src/Api/Program.cs#L11) | 11-31 | Не налаштовано authentication/authorization middleware; Swagger доступний без production-обмеження | 9/10 |
| 5 | MEDIUM | [src/Application/Handlers/Auth/RefreshHandler.cs](src/Application/Handlers/Auth/RefreshHandler.cs#L24) | 24-53 | Refresh-token rotation має race condition і допускає повторне конкурентне використання токена | 8/10 |
| 6 | RESOLVED | [src/Application/Handlers/ApiKeys/CreateApiKeyHandler.cs](src/Application/Handlers/ApiKeys/CreateApiKeyHandler.cs#L22) | 22-75 | Дубльована назва API-ключа перетворюється на стабільний `409 Conflict` | 9/10 |
| 7 | RESOLVED | [src/Application/Handlers/Auth/RegisterHandler.cs](src/Application/Handlers/Auth/RegisterHandler.cs#L23) | 23-76 | Race condition під час реєстрації та необроблений конфлікт унікального login | 9/10 |
| 8 | RESOLVED | [src/Api/Controllers/ApiKeysController.cs](src/Api/Controllers/ApiKeysController.cs#L27) | 27-51 | Додано paged-варіант із лімітом page size; повний список залишено окремим endpoint для сумісності | 8/10 |

## Деталі та рекомендації

### 1. Критичний: відсутній контроль доступу до API-ключів

`ApiKeysController` приймає `userId` із query/body, а handlers лише перевіряють існування користувача. У застосунку немає `AddAuthentication`, `AddAuthorization`, `UseAuthentication` або `UseAuthorization`, а endpoints не мають `[Authorize]`.

Зловмисник, який знає або підбирає `userId`, може створити для чужого акаунта `readwrite` API-ключ, отримати список метаданих ключів або відкликати ключі. Створений ключ далі можна обміняти на access token.

**Рекомендація:** додати JWT authentication та authorization, захистити всі endpoints керування ключами, брати ідентифікатор користувача з authenticated claims і перевіряти ownership на server side. Не довіряти `userId` з HTTP-запиту.

### 2. Високий: PostgreSQL доступний із host/network

У Compose database має `5432:5432`, а credentials задані як `app`/`app`. Якщо Compose використовується без додаткового firewall або network policy, база доступна з host interfaces.

**Рекомендація:** прибрати публікацію порту, залишити БД лише у приватній Docker network, передавати сильні credentials через secret manager/environment deployment і використовувати окремого least-privileged користувача застосунку.

### 3. Високий: токени й credentials передавалися через HTTP

Docker-режим слухає `http://+:8080`, тому login, refresh, API-key exchange і registration передаються без TLS.

**Рекомендація:** для production використовувати HTTPS reverse proxy або повернути TLS-конфігурацію контейнера.

### 4. Високий: production surface не захищений

У `Program.cs` відсутні authentication/authorization middleware, а Swagger і Swagger UI вмикаються без перевірки environment. Це збільшує exposed surface і не дає фактичного захисту навіть після додавання атрибутів без middleware.

**Рекомендація:** зареєструвати JWT bearer authentication, authorization policies та middleware у правильному порядку. Swagger залишати увімкненим лише в Development або захистити його окремою policy/мережевим доступом.

### 5. Середній: неатомарна refresh-token rotation

Два одночасні запити можуть обидва прочитати одну сесію як активну до того, як один із них збереже `RevokedAtUtc`. У результаті один refresh token породжує кілька successor sessions.

**Рекомендація:** виконувати умовний атомарний update на кшталт `UPDATE ... WHERE jti = @jti AND revoked_at IS NULL`, вимагати рівно один affected row і створювати нову сесію в тій самій транзакції. За потреби додати optimistic concurrency token.

**Статус:** виправлено. `RefreshHandler` виконує умовне атомарне відкликання активної сесії та створення successor session в одній транзакції. Повторний конкурентний запит отримує `Auth.RefreshTokenInvalid`, якщо update не змінив рівно один рядок.

### 6. Виправлено: дубльована назва API-ключа

У базі є unique index на `(UserId, Name)`. Pre-check не захищає від race condition між перевіркою та insert, тому конкурентний конфлікт обробляється окремо під час збереження.

**Виправлення:** fast pre-check залишено для звичайного сценарію, а конкурентне PostgreSQL-порушення unique constraint `api_keys(UserId, Name)` перехоплюється в `CreateApiKeyHandler` і повертається як `ApiKeys.NameAlreadyExists` з типом `Conflict`. Інші помилки збереження не маскуються.

### 7. Середній: race condition під час реєстрації

Перевірка login і insert розділені. За паралельної реєстрації один запит порушить unique index на `users.login`, а exception вийде назовні замість очікуваної помилки `Auth.LoginAlreadyExists`.

**Рекомендація:** перекладати unique-constraint violation у той самий `409 Conflict`, не покладаючись лише на pre-check. Якщо створення user і session повинно бути нероздільним, виконувати його в транзакції.

**Статус:** виправлено. Створення user і session виконується в одній транзакції, а конкурентне порушення unique index на `users.login` перетворюється на `Auth.LoginAlreadyExists` з типом `Conflict`.

### 8. Середній: варіанти завантаження API-ключів

Доступні два варіанти:

1. `GET /auth/keys` — повертає всі API-ключі користувача одним запитом. Це простий варіант для невеликих списків, але він не захищає від великої відповіді.
2. `GET /auth/keys/paged?page=1&pageSize=50` — повертає сторінку з максимум 100 елементами та метаданими `totalCount` і `totalPages`. Цей варіант рекомендований для production-клієнтів.

Пагінований варіант використовує `COUNT`, `Skip` і `Take` на стороні бази даних; список не завантажується цілком у пам’ять. Поведінка endpoint без пагінації залишена для сумісності та простих сценаріїв.ToListAsync` без page size, cursor або server-side cap. Користувач із великою кількістю ключів може спричинити надмірне споживання пам'яті та повільну відповідь.

**Рекомендація:** додати pagination із максимальним page size, стабільним сортуванням і бажано cursor-based pagination. Перевіряти, що запит має індекс, сумісний із pattern фільтрації та сортування.

## Порядок виправлення

1. Негайно закрити API-key endpoints authentication/authorization та прибрати довіру до `userId` з клієнта.
2. Забрати PostgreSQL з public binding, замінити статичні credentials і додати TLS на зовнішньому контурі.
3. Виправити атомарність refresh rotation.
4. Додати коректне перетворення unique-constraint exceptions у `409 Conflict`.
5. Додати pagination/ліміти та перевірити production observability.

## Обмеження аудиту

- Git metadata у робочій директорії відсутня, тому diff/історію змін перевірити неможливо.
- Не перевіряли зовнішній reverse proxy, firewall, cloud network policies та secret manager.
- Автоматизованих тестів у доступному дереві не знайдено; після виправлень потрібні integration tests для access control, concurrent refresh і duplicate conflicts.

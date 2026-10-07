# Auth Test Results

Дата запуску: 2026-10-07

## Команда

```powershell
dotnet test "D:\repos\mytwitter\auth\tests\Auth.Tests\Auth.Tests.csproj" --no-restore
```

## Підсумок

| Показник | Результат |
|---|---:|
| Тестовий проєкт | `Auth.Tests` |
| Target framework | `net10.0` |
| Загальна кількість | 33 |
| Успішно | 33 |
| Провалено | 0 |
| Пропущено | 0 |
| Результат | **PASSED** |

## Integration tests

Integration-тести запускають ASP.NET Core application через `WebApplicationFactory<Program>` і перевіряють HTTP pipeline, маршрути, middleware, authentication/authorization та JSON-відповіді.

| # | Тест | Що перевіряє |
|---:|---|---|
| 1 | `ApiIntegrationTests.Register_endpoint_returns_json_login_response` | `POST /auth/register` повертає HTTP 200 і JSON із access та refresh token |
| 2 | `ApiIntegrationTests.Login_exists_endpoint_returns_result_from_mediator` | `GET /auth/login/exists` повертає результат через mediator |
| 3 | `ApiIntegrationTests.Paged_api_keys_endpoint_is_protected` | `GET /auth/keys/paged` захищений authentication middleware |
| 4 | `ApiIntegrationTests.Protected_api_key_endpoint_requires_authentication` | `GET /auth/keys` без credentials повертає HTTP 401 |
| 5 | `ApiIntegrationTests.Unknown_route_returns_not_found` | Невідомий маршрут повертає HTTP 404 |

## Unit tests: authentication handlers

| # | Тест | Що перевіряє |
|---:|---|---|
| 6 | `ApplicationHandlerTests.Register_trims_login_hashes_password_and_creates_user_and_session` | Trim login, hash пароля, створення user/session і транзакційне збереження |
| 7 | `ApplicationHandlerTests.Register_rejects_existing_login_without_writing` | Відмова при дубльованому login без запису в repository |
| 8 | `ApplicationHandlerTests.Login_rejects_unknown_user_and_wrong_password_without_creating_session` | Відмова для невідомого користувача або неправильного пароля |
| 9 | `ApplicationHandlerTests.Login_creates_session_for_valid_credentials` | Успішний login створює session і повертає token pair |
| 10 | `ApplicationHandlerTests.Refresh_returns_error_for_invalid_token` | Невалідний refresh token повертає `Auth.RefreshTokenInvalid` |
| 11 | `ApplicationHandlerTests.Refresh_rotates_active_session_and_revokes_old_one` | Refresh token rotation, revoke старої сесії та створення нової |
| 12 | `ApplicationHandlerTests.Refresh_fails_when_session_was_concurrently_revoked` | Обробка race condition, коли сесію вже відкликано |
| 13 | `ApplicationHandlerTests.Logout_revokes_active_session_and_is_idempotent` | Logout відкликає активну сесію і безпечно повторюється |
| 14 | `ApplicationHandlerTests.Logout_rejects_unknown_or_mismatched_session` | Відмова для відсутньої або чужої refresh session |
| 15 | `ApplicationHandlerTests.Refresh_rejects_expired_session_before_transaction` | Прострочена сесія відхиляється до відкриття транзакції |
| 16 | `ApplicationHandlerTests.Check_login_exists_trims_and_returns_presence` | Trim login і перевірка наявності користувача |

## Unit tests: API keys

| # | Тест | Що перевіряє |
|---:|---|---|
| 17 | `ApplicationHandlerTests.Exchange_api_key_rejects_malformed_revoked_and_unverified_keys` | Відмова для malformed API key |
| 18 | `ApplicationHandlerTests.Exchange_api_key_updates_last_used_and_returns_access_token` | Перевірка API key, оновлення `LastUsedAtUtc` і видача access token |
| 19 | `ApplicationHandlerTests.Exchange_api_key_rejects_revoked_key_without_updating_it` | Відкликаний API key не можна використати або оновити |
| 20 | `ApplicationHandlerTests.Create_api_key_checks_identity_user_and_permission` | Відсутній authenticated user відхиляється |
| 21 | `ApplicationHandlerTests.Create_api_key_generates_and_persists_secret_only_once` | Генерація ключа, trim назви і збереження secret hash |
| 22 | `ApplicationHandlerTests.Create_api_key_reports_missing_user_and_missing_permission` | Помилки для відсутнього user або permission configuration |
| 23 | `ApplicationHandlerTests.List_and_paged_api_keys_map_active_state_and_metadata` | Mapping API keys, active/revoked state і pagination metadata |
| 24 | `ApplicationHandlerTests.Revoke_api_key_rejects_other_users_and_is_idempotent` | Revoke власного ключа та ідемпотентний повторний revoke |
| 25 | `ApplicationHandlerTests.Revoke_api_key_does_not_revoke_a_key_owned_by_another_user` | Неможливість відкликати чужий API key |
| 26 | `ApplicationHandlerTests.List_api_keys_requires_authenticated_current_user` | Список API keys вимагає authenticated current user |
| 27 | `ApplicationHandlerTests.Permissions_and_jwks_handlers_map_repository_and_provider_values` | Mapping permissions і public JWK response |

## Unit tests: validation

| # | Тест | Що перевіряє |
|---:|---|---|
| 28 | `ApplicationHandlerTests.Register_validator_rejects_invalid_input(login: "", password: "secret")` | Порожній login |
| 29 | `ApplicationHandlerTests.Register_validator_rejects_invalid_input(login: "ab", password: "secret")` | Login коротший за мінімальну довжину |
| 30 | `ApplicationHandlerTests.Register_validator_rejects_invalid_input(login: "alice", password: "short")` | Password коротший за мінімальну довжину |
| 31 | `ApplicationHandlerTests.Register_validator_rejects_invalid_input(login: "alice", password: "")` | Порожній password |
| 32 | `ApplicationHandlerTests.Validators_accept_valid_auth_and_api_key_commands` | Валідні register/login/refresh/create/revoke команди |
| 33 | `ApplicationHandlerTests.Validators_reject_values_over_configured_limits` | Перевищення максимальної довжини login/password/API key name |

## Додатковий regression fix

Під час розширення тестів було знайдено помилку в `LogoutHandler`: коли refresh token був криптографічно валідним, але відповідна session не існувала, handler передавав `Error.None` у `Result.Failure`. Це призводило до `ArgumentException` замість коректної відповіді про невалідний token.

Поведінку виправлено: тепер повертається помилка `Auth.RefreshTokenInvalid` типу `Unauthorized`, яка мапиться API на HTTP 401.

## Повторна перевірка

Після виправлення виконано повний тестовий набір:

```text
Passed! - Failed: 0, Passed: 33, Skipped: 0, Total: 33
```
